using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOtpCodeUseAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "OtpCodes");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "OtpCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ApiTokens_AccountId",
                table: "ApiTokens",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiTokens_Accounts_AccountId",
                table: "ApiTokens",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiTokens_Accounts_AccountId",
                table: "ApiTokens");

            migrationBuilder.DropIndex(
                name: "IX_ApiTokens_AccountId",
                table: "ApiTokens");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "OtpCodes");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "OtpCodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
