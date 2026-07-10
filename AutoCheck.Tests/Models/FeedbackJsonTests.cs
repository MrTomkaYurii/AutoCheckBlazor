using AutoCheck.Models;
using FluentAssertions;
using Xunit;

namespace AutoCheck.Tests.Models;

/// <summary>Parsing of the Gemini feedback JSON stored in TaskResult.Feedback.</summary>
public class FeedbackJsonTests
{
    [Fact]
    public void Parse_ValidJson_ExtractsAllParts()
    {
        var (done, issues, analysis) = FeedbackJson.Parse(
            """{"done":["req A","req B"],"issues":["req C"],"analysis":"загалом добре"}""");

        done.Should().Equal("req A", "req B");
        issues.Should().Equal("req C");
        analysis.Should().Be("загалом добре");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_NullOrEmpty_ReturnsEmpty(string? raw)
    {
        var (done, issues, analysis) = FeedbackJson.Parse(raw);
        done.Should().BeEmpty();
        issues.Should().BeEmpty();
        analysis.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MalformedJson_KeepsRawAsAnalysis()
    {
        // legacy plain-text feedback (pre-JSON) falls back to the analysis field
        var (done, issues, analysis) = FeedbackJson.Parse("просто текст без json");
        done.Should().BeEmpty();
        issues.Should().BeEmpty();
        analysis.Should().Be("просто текст без json");
    }

    [Fact]
    public void Parse_MissingKeys_ReturnEmptyArrays()
    {
        var (done, issues, analysis) = FeedbackJson.Parse("""{"analysis":"тільки аналіз"}""");
        done.Should().BeEmpty();
        issues.Should().BeEmpty();
        analysis.Should().Be("тільки аналіз");
    }

    [Fact]
    public void Parse_FiltersOutEmptyStrings()
    {
        var (done, _, _) = FeedbackJson.Parse("""{"done":["a","","b"]}""");
        done.Should().Equal("a", "b");
    }
}
