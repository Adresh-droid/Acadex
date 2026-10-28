using System.Text.Json;
using Acadex.Windows.Models;
using Xunit;

namespace Acadex.Windows.Tests;

public sealed class ModelSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ParsesLoginResponseWithCamelCaseFields()
    {
        const string json = """
            {
              "token":"abc123",
              "user":{"id":"usr_1","role":"teacher","email":"t@example.com",
                      "displayName":"Teacher","studentId":null,"className":null}
            }
            """;

        var response = JsonSerializer.Deserialize<AuthResponse>(json, JsonOptions)!;
        Assert.Equal("abc123", response.Token);
        Assert.Equal("teacher", response.User.Role);
        Assert.Equal("Teacher", response.User.DisplayName);
    }

    [Fact]
    public void ParsesTeacherExamListResponse()
    {
        const string json = """
            {
              "exams":[
                {"id":"exam_1","title":"Algebra","type":"template","durationMs":60000,
                 "createdAt":123,"folderId":null,"folderName":null,"attempts":4,
                 "submittedCount":3,"avgPercentage":77.5,"passMarkPercent":40,
                 "negativeMarking":0,"resultVisibility":"instant","peersVisible":true,
                 "archived":false,"archivedAt":null}
              ]
            }
            """;
        var response = JsonSerializer.Deserialize<TeacherExamsResponse>(json, JsonOptions)!;
        Assert.Single(response.Exams);
        Assert.Equal("exam_1", response.Exams[0].Id);
        Assert.Equal(3, response.Exams[0].SubmittedCount);
    }
}
