using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddIsWarningEmailSentToTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWarningEmailSent",
                table: "Tasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEIOB1cZGE2iFn61AQCNl/Y5UiSXEVJEyPvZEJ/qjjMp/uWfo8Mi9p/8QcN+RrQ7G6A==");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ProjectId",
                table: "AuditLogs",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Projects_ProjectId",
                table: "AuditLogs",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Projects_ProjectId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ProjectId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IsWarningEmailSent",
                table: "Tasks");

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEJNBxA1RPTuBgUU7zAJNvdz47IVSzkTaFcri8zzlQkP+zCECRyTrkxvyL5Kw++03GQ==");
        }
    }
}
