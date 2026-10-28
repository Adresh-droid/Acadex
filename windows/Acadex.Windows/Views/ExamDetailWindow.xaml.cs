using System.Text.Json;
using System.Windows;

namespace Acadex.Windows.Views;

public partial class ExamDetailWindow : Window
{
    public string Heading { get; }
    public string Description { get; }
    public string JsonText { get; }

    public ExamDetailWindow(JsonElement document)
    {
        InitializeComponent();
        Heading = document.TryGetProperty("title", out var title) ? title.GetString() ?? "Acadex details" :
            document.TryGetProperty("exam", out var exam) && exam.TryGetProperty("title", out var nestedTitle)
                ? nestedTitle.GetString() ?? "Acadex details" : "Acadex details";
        Description = "Data returned by the existing Acadex API. Sensitive server data is not stored by this window.";
        JsonText = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        DataContext = this;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(JsonText);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
