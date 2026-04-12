using ddpc.DartSuite.Application.Contracts.Notifications;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Infrastructure.Persistence;
using ddpc.DartSuite.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Infrastructure.Tests;

public sealed class NotificationAndPreferencesTests
{
    private static DartSuiteDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DartSuiteDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(TournamentManagementService service, Guid tournamentId)> SetupWithTournamentAsync()
    {
        var db = CreateDbContext();
        var service = new TournamentManagementService(db);
        var tournament = await service.CreateTournamentAsync(new CreateTournamentRequest(
            "Test-Turnier", "manager", DateOnly.FromDateTime(DateTime.Today), null, false, "Knockout", "OnSite"));
        return (service, tournament.Id);
    }

    [Fact]
    public async Task Subscribe_ShouldPersistSubscription()
    {
        var (service, tournamentId) = await SetupWithTournamentAsync();

        var result = await service.SubscribeNotificationsAsync(new CreateNotificationSubscriptionRequest(
            tournamentId, "user-a", "https://push.example.com/sub1", "p256dh-key", "auth-key", "OwnMatches"));

        result.TournamentId.Should().Be(tournamentId);
        result.UserAccountName.Should().Be("user-a");
        result.Endpoint.Should().Be("https://push.example.com/sub1");
        result.NotificationPreference.Should().Be("OwnMatches");
    }

    [Fact]
    public async Task Subscribe_DuplicateEndpoint_ShouldUpdate()
    {
        var (service, tournamentId) = await SetupWithTournamentAsync();

        await service.SubscribeNotificationsAsync(new CreateNotificationSubscriptionRequest(
            tournamentId, "user-a", "https://push.example.com/sub1", "old-p256dh", "old-auth", "OwnMatches"));

        var updated = await service.SubscribeNotificationsAsync(new CreateNotificationSubscriptionRequest(
            tournamentId, "user-a", "https://push.example.com/sub1", "new-p256dh", "new-auth", "AllMatches"));

        updated.NotificationPreference.Should().Be("AllMatches");
        updated.P256dh.Should().Be("new-p256dh");

        var subs = await service.GetNotificationSubscriptionsAsync(tournamentId, "user-a");
        subs.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetNotifications_ShouldFilterByUser()
    {
        var (service, tournamentId) = await SetupWithTournamentAsync();

        await service.SubscribeNotificationsAsync(new CreateNotificationSubscriptionRequest(
            tournamentId, "user-a", "https://push.example.com/a", "p1", "a1", "OwnMatches"));
        await service.SubscribeNotificationsAsync(new CreateNotificationSubscriptionRequest(
            tournamentId, "user-b", "https://push.example.com/b", "p2", "a2", "AllMatches"));

        var subsA = await service.GetNotificationSubscriptionsAsync(tournamentId, "user-a");
        var subsB = await service.GetNotificationSubscriptionsAsync(tournamentId, "user-b");

        subsA.Should().HaveCount(1);
        subsB.Should().HaveCount(1);
    }

    [Fact]
    public async Task Unsubscribe_ShouldRemove()
    {
        var (service, tournamentId) = await SetupWithTournamentAsync();

        var sub = await service.SubscribeNotificationsAsync(new CreateNotificationSubscriptionRequest(
            tournamentId, "user-a", "https://push.example.com/sub1", "p256dh", "auth", "OwnMatches"));

        var result = await service.UnsubscribeNotificationsAsync(sub.Id);
        var subs = await service.GetNotificationSubscriptionsAsync(tournamentId, "user-a");

        result.Should().BeTrue();
        subs.Should().BeEmpty();
    }

    [Fact]
    public async Task Unsubscribe_NonExistent_ReturnsFalse()
    {
        var (service, _) = await SetupWithTournamentAsync();

        var result = await service.UnsubscribeNotificationsAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    // ─── View Preferences ───

    [Fact]
    public async Task SaveViewPreference_ShouldPersist()
    {
        var (service, _) = await SetupWithTournamentAsync();

        var result = await service.SaveUserViewPreferenceAsync("user-a", "tournament-ko", "{\"viewMode\":\"tree\"}");

        result.UserAccountName.Should().Be("user-a");
        result.ViewContext.Should().Be("tournament-ko");
        result.SettingsJson.Should().Contain("tree");
    }

    [Fact]
    public async Task SaveViewPreference_Duplicate_ShouldUpdate()
    {
        var (service, _) = await SetupWithTournamentAsync();

        await service.SaveUserViewPreferenceAsync("user-a", "tournament-ko", "{\"viewMode\":\"tree\"}");
        var updated = await service.SaveUserViewPreferenceAsync("user-a", "tournament-ko", "{\"viewMode\":\"round\"}");

        updated.SettingsJson.Should().Contain("round");
    }

    [Fact]
    public async Task GetViewPreference_ShouldReturnSaved()
    {
        var (service, _) = await SetupWithTournamentAsync();

        await service.SaveUserViewPreferenceAsync("user-a", "tournament-ko", "{\"viewMode\":\"tree\"}");
        var pref = await service.GetUserViewPreferenceAsync("user-a", "tournament-ko");

        pref.Should().NotBeNull();
        pref!.SettingsJson.Should().Contain("tree");
    }

    [Fact]
    public async Task GetViewPreference_NonExistent_ReturnsNull()
    {
        var (service, _) = await SetupWithTournamentAsync();

        var pref = await service.GetUserViewPreferenceAsync("user-nonexistent", "unknown-context");

        pref.Should().BeNull();
    }
}
