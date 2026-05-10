using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Services;
using GameServerApp.Plugins.FiveM;
using GameServerApp.Plugins.Minecraft;
using GameServerApp.Plugins.PaperMC;
using GameServerApp.UI.Services;
using GameServerApp.UI.ViewModels;
using GameServerApp.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServerApp.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SetupGlobalExceptionHandling();

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(InMemoryLoggerProvider.Instance);
        });

        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<IServerManager, ServerManager>();

        services.AddSingleton<IGameServerPlugin, MinecraftPlugin>();
        services.AddSingleton<IGameServerPlugin, PaperPlugin>();
        services.AddSingleton<IGameServerPlugin, FiveMPlugin>();

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var serverManager = Services.GetRequiredService<IServerManager>();
            var mainVm = new MainWindowViewModel(serverManager);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };

            _ = mainVm.InitializeAsync();

            desktop.ShutdownRequested += async (_, _) =>
            {
                foreach (var instance in serverManager.Instances.ToList())
                {
                    if (instance.State == Core.Models.ServerState.Running)
                    {
                        try { await serverManager.StopServerAsync(instance.Id); }
                        catch { /* best effort on shutdown */ }
                    }
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetupGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var msg = $"[FATAL] Unhandled exception: {e.ExceptionObject}";
            Console.Error.WriteLine(msg);
            InMemoryLoggerProvider.Instance.AddEntry(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Critical,
                Category = "AppDomain",
                Message = msg
            });
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            var msg = $"Unobserved task exception: {e.Exception}";
            Console.Error.WriteLine($"[WARN] {msg}");
            InMemoryLoggerProvider.Instance.AddEntry(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Warning,
                Category = "TaskScheduler",
                Message = msg
            });
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            var msg = $"UI thread exception: {e.Exception}";
            Console.Error.WriteLine($"[ERROR] {msg}");
            InMemoryLoggerProvider.Instance.AddEntry(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Error,
                Category = "UIThread",
                Message = msg
            });
            e.Handled = true;
        };
    }
}
