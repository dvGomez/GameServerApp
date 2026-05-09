using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;

namespace GameServerApp.UI.ViewModels;

public partial class ServerConfigViewModel : ViewModelBase
{
    private readonly IServerManager _serverManager;

    public string InstanceId { get; }
    public string ServerDirectory { get; }

    [ObservableProperty]
    private int _port;

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private int _maxPlayers;

    [ObservableProperty]
    private string _serverName = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _selectedTabIndex;

    public bool IsSettingsTab => SelectedTabIndex == 0;
    public bool IsFilesTab => SelectedTabIndex == 1;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSettingsTab));
        OnPropertyChanged(nameof(IsFilesTab));
    }

    // --- Settings tab ---
    public ObservableCollection<ConfigFieldViewModel> Fields { get; } = new();

    // --- Files tab ---
    public ObservableCollection<FileEntryViewModel> FileEntries { get; } = new();

    [ObservableProperty]
    private string _currentBrowsePath = string.Empty;

    private string _lastBrowsePath = string.Empty;

    [ObservableProperty]
    private string? _openFilePath;

    [ObservableProperty]
    private string? _openFileName;

    [ObservableProperty]
    private string _fileContent = string.Empty;

    [ObservableProperty]
    private bool _isFileOpen;

    [ObservableProperty]
    private string? _fileStatusMessage;

    // --- Delete ---
    [ObservableProperty]
    private bool _showDeleteConfirmation;

    [ObservableProperty]
    private bool _isDeleting;

    public event Action? BackToConsole;
    public event Action? ServerDeleted;

    public ServerConfigViewModel(IServerManager serverManager, string instanceId)
    {
        _serverManager = serverManager;
        InstanceId = instanceId;

        var config = serverManager.GetServerConfig(instanceId);
        ServerDirectory = config?.ServerDirectory ?? string.Empty;

        var instance = serverManager.Instances.FirstOrDefault(i => i.Id == instanceId);
        Port = instance?.Port ?? 0;
        Version = instance?.Version ?? string.Empty;
        MaxPlayers = instance?.MaxPlayers ?? 0;

        _serverManager.ServerConfigChanged += OnServerConfigChanged;

        LoadConfig();
        LoadFileEntries(ServerDirectory);
    }

    private void OnServerConfigChanged(object? sender, Core.Events.ServerConfigChangedEventArgs e)
    {
        if (e.InstanceId != InstanceId) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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

    private void LoadConfig()
    {
        var config = _serverManager.GetServerConfig(InstanceId);
        if (config is null) return;

        ServerName = config.Name;

        var plugin = _serverManager.GetPlugin(config.GameId);
        if (plugin is null) return;

        var schema = plugin.GetConfigSchema();
        var currentValues = config.GameSettings;

        foreach (var field in schema)
        {
            var value = currentValues.TryGetValue(field.Key, out var v)
                ? v?.ToString() ?? ""
                : field.DefaultValue?.ToString() ?? "";

            Fields.Add(new ConfigFieldViewModel
            {
                Key = field.Key,
                DisplayName = field.DisplayName,
                Description = field.Description,
                FieldType = field.FieldType,
                Value = value,
                Category = field.Category,
                EnumOptions = field.EnumOptions != null
                    ? new ObservableCollection<string>(field.EnumOptions)
                    : null
            });
        }
    }

    // ---- Settings tab commands ----

    [RelayCommand]
    private async Task Save()
    {
        IsSaving = true;
        StatusMessage = null;

        try
        {
            var values = new Dictionary<string, object>();
            foreach (var field in Fields)
            {
                values[field.Key] = field.FieldType switch
                {
                    ConfigFieldType.Bool => bool.TryParse(field.Value, out var b) && b,
                    ConfigFieldType.Int => int.TryParse(field.Value, out var i) ? i : 0,
                    _ => field.Value
                };
            }

            // Persist the server name change to the config before saving
            var config = _serverManager.GetServerConfig(InstanceId);
            if (config != null)
                config.Name = ServerName;

            await _serverManager.SaveConfigAsync(InstanceId, values);
            StatusMessage = "Configuration saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ---- Files tab commands ----

    private void LoadFileEntries(string path)
    {
        FileEntries.Clear();
        CurrentBrowsePath = path;
        IsFileOpen = false;
        OpenFilePath = null;
        OpenFileName = null;
        FileStatusMessage = null;

        if (!Directory.Exists(path)) return;

        if (path != ServerDirectory)
        {
            FileEntries.Add(new FileEntryViewModel
            {
                Name = "..",
                FullPath = Directory.GetParent(path)?.FullName ?? ServerDirectory,
                IsDirectory = true,
                Size = null
            });
        }

        try
        {
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d)))
            {
                FileEntries.Add(new FileEntryViewModel
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsDirectory = true,
                    Size = null
                });
            }

            foreach (var file in Directory.GetFiles(path).OrderBy(f => Path.GetFileName(f)))
            {
                var info = new FileInfo(file);
                FileEntries.Add(new FileEntryViewModel
                {
                    Name = info.Name,
                    FullPath = file,
                    IsDirectory = false,
                    Size = FormatFileSize(info.Length)
                });
            }
        }
        catch { /* permission errors, etc */ }
    }

    [RelayCommand]
    private void NavigateToEntry(FileEntryViewModel? entry)
    {
        if (entry is null) return;

        if (entry.IsDirectory)
        {
            LoadFileEntries(entry.FullPath);
        }
        else
        {
            OpenFileForEditing(entry.FullPath);
        }
    }

    private void OpenFileForEditing(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (info.Length > 10 * 1024 * 1024)
            {
                FileStatusMessage = "File too large to edit (max 10MB).";
                return;
            }

            _lastBrowsePath = CurrentBrowsePath;
            FileContent = File.ReadAllText(filePath);
            OpenFilePath = filePath;
            OpenFileName = Path.GetFileName(filePath);
            IsFileOpen = true;
            FileStatusMessage = null;
        }
        catch (Exception ex)
        {
            FileStatusMessage = $"Error opening file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CloseFile()
    {
        IsFileOpen = false;
        OpenFilePath = null;
        OpenFileName = null;
        FileContent = string.Empty;
        FileStatusMessage = null;
    }

    [RelayCommand]
    private async Task SaveFile()
    {
        if (OpenFilePath is null) return;

        try
        {
            await File.WriteAllTextAsync(OpenFilePath, FileContent);
            CloseFile();
            LoadFileEntries(_lastBrowsePath);
        }
        catch (Exception ex)
        {
            FileStatusMessage = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RefreshFiles()
    {
        LoadFileEntries(CurrentBrowsePath);
    }

    // ---- Open folder / Delete ----

    [RelayCommand]
    private void OpenInExplorer()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{ServerDirectory}\"",
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{ServerDirectory}\"",
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{ServerDirectory}\"",
                    UseShellExecute = false
                });
            }
        }
        catch { /* ignore if file manager can't open */ }
    }

    [RelayCommand]
    private void ShowDeleteDialog()
    {
        ShowDeleteConfirmation = true;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteConfirmation = false;
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        IsDeleting = true;
        try
        {
            await _serverManager.DeleteServerAsync(InstanceId);
            ShowDeleteConfirmation = false;
            ServerDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting: {ex.Message}";
            ShowDeleteConfirmation = false;
        }
        finally
        {
            IsDeleting = false;
        }
    }

    [RelayCommand]
    private void SetTab(string index)
    {
        if (int.TryParse(index, out var i))
            SelectedTabIndex = i;
    }

    [RelayCommand]
    private void GoBack()
    {
        BackToConsole?.Invoke();
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}

public partial class ConfigFieldViewModel : ObservableObject
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required ConfigFieldType FieldType { get; init; }
    public string? Category { get; init; }
    public ObservableCollection<string>? EnumOptions { get; init; }

    [ObservableProperty]
    private string _value = string.Empty;
}

public partial class FileEntryViewModel : ObservableObject
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public required bool IsDirectory { get; init; }
    public string? Size { get; init; }
}
