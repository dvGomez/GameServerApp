using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using GameServerApp.UI.ViewModels;

namespace GameServerApp.UI.Views;

public partial class ServerConsoleView : UserControl
{
    public ServerConsoleView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is ServerConsoleViewModel vm)
        {
            vm.ConsoleOutput.CollectionChanged += OnConsoleOutputChanged;
        }

        CommandInput.KeyDown += OnCommandInputKeyDown;
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (DataContext is ServerConsoleViewModel vm)
        {
            vm.ConsoleOutput.CollectionChanged -= OnConsoleOutputChanged;
        }

        CommandInput.KeyDown -= OnCommandInputKeyDown;
    }

    private void OnConsoleOutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            ConsoleOutputList.ScrollIntoView(ConsoleOutputList.Items.Count - 1);
        }
    }

    private void OnCommandInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ServerConsoleViewModel vm)
        {
            vm.SendCommandCommand.Execute(null);
            e.Handled = true;
        }
    }
}
