using System.Windows;
using Acadex.Windows.Models;
using Acadex.Windows.Services;
using Acadex.Windows.ViewModels;

namespace Acadex.Windows.Views;

public partial class ExamGateWindow : Window
{
    private readonly AcadexApiClient _api;

    public ExamSessionResponse? Session { get; private set; }
    public string? ExamId { get; private set; }

    public ExamGateWindow(AcadexApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void Continue_Click(object sender, RoutedEventArgs e)
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        var id = ExamIdBox.Text.Trim();
        var password = ExamPasswordBox.Password;
        if (string.IsNullOrWhiteSpace(id) || id == "exam_" || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Enter both the exam ID and password.");
            return;
        }

        try
        {
            IsEnabled = false;
            var session = await new NativeExamService(_api).StartOrResumeAsync(id, password);
            Session = session;
            ExamId = id;
            DialogResult = true;
            Close();
        }
        catch (ApiException ex) { ShowError(ex.Message); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { IsEnabled = true; }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
