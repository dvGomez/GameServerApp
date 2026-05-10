using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GameServerApp.UI.ViewModels;

namespace GameServerApp.UI.Views;

public partial class SystemLogView : UserControl
{
    public SystemLogView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is SystemLogViewModel vm)
        {
            vm.ScrollRequested += () =>
            {
                var listBox = this.FindControl<ListBox>("LogListBox");
                if (listBox != null && listBox.ItemCount > 0)
                {
                    listBox.ScrollIntoView(listBox.ItemCount - 1);
                }
            };
        }
    }
}
