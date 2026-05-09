using CommunityToolkit.Mvvm.ComponentModel;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.ViewModels;

public partial class SidebarItemViewModel : ViewModelBase
{
    public required string InstanceId { get; init; }

    [ObservableProperty]
    private string _serverName = string.Empty;

    [ObservableProperty]
    private string _gameId = string.Empty;

    [ObservableProperty]
    private ServerState _state = ServerState.Stopped;

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private int _onlinePlayers;

    [ObservableProperty]
    private int _maxPlayers;

    public string PlayerInfo => State == ServerState.Running
        ? $"{OnlinePlayers}/{MaxPlayers}"
        : string.Empty;

    partial void OnOnlinePlayersChanged(int value) => OnPropertyChanged(nameof(PlayerInfo));
    partial void OnMaxPlayersChanged(int value) => OnPropertyChanged(nameof(PlayerInfo));
    partial void OnStateChanged(ServerState value) => OnPropertyChanged(nameof(PlayerInfo));
}
