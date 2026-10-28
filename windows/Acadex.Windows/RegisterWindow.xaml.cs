using System.Windows;
using System.Windows.Controls;
using Acadex.Windows.ViewModels;
using Acadex.Windows.Services;

namespace Acadex.Windows;

public partial class RegisterWindow : Window
{
    private readonly AcadexApiClient _api;
    private readonly MainViewModel _viewModel;

    public RegisterWindow(AcadexApiClient api, MainViewModel viewModel)
    {
        InitializeComponent();
        _api = api;
        _viewModel = viewModel;
        RoleBox.SelectionChanged += (_, _) =>
        {
            ClassBox.IsEnabled = (RoleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "student";
        };
        ClassBox.IsEnabled = true;
    }

    private async void CreateAccount_Click(object sender, RoutedEventArgs e)
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        var role = (RoleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "student";
        try
        {
            _api.SetBaseAddress(_viewModel.ApiUrl);
            var progress = new Progress<string>(_ => { });
            await _api.WaitForServiceAsync(progress, 6);
            var response = await _api.RegisterAsync(role, EmailBox.Text.Trim(), PasswordInput.Password,
                NameBox.Text.Trim(), role == "student" ? ClassBox.Text.Trim() : null);
            await StoreAndApplyAsync(response);
            DialogResult = true;
            Close();
        }
        catch (ApiException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
    }

    private async Task StoreAndApplyAsync(Acadex.Windows.Models.AuthResponse response)
    {
        var store = new SessionStore();
        await store.SaveAsync(response.Token);
        _api.SetToken(response.Token);
        // Reuse the regular view model identity path, preserving the same
        // session persistence and role state as signing in.
        await _viewModel.AcceptLoginAsync(response);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
