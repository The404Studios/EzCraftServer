using System.Windows;
using System.Windows.Controls;
using EzCraftModManager.ViewModels;

namespace EzCraftModManager.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            await vm.LoadAsync();
        }
    }
}
