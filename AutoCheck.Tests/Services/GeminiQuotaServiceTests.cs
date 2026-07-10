using AutoCheck.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AutoCheck.Tests.Services;

/// <summary>Daily Gemini call quota (protects the API budget).</summary>
public class GeminiQuotaServiceTests
{
    private static GeminiQuotaService New(string? limit)
    {
        var cfg = new Mock<IConfiguration>();
        cfg.Setup(c => c["Gemini:DailyLimit"]).Returns(limit);
        return new GeminiQuotaService(cfg.Object);
    }

    [Fact]
    public void DailyLimit_DefaultsTo50_WhenUnset() =>
        New(null).DailyLimit.Should().Be(50);

    [Theory]
    [InlineData("0")]      // non-positive → default
    [InlineData("-5")]
    [InlineData("abc")]    // unparseable → default
    public void DailyLimit_FallsBackTo50_WhenInvalid(string value) =>
        New(value).DailyLimit.Should().Be(50);

    [Fact]
    public void DailyLimit_ReadsConfiguredValue() =>
        New("30").DailyLimit.Should().Be(30);

    [Fact]
    public void RecordCall_IncrementsTodayCountAndReducesRemaining()
    {
        var q = New("10");
        q.RecordCall();
        q.RecordCall();

        q.TodayCount.Should().Be(2);
        q.Remaining.Should().Be(8);
        q.IsExhausted.Should().BeFalse();
    }

    [Fact]
    public void IsExhausted_TrueOnceLimitReached()
    {
        var q = New("2");
        q.RecordCall();
        q.RecordCall();

        q.Remaining.Should().Be(0);
        q.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void Remaining_NeverGoesNegative()
    {
        var q = New("1");
        q.RecordCall();
        q.RecordCall();   // over the limit
        q.Remaining.Should().Be(0);
    }
}
