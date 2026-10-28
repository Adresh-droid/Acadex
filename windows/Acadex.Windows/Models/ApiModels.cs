using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acadex.Windows.Models;

public sealed record ApiUser(
    string Id,
    string Role,
    string Email,
    string DisplayName,
    string? StudentId,
    string? ClassName);

public sealed record AuthResponse(
    string Token,
    ApiUser User);

public sealed record MeResponse(ApiUser User);

public sealed record HealthResponse(bool Ok, long Ts, string? Version);

public sealed record ExamSummary(
    string Id,
    string Title,
    string Type,
    long DurationMs,
    long CreatedAt,
    string? FolderId,
    string? FolderName,
    int Attempts,
    int SubmittedCount,
    double? AvgPercentage,
    double? PassMarkPercent,
    double NegativeMarking,
    string ResultVisibility,
    bool PeersVisible,
    bool Archived,
    long? ArchivedAt);

public sealed record TeacherExamsResponse(IReadOnlyList<ExamSummary> Exams);
public sealed record FolderItem(string Id, string Name, long CreatedAt, int ExamCount);
public sealed record FoldersResponse(IReadOnlyList<FolderItem> Folders);

public sealed record StudentResult(
    string? Id,
    string ExamId,
    string Title,
    double? Score,
    double? Total,
    double? Percentage,
    bool? Passed,
    int? AttemptNumber,
    int? AttemptCount,
    long? SubmittedAt,
    string? PublicResultUrl,
    bool? Submitted,
    bool? Released);

public sealed record StudentResultsResponse(IReadOnlyList<StudentResult> Results);

public sealed record ExamMetadata(
    string ExamId,
    string Title,
    string Type,
    long DurationMs,
    long CreatedAt,
    bool AllowRetake,
    int? MaxAttempts);

public sealed record ExamSessionResponse(
    string SessionToken,
    string StudentId,
    string StudentName,
    string DeviceId,
    long StartedAt,
    long EndAt,
    JsonElement Progress,
    string ExamId,
    string Title,
    string Type,
    string? PdfUrl,
    JsonElement Questions,
    long DurationMs,
    bool AllowRetake,
    int? MaxAttempts);

public sealed record ApiError(string? Error, string? Code);
