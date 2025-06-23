// File: Shared/Helpers/ServiceManagerHelper.cs

#region Usings
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
#endregion

namespace Shared.Helpers
{
    /// <summary>
    /// Helper class for managing Windows services. This version has been fully upgraded for
    /// reliability, performance, and a seamless user experience with UAC self-elevation.
    /// </summary>
    [SupportedOSPlatform("windows")] // FIX: Resolves all platform compatibility warnings for this class.
    public static class ServiceManagerHelper
    {
        // --- Configuration ---
        private static readonly string[] SqlServices = new[]
        {
            "MsDtsServer160", "MSSQLFDLauncher", "MSSQLSERVER", "MSSQLServerOLAPService",
            "SQLBrowser", "SQLSERVERAGENT", "SQLTELEMETRY", "SQLWriter",
            "SSASTELEMETRY", "SSISTELEMETRY160"
        };
        private const string MemuraiServiceName = "Memurai";
        private const string DefaultMemuraiInstallPath = @"C:\Program Files\Memurai";
        private const string AdminRelaunchArgument = "--run-service-manager";
        private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Ensures all configured SQL and Redis services are running and healthy.
        /// If not an administrator, it will trigger a UAC prompt to self-elevate.
        /// </summary>
        /// <returns>True if the process should continue, false if it is relaunching and should exit.</returns>
        public static bool EnsureAllServicesRunningAndHealthy()
        {
            if (!IsAdministrator())
            {
                return RelaunchAsAdmin();
            }

            WriteInfo("--- Starting Service Health Check (Running as Administrator) ---");
            EnsureSqlServicesRunning();
            ManageMemuraiRedisService();
            WriteInfo("--- Service Health Check Complete ---");
            return true;
        }

        private static void EnsureSqlServicesRunning()
        {
            WriteInfo("\n--- Checking SQL Services ---");
            var allSystemServices = ServiceController.GetServices().ToDictionary(s => s.ServiceName, StringComparer.OrdinalIgnoreCase);

            Parallel.ForEach(SqlServices, serviceName =>
            {
                if (!allSystemServices.TryGetValue(serviceName, out var service)) return;

                try
                {
                    service.Refresh();
                    if (service.Status == ServiceControllerStatus.Stopped)
                    {
                        WriteInfo($"[STARTING] SQL Service '{serviceName}' is stopped...");
                        StartService(serviceName);
                    }
                    else
                    {
                        WriteSuccess($"[OK] SQL Service '{serviceName}' is already in state: {service.Status}");
                    }
                }
                catch (Exception ex)
                {
                    WriteError($"[ERROR] Failed to manage SQL Service '{serviceName}': {ex.Message}");
                }
                finally
                {
                    service?.Dispose();
                }
            });
        }

