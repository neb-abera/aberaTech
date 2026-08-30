using aberaTech.Fitness;
using Xunit;

namespace aberaTech.Server.Tests.Fitness;

/// <summary>
/// The decision table for the fitness surface. The Development bypass exists
/// for `make up`; these tests are what keep it from ever applying anywhere
/// else.
/// </summary>
public sealed class FitnessGateTests
{
    private const string Db = "Host=localhost;Database=fitness";

    [Fact]
    public void Production_never_honors_the_development_bypass()
    {
        var options = new FitnessOptions { DevelopmentOwner = true };

        Assert.True(FitnessGate.RequiresOwnerSignIn(isDevelopment: false, options));
        Assert.False(FitnessGate.IsEnabled(
            isDevelopment: false, options, adminAuthConfigured: false, Db));
    }

    [Fact]
    public void Development_without_the_flag_still_requires_sign_in()
    {
        var options = new FitnessOptions();

        Assert.True(FitnessGate.RequiresOwnerSignIn(isDevelopment: true, options));
        Assert.False(FitnessGate.IsEnabled(
            isDevelopment: true, options, adminAuthConfigured: false, Db));
    }

    [Fact]
    public void Development_with_the_flag_enables_without_credentials()
    {
        var options = new FitnessOptions { DevelopmentOwner = true };

        Assert.False(FitnessGate.RequiresOwnerSignIn(isDevelopment: true, options));
        Assert.True(FitnessGate.IsEnabled(
            isDevelopment: true, options, adminAuthConfigured: false, Db));
    }

    [Fact]
    public void No_database_means_no_surface_bypass_or_not()
    {
        var bypass = new FitnessOptions { DevelopmentOwner = true };
        var real = new FitnessOptions { AllowedEmails = ["a@b.c"] };

        Assert.False(FitnessGate.IsEnabled(true, bypass, true, connectionString: null));
        Assert.False(FitnessGate.IsEnabled(false, real, true, connectionString: " "));
    }

    [Fact]
    public void The_real_path_needs_both_the_allowlist_and_the_sign_in_schemes()
    {
        var allowlistOnly = new FitnessOptions { AllowedEmails = ["a@b.c"] };
        var nothing = new FitnessOptions();

        Assert.False(FitnessGate.IsEnabled(false, allowlistOnly, adminAuthConfigured: false, Db));
        Assert.False(FitnessGate.IsEnabled(false, nothing, adminAuthConfigured: true, Db));
        Assert.True(FitnessGate.IsEnabled(false, allowlistOnly, adminAuthConfigured: true, Db));
    }
}
