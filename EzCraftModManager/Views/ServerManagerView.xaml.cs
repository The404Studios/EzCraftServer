using System.Windows;
using System.Windows.Controls;
using EzCraftModManager.ViewModels;

namespace EzCraftModManager.Views;

public partial class ServerManagerView : UserControl
{
    public ServerManagerView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ServerManagerViewModel vm)
        {
            await vm.LoadAsync();
        }
    }
}
