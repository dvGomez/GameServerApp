using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerApp.Core.Events;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.ViewModels;

public partial class ServerConsoleViewModel : ViewModelBase
{
    private readonly IServerManager _serverManager;
    private const int MaxConsoleLines = 5000;
    private const int LinesToRemoveOnOverflow = 1000;

    public string InstanceId { get; }

    [ObservableProperty]
    private string _serverName = string.Empty;

    [ObservableProperty]
    private ServerState _serverState;

    [ObservableProperty]
    private string _commandText = string.Empty;

    public ObservableCollection<ConsoleOutputLine> ConsoleOutput { get; } = new();

    public event Action<string>? NavigateToConfig;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private int _onlinePlayers;

    [ObservableProperty]
    private int _maxPlayers;

    public ServerConsoleViewModel(IServerManager serverManager, string instanceId)
    {
        _serverManager = serverManager;
        InstanceId = instanceId;

        var instance = serverManager.Instances.FirstOrDefault(i => i.Id == instanceId);
        if (instance != null)
        {
            ServerName = instance.Name;
            ServerState = instance.State;
            Port = instance.Port;
            Version = instance.Version;
            OnlinePlayers = instance.OnlinePlayers;
            MaxPlayers = instance.MaxPlayers;
        }

        // Load buffered history so logs persist across navigation
        var history = _serverManager.GetConsoleHistory(instanceId);
        foreach (var line in history)
            ConsoleOutput.Add(line);

        _serverManager.ConsoleOutput += OnConsoleOutput;
        _serverManager.ServerStateChanged += OnServerStateChanged;
        _serverManager.ServerConfigChanged += OnServerConfigChanged;
    }

    private void OnConsoleOutput(object? sender, ConsoleOutputEventArgs e)
    {
        if (e.InstanceId != InstanceId) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                ConsoleOutput.Add(e.Line);

                if (ConsoleOutput.Count > MaxConsoleLines)
                {
                    for (int i = 0; i < LinesToRemoveOnOverflow; i++)
                        ConsoleOutput.RemoveAt(0);
                }

                var instance = _serverManager.Instances.FirstOrDefault(i => i.Id == InstanceId);
                if (instance != null)
                    OnlinePlayers = instance.OnlinePlayers;
            }
            catch { /* prevent crash from UI binding errors */ }
        });
    }

    private void OnServerStateChanged(object? sender, ServerStateChangedEventArgs e)
    {
        if (e.InstanceId != InstanceId) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                ServerState = e.NewState;
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
            }
            catch { /* prevent crash */ }
        });
    }

    private void OnServerConfigChanged(object? sender, ServerConfigChangedEventArgs e)
    {
        if (e.InstanceId != InstanceId) return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                ServerName = e.ServerName;
                Port = e.Port;
                Version = e.Version;
                MaxPlayers = e.MaxPlayers;
            }
            catch { /* prevent crash */ }
        });
    }

    public bool CanStart => ServerState is ServerState.Stopped or ServerState.Error;
    public bool CanStop => ServerState == ServerState.Running;

    [RelayCommand]
    private async Task Start()
    {
        try
        {
            await _serverManager.StartServerAsync(InstanceId);
        }
        catch (Exception ex)
        {
            AddLocalLine($"Failed to start: {ex.Message}", ConsoleOutputLevel.Error);
        }
    }

    [RelayCommand]
    private async Task Stop()
    {
        try
        {
            await _serverManager.StopServerAsync(InstanceId);
        }
        catch (Exception ex)
        {
            AddLocalLine($"Failed to stop: {ex.Message}", ConsoleOutputLevel.Error);
        }
    }

    [RelayCommand]
    private async Task Restart()
    {
        try
        {
            await _serverManager.RestartServerAsync(InstanceId);
        }
        catch (Exception ex)
        {
            AddLocalLine($"Failed to restart: {ex.Message}", ConsoleOutputLevel.Error);
        }
    }

    [RelayCommand]
    private async Task SendCommand()
    {
        if (string.IsNullOrWhiteSpace(CommandText)) return;

        var cmd = CommandText;
        CommandText = string.Empty;

        AddLocalLine($"> {cmd}", ConsoleOutputLevel.System);

        try
        {
            await _serverManager.SendCommandAsync(InstanceId, cmd);
        }
        catch (Exception ex)
        {
            AddLocalLine($"Failed to send command: {ex.Message}", ConsoleOutputLevel.Error);
        }
    }

    /// <summary>
    /// Adds a line to the local UI collection and persists it in the central buffer
    /// so it survives navigation.
    /// </summary>
    private void AddLocalLine(string text, ConsoleOutputLevel level)
    {
        var line = new ConsoleOutputLine(text, level, DateTime.Now);
        ConsoleOutput.Add(line);
        _serverManager.AddConsoleEntry(InstanceId, line);
    }

    [RelayCommand]
    private void OpenConfig()
    {
        NavigateToConfig?.Invoke(InstanceId);
    }

    public void Dispose()
    {
        _serverManager.ConsoleOutput -= OnConsoleOutput;
        _serverManager.ServerStateChanged -= OnServerStateChanged;
        _serverManager.ServerConfigChanged -= OnServerConfigChanged;
    }
}
