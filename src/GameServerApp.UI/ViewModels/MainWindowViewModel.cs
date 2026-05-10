using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerApp.Core.Events;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServerManager _serverManager;

    public ObservableCollection<SidebarItemViewModel> SidebarItems { get; } = new();

    [ObservableProperty]
    private SidebarItemViewModel? _selectedSidebarItem;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private bool _isSystemLogVisible;

    public SystemLogViewModel SystemLog { get; } = new();

    public MainWindowViewModel(IServerManager serverManager)
    {
        _serverManager = serverManager;

        var homeVm = CreateHomeViewModel();
        _currentPage = homeVm;

        _serverManager.Instances.CollectionChanged += OnInstancesChanged;
        _serverManager.ServerStateChanged += OnServerStateChanged;
        _serverManager.ServerConfigChanged += OnServerConfigChanged;
        _serverManager.ConsoleOutput += OnConsoleOutputForPlayerCount;
    }

    private HomeViewModel CreateHomeViewModel()
    {
        var homeVm = new HomeViewModel();
        foreach (var plugin in GetAvailablePlugins())
        {
            homeVm.AvailableGames.Add(new GameDefinition
            {
                GameId = plugin.GameId,
                DisplayName = plugin.DisplayName,
                Description = plugin.Description,
                IconResourceKey = plugin.IconResourceKey
            });
        }
        homeVm.GameSelected += OnGameSelected;
        return homeVm;
    }

    private IEnumerable<IGameServerPlugin> GetAvailablePlugins()
    {
        return _serverManager.AvailablePlugins;
    }

    partial void OnSelectedSidebarItemChanged(SidebarItemViewModel? value)
    {
        if (value is null) return;
        NavigateToConsole(value.InstanceId);
    }

    [RelayCommand]
    private void NavigateHome()
    {
        SelectedSidebarItem = null;
        CurrentPage = CreateHomeViewModel();
    }

    [RelayCommand]
    private void CreateNewServer()
    {
        var homeVm = CreateHomeViewModel();
        homeVm.GameSelected += OnGameSelected;
        CurrentPage = homeVm;
        SelectedSidebarItem = null;
    }

    [RelayCommand]
    private void ToggleSystemLog()
    {
        IsSystemLogVisible = !IsSystemLogVisible;
    }

    private void OnGameSelected(string gameId)
    {
        var createVm = new CreateServerViewModel(_serverManager, gameId);
        createVm.ServerCreated += OnServerCreated;
        createVm.Cancelled += () => NavigateHome();
        CurrentPage = createVm;
    }

    private void OnServerCreated(string instanceId)
    {
        NavigateToConsole(instanceId);
        var sidebarItem = SidebarItems.FirstOrDefault(s => s.InstanceId == instanceId);
        if (sidebarItem != null)
            SelectedSidebarItem = sidebarItem;
    }

    private void NavigateToConsole(string instanceId)
    {
        var consoleVm = new ServerConsoleViewModel(_serverManager, instanceId);
        consoleVm.NavigateToConfig += OnNavigateToConfig;
        CurrentPage = consoleVm;
    }

    private void OnNavigateToConfig(string instanceId)
    {
        var configVm = new ServerConfigViewModel(_serverManager, instanceId);
        configVm.BackToConsole += () => NavigateToConsole(instanceId);
        configVm.ServerDeleted += () =>
        {
            SelectedSidebarItem = null;
            CurrentPage = CreateHomeViewModel();
        };
        CurrentPage = configVm;
    }

    private void OnConsoleOutputForPlayerCount(object? sender, ConsoleOutputEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var instance = _serverManager.Instances.FirstOrDefault(i => i.Id == e.InstanceId);
                if (instance == null) return;

                var item = SidebarItems.FirstOrDefault(s => s.InstanceId == e.InstanceId);
                if (item != null)
                    item.OnlinePlayers = instance.OnlinePlayers;
            }
            catch { /* prevent crash */ }
        });
    }

    private void OnInstancesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    foreach (ServerInstance instance in e.NewItems)
                    {
                        SidebarItems.Add(new SidebarItemViewModel
                        {
                            InstanceId = instance.Id,
                            ServerName = instance.Name,
                            GameId = instance.GameId,
                            State = instance.State,
                            Port = instance.Port,
                            Version = instance.Version,
                            MaxPlayers = instance.MaxPlayers,
                            OnlinePlayers = instance.OnlinePlayers
                        });
                    }
                }
                else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (ServerInstance instance in e.OldItems)
                    {
                        var item = SidebarItems.FirstOrDefault(s => s.InstanceId == instance.Id);
                        if (item != null)
                            SidebarItems.Remove(item);
                    }
                }
            }
            catch { /* prevent crash from UI binding issues */ }
        });
    }

    private void OnServerStateChanged(object? sender, ServerStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var item = SidebarItems.FirstOrDefault(s => s.InstanceId == e.InstanceId);
                if (item != null)
                {
                    item.State = e.NewState;
                    if (e.NewState is ServerState.Stopped or ServerState.Error)
                        item.OnlinePlayers = 0;
                }
            }
            catch { /* prevent crash */ }
        });
    }

    private void OnServerConfigChanged(object? sender, ServerConfigChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var item = SidebarItems.FirstOrDefault(s => s.InstanceId == e.InstanceId);
                if (item != null)
                {
                    item.ServerName = e.ServerName;
                    item.Port = e.Port;
                    item.Version = e.Version;
                    item.MaxPlayers = e.MaxPlayers;
                }
            }
            catch { /* prevent crash */ }
        });
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _serverManager.LoadAllServersAsync();
        }
        catch { /* app continues even if loading fails */ }
    }
}
