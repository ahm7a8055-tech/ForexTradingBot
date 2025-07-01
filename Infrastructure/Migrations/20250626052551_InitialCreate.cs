using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.CreateTable(
                name: "ForwardingRules",
                columns: table => new
                {
                    RuleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SourceChannelId = table.Column<long>(type: "bigint", nullable: false),
                    TargetChannelIds = table.Column<string>(type: "jsonb", nullable: false),
                    EditOptions_PrependText = table.Column<string>(type: "text", nullable: true),
                    EditOptions_AppendText = table.Column<string>(type: "text", nullable: true),
                    EditOptions_RemoveSourceForwardHeader = table.Column<bool>(type: "boolean", nullable: false),
                    EditOptions_RemoveLinks = table.Column<bool>(type: "boolean", nullable: false),
                    EditOptions_StripFormatting = table.Column<bool>(type: "boolean", nullable: false),
                    EditOptions_CustomFooter = table.Column<string>(type: "text", nullable: true),
                    EditOptions_DropAuthor = table.Column<bool>(type: "boolean", nullable: false),
                    EditOptions_DropMediaCaptions = table.Column<bool>(type: "boolean", nullable: false),
                    EditOptions_NoForwards = table.Column<bool>(type: "boolean", nullable: false),
                    FilterOptions_AllowedMessageTypes = table.Column<string>(type: "jsonb", nullable: false),
                    FilterOptions_AllowedMimeTypes = table.Column<string>(type: "jsonb", nullable: false),
                    FilterOptions_ContainsText = table.Column<string>(type: "text", nullable: true),
                    FilterOptions_ContainsTextIsRegex = table.Column<bool>(type: "boolean", nullable: false),
                    FilterOptions_ContainsTextRegexOptions = table.Column<int>(type: "integer", nullable: false),
                    FilterOptions_AllowedSenderUserIds = table.Column<string>(type: "jsonb", nullable: false),
                    FilterOptions_BlockedSenderUserIds = table.Column<string>(type: "jsonb", nullable: false),
                    FilterOptions_IgnoreEditedMessages = table.Column<bool>(type: "boolean", nullable: false),
                    FilterOptions_IgnoreServiceMessages = table.Column<bool>(type: "boolean", nullable: false),
                    FilterOptions_MinMessageLength = table.Column<int>(type: "integer", nullable: true),
                    FilterOptions_MaxMessageLength = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ForwardingRules", x => x.RuleName);
                });

            _ = migrationBuilder.CreateTable(
                name: "SignalCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_SignalCategories", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TelegramId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Level = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnableGeneralNotifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EnableVipSignalNotifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EnableRssNewsNotifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PreferredLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en")
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Users", x => x.Id);
                });

            _ = migrationBuilder.CreateTable(
                name: "ForwardingRuleTextReplacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Find = table.Column<string>(type: "text", nullable: false),
                    ReplaceWith = table.Column<string>(type: "text", nullable: false),
                    IsRegex = table.Column<bool>(type: "boolean", nullable: false),
                    RegexOptions = table.Column<int>(type: "integer", nullable: false),
                    ForwardingRuleName = table.Column<string>(type: "character varying(100)", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_ForwardingRuleTextReplacements", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_ForwardingRuleTextReplacements_ForwardingRules_ForwardingRu~",
                        column: x => x.ForwardingRuleName,
                        principalTable: "ForwardingRules",
                        principalColumn: "RuleName",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
                name: "RssSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2083)", maxLength: 2083, nullable: false),
                    SourceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedHeader = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ETag = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastFetchAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulFetchAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FetchIntervalMinutes = table.Column<int>(type: "integer", nullable: true),
                    FetchErrorCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DefaultSignalCategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_RssSources", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_RssSources_SignalCategories_DefaultSignalCategoryId",
                        column: x => x.DefaultSignalCategoryId,
                        principalTable: "SignalCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            _ = migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    StopLoss = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    TakeProfit = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    Timeframe = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsVipOnly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Signals", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_Signals_SignalCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "SignalCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            _ = migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActivatingTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
                name: "TokenWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_TokenWallets", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_TokenWallets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    PaymentGatewayInvoiceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaymentGatewayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentGatewayPayload = table.Column<string>(type: "TEXT", nullable: true),
                    PaymentGatewayResponse = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_Transactions", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_Transactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            _ = migrationBuilder.CreateTable(
                name: "UserSignalPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_UserSignalPreferences", x => new { x.UserId, x.CategoryId });
                    _ = table.ForeignKey(
                        name: "FK_UserSignalPreferences_SignalCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "SignalCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    _ = table.ForeignKey(
                        name: "FK_UserSignalPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Link = table.Column<string>(type: "character varying(2083)", maxLength: 2083, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    FullContent = table.Column<string>(type: "TEXT", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2083)", maxLength: 2083, nullable: true),
                    PublishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SourceItemId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SentimentScore = table.Column<double>(type: "double precision", nullable: true),
                    SentimentLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DetectedLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AffectedAssets = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RssSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsVipOnly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AssociatedSignalCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkHash = table.Column<byte[]>(type: "bytea", fixedLength: true, maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_NewsItems", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_NewsItems_RssSources_RssSourceId",
                        column: x => x.RssSourceId,
                        principalTable: "RssSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    _ = table.ForeignKey(
                        name: "FK_NewsItems_SignalCategories_AssociatedSignalCategoryId",
                        column: x => x.AssociatedSignalCategoryId,
                        principalTable: "SignalCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            _ = migrationBuilder.CreateTable(
                name: "UserRssPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RssSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_UserRssPreferences", x => new { x.UserId, x.RssSourceId });
                    _ = table.ForeignKey(
                        name: "FK_UserRssPreferences_RssSources_RssSourceId",
                        column: x => x.RssSourceId,
                        principalTable: "RssSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    _ = table.ForeignKey(
                        name: "FK_UserRssPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateTable(
                name: "SignalAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalystName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AnalysisText = table.Column<string>(type: "TEXT", nullable: false),
                    SentimentScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    _ = table.PrimaryKey("PK_SignalAnalyses", x => x.Id);
                    _ = table.ForeignKey(
                        name: "FK_SignalAnalyses_Signals_SignalId",
                        column: x => x.SignalId,
                        principalTable: "Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ForwardingRules_AllowedSenders_GIN",
                table: "ForwardingRules",
                column: "FilterOptions_AllowedSenderUserIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ForwardingRules_BySourceChannelAndStatus",
                table: "ForwardingRules",
                columns: new[] { "SourceChannelId", "IsEnabled" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ForwardingRules_ByTargetChannel_GIN",
                table: "ForwardingRules",
                column: "TargetChannelIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ForwardingRules_ContainsText_Trgm",
                table: "ForwardingRules",
                column: "FilterOptions_ContainsText")
                .Annotation("Npgsql:IndexMethod", "gist")
                .Annotation("Npgsql:IndexOperators", new[] { "gist_trgm_ops" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_ForwardingRules_IsEnabled",
                table: "ForwardingRules",
                column: "IsEnabled");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ForwardingRules_MessageTypes_GIN",
                table: "ForwardingRules",
                column: "FilterOptions_AllowedMessageTypes")
                .Annotation("Npgsql:IndexMethod", "gin");

            _ = migrationBuilder.CreateIndex(
                name: "IX_ForwardingRuleTextReplacements_ForwardingRuleName",
                table: "ForwardingRuleTextReplacements",
                column: "ForwardingRuleName");

            _ = migrationBuilder.CreateIndex(
                name: "IX_NewsItems_BySource",
                table: "NewsItems",
                column: "RssSourceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_NewsItems_CategorySearch",
                table: "NewsItems",
                columns: new[] { "AssociatedSignalCategoryId", "PublishedDate" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_NewsItems_LinkHash_Unique",
                table: "NewsItems",
                column: "LinkHash",
                unique: true,
                filter: "\"LinkHash\" IS NOT NULL");

            _ = migrationBuilder.CreateIndex(
                name: "IX_NewsItems_PrimarySearch",
                table: "NewsItems",
                columns: new[] { "IsVipOnly", "PublishedDate" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_NewsItems_RssSourceId_SourceItemId_Unique",
                table: "NewsItems",
                columns: new[] { "RssSourceId", "SourceItemId" },
                unique: true,
                filter: "\"SourceItemId\" IS NOT NULL");

            _ = migrationBuilder.CreateIndex(
                name: "IX_NewsItems_Unprocessed",
                table: "NewsItems",
                column: "LastProcessedAt",
                filter: "\"LastProcessedAt\" IS NULL");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RssSources_DefaultSignalCategoryId",
                table: "RssSources",
                column: "DefaultSignalCategoryId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RssSources_IsActive",
                table: "RssSources",
                column: "IsActive");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RssSources_IsActive_LastFetchAttemptAt",
                table: "RssSources",
                columns: new[] { "IsActive", "LastFetchAttemptAt" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_RssSources_LastFetchAttemptAt",
                table: "RssSources",
                column: "LastFetchAttemptAt");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RssSources_SourceName",
                table: "RssSources",
                column: "SourceName");

            _ = migrationBuilder.CreateIndex(
                name: "IX_RssSources_Url",
                table: "RssSources",
                column: "Url",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_SignalAnalyses_SignalId",
                table: "SignalAnalyses",
                column: "SignalId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_SignalCategories_Name",
                table: "SignalCategories",
                column: "Name",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_Signals_ByStatus",
                table: "Signals",
                column: "Status");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Signals_BySymbol",
                table: "Signals",
                column: "Symbol");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Signals_PrimarySearch",
                table: "Signals",
                columns: new[] { "CategoryId", "IsVipOnly", "Status", "PublishedAt" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ByEndDate",
                table: "Subscriptions",
                column: "EndDate");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_CheckIsActive",
                table: "Subscriptions",
                columns: new[] { "UserId", "EndDate" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_TokenWallets_UserId",
                table: "TokenWallets",
                column: "UserId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_Transactions_PaymentGatewayInvoiceId",
                table: "Transactions",
                column: "PaymentGatewayInvoiceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Transactions_Status",
                table: "Transactions",
                column: "Status");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Transactions_Timestamp",
                table: "Transactions",
                column: "Timestamp");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId",
                table: "Transactions",
                column: "UserId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_UserRssPreferences_BySource",
                table: "UserRssPreferences",
                column: "RssSourceId");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_Users_NotificationTarget_News",
                table: "Users",
                column: "EnableRssNewsNotifications");

            _ = migrationBuilder.CreateIndex(
                name: "IX_Users_NotificationTarget_Signal",
                table: "Users",
                columns: new[] { "Level", "EnableVipSignalNotifications" });

            _ = migrationBuilder.CreateIndex(
                name: "IX_Users_TelegramId",
                table: "Users",
                column: "TelegramId",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            _ = migrationBuilder.CreateIndex(
                name: "IX_UserSignalPreferences_CategoryId",
                table: "UserSignalPreferences",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            _ = migrationBuilder.DropTable(
                name: "ForwardingRuleTextReplacements");

            _ = migrationBuilder.DropTable(
                name: "NewsItems");

            _ = migrationBuilder.DropTable(
                name: "SignalAnalyses");

            _ = migrationBuilder.DropTable(
                name: "Subscriptions");

            _ = migrationBuilder.DropTable(
                name: "TokenWallets");

            _ = migrationBuilder.DropTable(
                name: "Transactions");

            _ = migrationBuilder.DropTable(
                name: "UserRssPreferences");

            _ = migrationBuilder.DropTable(
                name: "UserSignalPreferences");

            _ = migrationBuilder.DropTable(
                name: "ForwardingRules");

            _ = migrationBuilder.DropTable(
                name: "Signals");

            _ = migrationBuilder.DropTable(
                name: "RssSources");

            _ = migrationBuilder.DropTable(
                name: "Users");

            _ = migrationBuilder.DropTable(
                name: "SignalCategories");
        }
    }
}
