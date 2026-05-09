using System.Collections.Concurrent;
using System.Diagnostics;
using GameServerApp.Core.Events;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameServerApp.Core.Services;

public sealed class ProcessManager : IProcessManager
{
    private readonly ILogger<ProcessManager> _logger;
    private readonly ConcurrentDictionary<string, Process> _processes = new();

    public event EventHandler<ConsoleOutputEventArgs>? OutputReceived;
    public event EventHandler<ServerStateChangedEventArgs>? StateChanged;

    public ProcessManager(ILogger<ProcessManager> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(ServerInstance instance, ProcessStartInfo startInfo, CancellationToken ct = default)
    {
        if (_processes.ContainsKey(instance.Id))
            throw new InvalidOperationException($"Process already running for instance {instance.Id}");

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardInput = true;
        startInfo.CreateNoWindow = true;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            RaiseOutput(instance.Id, e.Data, ConsoleOutputLevel.Info);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            RaiseOutput(instance.Id, e.Data, ConsoleOutputLevel.Error);
        };

        process.Exited += (_, _) =>
        {
            try
            {
                _processes.TryRemove(instance.Id, out _);

                int exitCode;
                try { exitCode = process.ExitCode; }
                catch { exitCode = -1; }

                var newState = exitCode == 0 ? ServerState.Stopped : ServerState.Error;

                instance.State = newState;
                instance.Process = null;
                instance.ProcessId = null;

                RaiseStateChanged(instance.Id, ServerState.Running, newState);
                RaiseOutput(instance.Id, $"Process exited with code {exitCode}.", ConsoleOutputLevel.System);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Exited handler for {InstanceId}", instance.Id);
            }
            finally
            {
                try { process.Dispose(); } catch { /* ignore */ }
            }
        };

        var oldState = instance.State;
        instance.State = ServerState.Starting;
        RaiseStateChanged(instance.Id, oldState, ServerState.Starting);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            instance.State = ServerState.Error;
            RaiseStateChanged(instance.Id, ServerState.Starting, ServerState.Error);
            RaiseOutput(instance.Id, $"Failed to start process: {ex.Message}", ConsoleOutputLevel.Error);
            try { process.Dispose(); } catch { /* ignore */ }
            return Task.CompletedTask;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        instance.Process = process;
        instance.ProcessId = process.Id;
        instance.StartedAt = DateTime.UtcNow;
        _processes[instance.Id] = process;

        instance.State = ServerState.Running;
        RaiseStateChanged(instance.Id, ServerState.Starting, ServerState.Running);

        _logger.LogInformation("Started process {ProcessId} for instance {InstanceId}",
            process.Id, instance.Id);

        return Task.CompletedTask;
    }

    public async Task StopGracefullyAsync(ServerInstance instance, string? stopCommand,
        int timeoutMs, CancellationToken ct = default)
    {
        if (!_processes.TryGetValue(instance.Id, out var process))
            return;

        var oldState = instance.State;
        instance.State = ServerState.Stopping;
        RaiseStateChanged(instance.Id, oldState, ServerState.Stopping);

        if (!string.IsNullOrEmpty(stopCommand))
        {
            try
            {
                await process.StandardInput.WriteLineAsync(stopCommand);
                await process.StandardInput.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send stop command to instance {InstanceId}", instance.Id);
            }
        }

        var exited = await WaitForExitAsync(process, timeoutMs, ct);

        if (!exited)
        {
            _logger.LogWarning("Process did not exit gracefully for instance {InstanceId}, killing",
                instance.Id);
            await KillAsync(instance);
        }
    }

    public Task KillAsync(ServerInstance instance)
    {
        if (_processes.TryRemove(instance.Id, out var process))
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error killing process for instance {InstanceId}", instance.Id);
            }
        }

        return Task.CompletedTask;
    }

    public async Task SendCommandAsync(ServerInstance instance, string command)
    {
        if (!_processes.TryGetValue(instance.Id, out var process))
            throw new InvalidOperationException("Server is not running.");

        try
        {
            await process.StandardInput.WriteLineAsync(command);
            await process.StandardInput.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send command to instance {InstanceId}", instance.Id);
            throw;
        }
    }

    public bool IsRunning(ServerInstance instance)
    {
        try
        {
            return _processes.TryGetValue(instance.Id, out var process) && !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private void RaiseOutput(string instanceId, string text, ConsoleOutputLevel level)
    {
        try
        {
            OutputReceived?.Invoke(this, new ConsoleOutputEventArgs
            {
                InstanceId = instanceId,
                Line = new ConsoleOutputLine(text, level, DateTime.Now)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising OutputReceived for {InstanceId}", instanceId);
        }
    }

    private void RaiseStateChanged(string instanceId, ServerState oldState, ServerState newState)
    {
        try
        {
            StateChanged?.Invoke(this, new ServerStateChangedEventArgs
            {
                InstanceId = instanceId,
                OldState = oldState,
                NewState = newState
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising StateChanged for {InstanceId}", instanceId);
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
