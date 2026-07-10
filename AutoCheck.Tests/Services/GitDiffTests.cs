using AutoCheck.Services;
using FluentAssertions;
using Xunit;

namespace AutoCheck.Tests.Services;

/// <summary>Git-diff parsing that drives stored DiffEntry rows and the plagiarism corpus.</summary>
public class GitDiffTests
{
    // ── NormalizeUrl ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://github.com/user/repo", "https://github.com/user/repo.git")]
    [InlineData("https://github.com/user/repo/", "https://github.com/user/repo.git")]
    [InlineData("github.com/user/repo", "https://github.com/user/repo.git")]
    [InlineData("  github.com/user/repo  ", "https://github.com/user/repo.git")]
    [InlineData("https://github.com/user/repo.git", "https://github.com/user/repo.git")]
    [InlineData("http://github.com/user/repo", "http://github.com/user/repo.git")]
    public void NormalizeUrl_AddsSchemeAndGitSuffix(string input, string expected) =>
        GitDiff.NormalizeUrl(input).Should().Be(expected);

    // ── FilterToTask ────────────────────────────────────────────────────────

    [Fact]
    public void FilterToTask_PlainText_ReturnedUnchanged()
    {
        const string plain = "public class Foo { }";
        GitDiff.FilterToTask(plain, 1).Should().Be(plain);
    }

    [Fact]
    public void FilterToTask_KeepsOnlyMatchingTaskFile()
    {
        var diff =
            "commit abc123\n" +
            "Author: Stud\n" +
            "\n" +
            "diff --git a/Task1.cs b/Task1.cs\n" +
            "+int one = 1;\n" +
            "diff --git a/Task2.cs b/Task2.cs\n" +
            "+int two = 2;\n";

        var filtered = GitDiff.FilterToTask(diff, 2);

        filtered.Should().Contain("Task2.cs").And.Contain("int two = 2;");
        filtered.Should().NotContain("int one = 1;");
        filtered.Should().Contain("commit abc123");   // header is kept
    }

    [Fact]
    public void FilterToTask_NoMatchingFile_FallsBackToFullDiff()
    {
        var diff = "diff --git a/Task1.cs b/Task1.cs\n+int one = 1;\n";
        GitDiff.FilterToTask(diff, 9).Should().Be(diff);
    }

    // ── Parse ───────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyOrWhitespace_ReturnsEmpty()
    {
        GitDiff.Parse("").Should().BeEmpty();
        GitDiff.Parse("   \n  ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_PlainText_AllLinesAreContext()
    {
        var parsed = GitDiff.Parse("line one\nline two");
        parsed.Should().OnlyContain(l => l.Type == "ctx");
        parsed.Should().HaveCount(2);
        parsed[0].Text.Should().Be("line one");
        parsed[1].N1.Should().Be(2);   // sequential numbering
    }

    [Fact]
    public void Parse_Diff_ClassifiesAddDelContextAndHeaders()
    {
        var diff =
            "diff --git a/Task1.cs b/Task1.cs\n" +
            "index 111..222 100644\n" +
            "--- a/Task1.cs\n" +
            "+++ b/Task1.cs\n" +
            "@@ -1,2 +1,3 @@\n" +
            " unchanged line\n" +
            "-removed line\n" +
            "+added line one\n" +
            "+added line two\n";

        var parsed = GitDiff.Parse(diff);

        parsed.Where(l => l.Type == "add").Select(l => l.Text)
              .Should().Equal("added line one", "added line two");
        parsed.Should().ContainSingle(l => l.Type == "del" && l.Text == "removed line");
        parsed.Should().ContainSingle(l => l.Type == "ctx" && l.Text == "unchanged line");
        parsed.Should().Contain(l => l.Type == "hdr" && l.Text.StartsWith("@@"));
    }

    [Fact]
    public void Parse_Diff_TracksNewLineNumbersFromHunkHeader()
    {
        var diff =
            "diff --git a/Task1.cs b/Task1.cs\n" +
            "@@ -0,0 +5,2 @@\n" +
            "+first added\n" +
            "+second added\n";

        var adds = GitDiff.Parse(diff).Where(l => l.Type == "add").ToList();
        adds[0].N2.Should().Be(5);   // hunk starts new file at line 5
        adds[1].N2.Should().Be(6);
    }

    [Fact]
    public void Parse_IgnoresLinesBeforeAnyHunk()
    {
        // content between the file header and the first @@ must not become add/del/ctx
        var diff =
            "diff --git a/Task1.cs b/Task1.cs\n" +
            "similarity index 100%\n" +
            "@@ -1 +1 @@\n" +
            "+real change";

        var codeLines = GitDiff.Parse(diff).Where(l => l.Type is "add" or "del" or "ctx").ToList();

        codeLines.Should().NotContain(l => l.Text.Contains("similarity"));
        codeLines.Should().ContainSingle()
                 .Which.Text.Should().Be("real change");
    }
}
