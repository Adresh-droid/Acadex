namespace Acadex.Windows.Models;

public sealed record StudentItem(string Id, string Email, string DisplayName, string? ClassName);
public sealed record DraftItem(string Id, string Title, long UpdatedAt, System.Text.Json.JsonElement Data);
public sealed record PeerItem(int Rank, string DisplayName, string? ClassName, double BestPercentage, bool IsMe);
public sealed record AttemptItem(
    string? Id,
    string? SessionToken,
    string? StudentId,
    string? StudentName,
    bool? Submitted,
    long? AttemptedAt,
    long? SubmittedAt,
    double? Score = null,
    double? Total = null,
    double? Percentage = null,
    string? PublicResultUrl = null);
public sealed record FolderWriteResponse(FolderItem Folder);
