using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    public ObservableCollection<GameDefinition> AvailableGames { get; } = new();

    public event Action<string>? GameSelected;

    [RelayCommand]
    private void SelectGame(string gameId)
    {
        GameSelected?.Invoke(gameId);
    }
}
