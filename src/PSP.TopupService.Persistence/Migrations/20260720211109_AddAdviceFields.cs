using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSP.TopupService.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdviceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "advice_last_error",
                table: "topup_transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "advice_retry_count",
                table: "topup_transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "advice_last_error",
                table: "topup_transactions");

            migrationBuilder.DropColumn(
                name: "advice_retry_count",
                table: "topup_transactions");
        }
    }
}
