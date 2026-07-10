using AutoCheck.Services;
using FluentAssertions;
using Xunit;

namespace AutoCheck.Tests.Services;

/// <summary>UTC → Europe/Kyiv projection used for all displayed timestamps.</summary>
public class KyivTimeTests
{
    [Fact]
    public void FromUtc_Winter_IsUtcPlus2()
    {
        // January = standard time; real Europe/Kyiv and the no-tz-db fallback both = +2
        var utc  = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var kyiv = KyivTime.FromUtc(utc);
        (kyiv - utc).TotalHours.Should().Be(2);
    }

    [Fact]
    public void FromUtc_Summer_IsUtcPlus2Or3()
    {
        // July = DST (+3) with a real tz database; the fixed fallback stays +2
        var utc    = new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);
        var offset = (KyivTime.FromUtc(utc) - utc).TotalHours;
        offset.Should().BeInRange(2, 3);
    }

    [Fact]
    public void FromUtc_TreatsUnspecifiedKindAsUtc()
    {
        var unspecified = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);
        var utc         = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        KyivTime.FromUtc(unspecified).Should().Be(KyivTime.FromUtc(utc));
    }
}
