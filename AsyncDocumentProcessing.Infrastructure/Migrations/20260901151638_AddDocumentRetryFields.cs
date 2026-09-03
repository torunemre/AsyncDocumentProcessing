using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsyncDocumentProcessing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "Documents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Documents");
        }
    }
}
