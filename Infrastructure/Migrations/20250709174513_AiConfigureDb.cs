using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiConfigureDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "AiApiConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PromptTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_AiApiConfigurations", x => x.Id);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiApiConfigurations_IsEnabled",
                table: "AiApiConfigurations",
                column: "IsEnabled");

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiApiConfigurations_ProviderName_IsEnabled",
                table: "AiApiConfigurations",
                columns: new[] { "ProviderName", "IsEnabled" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_AiApiConfigurations_ProviderName_Unique",
                table: "AiApiConfigurations",
                column: "ProviderName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "AiApiConfigurations");
        }
    }
}
