using Microsoft.Extensions.Configuration;
using Moq;

namespace Application.Tests
{
    // Example: A hypothetical class or options that might hold DB connection parameters
    // In a real scenario, this might be part of a larger configuration object or
    // directly consumed by a service.
    public class DatabaseConnectionParameters
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? UserId { get; set; }
        public string? Password { get; set; } // Be careful with passwords in tests; use mock/dummy values

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Host) &&
                   Port > 0 &&
                   !string.IsNullOrEmpty(DatabaseName) &&
                   !string.IsNullOrEmpty(UserId);
            // Password might be optional for some auth methods or handled differently
        }

        public string ToPostgresConnectionString()
        {
            if (!IsValid() || string.IsNullOrEmpty(Password)) // Ensure password for this format
                return string.Empty; // Or throw exception

            return $"Host={Host};Port={Port};Database={DatabaseName};Username={UserId};Password={Password};Include Error Detail=true";
        }
    }

    // Example: A service that might consume these parameters or configuration
    public interface IDatabaseConfigurationProvider
    {
        DatabaseConnectionParameters GetConnectionParameters(string connectionName = "DefaultConnection");
        string GetFormattedConnectionString(string connectionName = "DefaultConnection");
    }

    public class DatabaseConfigurationProvider : IDatabaseConfigurationProvider
    {
        private readonly IConfiguration _configuration;

        public DatabaseConfigurationProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DatabaseConnectionParameters GetConnectionParameters(string connectionName = "DefaultConnection")
        {
            // In a real app, connectionName might map to different sections, e.g., "PostgresConnection", "SqlServerConnection"
            // For this test, we'll assume it maps to a single "Database" section that has sub-keys like Host, Port etc.
            // Or, it could be a direct connection string like in previous ConfigurationTests.cs
            // Here, let's imagine a structure like Database:Default:Host, Database:Default:Port
            return new DatabaseConnectionParameters
            {
                Host = _configuration[$"Database:{connectionName}:Host"],
                Port = _configuration.GetValue<int>($"Database:{connectionName}:Port"),
                DatabaseName = _configuration[$"Database:{connectionName}:DatabaseName"],
                UserId = _configuration[$"Database:{connectionName}:UserId"],
                Password = _configuration[$"Database:{connectionName}:Password"]
            };
        }

        public string GetFormattedConnectionString(string connectionName = "DefaultConnection")
        {
            // This might directly fetch a pre-formatted string from IConfiguration
            // For example, "ConnectionStrings:DefaultConnection"
            var preformatted = _configuration.GetConnectionString(connectionName);
            if (!string.IsNullOrEmpty(preformatted))
            {
                return preformatted;
            }

            // Or build it from parameters
            var dbParams = GetConnectionParameters(connectionName);
            return dbParams.ToPostgresConnectionString();
        }
    }


    public class DbConnectionTests
    {
        [Fact]
        public void DatabaseConnectionParameters_Validates_Correctly()
        {
            // Arrange
            var validParams = new DatabaseConnectionParameters
            {
                Host = "localhost", Port = 5432, DatabaseName = "mydb", UserId = "user", Password = "password"
            };
            var invalidParamsMissingHost = new DatabaseConnectionParameters
            {
                Port = 5432, DatabaseName = "mydb", UserId = "user", Password = "password"
            };
            var invalidParamsZeroPort = new DatabaseConnectionParameters
            {
                Host = "localhost", Port = 0, DatabaseName = "mydb", UserId = "user", Password = "password"
            };

            // Act & Assert
            Assert.True(validParams.IsValid());
            Assert.False(invalidParamsMissingHost.IsValid());
            Assert.False(invalidParamsZeroPort.IsValid());
        }

        [Fact]
        public void DatabaseConnectionParameters_Formats_Postgres_ConnectionString_Correctly()
        {
            // Arrange
            var dbParams = new DatabaseConnectionParameters
            {
                Host = "pg.example.com",
                Port = 5433,
                DatabaseName = "prod_db",
                UserId = "prod_user",
                Password = "ComplexPassword123!"
            };
            var expectedConnectionString = "Host=pg.example.com;Port=5433;Database=prod_db;Username=prod_user;Password=ComplexPassword123!;Include Error Detail=true";

            // Act
            string actualConnectionString = dbParams.ToPostgresConnectionString();

            // Assert
            Assert.Equal(expectedConnectionString, actualConnectionString);
        }

        [Fact]
        public void DatabaseConfigurationProvider_Retrieves_Preformatted_ConnectionString()
        {
            // Arrange
            var expectedConnectionString = "Host=test_host;Port=5432;Database=test_db;Username=test_user;Password=test_pass";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"ConnectionStrings:DefaultConnection", expectedConnectionString}
                })
                .Build();

            var provider = new DatabaseConfigurationProvider(configuration);

            // Act
            string actualConnectionString = provider.GetFormattedConnectionString("DefaultConnection");

            // Assert
            Assert.Equal(expectedConnectionString, actualConnectionString);
        }

        [Fact]
        public void DatabaseConfigurationProvider_Builds_ConnectionString_From_Parameters_If_Not_Preformatted()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"Database:MyDb:Host", "db.example.org"},
                    {"Database:MyDb:Port", "1234"},
                    {"Database:MyDb:DatabaseName", "special_db"},
                    {"Database:MyDb:UserId", "special_user"},
                    {"Database:MyDb:Password", "secret"}
                })
                .Build();

            var provider = new DatabaseConfigurationProvider(configuration);
            var expected = "Host=db.example.org;Port=1234;Database=special_db;Username=special_user;Password=secret;Include Error Detail=true";

            // Act
            string actual = provider.GetFormattedConnectionString("MyDb");

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetConnectionParameters_Returns_Null_For_Missing_Values_When_Not_Strictly_Typed()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"Database:PartialDb:Host", "only_host_here"}
                    // Port, DatabaseName, UserId, Password are missing
                })
                .Build();
            var provider = new DatabaseConfigurationProvider(configuration);

            // Act
            var dbParams = provider.GetConnectionParameters("PartialDb");

            // Assert
            Assert.Equal("only_host_here", dbParams.Host);
            Assert.Equal(0, dbParams.Port); // GetValue<int> defaults to 0 if missing/invalid
            Assert.Null(dbParams.DatabaseName);
            Assert.Null(dbParams.UserId);
            Assert.Null(dbParams.Password);
            Assert.False(dbParams.IsValid()); // Should be invalid
        }
    }
}
