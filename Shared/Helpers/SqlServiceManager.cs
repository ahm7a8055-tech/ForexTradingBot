using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq; // Still needed for other potential LINQ uses, and good practice to keep
using System.Management;
using System.ServiceProcess;
using System.Threading;

namespace Shared.Helpers
{
    /// <summary>
    /// Helper class for managing Windows services, specifically focusing on
    /// ensuring SQL services are running and Memurai Redis is running and
    /// correctly configured.
    /// </summary>
    public static class ServiceManagerHelper
    {
        // --- SQL Services Configuration ---
        private static readonly string[] SqlServices = new[]
        {
            "MsDtsServer160",          // SQL Server Integration Services 16.0
            "MSSQLFDLauncher",         // SQL Full-text Filter Daemon Launcher
            "MSSQLSERVER",             // SQL Server (Default Instance)
            "MSSQLServerOLAPService",  // SQL Server Analysis Services
            "SQLBrowser",              // SQL Server Browser
            "SQLSERVERAGENT",          // SQL Server Agent
            "SQLTELEMETRY",            // SQL Server CEIP (Telemetry)
            "SQLWriter",               // SQL Server VSS Writer
            "SSASTELEMETRY",           // SSAS Telemetry
            "SSISTELEMETRY160"         // SSIS Telemetry
        };

        // --- Memurai Redis Service Configuration ---

        // !! IMPORTANT !!
        // This is the CRITICAL variable you MUST set correctly.
        // The name "Memurai-for-Redis-v4.2.0.msi" is the INSTALLER filename.
        // You need the ACTUAL SERVICE NAME that Memurai runs as.
        //
        // To find it:
        // 1. Open the Services console (type "services.msc" in Run or Search).
        // 2. Find the Memurai service in the list.
        // 3. Look at the "Service name" column. This is what you need to put here.
        //
        // For example, it might be "Memurai", "redis", "redis-server", etc.
        // REPLACE "Memurai" BELOW WITH YOUR ACTUAL MEMURAI SERVICE NAME.
        private const string MemuraiServiceName = "Memurai";

        // The default installation path for Memurai, as provided.
        // This path is used for verifying the executable location.
        private const string DefaultMemuraiInstallPath = @"C:\Program Files\Memurai";

        /// <summary>
        /// Ensures that the configured SQL services are running, and also checks and
        /// manages the Memurai Redis service, including verifying its executable path
        /// and restarting it if it's not running or its path is incorrect.
        /// </summary>
        public static void EnsureAllServicesRunningAndHealthy()
        {
            EnsureSqlServicesRunning();
            ManageMemuraiRedisService();
        }

