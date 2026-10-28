using System.Collections.ObjectModel;
using System.Text.Json;
using Acadex.Windows.Models;
using Acadex.Windows.Services;

namespace Acadex.Windows.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly AcadexApiClient _api;
    private readonly SessionStore _sessionStore;
    private ApiUser? _user;
    private string _email = "";
    private string _password = "";
    private string _displayName = "";
    private string _studentClass = "";
    private string _apiUrl = "https://acadex.onrender.com/";
    private string _status = "Connecting to Acadex…";
    private string _error = "";
    private string _success = "";
    private string _currentPage = "Home";
    private bool _isBusy;
    private bool _isAuthenticated;
    private bool _isTeacher;
    private bool _isRegistering;
    private string _registerRole = "student";
    private string _examTitle = "";
    private string _examPassword = "";
    private string _examDurationMinutes = "30";
    private string _examType = "template";
    private string _questionText = "";
    private string _questionA = "";
    private string _questionB = "";
    private string _questionC = "";
    private string _questionD = "";
    private int _correctOption;
    private string _questionMark = "1";
    private string _negativeMarking = "0";
    private string _passMark = "";
    private string _resultVisibility = "instant";
    private bool _allowRetake;
    private string _maxAttempts = "";
    private string _selectedFolderId = "";
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmDeletePassword = "";
    private string _folderName = "";
    private string _draftTitle = "";
    private string _newFolderName = "";

    public ObservableCollection<ExamSummary> Exams { get; } = new();
    public ObservableCollection<FolderItem> Folders { get; } = new();
    public ObservableCollection<StudentResult> StudentResults { get; } = new();
    public ObservableCollection<StudentItem> Students { get; } = new();
    public ObservableCollection<DraftItem> Drafts { get; } = new();
    public ObservableCollection<PeerItem> Peers { get; } = new();
    public ObservableCollection<AttemptItem> TeacherResults { get; } = new();

    public MainViewModel(AcadexApiClient api, SessionStore sessionStore)
    {
        _api = api;
        _sessionStore = sessionStore;
        ConnectCommand = new AsyncCommand(ConnectAsync);
        LoginCommand = new AsyncCommand(LoginAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Email) && Password.Length > 0);
        RegisterCommand = new AsyncCommand(RegisterAsync, () => !IsBusy);
        LogoutCommand = new AsyncCommand(LogoutAsync);
        RefreshCommand = new AsyncCommand(RefreshCurrentPageAsync, () => IsAuthenticated && !IsBusy);
        NavigateCommand = new AsyncCommand(() => RefreshCurrentPageAsync());
        CreateExamCommand = new AsyncCommand(CreateExamAsync, () => IsTeacher && !IsBusy);
        NewQuestionCommand = new AsyncCommand(AddQuestionAsync, () => IsTeacher && !IsBusy);
        CreateFolderCommand = new AsyncCommand(CreateFolderAsync, () => IsTeacher && !IsBusy);
        SaveDraftCommand = new AsyncCommand(SaveDraftAsync, () => IsTeacher && !IsBusy);
        LoadStudentsCommand = new AsyncCommand(LoadStudentsAsync, () => IsTeacher && !IsBusy);
        ChangePasswordCommand = new AsyncCommand(ChangePasswordAsync);
        DeleteAccountCommand = new AsyncCommand(DeleteAccountAsync);
        OpenExamCommand = new AsyncCommand(OpenSelectedExamAsync);
        LoadTeacherResultsCommand = new AsyncCommand(LoadSelectedExamResultsAsync);
        RenameFolderCommand = new AsyncCommand(RenameFolderAsync);
        DeleteFolderCommand = new AsyncCommand(DeleteFolderAsync);
        DeleteExamCommand = new AsyncCommand(DeleteSelectedExamAsync);
        DeleteDraftCommand = new AsyncCommand(DeleteSelectedDraftAsync);
        LoadStudentResultCommand = new AsyncCommand(LoadSelectedStudentResultAsync);
        LoadPeerResultsCommand = new AsyncCommand(LoadPeersAsync);
        SaveSettingsCommand = new AsyncCommand(SaveSettingsAsync);
    }

    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand LoginCommand { get; }
    public AsyncCommand RegisterCommand { get; }
    public AsyncCommand LogoutCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand NavigateCommand { get; }
    public AsyncCommand CreateExamCommand { get; }
    public AsyncCommand NewQuestionCommand { get; }
    public AsyncCommand CreateFolderCommand { get; }
    public AsyncCommand SaveDraftCommand { get; }
    public AsyncCommand LoadStudentsCommand { get; }
    public AsyncCommand ChangePasswordCommand { get; }
    public AsyncCommand DeleteAccountCommand { get; }
    public AsyncCommand OpenExamCommand { get; }
    public AsyncCommand LoadTeacherResultsCommand { get; }
    public AsyncCommand RenameFolderCommand { get; }
    public AsyncCommand DeleteFolderCommand { get; }
    public AsyncCommand DeleteExamCommand { get; }
    public AsyncCommand DeleteDraftCommand { get; }
    public AsyncCommand LoadStudentResultCommand { get; }
    public AsyncCommand LoadPeerResultsCommand { get; }
    public AsyncCommand SaveSettingsCommand { get; }

    public ApiUser? User { get => _user; private set { if (Set(ref _user, value)) { Raise(nameof(UserName)); Raise(nameof(UserRole)); Raise(nameof(UserInitials)); } } }
    public string UserName => User?.DisplayName ?? User?.Email ?? "Guest";
    public string UserRole => User?.Role?.ToUpperInvariant() ?? "OFFLINE";
    public string UserInitials => string.Concat((User?.DisplayName ?? "A").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0])).ToUpperInvariant();
    public bool IsTeacher { get => _isTeacher; private set { if (Set(ref _isTeacher, value)) { Raise(nameof(IsStudent)); Raise(nameof(IsTeacher)); } } }
    public bool IsStudent => IsAuthenticated && !IsTeacher;
    public bool IsAuthenticated { get => _isAuthenticated; private set { if (Set(ref _isAuthenticated, value)) { Raise(nameof(IsGuest)); Raise(nameof(IsStudent)); Raise(nameof(IsTeacher)); } } }
    public bool IsGuest => !IsAuthenticated;
    public bool IsBusy { get => _isBusy; private set { if (Set(ref _isBusy, value)) { Raise(nameof(IsNotBusy)); RefreshCommands(); } } }
    public bool IsNotBusy => !IsBusy;
    public bool IsRegistering { get => _isRegistering; set => Set(ref _isRegistering, value); }
    public string RegisterRole { get => _registerRole; set => Set(ref _registerRole, value); }
    public string Email { get => _email; set { if (Set(ref _email, value)) RefreshCommands(); } }
    public string Password { get => _password; set { if (Set(ref _password, value)) RefreshCommands(); } }
    public string DisplayName { get => _displayName; set => Set(ref _displayName, value); }
    public string StudentClass { get => _studentClass; set => Set(ref _studentClass, value); }
    public string ApiUrl { get => _apiUrl; set => Set(ref _apiUrl, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Error { get => _error; private set { if (Set(ref _error, value)) Raise(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string Success { get => _success; private set { if (Set(ref _success, value)) Raise(nameof(HasSuccess)); } }
    public bool HasSuccess => !string.IsNullOrWhiteSpace(Success);
    public string CurrentPage { get => _currentPage; private set { if (Set(ref _currentPage, value)) Raise(nameof(PageTitle)); } }
    public string PageTitle => CurrentPage switch { "Home" => "Overview", "Exams" => "Exams", "Folders" => "Folders", "Create" => "Create exam", "Results" => "Results", "Students" => "Students", "Drafts" => "Drafts", "Settings" => "Settings", _ => CurrentPage };

    public string ExamTitle { get => _examTitle; set => Set(ref _examTitle, value); }
    public string ExamPassword { get => _examPassword; set => Set(ref _examPassword, value); }
    public string ExamDurationMinutes { get => _examDurationMinutes; set => Set(ref _examDurationMinutes, value); }
    public string ExamType { get => _examType; set { if (Set(ref _examType, value)) Raise(nameof(IsTemplateExam)); } }
    public bool IsTemplateExam => ExamType == "template";
    public string QuestionText { get => _questionText; set => Set(ref _questionText, value); }
    public string QuestionA { get => _questionA; set => Set(ref _questionA, value); }
    public string QuestionB { get => _questionB; set => Set(ref _questionB, value); }
    public string QuestionC { get => _questionC; set => Set(ref _questionC, value); }
    public string QuestionD { get => _questionD; set => Set(ref _questionD, value); }
    public int CorrectOption { get => _correctOption; set => Set(ref _correctOption, value); }
    public string QuestionMark { get => _questionMark; set => Set(ref _questionMark, value); }
    public string NegativeMarking { get => _negativeMarking; set => Set(ref _negativeMarking, value); }
    public string PassMark { get => _passMark; set => Set(ref _passMark, value); }
    public string ResultVisibility { get => _resultVisibility; set => Set(ref _resultVisibility, value); }
    public bool AllowRetake { get => _allowRetake; set => Set(ref _allowRetake, value); }
    public string MaxAttempts { get => _maxAttempts; set => Set(ref _maxAttempts, value); }
    public string SelectedFolderId { get => _selectedFolderId; set => Set(ref _selectedFolderId, value); }
    public string FolderName { get => _folderName; set => Set(ref _folderName, value); }
    public string NewFolderName { get => _newFolderName; set => Set(ref _newFolderName, value); }
    public string DraftTitle { get => _draftTitle; set => Set(ref _draftTitle, value); }
    public string CurrentPassword { get => _currentPassword; set => Set(ref _currentPassword, value); }
    public string NewPassword { get => _newPassword; set => Set(ref _newPassword, value); }
    public string ConfirmDeletePassword { get => _confirmDeletePassword; set => Set(ref _confirmDeletePassword, value); }

    public ExamSummary? SelectedExam { get; set; }
    public FolderItem? SelectedFolder { get => _selectedFolder; set { _selectedFolder = value; FolderName = value?.Name ?? ""; } }
    private FolderItem? _selectedFolder;
    public StudentItem? SelectedStudent { get; set; }
    public DraftItem? SelectedDraft { get; set; }
    public StudentResult? SelectedStudentResult { get; set; }
    public AttemptItem? SelectedAttempt { get; set; }

    public int PendingQuestionCount => _pendingQuestions.Count;
    public string PendingQuestionsText => _pendingQuestions.Count == 0 ? "No questions added yet." :
        string.Join(Environment.NewLine + Environment.NewLine, _pendingQuestions.Select((q, i) =>
            $"{i + 1}. {q.Text}  ·  correct: {q.Options.ElementAtOrDefault(q.Answer) ?? "?"}  ·  {q.Marks} mark(s)"));

    private readonly List<QuestionDto> _pendingQuestions = new();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await _sessionStore.LoadAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                _api.SetToken(stored);
                var me = await _api.GetMeAsync(cancellationToken);
                ApplyUser(me.User);
                await RefreshCurrentPageAsync();
                return;
            }
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException)
        {
            _api.SetToken(null);
            await _sessionStore.DeleteAsyncSafe();
        }
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        await RunBusyAsync(async ct =>
        {
            _api.SetBaseAddress(ApiUrl);
            var progress = new Progress<string>(value => Status = value);
            var h = await _api.WaitForServiceAsync(progress, 6, ct);
            Status = $"Connected to Acadex · {h.Version ?? "API online"}";
        });
    }

    private async Task LoginAsync()
    {
        await RunBusyAsync(async ct =>
        {
            Error = ""; Success = "";
            _api.SetBaseAddress(ApiUrl);
            await _api.WaitForServiceAsync(new Progress<string>(x => Status = x), 6, ct);
            var response = await _api.LoginAsync(Email.Trim(), Password, ct);
            _api.SetToken(response.Token);
            await _sessionStore.SaveAsync(response.Token, ct);
            ApplyUser(response.User);
            Password = "";
            Success = "Signed in successfully.";
            await RefreshCurrentPageCoreAsync(ct);
        });
    }

    private async Task RegisterAsync()
    {
        if (RegisterRole == "student" && string.IsNullOrWhiteSpace(StudentClass))
        { Error = "Class is required for student accounts."; return; }
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Email) || Password.Length < 6)
        { Error = "Enter your name, email, and a password of at least 6 characters."; return; }
        await RunBusyAsync(async ct =>
        {
            _api.SetBaseAddress(ApiUrl);
            var response = await _api.RegisterAsync(RegisterRole, Email.Trim(), Password, DisplayName.Trim(),
                RegisterRole == "student" ? StudentClass.Trim() : null, ct);
            _api.SetToken(response.Token);
            await _sessionStore.SaveAsync(response.Token, ct);
            ApplyUser(response.User);
            Password = "";
            Success = "Your account has been created.";
            await RefreshCurrentPageCoreAsync(ct);
        });
    }

    public async Task AcceptLoginAsync(AuthResponse response)
    {
        _api.SetToken(response.Token);
        await _sessionStore.SaveAsync(response.Token);
        ApplyUser(response.User);
        await RefreshCurrentPageAsync();
    }

    private void ApplyUser(ApiUser user)
    {
        User = user;
        IsTeacher = user.Role.Equals("teacher", StringComparison.OrdinalIgnoreCase);
        IsAuthenticated = true;
        CurrentPage = "Home";
        RefreshCommands();
    }

    private async Task LogoutAsync()
    {
        await RunBusyAsync(async ct =>
        {
            try { await _api.LogoutAsync(ct); } catch { _api.SetToken(null); }
            _sessionStore.Delete();
            User = null; IsTeacher = false; IsAuthenticated = false;
            Exams.Clear(); Folders.Clear(); StudentResults.Clear(); Students.Clear(); Drafts.Clear(); Peers.Clear(); TeacherResults.Clear();
            SelectedExam = null; SelectedFolder = null; SelectedStudent = null; SelectedDraft = null;
            Success = "Signed out.";
        });
    }

    private async Task RefreshCurrentPageAsync() => await RunBusyAsync(RefreshCurrentPageCoreAsync);

    private async Task RefreshCurrentPageCoreAsync(CancellationToken ct)
    {
        if (!IsAuthenticated) return;
        switch (CurrentPage)
        {
            case "Exams": if (IsTeacher) await LoadExamsAsync(ct); break;
            case "Folders": if (IsTeacher) await LoadFoldersAsync(ct); break;
            case "Results":
                if (IsTeacher) await LoadExamsAsync(ct);
                else await LoadStudentResultsAsync(ct);
                break;
            case "Students": if (IsTeacher) await LoadStudentsCoreAsync(ct); else await LoadStudentTeachersAsync(ct); break;
            case "Drafts": if (IsTeacher) await LoadDraftsAsync(ct); break;
            case "Home":
            default:
                if (IsTeacher) { await LoadExamsAsync(ct); await LoadFoldersAsync(ct); }
                else await LoadStudentResultsAsync(ct);
                break;
        }
    }

    public async Task NavigateAsync(string page)
    {
        if (!IsAuthenticated) return;
        CurrentPage = page;
        await RefreshCurrentPageAsync();
    }

    private async Task LoadExamsAsync(CancellationToken ct)
    {
        var response = await _api.GetJsonAsync<TeacherExamsResponse>("api/teacher/exams", cancellationToken: ct);
        Replace(Exams, response.Exams);
    }
    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        var response = await _api.GetJsonAsync<FoldersResponse>("api/teacher/folders", cancellationToken: ct);
        Replace(Folders, response.Folders);
    }
    private async Task LoadStudentResultsAsync(CancellationToken ct)
    {
        var response = await _api.GetJsonAsync<StudentResultsResponse>("api/student/results", cancellationToken: ct);
        Replace(StudentResults, response.Results);
    }
    private async Task LoadStudentTeachersAsync(CancellationToken ct)
    {
        var response = await _api.GetJsonAsync<TeachersResponse>("api/student/teachers", cancellationToken: ct);
        Replace(Students, response.Teachers.Select(x => new StudentItem(x.Id, x.Email, x.DisplayName, null)));
    }
    private async Task LoadDraftsAsync(CancellationToken ct)
    {
        var response = await _api.GetJsonAsync<DraftsResponse>("api/teacher/drafts", cancellationToken: ct);
        Replace(Drafts, response.Drafts);
    }
    private async Task LoadStudentsCoreAsync(CancellationToken ct)
    {
        var response = await _api.GetJsonAsync<StudentsResponse>("api/teacher/students", cancellationToken: ct);
        Replace(Students, response.Students);
    }

    private async Task CreateExamAsync()
    {
        if (!IsTeacher) return;
        if (!double.TryParse(ExamDurationMinutes, out var minutes) || minutes <= 0) { Error = "Duration must be positive."; return; }
        if (string.IsNullOrWhiteSpace(ExamTitle) || string.IsNullOrWhiteSpace(ExamPassword)) { Error = "Exam title and password are required."; return; }
        if (ExamType != "template") { Error = "PDF creation requires the B2 upload workflow; it is not enabled yet. Template creation works here."; return; }
        if (_pendingQuestions.Count == 0) { Error = "Add at least one question first."; return; }
        if (!double.TryParse(NegativeMarking, out var negative) || negative < 0 || negative > 1) { Error = "Negative marking must be from 0 to 1."; return; }
        int? max = null;
        if (!string.IsNullOrWhiteSpace(MaxAttempts)) { if (!int.TryParse(MaxAttempts, out var n) || n < 1) { Error = "Maximum attempts must be a positive integer."; return; } max = n; }
        double? pass = null;
        if (!string.IsNullOrWhiteSpace(PassMark)) { if (!double.TryParse(PassMark, out var n) || n < 0 || n > 100 || n % 1 != 0) { Error = "Pass mark must be a whole percentage from 0 to 100."; return; } pass = n; }
        await RunBusyAsync(async ct =>
        {
            var body = new {
                title = ExamTitle.Trim(), studentPassword = ExamPassword, durationMs = (long)(minutes * 60000),
                type = "template", questions = _pendingQuestions.ToArray(),
                folderId = string.IsNullOrWhiteSpace(SelectedFolderId) ? null : SelectedFolderId,
                allowRetake = AllowRetake, maxAttempts = max, negativeMarking = negative, passMarkPercent = pass,
                resultVisibility = ResultVisibility, peersVisible = true
            };
            var d = await _api.SendJsonAsync<JsonElement>(HttpMethod.Post, "exam/create", body, cancellationToken: ct);
            var url = d.TryGetProperty("url", out var p) ? p.GetString() : null;
            Success = "Exam created successfully." + (url is null ? "" : " " + url);
            ExamTitle = ""; ExamPassword = ""; _pendingQuestions.Clear();
            Raise(nameof(PendingQuestionCount)); Raise(nameof(PendingQuestionsText));
            CurrentPage = "Exams"; await LoadExamsAsync(ct);
        });
    }

    private Task AddQuestionAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestionText) || new[] { QuestionA, QuestionB, QuestionC, QuestionD }.Any(string.IsNullOrWhiteSpace))
        { Error = "Enter the question and all four options."; return Task.CompletedTask; }
        if (!double.TryParse(QuestionMark, out var marks) || marks <= 0 || marks > 100) { Error = "Marks must be greater than 0 and at most 100."; return Task.CompletedTask; }
        _pendingQuestions.Add(new QuestionDto("mcq", QuestionText.Trim(), new[] { QuestionA.Trim(), QuestionB.Trim(), QuestionC.Trim(), QuestionD.Trim() }, CorrectOption, marks));
        QuestionText = QuestionA = QuestionB = QuestionC = QuestionD = "";
        QuestionMark = "1"; CorrectOption = 0; Raise(nameof(PendingQuestionCount)); Raise(nameof(PendingQuestionsText));
        Error = ""; Success = "Question added.";
        return Task.CompletedTask;
    }

    private async Task CreateFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFolderName)) { Error = "Enter a folder name."; return; }
        await RunBusyAsync(async ct =>
        {
            await _api.SendJsonAsync<JsonElement>(HttpMethod.Post, "api/teacher/folders", new { name = NewFolderName.Trim() }, cancellationToken: ct);
            NewFolderName = ""; await LoadFoldersAsync(ct); Success = "Folder created.";
        });
    }

    private async Task RenameFolderAsync()
    {
        if (SelectedFolder is null) return;
        if (string.IsNullOrWhiteSpace(FolderName)) { Error = "Folder name is required."; return; }
        await RunBusyAsync(async ct =>
        {
            await _api.SendJsonAsync<JsonElement>(HttpMethod.Patch, $"api/teacher/folders/{Uri.EscapeDataString(SelectedFolder.Id)}",
                new { name = FolderName.Trim() }, cancellationToken: ct);
            await LoadFoldersAsync(ct); Success = "Folder renamed.";
        });
    }

    private async Task DeleteFolderAsync()
    {
        if (SelectedFolder is null) return;
        if (System.Windows.MessageBox.Show("Delete this folder? Its exams will remain and become unfiled.", "Delete folder",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
        await RunBusyAsync(async ct =>
        {
            await _api.SendJsonAsync<JsonElement>(HttpMethod.Delete, $"api/teacher/folders/{Uri.EscapeDataString(SelectedFolder.Id)}", new { }, cancellationToken: ct);
            await LoadFoldersAsync(ct); await LoadExamsAsync(ct); Success = "Folder deleted.";
        });
    }

    private async Task DeleteSelectedExamAsync()
    {
        if (SelectedExam is null) return;
        if (System.Windows.MessageBox.Show($"Permanently delete '{SelectedExam.Title}' and all its results?", "Delete exam",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
        await RunBusyAsync(async ct =>
        {
            await _api.SendJsonAsync<JsonElement>(HttpMethod.Delete, $"api/teacher/exams/{Uri.EscapeDataString(SelectedExam.Id)}", new { }, cancellationToken: ct);
            await LoadExamsAsync(ct); Success = "Exam deleted.";
        });
    }

    private async Task DeleteSelectedDraftAsync()
    {
        if (SelectedDraft is null) return;
        if (System.Windows.MessageBox.Show($"Delete draft '{SelectedDraft.Title}'?", "Delete draft",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
        await RunBusyAsync(async ct =>
        {
            await _api.SendJsonAsync<JsonElement>(HttpMethod.Delete, $"api/teacher/drafts/{Uri.EscapeDataString(SelectedDraft.Id)}", new { }, cancellationToken: ct);
            await LoadDraftsAsync(ct); Success = "Draft deleted.";
        });
    }

    private async Task LoadSelectedExamResultsAsync()
    {
        if (SelectedExam is null) { Error = "Select an exam first."; return; }
        await RunBusyAsync(async ct =>
        {
            var data = await _api.GetJsonAsync<ExamResultsResponse>($"api/teacher/exams/{Uri.EscapeDataString(SelectedExam.Id)}/results", cancellationToken: ct);
            Replace(TeacherResults, data.Results);
            CurrentPage = "Results";
        });
    }

    private async Task OpenSelectedExamAsync()
    {
        if (SelectedExam is null) { Error = "Select an exam first."; return; }
        await RunBusyAsync(async ct =>
        {
            var data = await _api.GetJsonAsync<JsonElement>($"api/teacher/exams/{Uri.EscapeDataString(SelectedExam.Id)}", cancellationToken: ct);
            var view = new ExamDetailWindow(data) { Owner = System.Windows.Application.Current.MainWindow };
            view.ShowDialog();
        });
    }

    private async Task LoadSelectedStudentResultAsync()
    {
        if (SelectedStudentResult is null || string.IsNullOrWhiteSpace(SelectedStudentResult.Id)) return;
        await RunBusyAsync(async ct =>
        {
            var data = await _api.GetJsonAsync<JsonElement>($"api/student/results/{Uri.EscapeDataString(SelectedStudentResult.Id)}", cancellationToken: ct);
            var view = new ExamDetailWindow(data) { Owner = System.Windows.Application.Current.MainWindow };
            view.ShowDialog();
        });
    }

    private async Task LoadPeersAsync()
    {
        var examId = SelectedExam?.Id ?? StudentResults.FirstOrDefault()?.ExamId;
        if (string.IsNullOrWhiteSpace(examId)) { Error = "No exam is selected."; return; }
        await RunBusyAsync(async ct =>
        {
            var data = await _api.GetJsonAsync<PeersResponse>($"api/student/exams/{Uri.EscapeDataString(examId)}/peers", cancellationToken: ct);
            Replace(Peers, data.Peers);
            CurrentPage = "Results";
        });
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            _api.SetBaseAddress(ApiUrl);
            var health = await _api.CheckHealthAsync();
            Status = $"Connected · {health.Version ?? "API online"}";
            Success = "API URL saved for this session.";
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword) || NewPassword.Length < 6) { Error = "Enter current password and a new password of at least 6 characters."; return; }
        await RunBusyAsync(async ct =>
        {
            await _api.SendJsonAsync<JsonElement>(HttpMethod.Post, "api/auth/change-password",
                new { currentPassword = CurrentPassword, newPassword = NewPassword }, cancellationToken: ct);
            CurrentPassword = ""; NewPassword = ""; Success = "Password changed.";
        });
    }

    private async Task DeleteAccountAsync()
    {
        if (System.Windows.MessageBox.Show("This permanently deletes your Acadex account, owned exams, and associated submissions. Continue?",
            "Delete account", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;
        if (string.IsNullOrWhiteSpace(ConfirmDeletePassword)) { Error = "Enter your password to confirm deletion."; return; }
        await RunBusyAsync(async ct =>
        {
            await _api.SendJsonAsync<JsonElement>(HttpMethod.Delete, "api/auth/account", new { password = ConfirmDeletePassword }, cancellationToken: ct);
            _sessionStore.Delete(); _api.SetToken(null); User = null; IsAuthenticated = false; IsTeacher = false;
            ConfirmDeletePassword = ""; Success = "Account deleted.";
        });
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Error = "";
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await action(cts.Token); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { Error = "The request was cancelled or timed out."; }
        catch (ApiException ex)
        {
            Error = ex.StatusCode == 401 && IsAuthenticated ? "Your session expired. Please sign in again." : ex.Message;
            if (ex.StatusCode == 401 && IsAuthenticated) { _api.SetToken(null); _sessionStore.Delete(); User = null; IsAuthenticated = false; IsTeacher = false; }
        }
        catch (HttpRequestException) { Error = "Could not connect to Acadex. Check the network and retry."; }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    private void RefreshCommands()
    {
        LoginCommand.Refresh(); RegisterCommand.Refresh(); RefreshCommand.Refresh();
        CreateExamCommand.Refresh(); NewQuestionCommand.Refresh(); CreateFolderCommand.Refresh();
        SaveDraftCommand.Refresh(); LoadStudentsCommand.Refresh(); OpenExamCommand.Refresh(); LoadTeacherResultsCommand.Refresh();
        RenameFolderCommand.Refresh(); DeleteFolderCommand.Refresh(); DeleteExamCommand.Refresh(); DeleteDraftCommand.Refresh();
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear(); foreach (var value in values) collection.Add(value);
    }

    private sealed record QuestionDto(string Type, string Text, string[] Options, int Answer, double Marks);
    private sealed record TeachersResponse(IReadOnlyList<TeacherDto> Teachers);
    private sealed record TeacherDto(string Id, string Email, string DisplayName);
    private sealed record StudentsResponse(IReadOnlyList<StudentItem> Students);
    private sealed record DraftsResponse(IReadOnlyList<DraftItem> Drafts);
    private sealed record ExamResultsResponse(JsonElement Exam, bool Pdf, IReadOnlyList<AttemptItem> Results);
    private sealed record PeersResponse(IReadOnlyList<PeerItem> Peers);
}
