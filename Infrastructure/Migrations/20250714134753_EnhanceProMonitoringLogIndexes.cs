using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceProMonitoringLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_Level_Status_Timestamp",
                table: "ProMonitoringLogs",
                columns: new[] { "Level", "Status", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_Source_EventType_Timestamp",
                table: "ProMonitoringLogs",
                columns: new[] { "Source", "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ProMonitoringLog_Tags_Timestamp",
                table: "ProMonitoringLogs",
                columns: new[] { "Tags", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProMonitoringLog_Level_Status_Timestamp",
                table: "ProMonitoringLogs");

            migrationBuilder.DropIndex(
                name: "IX_ProMonitoringLog_Source_EventType_Timestamp",
                table: "ProMonitoringLogs");

            migrationBuilder.DropIndex(
                name: "IX_ProMonitoringLog_Tags_Timestamp",
                table: "ProMonitoringLogs");
        }
    }
}