        /// <summary>
        /// Checks the status of configured SQL services and starts any that are stopped.
        /// </summary>
        private static void EnsureSqlServicesRunning()
        {
            List<string> servicesToStart = new();

            foreach (var service in SqlServices)
            {
                try
                {
                    // Using 'sc query' for SQL services, as it's robust for these specific services.
                    string state = GetServiceStateUsingSC(service);

                    switch (state)
                    {
                        case "RUNNING":
                            Console.WriteLine($"[OK] SQL Service '{service}' is already running.");
                            break;
                        case "STOPPED":
                            Console.WriteLine($"[INFO] SQL Service '{service}' is stopped. Will attempt to start.");
                            servicesToStart.Add(service);
                            break;
                        case "START_PENDING":
                            Console.WriteLine($"[WAIT] SQL Service '{service}' is currently starting...");
                            break;
                        case "NOT_FOUND":
                            Console.WriteLine($"[SKIP] SQL Service '{service}' not found.");
                            break;
                        default:
                            Console.WriteLine($"[WARN] SQL Service '{service}' has unknown state: {state}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to check SQL Service '{service}': {ex.Message}");
                }
            }

            if (servicesToStart.Any())
            {
                Console.WriteLine($"[INFO] Attempting to start {servicesToStart.Count} stopped SQL services...");
                StartMultipleServicesUsingNetStart(servicesToStart.ToArray());
            }
        }

        /// <summary>
        /// Manages the Memurai Redis service:
        /// 1. Checks if the service exists.
        /// 2. Verifies its executable path against the default installation path.
        /// 3. If the service is stopped, starts it.
        /// 4. If the service executable path is incorrect, it logs a warning and attempts a restart.
        /// </summary>
        private static void ManageMemuraiRedisService()
        {
            try
            {
                if (!ServiceExists(MemuraiServiceName))
                {
                    Console.WriteLine($"[INFO] Memurai Redis service '{MemuraiServiceName}' not found. Please ensure Memurai is installed and its service is correctly named.");
                    return;
                }

                // Check if the service executable is in the default Memurai path.
                bool isPathCorrect = IsMemuraiExecutablePathCorrect();

                if (!isPathCorrect)
                {
                    Console.WriteLine($"[WARN] Memurai Redis service '{MemuraiServiceName}' executable is not in the default path ('{DefaultMemuraiInstallPath}'). Attempting restart to potentially correct or highlight the issue.");
                    RestartMemuraiRedisService(); // Attempt restart, which might fix it if it's just an issue from a bad install/config.
                    // Re-check path after restart attempt to see if it was fixed.
                    if (!IsMemuraiExecutablePathCorrect())
                    {
                        Console.WriteLine($"[ERROR] Memurai Redis service executable path remains incorrect after restart attempt. Manual intervention may be required.");
                    }
                    return; // Exit after handling incorrect path, as start might be handled by restart.
                }

                // If the path is correct, ensure the service is running.
                ServiceControllerStatus status = GetServiceStatus(MemuraiServiceName);

                switch (status)
                {
                    case ServiceControllerStatus.Running:
                        Console.WriteLine($"[OK] Memurai Redis service '{MemuraiServiceName}' is already running and path is correct.");
                        break;
                    case ServiceControllerStatus.Stopped:
                        Console.WriteLine($"[INFO] Memurai Redis service '{MemuraiServiceName}' is stopped. Attempting to start...");
                        StartService(MemuraiServiceName);
                        break;
                    case ServiceControllerStatus.StartPending:
                        Console.WriteLine($"[WAIT] Memurai Redis service '{MemuraiServiceName}' is currently starting...");
                        break;
                    case ServiceControllerStatus.StopPending:
                        Console.WriteLine($"[WAIT] Memurai Redis service '{MemuraiServiceName}' is currently stopping...");
                        break;
                    default:
                        Console.WriteLine($"[WARN] Memurai Redis service '{MemuraiServiceName}' is in an unknown state: {status}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to manage Memurai Redis service '{MemuraiServiceName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Restarts the Memurai Redis service. This is called if the service is stopped,
        /// or if its executable path is found to be incorrect.
        /// </summary>
        public static void RestartMemuraiRedisService()
        {
            try
            {
                Console.WriteLine($"[INFO] Attempting to restart Memurai Redis service '{MemuraiServiceName}'...");

                // Ensure the service exists before attempting to stop/start.
                if (!ServiceExists(MemuraiServiceName))
                {
                    Console.WriteLine($"[ERROR] Memurai Redis service '{MemuraiServiceName}' not found. Cannot restart.");
                    return;
                }

                // Stop the service if it's running.
                if (GetServiceStatus(MemuraiServiceName) == ServiceControllerStatus.Running)
                {
                    StopService(MemuraiServiceName);
                    Console.WriteLine($"[INFO] Memurai Redis service '{MemuraiServiceName}' stopped. Waiting for it to fully stop...");
                    Thread.Sleep(3000); // Wait for 3 seconds; adjust if needed.
                }

                // Start the service again.
                StartService(MemuraiServiceName);
                Console.WriteLine($"[SUCCESS] Memurai Redis service '{MemuraiServiceName}' restart initiated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to restart Memurai Redis service '{MemuraiServiceName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the executable for the Memurai service is located within the default installation path.
        /// Returns true if the path is correct, false if it's incorrect or verification failed.
        /// </summary>
        /// <returns>True if the path is correct, false if it's incorrect or verification failed.</returns>
        private static bool IsMemuraiExecutablePathCorrect()
        {
            try
            {
                // Ensure the MemuraiServiceName is correctly set and the service actually exists.
                if (!ServiceExists(MemuraiServiceName))
                {
                    Console.WriteLine($"[WARN] Cannot verify path for Memurai service '{MemuraiServiceName}' as it does not exist.");
                    return false; // Cannot verify if service doesn't exist.
                }

                // WMI query to get the PathName property of the service.
                string query = $"SELECT PathName FROM Win32_Service WHERE Name = '{MemuraiServiceName}'";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection queryCollection = searcher.Get();

                    // Check if the collection has any items by trying to get an enumerator.
                    // This avoids the 'Any()' extension method directly on ManagementObjectCollection.
                    using (var enumerator = queryCollection.GetEnumerator())
                    {
                        if (!enumerator.MoveNext()) // If MoveNext() returns false, the collection is empty.
                        {
                            Console.WriteLine($"[WARN] Could not retrieve WMI service definition for '{MemuraiServiceName}'. Path verification skipped.");
                            return false; // Could not retrieve info, treat as an issue.
                        }

                        // If we reached here, enumerator.Current should be the first (and likely only) ManagementObject.
                        ManagementObject obj = (ManagementObject)enumerator.Current;

                        string pathName = obj["PathName"]?.ToString();

                        if (string.IsNullOrEmpty(pathName))
                        {
                            Console.WriteLine($"[WARN] Memurai Redis service '{MemuraiServiceName}' has no 'PathName' defined in WMI. Path verification skipped.");
                            return false; // Missing PathName is an issue.
                        }

                        // The PathName might be quoted, e.g., "\"C:\\Program Files\\Memurai\\memurai.exe\""
                        // We need to clean it up and extract the directory.
                        string executablePath = pathName.Trim('"');
                        string executableDirectory = Path.GetDirectoryName(executablePath);

                        // Perform a case-insensitive comparison for the path.
                        if (!string.IsNullOrEmpty(executableDirectory) && executableDirectory.StartsWith(DefaultMemuraiInstallPath, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[OK] Memurai Redis executable '{executablePath}' found within the default path '{DefaultMemuraiInstallPath}'.");
                            return true; // Path is correct.
                        }
                        else
                        {
                            Console.WriteLine($"[WARN] Memurai Redis executable '{executablePath}' is NOT located in the expected default path '{DefaultMemuraiInstallPath}'.");
                            return false; // Path is incorrect.
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Catch any exceptions during WMI query or path processing.
                Console.WriteLine($"[ERROR] Exception during Memurai Redis executable path verification: {ex.Message}");
                return false; // If any error occurs during verification, consider it a failure.
            }
        }

        // --- Helper Methods (mostly unchanged, kept for completeness) ---

        /// <summary>
        /// Checks if a Windows service with the given name exists.
        /// </summary>
        private static bool ServiceExists(string serviceName)
        {
            try
            {
                // Using ServiceController to check for service existence.
                // Accessing a property like Status will throw InvalidOperationException if the service doesn't exist.
                using (var sc = new ServiceController(serviceName))
                {
                    var _ = sc.Status; // This line will throw if the service is not found.
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                return false; // Service not found.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Exception encountered while checking for service '{serviceName}': {ex.Message}");
                return false; // Treat other exceptions as service not found or inaccessible.
            }
        }

        /// <summary>
        /// Gets the current status of a Windows service using ServiceController.
        /// </summary>
        private static ServiceControllerStatus GetServiceStatus(string serviceName)
        {
            using (var sc = new ServiceController(serviceName))
            {
                sc.Refresh(); // Ensure the status is up-to-date.
                return sc.Status;
            }
        }

        /// <summary>
        /// Starts a Windows service.
        /// </summary>
        private static void StartService(string serviceName)
        {
            using (var sc = new ServiceController(serviceName))
            {
                // Only attempt to start if the service is stopped or pending stop.
                if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
                {
                    sc.Start();
                    // Wait for the service to reach the running state, with a timeout.
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
            }
        }

        /// <summary>
        /// Stops a Windows service.
        /// </summary>
        private static void StopService(string serviceName)
        {
            using (var sc = new ServiceController(serviceName))
            {
                // Only attempt to stop if the service is running or pending start.
                if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                {
                    sc.Stop();
                    // Wait for the service to reach the stopped state, with a timeout.
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
            }
        }

        /// <summary>
        /// Gets the state of a Windows service using the 'sc query' command.
        /// This method is primarily used for SQL services.
        /// </summary>
        /// <returns>A string representing the service state (RUNNING, STOPPED, START_PENDING, NOT_FOUND, or UNKNOWN).</returns>
        private static string GetServiceStateUsingSC(string serviceName)
        {
            try
            {
                Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe", // Explicitly use sc.exe for clarity.
                        Arguments = $"query \"{serviceName}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true // Hide the command prompt window.
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse the output from 'sc query' to determine the service state.
                if (output.Contains("STATE              : 4  RUNNING")) return "RUNNING";
                if (output.Contains("STATE              : 10 STOPPED") || output.Contains("STATE              : 2  STOPPED")) return "STOPPED";
                if (output.Contains("STATE              : 3  START_PENDING") || output.Contains("STATE              : 20 START_PENDING")) return "START_PENDING";
                if (output.Contains("STATE              : 128 PENDING_STOP")) return "PENDING_STOP";
                if (output.Contains("STATE              : 32 STOP_PENDING")) return "STOP_PENDING";

                // If none of the known states are found in the output.
                return "UNKNOWN";
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1060) // 1060 is ERROR_SERVICE_DOES_NOT_EXIST
            {
                return "NOT_FOUND"; // Specifically handle the case where the service doesn't exist.
            }
            catch (Exception ex)
            {
                // Catch any other exceptions that might occur during process execution.
                Console.WriteLine($"[ERROR] Exception during 'sc query' for service '{serviceName}': {ex.Message}");
                return "ERROR"; // Indicate an error occurred during state retrieval.
            }
        }

        /// <summary>
        /// Starts multiple services by executing 'net start' commands sequentially.
        /// This method uses 'cmd.exe /c' and requires administrative privileges.
        /// </summary>
        public static void StartMultipleServicesUsingNetStart(string[] services)
        {
            if (services == null || !services.Any())
            {
                Console.WriteLine("[INFO] No services provided to start.");
                return;
            }

            try
            {
                // Construct the command to start all services, separated by '&&' for sequential execution.
                string allStarts = string.Join(" && ", services.Select(s => $"net start \"{s}\""));

                Process process = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {allStarts}",
                        Verb = "runas", // Request administrative privileges (UAC prompt will appear if not already admin).
                        UseShellExecute = true, // Required for 'Verb' to work.
                        CreateNoWindow = false // Set to true if you want to hide the cmd window briefly. False is better for debugging.
                    }
                };

                process.Start();
                // We don't strictly need to WaitForExit() here as the commands are issued.
                // For service starts, they might take time to transition.
                Console.WriteLine($"[SUCCESS] Commands issued to start: {string.Join(", ", services)}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FAILED] Could not start services: {ex.Message}");
            }
        }
    }
}