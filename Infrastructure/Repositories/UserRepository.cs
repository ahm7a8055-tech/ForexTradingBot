// File: Infrastructure/Persistence/Repositories/UserRepository.cs

#region Usings
// Standard .NET & NuGet
// Project specific
using Application.Common.Interfaces; // For IUserRepository
using Dapper; // Dapper for micro-ORM operations
using Domain.Entities;             // For User, Subscription, TokenWallet, UserSignalPreference entities
using Domain.Enums; // For UserLevel enum (stored as string in DB)
using Microsoft.Data.SqlClient; // SQL Server specific connection
using Microsoft.Extensions.Configuration; // To access connection strings
using Microsoft.Extensions.Logging; // For logging
using Npgsql;
using Polly; // For resilience policies
using Polly.Retry; // For retry policies
using Polly.Timeout; // For custom RepositoryException
using Shared.Extensions;
using System.Data; // Common Ado.Net interfaces like IDbConnection, IDbTransaction
using System.Data.Common; // For DbException (base class for database exceptions)
using System.Linq.Expressions;
using System.Text;
using System.Text.Json; // Still included, but will throw NotSupportedException
#endregion

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Implements the INewsItemRepository for data operations related to NewsItem entities
    /// using Dapper.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<UserRepository> _logger;
        private readonly AsyncRetryPolicy _retryPolicy; // Polly policy for DB operations
        private class UserWithRelatedDataDto
        {
            // --- User Properties (from Users table) ---
            public Guid Id { get; set; }
            public string Username { get; set; } = default!;
            public string TelegramId { get; set; } = default!;
            public string Email { get; set; } = default!;
            public string Level { get; set; } = default!; // Mapped from DB string
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public bool EnableGeneralNotifications { get; set; }
            public bool EnableVipSignalNotifications { get; set; }
            public bool EnableRssNewsNotifications { get; set; }
            public string PreferredLanguage { get; set; } = default!;

            // --- JSON Properties for Related Data ---
            // These properties will hold the JSON strings returned by PostgreSQL's json_agg.
            // We expect them to be in the format '[{"Id": "...", ...}]' or '[]' if no records exist.
            public string TokenWalletJson { get; set; } = default!;
            public string SubscriptionsJson { get; set; } = default!;
            public string PreferencesJson { get; set; } = default!;

            /// <summary>
            /// Converts this DTO into the User domain entity, deserializing JSON data
            /// and utilizing the existing internal DTOs' ToDomainEntity methods.
            /// </summary>
            /// <param name="logger">The logger to use for reporting deserialization errors.</param>
            /// <returns>The constructed User domain entity.</returns>
            public User ToDomainEntity(ILogger logger)
            {
                // Create the main user entity using the properties from this DTO.
                // This is similar to what UserDbDto.ToDomainEntity() does, but here we map directly.
                var user = new User
                {
                    Id = Id,
                    Username = Username,
                    TelegramId = TelegramId,
                    Email = Email,
                    Level = Enum.Parse<UserLevel>(Level), // Convert string back to enum
                    CreatedAt = CreatedAt,
                    UpdatedAt = UpdatedAt,
                    EnableGeneralNotifications = EnableGeneralNotifications,
                    EnableVipSignalNotifications = EnableVipSignalNotifications,
                    EnableRssNewsNotifications = EnableRssNewsNotifications,
                    PreferredLanguage = PreferredLanguage
                };

                // --- Deserialize Token Wallet ---
                TokenWalletDbDto? singleWalletDto = null;
                // Check for null, empty string, or the specific "[]" string that json_agg can return for zero rows.
                if (!string.IsNullOrWhiteSpace(TokenWalletJson) && TokenWalletJson != "[]")
                {
                    try
                    {
                        // Deserialize the JSON array string into a list of TokenWalletDbDto.
                        var walletDtos = System.Text.Json.JsonSerializer.Deserialize<List<TokenWalletDbDto>>(TokenWalletJson);
                        if (walletDtos != null && walletDtos.Any())
                        {
                            singleWalletDto = walletDtos.First(); // Get the first (and likely only) wallet
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        // Log any errors during JSON deserialization. For read operations, it's often best to continue with default values.
                        logger.LogError(jsonEx, "UserRepository: Failed to deserialize TokenWalletJson for User {UserId}. JSON: {TokenWalletJson}", user.Id, TokenWalletJson);
                    }
                }
                // Assign the domain entity, providing a default if not found or if deserialization failed.
                user.TokenWallet = singleWalletDto?.ToDomainEntity() ?? TokenWallet.Create(user.Id); // Assuming TokenWallet.Create() provides a default instance.

                // --- Deserialize Subscriptions ---
                List<SubscriptionDbDto>? subscriptionDtos = null;
                if (!string.IsNullOrWhiteSpace(SubscriptionsJson) && SubscriptionsJson != "[]")
                {
                    try
                    {
                        subscriptionDtos = System.Text.Json.JsonSerializer.Deserialize<List<SubscriptionDbDto>>(SubscriptionsJson);
                    }
                    catch (JsonException jsonEx)
                    {
                        logger.LogError(jsonEx, "UserRepository: Failed to deserialize SubscriptionsJson for User {UserId}. JSON: {SubscriptionsJson}", user.Id, SubscriptionsJson);
                    }
                }
                // Assign the domain entity list, providing an empty list if not found or if deserialization failed.
                user.Subscriptions = subscriptionDtos?.Select(dto => dto.ToDomainEntity()).ToList() ?? new List<Subscription>();

                // --- Deserialize User Signal Preferences ---
                List<UserSignalPreferenceDbDto>? preferenceDtos = null;
                if (!string.IsNullOrWhiteSpace(PreferencesJson) && PreferencesJson != "[]")
                {
                    try
                    {
                        preferenceDtos = System.Text.Json.JsonSerializer.Deserialize<List<UserSignalPreferenceDbDto>>(PreferencesJson);
                    }
                    catch (JsonException jsonEx)
                    {
                        logger.LogError(jsonEx, "UserRepository: Failed to deserialize PreferencesJson for User {UserId}. JSON: {PreferencesJson}", user.Id, PreferencesJson);
                    }
                }
                // Assign the domain entity list, providing an empty list if not found or if deserialization failed.
                user.Preferences = preferenceDtos?.Select(dto => dto.ToDomainEntity()).ToList() ?? new List<UserSignalPreference>();

                // Transactions are not fetched in this query. Ensure the list is initialized.
                user.Transactions = new List<Transaction>();

                return user;
            }
        }
        // --- Internal DTOs for Dapper Mapping ---
        private class UserDbDto
        {
            public Guid Id { get; set; }
            public string Username { get; set; } = default!;
            public string TelegramId { get; set; } = default!;
            public string Email { get; set; } = default!;
            public string Level { get; set; } = default!; // Mapped from DB string
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public bool EnableGeneralNotifications { get; set; }
            public bool EnableVipSignalNotifications { get; set; }
            public bool EnableRssNewsNotifications { get; set; }
            public string PreferredLanguage { get; set; } = default!;

            public User ToDomainEntity()
            {
                return new User
                {
                    Id = Id,
                    Username = Username,
                    TelegramId = TelegramId,
                    Email = Email,
                    Level = Enum.Parse<UserLevel>(Level), // Convert string back to enum
                    CreatedAt = CreatedAt,
                    UpdatedAt = UpdatedAt,
                    EnableGeneralNotifications = EnableGeneralNotifications,
                    EnableVipSignalNotifications = EnableVipSignalNotifications,
                    EnableRssNewsNotifications = EnableRssNewsNotifications,
                    PreferredLanguage = PreferredLanguage
                };
            }
        }

        private class TokenWalletDbDto
        {
            public Guid Id { get; set; }
            public Guid UserId { get; set; }
            public decimal Balance { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }

            public TokenWallet ToDomainEntity()
            {
                // Assuming TokenWallet now has a constructor that accepts all these values.
                return new TokenWallet(Id, UserId, Balance, IsActive, CreatedAt, UpdatedAt);
            }
        }

        private class SubscriptionDbDto
        {
            public Guid Id { get; set; }
            public Guid UserId { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string Status { get; set; } = default!;
            public Guid? ActivatingTransactionId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }

            public Subscription ToDomainEntity()
            {
                return new Subscription
                {
                    Id = Id,
                    UserId = UserId,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    Status = Status,
                    ActivatingTransactionId = ActivatingTransactionId,
                    CreatedAt = CreatedAt,
                    UpdatedAt = UpdatedAt
                };
            }
        }

        private class UserSignalPreferenceDbDto
        {
            public Guid Id { get; set; } // Matches PK on UserSignalPreferences table
            public Guid UserId { get; set; }
            public Guid CategoryId { get; set; }
            public DateTime CreatedAt { get; set; }

            public UserSignalPreference ToDomainEntity()
            {
                return new UserSignalPreference
                {
                    Id = Id,
                    UserId = UserId,
                    CategoryId = CategoryId,
                    CreatedAt = CreatedAt
                };
            }
        }
        private readonly AsyncTimeoutPolicy _timeoutPolicy;
        private readonly ILoggingSanitizer _logSanitizer;
        // --- Constructor ---
        public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger, ILoggingSanitizer logSanitizer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new ArgumentNullException("DefaultConnection", "DefaultConnection string not found.");

            _timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromMinutes(5), TimeoutStrategy.Pessimistic);
            // Polly configuration for transient errors (e.g., network issues, temporary DB unavailability)
            // Excludes primary key violation errors (e.g., trying to add a user with an existing ID/email/telegramId)
            _retryPolicy = Policy
                .Handle<DbException>(ex => !(ex is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))) // SQL Server PK/Unique constraint violation
                .WaitAndRetryAsync(
                    retryCount: 3, // Max 3 retries
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 2s, 4s, 8s
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(exception,
                            "UserRepository: Transient database error encountered. Retrying in {TimeSpan} for attempt {RetryAttempt}. Error: {Message}",
                            timeSpan, retryAttempt, exception.Message);
                    });

            // --- THIS IS THE LINE TO CORRECT ---
            // It should assign the 'logSanitizer' parameter to the '_logSanitizer' class field.
            _logSanitizer = logSanitizer ?? throw new ArgumentNullException(nameof(logSanitizer));
            // --- END OF CORRECTION ---
        }

        // --- Helper to create a new SqlConnection ---
        private NpgsqlConnection CreateConnection()
        {
            try
            {
                // --- CHANGE: Returning NpgsqlConnection ---
                return new NpgsqlConnection(_connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserRepository: Error creating PostgreSQL database connection. ConnectionString: {ConnectionString}", _connectionString);
                throw;
            }
        }
        private const string UserWithRelatedDataSqlFragment = @"
            SELECT
                u.""Id"", u.""Username"", u.""TelegramId"", u.""Email"", u.""Level"", u.""CreatedAt"", u.""UpdatedAt"",
                u.""EnableGeneralNotifications"", u.""EnableVipSignalNotifications"", u.""EnableRssNewsNotifications"", u.""PreferredLanguage"",
                (
                    SELECT COALESCE(json_agg(json_build_object(
                        'Id', tw.""Id"", 'UserId', tw.""UserId"", 'Balance', tw.""Balance"", 'IsActive', tw.""IsActive"", 'CreatedAt', tw.""CreatedAt"", 'UpdatedAt', tw.""UpdatedAt""
                    )), '[]'::json)
                    FROM public.""TokenWallets"" tw WHERE tw.""UserId"" = u.""Id""
                ) AS TokenWalletJson,
                (
                    SELECT COALESCE(json_agg(json_build_object(
                        'Id', s.""Id"", 'UserId', s.""UserId"", 'StartDate', s.""StartDate"", 'EndDate', s.""EndDate"", 'Status', s.""Status"",
                        'ActivatingTransactionId', s.""ActivatingTransactionId"", 'CreatedAt', s.""CreatedAt"", 'UpdatedAt', s.""UpdatedAt""
                    )), '[]'::json)
                    FROM public.""Subscriptions"" s WHERE s.""UserId"" = u.""Id""
                ) AS SubscriptionsJson,
                (
                    SELECT COALESCE(json_agg(json_build_object(
                        'Id', usp.""Id"", 'UserId', usp.""UserId"", 'CategoryId', usp.""CategoryId"", 'CreatedAt', usp.""CreatedAt""
                    )), '[]'::json)
                    FROM public.""UserSignalPreferences"" usp WHERE usp.""UserId"" = u.""Id""
                ) AS PreferencesJson
            FROM public.""Users"" u";

        // --- Read Operations ---

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetUsersForNewsNotificationAsync(
     Guid? newsItemSignalCategoryId,
     bool isNewsItemVipOnly,
     CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "UserRepository: Fetching users for news notification. CategoryId: {CategoryId}, IsVipOnly: {IsVip}",
                newsItemSignalCategoryId, isNewsItemVipOnly);

            // --- 1. BUILD THE SQL QUERY ---
            // Start with the base query that fetches users and their related data as JSON.
            // We will dynamically add WHERE clauses based on the input parameters.
            var baseSql = new StringBuilder(UserWithRelatedDataSqlFragment); // Use the shared SQL fragment
            baseSql.AppendLine("WHERE u.\"EnableRssNewsNotifications\" = true");

            var parameters = new DynamicParameters();

            // Clause for VIP-only news
            if (isNewsItemVipOnly)
            {
                // For PostgreSQL, use NOW() or CURRENT_TIMESTAMP for the current UTC time.
                // The UserLevel enum values ('Premium', 'Vip') should match what's in your DB.
                baseSql.AppendLine(@"
            AND u.""Level"" IN ('Premium', 'Vip') 
            AND EXISTS (
                SELECT 1 FROM public.""Subscriptions"" s_sub 
                WHERE s_sub.""UserId"" = u.""Id"" 
                  AND s_sub.""StartDate"" <= NOW() 
                  AND s_sub.""EndDate"" >= NOW() 
                  AND s_sub.""Status"" = 'Active'
            )");
            }

            // Clause for specific news categories
            if (newsItemSignalCategoryId.HasValue)
            {
                baseSql.AppendLine(@"
            AND (
                NOT EXISTS (SELECT 1 FROM public.""UserSignalPreferences"" usp_pref WHERE usp_pref.""UserId"" = u.""Id"") 
                OR EXISTS (
                    SELECT 1 FROM public.""UserSignalPreferences"" usp_pref 
                    WHERE usp_pref.""UserId"" = u.""Id"" AND usp_pref.""CategoryId"" = @NewsItemSignalCategoryId
                )
            )");
                parameters.Add("NewsItemSignalCategoryId", newsItemSignalCategoryId.Value);
            }

            var finalSql = baseSql.ToString();

            try
            {
                // Use the combined Polly policy for resilience.
                var combinedPolicy = _timeoutPolicy.WrapAsync(_retryPolicy);

                return await combinedPolicy.ExecuteAsync(async (ct) =>
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(ct);

                    // --- 2. EXECUTE THE QUERY ---
                    // Execute the single, powerful JSON aggregation query.
                    // Dapper will map each row to our UserWithRelatedDataDto.
                    var userDtos = await connection.QueryAsync<UserWithRelatedDataDto>(
                        new CommandDefinition(finalSql, parameters, cancellationToken: ct)
                    );

                    // --- 3. MAP TO DOMAIN ENTITIES ---
                    // Convert the list of DTOs to a list of domain User entities.
                    // The ToDomainEntity method handles the JSON deserialization for each user.
                    var eligibleUsers = userDtos
                        .Select(dto => dto.ToDomainEntity(_logger))
                        .ToList();

                    _logger.LogInformation("UserRepository: Found {UserCount} eligible users for news notification.", eligibleUsers.Count);
                    return eligibleUsers;

                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserRepository: Error fetching users for news notification.");
                throw new RepositoryException("Failed to fetch users for news notification.", ex);
            }
        }


        /// <inheritdoc />
        // Make sure the UserWithRelatedDataDto class is defined within your UserRepository class
        // as shown in the previous example for GetByTelegramIdAsync.

        // If you haven't added it yet, here's the DTO again for context:
        /*
        private class UserWithRelatedDataDto
        {
            public Guid Id { get; set; }
            public string Username { get; set; } = default!;
            public string TelegramId { get; set; } = default!;
            public string Email { get; set; } = default!;
            public string Level { get; set; } = default!;
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public bool EnableGeneralNotifications { get; set; }
            public bool EnableVipSignalNotifications { get; set; }
            public bool EnableRssNewsNotifications { get; set; }
            public string PreferredLanguage { get; set; } = default!;

            public string TokenWalletJson { get; set; } = default!;
            public string SubscriptionsJson { get; set; } = default!;
            public string PreferencesJson { get; set; } = default!;

            public User ToDomainEntity(ILogger logger)
            {
                var user = new User
                {
                    Id = Id,
                    Username = Username,
                    TelegramId = TelegramId,
                    Email = Email,
                    Level = Enum.Parse<UserLevel>(Level),
                    CreatedAt = CreatedAt,
                    UpdatedAt = UpdatedAt,
                    EnableGeneralNotifications = EnableGeneralNotifications,
                    EnableVipSignalNotifications = EnableVipSignalNotifications,
                    EnableRssNewsNotifications = EnableRssNewsNotifications,
                    PreferredLanguage = PreferredLanguage
                };

                TokenWalletDbDto? singleWalletDto = null;
                if (!string.IsNullOrWhiteSpace(TokenWalletJson) && TokenWalletJson != "[]")
                {
                    try {
                        var walletDtos = System.Text.Json.JsonSerializer.Deserialize<List<TokenWalletDbDto>>(TokenWalletJson);
                        if (walletDtos != null && walletDtos.Any()) {
                            singleWalletDto = walletDtos.First();
                        }
                    } catch (JsonException jsonEx) {
                        logger.LogError(jsonEx, "UserRepository: Failed to deserialize TokenWalletJson for User {UserId}. JSON: {TokenWalletJson}", user.Id, TokenWalletJson);
                    }
                }
                user.TokenWallet = singleWalletDto?.ToDomainEntity() ?? TokenWallet.Create(user.Id);

                List<SubscriptionDbDto>? subscriptionDtos = null;
                if (!string.IsNullOrWhiteSpace(SubscriptionsJson) && SubscriptionsJson != "[]")
                {
                    try {
                        subscriptionDtos = System.Text.Json.JsonSerializer.Deserialize<List<SubscriptionDbDto>>(SubscriptionsJson);
                    } catch (JsonException jsonEx) {
                        logger.LogError(jsonEx, "UserRepository: Failed to deserialize SubscriptionsJson for User {UserId}. JSON: {SubscriptionsJson}", user.Id, SubscriptionsJson);
                    }
                }
                user.Subscriptions = subscriptionDtos?.Select(dto => dto.ToDomainEntity()).ToList() ?? new List<Subscription>();

                List<UserSignalPreferenceDbDto>? preferenceDtos = null;
                if (!string.IsNullOrWhiteSpace(PreferencesJson) && PreferencesJson != "[]")
                {
                    try {
                        preferenceDtos = System.Text.Json.JsonSerializer.Deserialize<List<UserSignalPreferenceDbDto>>(PreferencesJson);
                    } catch (JsonException jsonEx) {
                        logger.LogError(jsonEx, "UserRepository: Failed to deserialize PreferencesJson for User {UserId}. JSON: {PreferencesJson}", user.Id, PreferencesJson);
                    }
                }
                user.Preferences = preferenceDtos?.Select(dto => dto.ToDomainEntity()).ToList() ?? new List<UserSignalPreference>();

                user.Transactions = new List<Transaction>(); // Not fetched in this query

                return user;
            }
        }
        */


        // ... (Rest of your UserRepository class) ...

        /// <inheritdoc />
        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("UserRepository: Fetching user by ID: {UserId} using PostgreSQL JSON aggregation.", id);

            // --- OPTIMIZED SQL USING PostgreSQL JSON AGGREGATION WITH PROPER IDENTIFIERS ---
            // CRITICAL: All column and table references MUST match the DDL casing and quotes.
            const string sql = @"
                SELECT
                    u.""Id"",
                    u.""Username"",
                    u.""TelegramId"",
                    u.""Email"",
                    u.""Level"",
                    u.""CreatedAt"",
                    u.""UpdatedAt"",
                    u.""EnableGeneralNotifications"",
                    u.""EnableVipSignalNotifications"",
                    u.""EnableRssNewsNotifications"",
                    u.""PreferredLanguage"",
                    ( -- Aggregate TokenWallets into a JSON array
                        SELECT COALESCE(json_agg(json_build_object(
                            'Id', tw.""Id"",
                            'UserId', tw.""UserId"",
                            'Balance', tw.Balance, -- Assuming Balance was created without quotes, otherwise quote it.
                            'IsActive', tw.IsActive, -- Assuming IsActive was created without quotes, otherwise quote it.
                            'CreatedAt', tw.""CreatedAt"",
                            'UpdatedAt', tw.""UpdatedAt""
                        )), '[]'::json)
                        FROM public.""TokenWallets"" tw
                        WHERE tw.""UserId"" = u.""Id""
                    ) AS TokenWalletJson,
                    ( -- Aggregate Subscriptions into a JSON array
                        SELECT COALESCE(json_agg(json_build_object(
                            'Id', s.""Id"",
                            'UserId', s.""UserId"",
                            'StartDate', s.StartDate, -- Assuming StartDate was created without quotes
                            'EndDate', s.EndDate,     -- Assuming EndDate was created without quotes
                            'Status', s.Status,       -- Assuming Status was created without quotes
                            'ActivatingTransactionId', s.ActivatingTransactionId, -- Assuming this was created without quotes
                            'CreatedAt', s.""CreatedAt"",
                            'UpdatedAt', s.""UpdatedAt""
                        )), '[]'::json)
                        FROM public.""Subscriptions"" s
                        WHERE s.""UserId"" = u.""Id""
                    ) AS SubscriptionsJson,
                    ( -- Aggregate UserSignalPreferences into a JSON array
                        SELECT COALESCE(json_agg(json_build_object(
                            'Id', usp.""Id"",
                            'UserId', usp.""UserId"",
                            'CategoryId', usp.CategoryId, -- Assuming CategoryId was created without quotes
                            'CreatedAt', usp.""CreatedAt""
                        )), '[]'::json)
                        FROM public.""UserSignalPreferences"" usp
                        WHERE usp.""UserId"" = u.""Id""
                    ) AS PreferencesJson
                FROM public.""Users"" u
                WHERE u.""Id"" = @Id;";
       
            try
            {
                // Apply the combined resilience policy (timeout wrapping retry)
                var combinedPolicy = _timeoutPolicy.WrapAsync(_retryPolicy);

                // Execute the database operation within the policy
                return await combinedPolicy.ExecuteAsync(async (ct) =>
                {
                    using var connection = CreateConnection(); // Ensure this returns NpgsqlConnection
                    await connection.OpenAsync(ct); // Pass the policy-managed cancellation token

                    // Execute the single, complex SQL query and map directly to our UserWithRelatedDataDto.
                    // Dapper's CommandDefinition ensures the cancellation token is respected.
                    var userWithRelatedDataDto = await connection.QueryFirstOrDefaultAsync<UserWithRelatedDataDto>(
                        new CommandDefinition(sql, new { Id = id }, cancellationToken: ct) // Parameter name is 'Id' matching @Id
                    );

                    // If no user was found with the given ID
                    if (userWithRelatedDataDto == null)
                    {
                        _logger.LogTrace("User with ID {UserId} not found.", id);
                        return null;
                    }

                    // Convert the DTO (which includes JSON deserialization) into the domain entity.
                    // Pass the logger to the DTO's method for handling potential JSON deserialization errors.
                    var user = userWithRelatedDataDto.ToDomainEntity(_logger);

                    _logger.LogDebug("Successfully fetched user {UserId} with all related entities.", id);
                    return user;

                }, cancellationToken); // Pass the original cancellationToken to the policy execution
            }
            catch (TimeoutRejectedException ex)
            {
                // Catch specific timeout exceptions from Polly
                _logger.LogError(ex, "UserRepository: Operation timed out after {TimeoutDuration} while fetching user by ID {UserId}.", 30, id);
                // Rethrow as a more specific domain/application exception for better error handling upstream.
                throw new RepositoryException($"The operation to fetch user by ID {id} timed out.", ex);
            }
            catch (Exception ex)
            {
                // Catch other exceptions that might occur during query execution or retries
                _logger.LogError(ex, "Failed to get user by ID {UserId} after retries and within timeout.", id);
                // Rethrow as a more specific domain/application exception.
                throw new RepositoryException($"An error occurred while fetching user by ID: {id}", ex);
            }
        }
        /// <inheritdoc />
        /// <summary>
        /// Fetches a user and their complete related entity graph by their Telegram ID in a single,
        /// highly optimized database round-trip.
        /// </summary>
        /// <param name="telegramId">The user's unique Telegram ID.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The complete User entity, or null if not found.</returns>
        public async Task<User?> GetByTelegramIdAsync(string telegramId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(telegramId)) return null;

            _logger.LogTrace("UserRepository: Fetching user by TelegramID: {TelegramId}", telegramId);

            var sql = $"{UserWithRelatedDataSqlFragment} WHERE u.\"TelegramId\" = @TelegramId;";

            try
            {
                var combinedPolicy = _timeoutPolicy.WrapAsync(_retryPolicy);
                return await combinedPolicy.ExecuteAsync(async (ct) =>
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(ct);
                    var userDto = await connection.QueryFirstOrDefaultAsync<UserWithRelatedDataDto>(
                        new CommandDefinition(sql, new { TelegramId = telegramId }, cancellationToken: ct));

                    if (userDto == null) return null;
                    return userDto.ToDomainEntity(_logger);

                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user by TelegramID {TelegramId}", telegramId);
                throw new RepositoryException($"An error occurred while fetching user by TelegramID: {telegramId}", ex);
            }
        }


        /// <summary>
        /// Fetches a user and their complete related entity graph by their email address.
        /// This method is optimized to prevent duplicate code by finding the user's ID and
        /// then delegating the full entity fetch to GetByIdAsync.
        /// </summary>
        /// <inheritdoc />
        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("UserRepository: GetByEmailAsync called with null or empty email.");
                return null;
            }

            string lowerEmail = email.ToLowerInvariant();

            // REFACTOR: Injected ILoggingSanitizer is used for all sanitization, adhering to SRP & DIP.
            var sanitizedEmail = _logSanitizer.Sanitize(lowerEmail);
            _logger.LogTrace("UserRepository: Fetching user by Email: {SanitizedEmail}.", sanitizedEmail);

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);

                    // OPTIMIZATION (EFFICIENCY): Perform a single, lightweight query to get only the ID.
                    var userIdQuery = "SELECT Id FROM Users WHERE LOWER(Email) = LOWER(@Email);";
                    var userId = await connection.ExecuteScalarAsync<Guid?>(userIdQuery, new { Email = lowerEmail });

                    if (!userId.HasValue)
                    {
                        _logger.LogDebug("User with email {SanitizedEmail} not found.", sanitizedEmail);
                        return null;
                    }

                    // REFACTOR (DRY): Re-use GetByIdAsync to avoid duplicating the complex data hydration logic.
                    // This makes the repository significantly more maintainable.
                    return await GetByIdAsync(userId.Value, cancellationToken);
                });
            }
            catch (Exception ex)
            {
                // HARDENING: The raw exception message is now sanitized before being logged to prevent PII leakage.
                _logger.LogError(ex, "UserRepository: Error fetching user by email {SanitizedEmail}. Exception (Sanitized): {SanitizedException}",
                    sanitizedEmail, _logSanitizer.Sanitize(ex.Message));

                // The re-thrown exception also uses the sanitized email.
                throw new RepositoryException($"Failed to fetch user by email '{sanitizedEmail}'.", ex);
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogTrace("UserRepository: Fetching all users.");
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                using var connection = CreateConnection();
                await connection.OpenAsync(cancellationToken);

                // Fetch all users first
                var users = (await connection.QueryAsync<UserDbDto>("SELECT Id, Username, TelegramId, Email, Level, CreatedAt, UpdatedAt, EnableGeneralNotifications, EnableVipSignalNotifications, EnableRssNewsNotifications, PreferredLanguage FROM Users ORDER BY Username;")).Select(dto => dto.ToDomainEntity()).ToList();

                if (users.Any())
                {
                    var userIds = users.Select(u => u.Id).ToList();

                    // Batch fetch all related data for all users in one go
                    // This is more efficient for N-many users than individual QueryMultiple calls
                    var wallets = (await connection.QueryAsync<TokenWalletDbDto>("SELECT Id, UserId, Balance, IsActive, CreatedAt, UpdatedAt FROM TokenWallets WHERE UserId IN @UserIds;", new { UserIds = userIds })).ToList();
                    var subscriptions = (await connection.QueryAsync<SubscriptionDbDto>("SELECT Id, UserId, StartDate, EndDate, Status, ActivatingTransactionId, CreatedAt, UpdatedAt FROM Subscriptions WHERE UserId IN @UserIds;", new { UserIds = userIds })).ToList();
                    var preferences = (await connection.QueryAsync<UserSignalPreferenceDbDto>("SELECT Id, UserId, CategoryId, CreatedAt FROM UserSignalPreferences WHERE UserId IN @UserIds;", new { UserIds = userIds })).ToList();

                    // Manually map collections back to the parent users
                    foreach (var user in users)
                    {
                        user.TokenWallet = wallets.FirstOrDefault(tw => tw.UserId == user.Id)?.ToDomainEntity() ?? TokenWallet.Create(user.Id);
                        user.Subscriptions = subscriptions.Where(s => s.UserId == user.Id).Select(s => s.ToDomainEntity()).ToList();
                        user.Preferences = preferences.Where(usp => usp.UserId == user.Id).Select(usp => usp.ToDomainEntity()).ToList();
                        user.Transactions = []; // Not fetched in this method
                    }
                }

                return users;
            });
        }

        /// <summary>
        /// This method cannot directly translate an arbitrary LINQ Expression to SQL with Dapper.
        /// It's a fundamental difference between an ORM like EF Core and a micro-ORM like Dapper.
        /// You should refactor callers to provide SQL 'WHERE' clauses and parameters directly,
        /// or consider building a more sophisticated expression parser, which is non-trivial.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown to indicate that arbitrary LINQ Expression predicates are not supported by this Dapper repository.</exception>
        public Task<IEnumerable<User>> FindAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken = default)
        {
            _logger.LogError("UserRepository: FindAsync with Expression<Func<User, bool>> is not directly supported by Dapper. " +
                             "Please refactor to use specific Get methods or pass raw SQL conditions.");
            throw new NotSupportedException("Arbitrary LINQ Expression predicates are not supported by this Dapper repository. " +
                                            "Please use specific query methods (e.g., GetByTelegramIdAsync, GetByEmailAsync) " +
                                            "or extend the repository with methods that accept SQL query parts and parameters.");
        }
        private string SanitizeSensitiveData(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            // Simple regex to find and redact email-like patterns.
            // This prevents leaking PII like emails into logs.
            return System.Text.RegularExpressions.Regex.Replace(input, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", "[REDACTED_EMAIL]");
        }

        // In Infrastructure/Persistence/Repositories/UserRepository.cs

        /// <summary>
        /// Atomically adds a new user and their associated token wallet to the database using a single,
        /// efficient, and resilient Dapper transaction. This method is designed for high performance
        /// and data integrity, with comprehensive, sanitized logging.
        /// </summary>
        // ... (rest of your UserRepository class including ILoggingSanitizer, _logger, _retryPolicy, _timeoutPolicy, CreateConnection, UserDbDto, TokenWalletDbDto, etc.) ...

        public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            // --- 1. Input Validation & Security (This part is fine) ---
            if (user == null)
            {
                _logger.LogError("UserRepository: Attempted to add a null User object.");
                throw new ArgumentNullException(nameof(user));
            }
            if (user.TokenWallet == null)
            {
                _logger.LogError("UserRepository: Attempted to add a User with a null TokenWallet. UserID for logging: {UserId}", user.Id);
                throw new InvalidOperationException("A User entity must have an associated TokenWallet for registration.");
            }
            if (user.Id != user.TokenWallet.UserId)
            {
                _logger.LogError("Data integrity violation: User.Id ({UserId}) does not match TokenWallet.UserId ({WalletUserId}).", user.Id, user.TokenWallet.UserId);
                throw new InvalidOperationException("User ID and TokenWallet User ID must match for a new user.");
            }

            var sanitizedUsername = _logSanitizer.Sanitize(user.Username);
            var sanitizedEmail = _logSanitizer.Sanitize(user.Email);

            _logger.LogInformation("UserRepository: Preparing to add new user '{SanitizedUsername}' with Email '{SanitizedEmail}'.",
                                   sanitizedUsername, sanitizedEmail);

            // --- 2. Corrected SQL with Quoted Identifiers ---
            // All table and column names that were created with quotes must be quoted here.
            const string insertSql = @"
        -- Statement 1: Insert the User
        INSERT INTO public.""Users"" (""Id"", ""Username"", ""TelegramId"", ""Email"", ""Level"", ""CreatedAt"", ""UpdatedAt"", ""EnableGeneralNotifications"", ""EnableVipSignalNotifications"", ""EnableRssNewsNotifications"", ""PreferredLanguage"")
        VALUES (@UserId, @Username, @TelegramId, @Email, @Level, @CreatedAt, @UpdatedAt, @EnableGeneralNotifications, @EnableVipSignalNotifications, @EnableRssNewsNotifications, @PreferredLanguage);

        -- Statement 2: Insert the TokenWallet
        INSERT INTO public.""TokenWallets"" (""Id"", ""UserId"", ""Balance"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
        VALUES (@TokenWalletId, @UserId, @Balance, @IsActive, @TokenWalletCreatedAt, @TokenWalletUpdatedAt);
    ";

            try
            {
                // --- 3. Polly Resilience Policy (This part is fine) ---
                var combinedPolicy = _timeoutPolicy.WrapAsync(_retryPolicy);

                await combinedPolicy.ExecuteAsync(async (ct) =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(ct);
                    await using var transaction = await connection.BeginTransactionAsync(ct);

                    try
                    {
                        // --- 4. Parameterization (This part is fine) ---
                        var parameters = new
                        {
                            UserId = user.Id,
                            user.Username,
                            user.TelegramId,
                            user.Email,
                            Level = user.Level.ToString(),
                            user.CreatedAt,
                            user.UpdatedAt,
                            user.EnableGeneralNotifications,
                            user.EnableVipSignalNotifications,
                            user.EnableRssNewsNotifications,
                            user.PreferredLanguage,
                            TokenWalletId = user.TokenWallet.Id,
                            user.TokenWallet.Balance,
                            user.TokenWallet.IsActive,
                            TokenWalletCreatedAt = user.TokenWallet.CreatedAt,
                            TokenWalletUpdatedAt = user.TokenWallet.UpdatedAt
                        };

                        // --- 5. Atomic Execution (This part is fine) ---
                        await connection.ExecuteAsync(insertSql, parameters, transaction: transaction);
                        await transaction.CommitAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(ct);
                        _logger.LogError(ex, "Transaction failed for user '{SanitizedUsername}'. Rolling back.", sanitizedUsername);
                        throw new RepositoryException($"Dapper transaction failed for user '{sanitizedUsername}'.", ex);
                    }
                }, cancellationToken);

                _logger.LogInformation("UserRepository: Successfully added user '{SanitizedUsername}' and their wallet.", sanitizedUsername);
            }
            catch (RepositoryException dbEx) // This part is fine
            {
                _logger.LogError(dbEx, "UserRepository: A database error occurred while adding user '{SanitizedUsername}' after retries. Exception (Sanitized): {SanitizedException}",
                    sanitizedUsername, _logSanitizer.Sanitize(dbEx.GetBaseException().Message));
                throw;
            }
            catch (Exception ex) // This part is fine
            {
                _logger.LogCritical(ex, "UserRepository: An unexpected critical error occurred while adding user '{SanitizedUsername}'. Exception (Sanitized): {SanitizedException}",
                    sanitizedUsername, _logSanitizer.Sanitize(ex.Message));
                throw;
            }
        }

        // In Infrastructure/Repositories/UserRepository.cs

        public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            if (user == null)
            {
                _logger.LogError("UserRepository: Attempted to update with a null User object.");
                throw new ArgumentNullException(nameof(user));
            }
            _logger.LogInformation("UserRepository: Updating user. UserID: {UserId}, Username: {Username}.", user.Id, user.Username);

            // --- 1. SQL STATEMENTS WITH QUOTED IDENTIFIERS AND PG-SYNTAX ---

            // SQL to update the main User record. All identifiers are quoted.
            const string updateUserSql = @"
        UPDATE public.""Users"" SET
            ""Username"" = @Username,
            ""TelegramId"" = @TelegramId,
            ""Email"" = @Email,
            ""Level"" = @Level,
            ""UpdatedAt"" = @UpdatedAt,
            ""EnableGeneralNotifications"" = @EnableGeneralNotifications,
            ""EnableVipSignalNotifications"" = @EnableVipSignalNotifications,
            ""EnableRssNewsNotifications"" = @EnableRssNewsNotifications,
            ""PreferredLanguage"" = @PreferredLanguage
        WHERE ""Id"" = @Id;";

            // SQL for PostgreSQL UPSERT (INSERT ... ON CONFLICT).
            // This atomically inserts a TokenWallet or updates it if it already exists.
            // We specify the unique constraint on ""UserId"" as the conflict target.
            const string upsertWalletSql = @"
        INSERT INTO public.""TokenWallets"" (""Id"", ""UserId"", ""Balance"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
        VALUES (@Id, @UserId, @Balance, @IsActive, @CreatedAt, @UpdatedAt)
        ON CONFLICT (""UserId"") DO UPDATE SET
            ""Balance"" = EXCLUDED.""Balance"",
            ""IsActive"" = EXCLUDED.""IsActive"",
            ""UpdatedAt"" = EXCLUDED.""UpdatedAt"";";

            try
            {
                // Use the combined Polly policy for resilience
                var combinedPolicy = _timeoutPolicy.WrapAsync(_retryPolicy);

                await combinedPolicy.ExecuteAsync(async (ct) =>
                {
                    await using var connection = CreateConnection();
                    await connection.OpenAsync(ct);

                    // It's crucial that all operations happen within a single transaction for atomicity.
                    await using var transaction = await connection.BeginTransactionAsync(ct);
                    try
                    {
                        // --- 2. PARAMETER PREPARATION ---

                        // Prepare parameters for the User update
                        var userParams = new
                        {
                            user.Id,
                            user.Username,
                            user.TelegramId,
                            user.Email,
                            Level = user.Level.ToString(),
                            UpdatedAt = DateTime.UtcNow, // Always set UpdatedAt on modification
                            user.EnableGeneralNotifications,
                            user.EnableVipSignalNotifications,
                            user.EnableRssNewsNotifications,
                            user.PreferredLanguage
                        };

                        // Execute the User update
                        var rowsAffected = await connection.ExecuteAsync(updateUserSql, userParams, transaction: transaction);

                        if (rowsAffected == 0)
                        {
                            // This is a valid concurrency check. If the user doesn't exist, we throw.
                            throw new InvalidOperationException($"User with ID '{user.Id}' not found for update. Concurrency conflict or record does not exist.");
                        }

                        // Prepare and execute the TokenWallet UPSERT
                        if (user.TokenWallet != null)
                        {
                            var walletParams = new
                            {
                                user.TokenWallet.Id,
                                user.TokenWallet.UserId,
                                user.TokenWallet.Balance,
                                user.TokenWallet.IsActive,
                                user.TokenWallet.CreatedAt, // Pass the original creation time for the INSERT part
                                UpdatedAt = DateTime.UtcNow  // Set a new UpdatedAt for both INSERT and UPDATE parts
                            };

                            await connection.ExecuteAsync(upsertWalletSql, walletParams, transaction: transaction);
                        }
                        else
                        {
                            _logger.LogWarning("UserRepository: User {UserId} has no TokenWallet object for update. Skipping wallet update.", user.Id);
                        }

                        // If all operations succeed, commit the transaction.
                        await transaction.CommitAsync(ct);
                    }
                    catch (Exception)
                    {
                        // If any error occurs (e.g., the concurrency exception or a DB error),
                        // roll back the entire transaction.
                        await transaction.RollbackAsync();
                        throw; // Re-throw the original exception to be handled by the outer catch blocks.
                    }
                }, cancellationToken);

                _logger.LogInformation("UserRepository: Successfully updated user: {Username}", user.Username);
            }
            catch (InvalidOperationException concEx) // Catch the specific concurrency exception
            {
                _logger.LogError(concEx, "UserRepository: Concurrency conflict or user not found while updating user {Username}.", user.Username);
                throw;
            }
            catch (Exception ex) // Catch any other exceptions, including wrapped ones from Polly
            {
                _logger.LogError(ex, "UserRepository: An error occurred while updating user {Username}.", user.Username);
                // Wrap in a custom RepositoryException if it isn't one already.
                if (ex is not RepositoryException)
                {
                    throw new RepositoryException($"Failed to update user '{user.Username}'.", ex);
                }
                throw;
            }
        }

        public async Task<User?> GetByTelegramIdWithNotificationsAsync(string telegramId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(telegramId))
            {
                _logger.LogWarning("UserRepository: GetByTelegramIdWithNotificationsAsync called with null or empty telegramId.");
                return null;
            }

            _logger.LogTrace("UserRepository: Fetching user with notifications by TelegramID: {TelegramId} using JSON aggregation.", telegramId);

            // --- USE THE OPTIMIZED SQL FRAGMENT ---
            // This query is identical to the one in GetByTelegramIdAsync.
            var sql = $"{UserWithRelatedDataSqlFragment} WHERE u.\"TelegramId\" = @TelegramId;";

            try
            {
                var combinedPolicy = _timeoutPolicy.WrapAsync(_retryPolicy);

                return await combinedPolicy.ExecuteAsync(async (ct) =>
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(ct);

                    // Use QueryFirstOrDefaultAsync with the DTO designed for this query.
                    var userDto = await connection.QueryFirstOrDefaultAsync<UserWithRelatedDataDto>(
                        new CommandDefinition(sql, new { TelegramId = telegramId }, cancellationToken: ct)
                    );

                    if (userDto == null)
                    {
                        _logger.LogTrace("User with TelegramID {TelegramId} not found.", telegramId);
                        return null;
                    }

                    // The DTO handles all the complex mapping and JSON deserialization.
                    var user = userDto.ToDomainEntity(_logger);

                    _logger.LogDebug("Successfully fetched user {UserId} (TelegramID: {TelegramId}) with all related entities.", user.Id, telegramId);
                    return user;

                }, cancellationToken);
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogError(ex, "UserRepository: Operation timed out while fetching user by TelegramID {TelegramId}.", telegramId);
                throw new RepositoryException($"The operation to fetch user by TelegramID {telegramId} timed out.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user by TelegramID {TelegramId} after retries.", telegramId);
                throw new RepositoryException($"An error occurred while fetching user by TelegramID: {telegramId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
        {
            if (user == null)
            {
                _logger.LogError("UserRepository: Attempted to delete a null User object.");
                throw new ArgumentNullException(nameof(user));
            }
            _logger.LogInformation("UserRepository: Deleting user by object. UserID: {UserId}, Username: {Username}.", user.Id, user.Username);
            await DeleteUserAndRelatedData(user.Id, cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("UserRepository: Attempting to delete user by ID: {UserId}.", id);
            await DeleteUserAndRelatedData(id, cancellationToken);
        }

        /// <inheritdoc />
        // In Infrastructure/Repositories/UserRepository.cs

        public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            string lowerEmail = email.ToLowerInvariant();
            var sanitizedEmail = _logSanitizer.Sanitize(lowerEmail);
            _logger.LogTrace("UserRepository: Checking existence by Email: {SanitizedEmail}.", sanitizedEmail);

            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE LOWER(Email) = LOWER(@Email);", new { Email = lowerEmail });
                    return count > 0;
                });
            }
            catch (Exception ex)
            {
                // HARDENING: Sanitize the raw exception message before logging.
                _logger.LogError(ex, "UserRepository: Error checking existence for email {SanitizedEmail}. Exception (Sanitized): {SanitizedException}",
                    sanitizedEmail, _logSanitizer.Sanitize(ex.Message));

                // The re-thrown exception also uses the sanitized email to prevent leaks up the call stack.
                throw new RepositoryException($"Failed to check existence for email '{sanitizedEmail}'.", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByTelegramIdAsync(string telegramId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(telegramId))
            {
                return false;
            }

            _logger.LogTrace("UserRepository: Checking existence by TelegramID: {TelegramId}.", telegramId);
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);
                    var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE TelegramId = @TelegramId;", new { TelegramId = telegramId });
                    return count > 0;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserRepository: Error checking existence by TelegramID {TelegramId}.", telegramId);
                throw new RepositoryException($"Failed to check existence by TelegramID '{telegramId}'.", ex);
            }
        }

        /// <inheritdoc />
        public async Task DeleteAndSaveAsync(User user, CancellationToken cancellationToken)
        {
            if (user == null)
            {
                _logger.LogError("UserRepository: Attempted to delete and save a null User object.");
                throw new ArgumentNullException(nameof(user));
            }
            _logger.LogInformation("UserRepository: Marking user for deletion and immediate save. UserID: {UserId}, Username: {Username}.", user.Id, user.Username);
            // This method effectively just calls the same underlying delete logic.
            await DeleteUserAndRelatedData(user.Id, cancellationToken);
            _logger.LogInformation("UserRepository: Successfully deleted user (ID: {UserId}) and saved changes.", user.Id);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<User>> GetUsersWithNotificationSettingAsync(
    Expression<Func<User, bool>> notificationPredicate,
    CancellationToken cancellationToken = default)
        {
            _logger.LogError("UserRepository: GetUsersWithNotificationSettingAsync with Expression<Func<User, bool>> is NOT SUPPORTED by Dapper directly. " +
                             "This method will throw a NotSupportedException.");
            return await Task.FromException<IEnumerable<User>>(
                new NotSupportedException("Arbitrary LINQ Expression predicates are not supported by this Dapper repository for notification settings. " +
                                           "Please replace calls to this method with specific SQL queries or dedicated methods like GetUsersForNewsNotificationAsync, " +
                                           "or pass raw SQL conditions from the calling layer.")
            );
        }

        // --- Private Helper Method for Deletion (to encapsulate transaction and retry logic) ---
        private async Task DeleteUserAndRelatedData(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    using var connection = CreateConnection();
                    await connection.OpenAsync(cancellationToken);

                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        // Check if the user exists before attempting to delete
                        var userExists = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Id = @Id;", new { Id = userId }, transaction: transaction);
                        if (userExists == 0)
                        {
                            _logger.LogWarning("UserRepository: User with ID {UserId} not found for deletion in DeleteUserAndRelatedData.", userId);
                            transaction.Rollback(); // Rollback empty transaction
                            return; // Return silently if not found
                        }

                        // Due to ON DELETE CASCADE foreign key constraints configured in your EF Core model:
                        // Deleting the parent 'Users' record will automatically delete related records
                        // in 'TokenWallets', 'Subscriptions', 'Transactions', and 'UserSignalPreferences'.
                        // If you did NOT have ON DELETE CASCADE, you would need explicit DELETE statements
                        // for child tables BEFORE deleting from the Users table, respecting foreign key order.
                        var rowsAffected = await connection.ExecuteAsync("DELETE FROM Users WHERE Id = @Id;", new { Id = userId }, transaction: transaction);

                        if (rowsAffected == 0)
                        {
                            // This might indicate a concurrency issue where the user was deleted by another process
                            // between the initial existence check and this actual delete.
                            throw new InvalidOperationException($"User with ID '{userId}' was not found for deletion or was deleted by another process. Concurrency conflict suspected.");
                        }

                        transaction.Commit();
                    }
                    catch (InvalidOperationException) // Catch the custom concurrency exception
                    {
                        transaction.Rollback();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        // Wrap the exception in a RepositoryException for consistency
                        throw new RepositoryException($"Failed to delete user with ID '{userId}' with Dapper transaction.", ex);
                    }
                });
                _logger.LogInformation("UserRepository: Successfully deleted user with ID: {UserId}", userId);
            }
            catch (InvalidOperationException concEx)
            {
                _logger.LogError(concEx, "UserRepository: Concurrency conflict or user not found while deleting user with ID {UserId}.", userId);
                throw;
            }
            catch (RepositoryException dbEx)
            {
                _logger.LogError(dbEx, "UserRepository: Error deleting user with ID {UserId} from the database after retries.", userId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UserRepository: An unexpected error occurred while deleting user with ID {UserId}.", ex);
                throw;
            }
        }
    }
}