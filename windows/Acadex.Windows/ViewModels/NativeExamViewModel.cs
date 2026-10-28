using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Acadex.Windows.Models;
using Acadex.Windows.Services;

namespace Acadex.Windows.ViewModels;

public sealed class NativeExamViewModel : ViewModelBase, IDisposable
{
    private readonly NativeExamService _service;
    private readonly string _examId;
    private readonly string _sessionToken;
    private readonly long _endAt;
    private readonly string _type;
    private readonly string _userDataFolder;
    private System.Threading.Timer? _timer;
    private readonly Dictionary<string, JsonElement> _answers = new();
    private readonly List<NativeQuestion> _questions;
    private int _currentIndex;
    private string _status = "Exam in progress";
    private string _timerText = "--:--";
    private string _error = "";
    private string _success = "";
    private bool _isBusy;
    private string _pdfPath = "";
    private bool _finishTriggered;

    public NativeExamViewModel(NativeExamService service, string examId, ExamSessionResponse session)
    {
        _service = service;
        _examId = examId;
        _sessionToken = session.SessionToken;
        _endAt = session.EndAt;
        _type = session.Type;
        Title = session.Title;
        _userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Acadex", "Exams");
        Directory.CreateDirectory(_userDataFolder);

        _questions = ParseQuestions(session.Questions);
        if (session.Progress.ValueKind == JsonValueKind.Object &&
            session.Progress.TryGetProperty("answers", out var savedAnswers) &&
            savedAnswers.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in savedAnswers.EnumerateObject())
                _answers[prop.Name] = prop.Value.Clone();
        }
        if (session.Progress.ValueKind == JsonValueKind.Object &&
            session.Progress.TryGetProperty("currentQuestion", out var savedIndex) &&
            savedIndex.TryGetInt32(out var index) && _questions.Count > 0)
            _currentIndex = Math.Clamp(index, 0, _questions.Count - 1);

