using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameServerApp.UI.Services;

namespace GameServerApp.UI.ViewModels;

public partial class SystemLogViewModel : ViewModelBase
{
    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty]
    private bool _autoScroll = true;

    public event Action? ScrollRequested;

    public SystemLogViewModel()
    {
        // Load existing logs
        foreach (var entry in InMemoryLoggerProvider.Instance.GetEntries())
            LogLines.Add(entry.FormattedLine);

        // Subscribe to new logs
        InMemoryLoggerProvider.Instance.LogReceived += OnLogReceived;
    }

    private void OnLogReceived(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogLines.Add(entry.FormattedLine);

            // Trim if too many
            while (LogLines.Count > 3000)
                LogLines.RemoveAt(0);

            if (AutoScroll)
                ScrollRequested?.Invoke();
        });
    }

    [RelayCommand]
    private void ClearLogs()
    {
        LogLines.Clear();
    }

    public void Dispose()
    {
        InMemoryLoggerProvider.Instance.LogReceived -= OnLogReceived;
    }
}
