// In Infrastructure/Services/EmbeddedRedisService.cs

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis; // We need this to ping the server
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    // --- STEP 1: Add a small settings class for configurability ---
    public class EmbeddedRedisSettings
    {
        public const string SectionName = "EmbeddedRedis";
        public string ServerExecutablePath { get; set; } = Path.Combine("redis-server", "redis-server.exe");
        public string ConnectionString { get; set; } = "localhost:6379";
        public int StartupPingTimeoutSeconds { get; set; } = 5;
    }

    /// <summary>
    /// An IHostedService that manages the lifecycle of an embedded, portable Redis server.
    /// It starts the redis-server.exe process, actively verifies its responsiveness,
    /// and ensures it's terminated when the application shuts down.
    /// </summary>
    public class EmbeddedRedisService : IHostedService
    {
        private readonly ILogger<EmbeddedRedisService> _logger;
        private readonly EmbeddedRedisSettings _settings;
        private Process? _redisProcess;
        private readonly string _redisServerFullPath;

        public EmbeddedRedisService(ILogger<EmbeddedRedisService> logger, IOptions<EmbeddedRedisSettings> settings)
        {
            _logger = logger;
            _settings = settings.Value;
            // The path is now relative to the application's base directory.
            _redisServerFullPath = Path.Combine(AppContext.BaseDirectory, _settings.ServerExecutablePath);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // --- STEP 2: Use Console Helpers for beautiful, clear UI output ---
            ConsoleUI.WriteHeader("Embedded Redis Service");

            if (!File.Exists(_redisServerFullPath))
            {
                ConsoleUI.WriteError($"Embedded Redis server not found at '{_redisServerFullPath}'.");
                _logger.LogError("Embedded Redis server executable not found. The server will not be started.");
                return;
            }

            ConsoleUI.WriteInfo($"Attempting to start embedded Redis server...");
            _logger.LogInformation("Starting embedded Redis server from: {Path}", _redisServerFullPath);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _redisServerFullPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, // Redirect output to monitor for "Ready to accept connections"
                RedirectStandardError = true
            };

            try
            {
                _redisProcess = new Process { StartInfo = processStartInfo };
                _redisProcess.EnableRaisingEvents = true;

                // Capture output to know when the server is ready
                var serverReadyTcs = new TaskCompletionSource<bool>();
                _redisProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data) && args.Data.Contains("Ready to accept connections"))
                    {
                        serverReadyTcs.TrySetResult(true);
                    }
                };
                _redisProcess.ErrorDataReceived += (sender, args) => {
                    if (!string.IsNullOrEmpty(args.Data))
                        _logger.LogWarning("Redis Process stderr: {RedisError}", args.Data);
                };

                _redisProcess.Start();
                _redisProcess.BeginOutputReadLine();
                _redisProcess.BeginErrorReadLine();

                // --- STEP 3: Actively verify the connection ---
                ConsoleUI.WriteInfo($"Redis process started with ID {_redisProcess.Id}. Waiting for it to become responsive...");

                // Wait for the "Ready" message from the output OR a timeout
                var completedTask = await Task.WhenAny(serverReadyTcs.Task, Task.Delay(TimeSpan.FromSeconds(_settings.StartupPingTimeoutSeconds), cancellationToken));

                if (completedTask == serverReadyTcs.Task && serverReadyTcs.Task.Result)
                {
                    ConsoleUI.WriteSuccess("Redis server is ready to accept connections.");
                }
                else
                {
                    // If it timed out, try one final ping as a backup check.
                    if (await IsRedisResponsiveAsync())
                    {
                        ConsoleUI.WriteSuccess("Redis server is responding to pings.");
                    }
                    else
                    {
                        ConsoleUI.WriteError("Redis process started but did not become responsive in time. Check logs for errors.");
                        _redisProcess.Kill(); // Kill the unresponsive process
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteError("A critical error occurred while starting the embedded Redis server.");
                _logger.LogCritical(ex, "Embedded Redis startup failed. Please ensure it's not blocked by antivirus.");
            }
        }

        private async Task<bool> IsRedisResponsiveAsync()
        {
            try
            {
                var connection = await ConnectionMultiplexer.ConnectAsync(_settings.ConnectionString + ",connectTimeout=1000");
                if (connection.IsConnected)
                {
                    await connection.GetDatabase().PingAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Ping to embedded Redis failed (this can be normal during startup).");
            }
            return false;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_redisProcess != null && !_redisProcess.HasExited)
            {
                ConsoleUI.WriteHeader("Shutting Down Embedded Redis");
                _logger.LogInformation("Stopping embedded Redis server (Process ID: {ProcessId})...", _redisProcess.Id);
                try
                {
                    if (!_redisProcess.CloseMainWindow())
                    {
                        _redisProcess.Kill();
                        _logger.LogWarning("Redis process did not shut down gracefully and was killed.");
                    }
                    ConsoleUI.WriteSuccess("Embedded Redis server stopped.");
                }
                catch (Exception ex)
                {
                    ConsoleUI.WriteError("An error occurred while stopping the embedded Redis server.");
                    _logger.LogError(ex, "Failure during embedded Redis shutdown.");
                }
                finally
                {
                    _redisProcess.Dispose();
                }
            }
            return Task.CompletedTask;
        }
    }

    // --- STEP 4: Add a static UI helper class for beautiful console output ---
    public static class ConsoleUI
    {
        public static void WriteHeader(string text)
        {
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"--- ╔═════════════════════════════════════╗ ---");
            Console.WriteLine($"--- ║   {text.PadRight(35).Substring(0, 35)} ║ ---");
            Console.WriteLine($"--- ╚═════════════════════════════════════╝ ---");
            Console.ResetColor();
        }

        public static void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ {message}");
            Console.ResetColor();
        }

        public static void WriteInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"ℹ️ {message}");
            Console.ResetColor();
        }

        public static void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️ {message}");
            Console.ResetColor();
        }

        public static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ {message}");
            Console.ResetColor();
        }
    }
}