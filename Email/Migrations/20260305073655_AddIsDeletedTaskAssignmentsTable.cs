using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedTaskAssignmentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaskAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TaskAssignments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_AccountId",
                table: "TaskAssignments",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_Accounts_AccountId",
                table: "TaskAssignments",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Accounts_AccountId",
                table: "TaskAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_AccountId",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TaskAssignments");
        }
    }
}
