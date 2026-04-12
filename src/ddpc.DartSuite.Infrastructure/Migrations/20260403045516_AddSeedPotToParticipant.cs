using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ddpc.DartSuite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedPotToParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeedPot",
                table: "Participants",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeedPot",
                table: "Participants");
        }
    }
}