        PreviousCommand = new AsyncCommand(PreviousAsync, () => !IsBusy && CurrentIndex > 0 && IsTemplate);
        NextCommand = new AsyncCommand(NextAsync, () => !IsBusy && CurrentIndex < _questions.Count - 1 && IsTemplate);
        SaveProgressCommand = new AsyncCommand(SaveProgressAsync, () => !IsBusy && Status != "Submitted");
        FinishCommand = new AsyncCommand(FinishAsync, () => !IsBusy && Status != "Submitted");
        DownloadPdfCommand = new AsyncCommand(DownloadPdfAsync, () => !IsBusy && IsPdf);
        RefreshCurrentQuestion();
        UpdateTimer();
        _timer = new System.Threading.Timer(_ => Application.Current.Dispatcher.Invoke(UpdateTimer),
            null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public string Title { get; }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string TimerText { get => _timerText; private set => Set(ref _timerText, value); }
    public string Error { get => _error; private set { if (Set(ref _error, value)) Raise(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string Success { get => _success; private set { if (Set(ref _success, value)) Raise(nameof(HasSuccess)); } }
    public bool HasSuccess => !string.IsNullOrWhiteSpace(Success);
    public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) { Raise(nameof(IsNotBusy)); RefreshCommands(); } } }
    public bool IsNotBusy => !IsBusy;
    public bool IsTemplate => _type == "template";
    public bool IsPdf => _type == "pdf";
    public string PdfPath { get => _pdfPath; private set => Set(ref _pdfPath, value); }
    public int CurrentIndex { get => _currentIndex; private set { if (Set(ref _currentIndex, value)) { Raise(nameof(QuestionCounter)); RefreshCurrentQuestion(); RefreshCommands(); } } }
    public string QuestionCounter => _questions.Count == 0 ? "No questions" : $"Question {CurrentIndex + 1} of {_questions.Count}";
    public string CurrentQuestionText => _questions.ElementAtOrDefault(CurrentIndex)?.Text ?? "No questions were returned by the server.";
    public ObservableCollection<NativeExamOption> CurrentOptions { get; } = new();

    public AsyncCommand PreviousCommand { get; }
    public AsyncCommand NextCommand { get; }
    public AsyncCommand SaveProgressCommand { get; }
    public AsyncCommand FinishCommand { get; }
    public AsyncCommand DownloadPdfCommand { get; }

    private static List<NativeQuestion> ParseQuestions(JsonElement element)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.Array) return new();
            return element.EnumerateArray().Select(q =>
            {
                var type = q.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "mcq" : "mcq";
                var text = q.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
                IReadOnlyList<string> options = q.TryGetProperty("options", out var optionProp) && optionProp.ValueKind == JsonValueKind.Array
                    ? optionProp.EnumerateArray().Select(o => o.GetString() ?? "").ToArray() : Array.Empty<string>();
                var answer = q.TryGetProperty("answer", out var answerProp) && answerProp.TryGetInt32(out var a) ? a : (int?)null;
                var marks = q.TryGetProperty("marks", out var markProp) && markProp.TryGetDouble(out var m) ? m : 1;
                return new NativeQuestion(type, text, options, answer, marks);
            }).ToList();
        }
        catch (JsonException) { return new(); }
    }

    private void RefreshCurrentQuestion()
    {
        Raise(nameof(CurrentQuestionText));
        Raise(nameof(QuestionCounter));
        CurrentOptions.Clear();
        var question = _questions.ElementAtOrDefault(CurrentIndex);
        if (question is null) return;
        IReadOnlyList<string> options = question.Type == "tf" ? new[] { "True", "False" } : (question.Options ?? Array.Empty<string>());
        _answers.TryGetValue(CurrentIndex.ToString(), out var chosen);
        string? selected = chosen.ValueKind == JsonValueKind.String ? chosen.GetString()
            : chosen.ValueKind == JsonValueKind.Number ? chosen.ToString() : null;
        for (var i = 0; i < options.Count; i++)
        {
            var value = question.Type == "tf" ? (i == 0 ? "true" : "false") : i.ToString();
            CurrentOptions.Add(new NativeExamOption(i, options[i], string.Equals(value, selected, StringComparison.OrdinalIgnoreCase), this));
        }
    }

    internal void SelectOption(int optionIndex)
    {
        var question = _questions.ElementAtOrDefault(CurrentIndex);
        if (question is null) return;
        var answer = question.Type == "tf" ? (optionIndex == 0 ? "true" : "false") : optionIndex.ToString();
        _answers[CurrentIndex.ToString()] = JsonSerializer.SerializeToElement(answer);
        foreach (var option in CurrentOptions)
            option.SetSelectedFromOwner(option.Index == optionIndex);
    }

    private Task PreviousAsync() { if (CurrentIndex > 0) CurrentIndex--; return Task.CompletedTask; }
    private Task NextAsync() { if (CurrentIndex < _questions.Count - 1) CurrentIndex++; return Task.CompletedTask; }

    private async Task SaveProgressAsync()
    {
        await RunBusyAsync(async ct =>
        {
            var progress = new { answers = _answers, currentQuestion = CurrentIndex, pdfPage = 1, pdfScrollRatio = 0 };
            await _service.SaveProgressAsync(_examId, _sessionToken, progress, ct);
            Success = "Progress saved.";
        });
    }

    private async Task FinishAsync()
    {
        if (Status == "Submitted" || _finishTriggered) return;
        var result = MessageBox.Show("Finish and submit this exam now? You may not be able to start another attempt.",
            "Submit exam", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        await SubmitAsync(expired: false);
    }

    private async Task SubmitAsync(bool expired)
    {
        if (_finishTriggered || Status == "Submitted") return;
        _finishTriggered = true;
        await RunBusyAsync(async ct =>
        {
            Status = expired ? "Time expired — submitting" : "Submitting exam…";
            var data = await _service.FinishAsync(_examId, _sessionToken, _answers, ct);
            Status = "Submitted";
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            Success = GetSummary(data);
            MessageBox.Show(Success, "Acadex — Submission received", MessageBoxButton.OK, MessageBoxImage.Information);
        });
        if (Status != "Submitted") _finishTriggered = false;
    }

    private static string GetSummary(JsonElement data)
    {
        if (data.TryGetProperty("released", out var released) && released.ValueKind == JsonValueKind.False)
            return "Your work was submitted. Results have not been released yet.";
        if (data.TryGetProperty("score", out var score) && data.TryGetProperty("total", out var total))
            return $"Submitted successfully. Score: {score} / {total}" +
                   (data.TryGetProperty("percentage", out var pct) ? $" ({pct}%)" : "");
        return "Your work was submitted successfully.";
    }

    private async Task DownloadPdfAsync()
    {
        await RunBusyAsync(async ct =>
        {
            Status = "Downloading PDF securely…";
            using var stream = await _service.DownloadPdfAsync(_examId, _sessionToken, ct);
            var path = Path.Combine(_userDataFolder, $"exam-{SafeFilePart(_examId)}.pdf");
            await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await stream.CopyToAsync(output, ct);
            PdfPath = path;
            Status = "PDF downloaded";
            if (MessageBox.Show("The exam PDF is ready. Open it with your default PDF reader?", "Acadex PDF",
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }

    private static string SafeFilePart(string value) =>
        new(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());

    private void UpdateTimer()
    {
        var remaining = Math.Max(0, _endAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var seconds = remaining / 1000;
        TimerText = $"{seconds / 60:00}:{seconds % 60:00}";
        if (remaining == 0 && Status != "Submitted" && !_finishTriggered)
            _ = SubmitAsync(expired: true);
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = "";
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await action(cts.Token); }
        catch (ApiException ex) { Error = ex.Message; }
        catch (OperationCanceledException) { Error = "The request timed out or was cancelled. Try again."; }
        catch (HttpRequestException) { Error = "Could not reach Acadex. Please check your network."; }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private void RefreshCommands()
    {
        PreviousCommand.Refresh(); NextCommand.Refresh(); SaveProgressCommand.Refresh(); FinishCommand.Refresh(); DownloadPdfCommand.Refresh();
    }

    public void Dispose() => _timer?.Dispose();
}

public sealed class NativeExamOption : ViewModelBase
{
    private bool _isChecked;
    private readonly NativeExamViewModel _owner;

    public NativeExamOption(int index, string text, bool isChecked, NativeExamViewModel owner)
    {
        Index = index; Text = text; _isChecked = isChecked; _owner = owner;
    }

    public int Index { get; }
    public string Text { get; }
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (!Set(ref _isChecked, value) || !value) return;
            _owner.SelectOption(Index);
        }
    }

    internal void SetSelectedFromOwner(bool value) => Set(ref _isChecked, value);
}
