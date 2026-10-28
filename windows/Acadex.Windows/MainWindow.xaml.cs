using System.Windows;
using System.Windows.Controls;
using Acadex.Windows.Services;
using Acadex.Windows.ViewModels;

namespace Acadex.Windows;

public partial class MainWindow : Window
{
    private readonly AcadexApiClient _api;
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _api = new AcadexApiClient();
        var store = new SessionStore();
        _viewModel = new MainViewModel(_api, store);
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _api.Dispose();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page })
            await _viewModel.NavigateFromUiAsync(page);
    }

    private void LoginPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox box)
            vm.Password = box.Password;
    }

    private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox box)
            vm.CurrentPassword = box.Password;
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox box)
            vm.NewPassword = box.Password;
    }

    private void DeleteAccountPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox box)
            vm.ConfirmDeletePassword = box.Password;
    }

    private void RegisterToggle_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RegisterWindow(_api, _viewModel) { Owner = this };
        dialog.ShowDialog();
    }
}
