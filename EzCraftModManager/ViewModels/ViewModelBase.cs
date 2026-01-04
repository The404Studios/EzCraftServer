using CommunityToolkit.Mvvm.ComponentModel;

namespace EzCraftModManager.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    protected void ClearMessages()
    {
        ErrorMessage = null;
        StatusMessage = null;
    }
}
