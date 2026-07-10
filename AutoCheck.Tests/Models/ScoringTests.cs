using AutoCheck.Models;
using FluentAssertions;
using Xunit;

namespace AutoCheck.Tests.Models;

/// <summary>Difficulty-weighted attempt score — the heart of auto-grading.</summary>
public class ScoringTests
{
    [Fact]
    public void Weighted_Empty_ReturnsZero() =>
        Scoring.Weighted(Array.Empty<(int, int)>()).Should().Be(0);

    [Fact]
    public void Weighted_SingleItem_ReturnsItsScore() =>
        Scoring.Weighted(new[] { (85, 2) }).Should().Be(85);

    [Fact]
    public void Weighted_EqualDifficulty_IsPlainAverage() =>
        Scoring.Weighted(new[] { (100, 1), (0, 1), (50, 1) }).Should().Be(50);

    [Fact]
    public void Weighted_HarderTaskCountsMore() =>
        // 100×3 + 0×1 = 300 / 4 = 75
        Scoring.Weighted(new[] { (100, 3), (0, 1) }).Should().Be(75);

    [Fact]
    public void Weighted_ZeroTotalWeight_FallsBackToPlainAverage() =>
        // difficulty all 0 → average of scores: (80 + 40) / 2 = 60
        Scoring.Weighted(new[] { (80, 0), (40, 0) }).Should().Be(60);

    [Theory]
    [InlineData(90, 1, 70, 1, 80)]    // equal weights → average
    [InlineData(100, 4, 50, 1, 90)]   // 400 + 50 = 450 / 5 = 90
    [InlineData(60, 2, 90, 1, 70)]    // 120 + 90 = 210 / 3 = 70
    public void Weighted_Theory(int s1, int d1, int s2, int d2, int expected) =>
        Scoring.Weighted(new[] { (s1, d1), (s2, d2) }).Should().Be(expected);

    // ── Final = 0.4·auto + 0.6·defense ──────────────────────────────────────

    [Theory]
    [InlineData(100, 100, 100)]
    [InlineData(0, 0, 0)]
    [InlineData(90, 80, 84)]    // 36 + 48 = 84
    [InlineData(50, 100, 80)]   // 20 + 60 = 80
    [InlineData(100, 50, 70)]   // 40 + 30 = 70 — defence weighs more than auto
    public void Final_AppliesWeightedFormula(int auto, int defense, int expected) =>
        Scoring.Final(auto, defense).Should().Be(expected);

    [Fact]
    public void Final_DefenceOutweighsAuto() =>
        // same spread, but the higher defence wins because it carries 60%
        Scoring.Final(40, 90).Should().BeGreaterThan(Scoring.Final(90, 40));
}
