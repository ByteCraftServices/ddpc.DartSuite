using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ddpc.DartSuite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueIndexOnBoardExternalBoardId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Boards_ExternalBoardId",
                table: "Boards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Boards_ExternalBoardId",
                table: "Boards",
                column: "ExternalBoardId",
                unique: true);
        }
    }
}
