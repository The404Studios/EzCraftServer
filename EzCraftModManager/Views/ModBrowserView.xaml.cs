using System.Windows;
using System.Windows.Controls;
using EzCraftModManager.ViewModels;

namespace EzCraftModManager.Views;

public partial class ModBrowserView : UserControl
{
    public ModBrowserView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModBrowserViewModel vm)
        {
            await vm.LoadPopularAsync();
        }
    }
}
