using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MortgageApplications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MortgageApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AnnualIncome = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MonthlyExpenses = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DepositAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PropertyValue = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TermYears = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MortgageApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AffordabilityAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AssessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffordabilityAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffordabilityAssessments_MortgageApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "MortgageApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffordabilityAssessments_ApplicationId",
                table: "AffordabilityAssessments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_MortgageApplications_OwnerId",
                table: "MortgageApplications",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffordabilityAssessments");

            migrationBuilder.DropTable(
                name: "MortgageApplications");
        }
    }
}
