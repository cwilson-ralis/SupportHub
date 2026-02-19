using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportHub.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedMailboxAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PollingIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    LastPolledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastPolledMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AutoCreateTickets = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPriority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailConfigurations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailProcessingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmailConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SenderEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProcessingResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailProcessingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailProcessingLogs_EmailConfigurations_EmailConfigurationId",
                        column: x => x.EmailConfigurationId,
                        principalTable: "EmailConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailProcessingLogs_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfigurations_CompanyId_SharedMailboxAddress",
                table: "EmailConfigurations",
                columns: new[] { "CompanyId", "SharedMailboxAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailConfigurations_IsActive",
                table: "EmailConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_EmailProcessingLogs_EmailConfigurationId",
                table: "EmailProcessingLogs",
                column: "EmailConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailProcessingLogs_ExternalMessageId",
                table: "EmailProcessingLogs",
                column: "ExternalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailProcessingLogs_ProcessedAt",
                table: "EmailProcessingLogs",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailProcessingLogs_TicketId",
                table: "EmailProcessingLogs",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailProcessingLogs");

            migrationBuilder.DropTable(
                name: "EmailConfigurations");
        }
    }
}
