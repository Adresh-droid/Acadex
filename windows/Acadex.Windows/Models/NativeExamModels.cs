using System.Text.Json;

namespace Acadex.Windows.Models;

public sealed record NativeQuestion(string Type, string Text, IReadOnlyList<string>? Options, int? Answer, double? Marks);
public sealed record NativeExamResult(string? SubmissionId, bool? Submitted, bool? Released, double? Score, double? Total, double? Percentage);
public sealed record NativeExamProgress(
    Dictionary<string, JsonElement> Answers,
    int CurrentQuestion,
    int PdfPage,
    double PdfScrollRatio);
