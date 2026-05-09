using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerApp.Core.Interfaces;

namespace GameServerApp.UI.ViewModels;

public partial class CreateServerViewModel : ViewModelBase
{
    private readonly IServerManager _serverManager;

    public string GameId { get; }
    public string GameDisplayName { get; }

    [ObservableProperty]
    private string _serverName = string.Empty;

    [ObservableProperty]
    private int _memoryMb = 1024;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string? _errorMessage;

    public event Action<string>? ServerCreated;
    public event Action? Cancelled;

    public CreateServerViewModel(IServerManager serverManager, string gameId)
    {
        _serverManager = serverManager;
        GameId = gameId;

        var plugin = serverManager.GetPlugin(gameId);
        GameDisplayName = plugin?.DisplayName ?? gameId;
    }

    [RelayCommand]
    private async Task CreateServer()
    {
        if (string.IsNullOrWhiteSpace(ServerName))
        {
            ErrorMessage = "Server name is required.";
            return;
        }

        IsCreating = true;
        ErrorMessage = null;

        try
        {
            var progress = new Progress<double>(p => DownloadProgress = p);
            var initialConfig = new Dictionary<string, object>
            {
                ["memory-mb"] = MemoryMb
            };

            var instance = await _serverManager.CreateServerAsync(
                GameId, ServerName, initialConfig, progress);

            ServerCreated?.Invoke(instance.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Cancelled?.Invoke();
    }
}
