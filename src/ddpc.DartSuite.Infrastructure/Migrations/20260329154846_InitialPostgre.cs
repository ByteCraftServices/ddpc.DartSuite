using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ddpc.DartSuite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalBoardId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LocalIpAddress = table.Column<string>(type: "text", nullable: true),
                    BoardManagerUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentMatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentMatchLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ManagedMode = table.Column<int>(type: "integer", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastExtensionPollUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    GroupNumber = table.Column<int>(type: "integer", nullable: true),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    MatchNumber = table.Column<int>(type: "integer", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: true),
                    HomeParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AwayParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeLegs = table.Column<int>(type: "integer", nullable: false),
                    AwayLegs = table.Column<int>(type: "integer", nullable: false),
                    HomeSets = table.Column<int>(type: "integer", nullable: false),
                    AwaySets = table.Column<int>(type: "integer", nullable: false),
                    WinnerParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlannedStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsStartTimeLocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsBoardLocked = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExternalMatchId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsAutodartsAccount = table.Column<bool>(type: "boolean", nullable: false),
                    IsManager = table.Column<bool>(type: "boolean", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    GroupNumber = table.Column<int>(type: "integer", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoringCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoringCriteria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GroupNumber = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TournamentRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    BaseScore = table.Column<int>(type: "integer", nullable: false),
                    InMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OutMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GameMode = table.Column<int>(type: "integer", nullable: false),
                    Legs = table.Column<int>(type: "integer", nullable: false),
                    Sets = table.Column<int>(type: "integer", nullable: true),
                    MaxRounds = table.Column<int>(type: "integer", nullable: false),
                    BullMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BullOffMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MatchDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PauseBetweenMatchesMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinPlayerPauseMinutes = table.Column<int>(type: "integer", nullable: false),
                    BoardAssignment = table.Column<int>(type: "integer", nullable: false),
                    FixedBoardId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrganizerAccount = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Variant = table.Column<int>(type: "integer", nullable: false),
                    TeamplayEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    AreGameModesLocked = table.Column<bool>(type: "boolean", nullable: false),
                    JoinCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    GroupCount = table.Column<int>(type: "integer", nullable: false),
                    PlayoffAdvancers = table.Column<int>(type: "integer", nullable: false),
                    KnockoutsPerRound = table.Column<int>(type: "integer", nullable: false),
                    MatchesPerOpponent = table.Column<int>(type: "integer", nullable: false),
                    GroupMode = table.Column<int>(type: "integer", nullable: false),
                    GroupDrawMode = table.Column<int>(type: "integer", nullable: false),
                    PlanningVariant = table.Column<int>(type: "integer", nullable: false),
                    GroupOrderMode = table.Column<int>(type: "integer", nullable: false),
                    ThirdPlaceMatch = table.Column<bool>(type: "boolean", nullable: false),
                    PlayersPerTeam = table.Column<int>(type: "integer", nullable: false),
                    WinPoints = table.Column<int>(type: "integer", nullable: false),
                    LegFactor = table.Column<int>(type: "integer", nullable: false),
                    IsRegistrationOpen = table.Column<bool>(type: "boolean", nullable: false),
                    RegistrationStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RegistrationEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Boards_ExternalBoardId",
                table: "Boards",
                column: "ExternalBoardId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TournamentId_Phase_Round_MatchNumber",
                table: "Matches",
                columns: new[] { "TournamentId", "Phase", "Round", "MatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Participants_TournamentId_AccountName",
                table: "Participants",
                columns: new[] { "TournamentId", "AccountName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringCriteria_TournamentId_Type",
                table: "ScoringCriteria",
                columns: new[] { "TournamentId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TournamentId_Name",
                table: "Teams",
                columns: new[] { "TournamentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRounds_TournamentId_Phase_RoundNumber",
                table: "TournamentRounds",
                columns: new[] { "TournamentId", "Phase", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_JoinCode",
                table: "Tournaments",
                column: "JoinCode",
                unique: true,
                filter: "\"JoinCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "ScoringCriteria");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "TournamentRounds");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
