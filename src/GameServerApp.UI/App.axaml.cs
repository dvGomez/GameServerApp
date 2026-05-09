using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Services;
using GameServerApp.Plugins.Minecraft;
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

        services.AddLogging();

        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<IServerManager, ServerManager>();

        services.AddSingleton<IGameServerPlugin, MinecraftPlugin>();

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
            Console.Error.WriteLine($"[FATAL] Unhandled exception: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine($"[WARN] Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"[ERROR] UI thread exception: {e.Exception}");
            e.Handled = true;
        };
    }
}