        private static void ManageMemuraiRedisService()
        {
            WriteInfo("\n--- Checking Memurai Redis Service ---");
            if (!ServiceExists(MemuraiServiceName))
            {
                WriteWarning($"[NOT FOUND] Memurai Redis service '{MemuraiServiceName}' not found. Please ensure it is installed.");
                return;
            }

            try
            {
                bool isPathCorrect = IsMemuraiExecutablePathCorrect();
                if (!isPathCorrect)
                {
                    WriteError($"[CONFIG ERROR] Memurai Redis service '{MemuraiServiceName}' executable path is incorrect. Expected in '{DefaultMemuraiInstallPath}'.");
                    WriteWarning("Attempting a restart, but manual intervention may be required to fix the installation.");
                    RestartMemuraiRedisService(); // Attempt to fix, will start if stopped.
                    return; // Exit after restart attempt.
                }

                // If path is correct, check status.
                using var service = new ServiceController(MemuraiServiceName);
                service.Refresh();

                switch (service.Status)
                {
                    case ServiceControllerStatus.Running:
                        WriteSuccess($"[OK] Memurai Redis service '{MemuraiServiceName}' is already running and path is correct.");
                        break;
                    case ServiceControllerStatus.Stopped:
                        WriteInfo($"[STARTING] Memurai Redis service '{MemuraiServiceName}' is stopped. Attempting to start...");
                        StartService(MemuraiServiceName);
                        break;
                    default:
                        WriteWarning($"[STATE] Memurai Redis service '{MemuraiServiceName}' is in an unknown state: {service.Status}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteError($"[ERROR] Failed to manage Memurai Redis service '{MemuraiServiceName}': {ex.Message}");
            }
        }

        public static void RestartMemuraiRedisService()
        {
            if (!IsAdministrator())
            {
                WriteError("Cannot restart service without Administrator privileges.");
                return;
            }

            WriteInfo($"[RESTARTING] Attempting to restart Memurai Redis service '{MemuraiServiceName}'...");
            if (!ServiceExists(MemuraiServiceName))
            {
                WriteError($"[NOT FOUND] Memurai Redis service '{MemuraiServiceName}' not found. Cannot restart.");
                return;
            }

            try
            {
                using var service = new ServiceController(MemuraiServiceName);
                if (service.CanStop && service.Status == ServiceControllerStatus.Running)
                {
                    StopService(MemuraiServiceName);
                }
                StartService(MemuraiServiceName);
                WriteSuccess($"[SUCCESS] Memurai Redis service '{MemuraiServiceName}' restart completed.");
            }
            catch (Exception ex)
            {
                WriteError($"[ERROR] Failed to restart Memurai Redis service '{MemuraiServiceName}': {ex.Message}");
            }
        }

        private static bool IsMemuraiExecutablePathCorrect()
        {
            try
            {
                string query = $"SELECT PathName FROM Win32_Service WHERE Name = '{MemuraiServiceName}'";
                using var searcher = new ManagementObjectSearcher(query);
                ManagementObject? managementObject = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

                if (managementObject == null) return false;

                string? pathName = managementObject["PathName"]?.ToString();
                if (string.IsNullOrEmpty(pathName)) return false;

                string? executableDirectory = Path.GetDirectoryName(pathName.Trim('"'));
                return !string.IsNullOrEmpty(executableDirectory) &&
                       executableDirectory.StartsWith(DefaultMemuraiInstallPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                WriteError($"[ERROR] Exception during Memurai path verification: {ex.Message}");
                return false;
            }
        }

        #region --- Core Service & System Helpers ---

        private static bool RelaunchAsAdmin()
        {
            WriteWarning("Administrator privileges are required. Attempting to relaunch with elevation...");
            try
            {
                var exeName = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exeName)) throw new InvalidOperationException("Could not determine application path.");

                var startInfo = new ProcessStartInfo(exeName)
                {
                    Arguments = AdminRelaunchArgument,
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                WriteError("UAC prompt was canceled. The operation cannot proceed without admin rights.");
            }
            catch (Exception ex)
            {
                WriteError($"Failed to restart with admin privileges: {ex.Message}");
            }
            return false; // Signal to the caller that the process is exiting.
        }

        private static bool ServiceExists(string serviceName) =>
            ServiceController.GetServices().Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        private static void StartService(string serviceName)
        {
            using var service = new ServiceController(serviceName);
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout);
            WriteSuccess($"[SUCCESS] Service '{serviceName}' started.");
        }

        private static void StopService(string serviceName)
        {
            using var service = new ServiceController(serviceName);
            if (service.CanStop)
            {
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout);
                WriteInfo($"[SUCCESS] Service '{serviceName}' stopped.");
            }
        }

        private static bool IsAdministrator() =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        #endregion

        #region --- Console Logging Helpers ---
        private static void WriteSuccess(string message) => WriteInColor(message, ConsoleColor.Green);
        private static void WriteInfo(string message) => WriteInColor(message, ConsoleColor.White);
        private static void WriteWarning(string message) => WriteInColor(message, ConsoleColor.Yellow);
        private static void WriteError(string message) => WriteInColor(message, ConsoleColor.Red);
        private static void WriteInColor(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        #endregion

        #region --- Obsolete Methods (Removed) ---
        // GetServiceStateUsingSC, StartMultipleServicesUsingNetStart, GetServiceStatus have been removed as
        // their functionality is now handled by more reliable, modern methods above.
        #endregion
    }
}