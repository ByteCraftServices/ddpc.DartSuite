using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ddpc.DartSuite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusMonitoringAndStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordWebhookDisplayText",
                table: "Tournaments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordWebhookUrl",
                table: "Tournaments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeedTopCount",
                table: "Tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SeedingEnabled",
                table: "Tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NotificationPreference",
                table: "Participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AwaySlotOrigin",
                table: "Matches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DelayMinutes",
                table: "Matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpectedEndUtc",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeSlotOrigin",
                table: "Matches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextMatchInfo",
                table: "Matches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlannedEndUtc",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchedulingStatus",
                table: "Matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConnectionState",
                table: "Boards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExtensionStatus",
                table: "Boards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchedulingStatus",
                table: "Boards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MatchFollowers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchFollowers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchPlayerStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Average = table.Column<double>(type: "double precision", nullable: false),
                    First9Average = table.Column<double>(type: "double precision", nullable: false),
                    DartsThrown = table.Column<int>(type: "integer", nullable: false),
                    LegsWon = table.Column<int>(type: "integer", nullable: false),
                    LegsLost = table.Column<int>(type: "integer", nullable: false),
                    SetsWon = table.Column<int>(type: "integer", nullable: false),
                    SetsLost = table.Column<int>(type: "integer", nullable: false),
                    HighestCheckout = table.Column<int>(type: "integer", nullable: false),
                    CheckoutPercent = table.Column<double>(type: "double precision", nullable: false),
                    CheckoutHits = table.Column<int>(type: "integer", nullable: false),
                    CheckoutAttempts = table.Column<int>(type: "integer", nullable: false),
                    Plus100 = table.Column<int>(type: "integer", nullable: false),
                    Plus140 = table.Column<int>(type: "integer", nullable: false),
                    Plus170 = table.Column<int>(type: "integer", nullable: false),
                    Plus180 = table.Column<int>(type: "integer", nullable: false),
                    Breaks = table.Column<int>(type: "integer", nullable: false),
                    AverageDartsPerLeg = table.Column<double>(type: "double precision", nullable: false),
                    BestLegDarts = table.Column<int>(type: "integer", nullable: false),
                    WorstLegDarts = table.Column<int>(type: "integer", nullable: false),
                    TonPlusCheckouts = table.Column<int>(type: "integer", nullable: false),
                    DoubleQuota = table.Column<double>(type: "double precision", nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    HighestRoundScore = table.Column<int>(type: "integer", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchPlayerStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NotificationPreference = table.Column<int>(type: "integer", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Auth = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserViewPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ViewContext = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SettingsJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserViewPreferences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchFollowers_MatchId_UserAccountName",
                table: "MatchFollowers",
                columns: new[] { "MatchId", "UserAccountName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayerStatistics_MatchId_ParticipantId",
                table: "MatchPlayerStatistics",
                columns: new[] { "MatchId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_TournamentId_UserAccountName_Endp~",
                table: "NotificationSubscriptions",
                columns: new[] { "TournamentId", "UserAccountName", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserViewPreferences_UserAccountName_ViewContext",
                table: "UserViewPreferences",
                columns: new[] { "UserAccountName", "ViewContext" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchFollowers");

            migrationBuilder.DropTable(
                name: "MatchPlayerStatistics");

            migrationBuilder.DropTable(
                name: "NotificationSubscriptions");

            migrationBuilder.DropTable(
                name: "UserViewPreferences");

            migrationBuilder.DropColumn(
                name: "DiscordWebhookDisplayText",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "DiscordWebhookUrl",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "SeedTopCount",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "SeedingEnabled",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "NotificationPreference",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "AwaySlotOrigin",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "DelayMinutes",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ExpectedEndUtc",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "HomeSlotOrigin",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "NextMatchInfo",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "PlannedEndUtc",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "SchedulingStatus",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ConnectionState",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "ExtensionStatus",
                table: "Boards");

            migrationBuilder.DropColumn(
                name: "SchedulingStatus",
                table: "Boards");
        }
    }
}
