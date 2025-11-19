using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProMonitoringLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "ProMonitoringLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    JobId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    Exception = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ProMonitoringLogs", x => x.Id);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_CorrelationId",
                table: "ProMonitoringLogs",
                column: "CorrelationId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_EventType_Timestamp",
                table: "ProMonitoringLogs",
                columns: new[] { "EventType", "Timestamp" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_JobId",
                table: "ProMonitoringLogs",
                column: "JobId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_Level_Timestamp",
                table: "ProMonitoringLogs",
                columns: new[] { "Level", "Timestamp" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_Status",
                table: "ProMonitoringLogs",
                column: "Status");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_UserId",
                table: "ProMonitoringLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "ProMonitoringLogs");
        }
    }
}
