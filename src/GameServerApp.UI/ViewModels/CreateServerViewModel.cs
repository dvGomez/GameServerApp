using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // --- Version selection ---
    public ObservableCollection<string> AvailableVersions { get; } = new();

    [ObservableProperty]
    private string? _selectedVersion;

    [ObservableProperty]
    private bool _isLoadingVersions = true;

    public event Action<string>? ServerCreated;
    public event Action? Cancelled;

    public CreateServerViewModel(IServerManager serverManager, string gameId)
    {
        _serverManager = serverManager;
        GameId = gameId;

        var plugin = serverManager.GetPlugin(gameId);
        GameDisplayName = plugin?.DisplayName ?? gameId;

        _ = LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        IsLoadingVersions = true;
        ErrorMessage = null;

        try
        {
            var plugin = _serverManager.GetPlugin(GameId);
            if (plugin is null) return;

            var versions = await plugin.GetAvailableVersionsAsync();
            foreach (var version in versions)
                AvailableVersions.Add(version);

            // Pre-select the latest (first in the list)
            if (AvailableVersions.Count > 0)
                SelectedVersion = AvailableVersions[0];
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load versions: {ex.Message}";
        }
        finally
        {
            IsLoadingVersions = false;
        }
    }

    [RelayCommand]
    private async Task CreateServer()
    {
        if (string.IsNullOrWhiteSpace(ServerName))
        {
            ErrorMessage = "Server name is required.";
            return;
        }

        if (SelectedVersion is null)
        {
            ErrorMessage = "Please select a server version.";
            return;
        }

        IsCreating = true;
        ErrorMessage = null;
        StatusMessage = "Preparing...";

        try
        {
            var progress = new Progress<double>(p => DownloadProgress = p);
            var initialConfig = new Dictionary<string, object>
            {
                ["memory-mb"] = MemoryMb
            };

            var instance = await _serverManager.CreateServerAsync(
                GameId, ServerName, SelectedVersion, initialConfig, progress,
                line => Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = line));

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
