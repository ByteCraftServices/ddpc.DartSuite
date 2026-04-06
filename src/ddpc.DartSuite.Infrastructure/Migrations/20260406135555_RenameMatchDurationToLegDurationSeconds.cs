using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ddpc.DartSuite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameMatchDurationToLegDurationSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MatchDurationMinutes",
                table: "TournamentRounds",
                newName: "LegDurationSeconds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LegDurationSeconds",
                table: "TournamentRounds",
                newName: "MatchDurationMinutes");
        }
    }
}
