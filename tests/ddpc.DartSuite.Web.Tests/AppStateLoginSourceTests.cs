using ddpc.DartSuite.Application.Contracts.Autodarts;
using ddpc.DartSuite.Web.Services;
using FluentAssertions;

namespace ddpc.DartSuite.Web.Tests;

/// <summary>
/// Unit tests for <see cref="AutodartsLoginSource"/> tracking in <see cref="AppStateService"/>.
/// </summary>
public sealed class AppStateLoginSourceTests
{
    private static AutodartsProfileDto MakeProfile() => new("1", "TestUser", "DE", "test@test.com");

    [Fact]
    public void LoginSource_IsNone_Initially()
    {
        var sut = new AppStateService();

        sut.LoginSource.Should().Be(AutodartsLoginSource.None);
    }

    [Fact]
    public void SetAutodartsProfile_Manual_SetsLoginSourceManual()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(MakeProfile(), true, AutodartsLoginSource.Manual);

        sut.LoginSource.Should().Be(AutodartsLoginSource.Manual);
    }

    [Fact]
    public void SetAutodartsProfile_AutoRestore_SetsLoginSourceAutoRestore()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(MakeProfile(), true, AutodartsLoginSource.AutoRestore);

        sut.LoginSource.Should().Be(AutodartsLoginSource.AutoRestore);
    }

    [Fact]
    public void SetAutodartsProfile_DefaultSource_IsManual()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(MakeProfile(), true);

        sut.LoginSource.Should().Be(AutodartsLoginSource.Manual);
    }

    [Fact]
    public void SetAutodartsProfile_Disconnected_SetsLoginSourceNone_RegardlessOfSource()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(null, false, AutodartsLoginSource.Manual);

        sut.LoginSource.Should().Be(AutodartsLoginSource.None);
    }

    [Fact]
    public void SetAutodartsProfile_Disconnected_AutoRestore_Source_StillSetsNone()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfile(null, false, AutodartsLoginSource.AutoRestore);

        sut.LoginSource.Should().Be(AutodartsLoginSource.None);
    }

    [Fact]
    public void SetAutodartsProfileSilent_AutoRestore_SetsLoginSourceAutoRestore()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfileSilent(MakeProfile(), true, AutodartsLoginSource.AutoRestore);

        sut.LoginSource.Should().Be(AutodartsLoginSource.AutoRestore);
    }

    [Fact]
    public void SetAutodartsProfileSilent_Disconnected_SetsLoginSourceNone()
    {
        var sut = new AppStateService();

        sut.SetAutodartsProfileSilent(null, false, AutodartsLoginSource.AutoRestore);

        sut.LoginSource.Should().Be(AutodartsLoginSource.None);
    }

    [Fact]
    public void ReconnectFallback_AfterDisconnect_ResetsLoginSource()
    {
        var sut = new AppStateService();
        // Initial auto-restore
        sut.SetAutodartsProfile(MakeProfile(), true, AutodartsLoginSource.AutoRestore);
        sut.LoginSource.Should().Be(AutodartsLoginSource.AutoRestore);

        // Session lost
        sut.SetAutodartsProfile(null, false, AutodartsLoginSource.None);
        sut.LoginSource.Should().Be(AutodartsLoginSource.None);

        // Manual re-login
        sut.SetAutodartsProfile(MakeProfile(), true, AutodartsLoginSource.Manual);
        sut.LoginSource.Should().Be(AutodartsLoginSource.Manual);
    }

    [Fact]
    public void SetAutodartsProfile_FiresOnChange_WithLoginSourceSet()
    {
        var sut = new AppStateService();
        AutodartsLoginSource capturedSource = AutodartsLoginSource.None;
        sut.OnChange += () => capturedSource = sut.LoginSource;

        sut.SetAutodartsProfile(MakeProfile(), true, AutodartsLoginSource.AutoRestore);

        capturedSource.Should().Be(AutodartsLoginSource.AutoRestore);
    }

    [Fact]
    public void SetAutodartsProfileSilent_DoesNotFireOnChange_ButUpdatesLoginSource()
    {
        var sut = new AppStateService();
        var fired = false;
        sut.OnChange += () => fired = true;

        sut.SetAutodartsProfileSilent(MakeProfile(), true, AutodartsLoginSource.AutoRestore);

        fired.Should().BeFalse();
        sut.LoginSource.Should().Be(AutodartsLoginSource.AutoRestore);
    }
}
