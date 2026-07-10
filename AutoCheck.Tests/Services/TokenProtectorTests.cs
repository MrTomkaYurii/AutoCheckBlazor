using AutoCheck.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace AutoCheck.Tests.Services;

/// <summary>Encryption of student GitHub tokens at rest.</summary>
public class TokenProtectorTests
{
    private static TokenProtector New() => new(new EphemeralDataProtectionProvider());

    [Fact]
    public void ProtectThenUnprotect_Roundtrips()
    {
        var tp  = New();
        var enc = tp.Protect("ghp_secret123");

        enc.Should().StartWith("enc:v1:").And.NotContain("ghp_secret123");
        tp.Unprotect(enc).Should().Be("ghp_secret123");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Protect_NullOrEmpty_ReturnsEmpty(string? input) =>
        New().Protect(input).Should().BeEmpty();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Unprotect_NullOrEmpty_ReturnsEmpty(string? input) =>
        New().Unprotect(input).Should().BeEmpty();

    [Fact]
    public void Unprotect_LegacyPlaintext_PassesThrough() =>
        // values without the enc:v1: prefix are pre-encryption tokens — kept as-is
        New().Unprotect("ghp_legacy_plain").Should().Be("ghp_legacy_plain");

    [Fact]
    public void Unprotect_WithDifferentKeyRing_ReturnsEmpty()
    {
        var enc   = New().Protect("secret");   // encrypted with key ring A
        var other = New();                      // fresh instance = different key ring B
        other.Unprotect(enc).Should().BeEmpty(); // undecryptable → treated as unset
    }
}
