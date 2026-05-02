using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ddpc.DartSuite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineLobbyAndRoundDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RoundDate",
                table: "TournamentRounds",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "RoundStartTime",
                table: "TournamentRounds",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalLobbyId",
                table: "Matches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalLobbyJoinUrl",
                table: "Matches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LobbyCreatedAtUtc",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LobbyCreatedByParticipantId",
                table: "Matches",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoundDate",
                table: "TournamentRounds");

            migrationBuilder.DropColumn(
                name: "RoundStartTime",
                table: "TournamentRounds");

            migrationBuilder.DropColumn(
                name: "ExternalLobbyId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "ExternalLobbyJoinUrl",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LobbyCreatedAtUtc",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "LobbyCreatedByParticipantId",
                table: "Matches");
        }
    }
}
