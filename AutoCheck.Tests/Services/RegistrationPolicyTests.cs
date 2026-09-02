using AutoCheck.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutoCheck.Tests.Services;

public class RegistrationPolicyTests
{
    private static IConfiguration Cfg(string? domain) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:AllowedEmailDomain"] = domain,
            })
            .Build();

    [Fact]
    public void EmptyConfig_AllowsAnyDomain()
    {
        RegistrationPolicy.EmailAllowed("anyone@gmail.com", Cfg("")).Should().BeTrue();
        RegistrationPolicy.EmailAllowed("anyone@gmail.com", Cfg(null)).Should().BeTrue();
    }

    [Theory]
    [InlineData("student@chnu.edu.ua", true)]
    [InlineData("Student@CHNU.EDU.UA", true)]
    [InlineData("student@gmail.com", false)]
    [InlineData("student@sub.chnu.edu.ua", false)]
    [InlineData("no-at-sign", false)]
    [InlineData("", false)]
    public void SingleDomain_GatesByExactDomain(string email, bool allowed)
    {
        RegistrationPolicy.EmailAllowed(email, Cfg("chnu.edu.ua")).Should().Be(allowed);
    }

    [Fact]
    public void MultipleDomains_AllowAny()
    {
        var cfg = Cfg("chnu.edu.ua, example.org");
        RegistrationPolicy.EmailAllowed("a@example.org", cfg).Should().BeTrue();
        RegistrationPolicy.EmailAllowed("a@chnu.edu.ua", cfg).Should().BeTrue();
        RegistrationPolicy.EmailAllowed("a@other.com", cfg).Should().BeFalse();
    }

    [Fact]
    public void Hint_EmptyWhenUnrestricted_TextWhenRestricted()
    {
        RegistrationPolicy.Hint(Cfg("")).Should().BeEmpty();
        RegistrationPolicy.Hint(Cfg("chnu.edu.ua")).Should().Contain("chnu.edu.ua");
    }
}
