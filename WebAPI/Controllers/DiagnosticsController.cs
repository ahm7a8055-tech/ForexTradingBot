using Application.DTOs.Diagnostics;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization; // Required for [Authorize]
using Microsoft.AspNetCore.Mvc;
using Hangfire; // Required for JobStorage and IMonitoringApi
using Hangfire.Storage; // Required for IMonitoringApi access patterns
using Hangfire.Common; // Added for ToGenericTypeString extension method
using System.Linq; // Required for LINQ operations on collections
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/diagnostics")]
    [Authorize(Roles = "Admin")] // Changed to Admin role for all diagnostic endpoints now
    public class DiagnosticsController : ControllerBase
    {
        private readonly IDiagnosticsService _diagnosticsService;
        private readonly ILogger<DiagnosticsController> _logger;
        private readonly IMonitoringApi _hangfireMonitoringApi;

        public DiagnosticsController(
            IDiagnosticsService diagnosticsService,
            ILogger<DiagnosticsController> logger)
            // JobStorage is a static accessor, IMonitoringApi can be resolved or obtained from JobStorage.Current
            // For direct injection, you might register IMonitoringApi in Startup:
            // services.AddSingleton<IMonitoringApi>(JobStorage.Current.GetMonitoringApi());
            // However, accessing JobStorage.Current directly in controller is also common for Hangfire.
        {
            _diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hangfireMonitoringApi = JobStorage.Current.GetMonitoringApi();
        }

        [HttpGet("connectivity-status")]
        [ProducesResponseType(typeof(ConnectivityStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConnectivityStatus(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to retrieve connectivity status.");
            try
            {
                var status = await _diagnosticsService.CheckConnectivityAsync(cancellationToken);
                _logger.LogInformation(
                    "Connectivity status retrieved: DB Connected - {DbStatus}, Telegram API Connected - {TelegramStatus}",
                    status.CanConnectToDatabase,
                    status.CanAccessTelegramApi);
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving connectivity status.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while checking system connectivity.");
            }
        }

        [HttpGet("hangfire-status")]
        [ProducesResponseType(typeof(HangfireStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult GetHangfireStatus()
        {
            _logger.LogInformation("Attempting to retrieve Hangfire status.");
            try
            {
                var stats = _hangfireMonitoringApi.GetStatistics();
                var servers = _hangfireMonitoringApi.Servers();

                // Corrected way to get recurring jobs
                List<Hangfire.Storage.RecurringJobDto> hangfireRecurringJobs;
                using (var connection = JobStorage.Current.GetConnection())
                {
                    hangfireRecurringJobs = connection.GetRecurringJobs();
                }

                var queues = new List<HangfireQueueDto>();
                foreach(var queueStat in _hangfireMonitoringApi.Queues())
                {
                    queues.Add(new HangfireQueueDto
                    {
                        Name = queueStat.Name,
                        Length = queueStat.Length,
                        Fetched = queueStat.Fetched ?? 0
                    });
                }

                var dto = new HangfireStatusDto
                {
                    EnqueuedCount = stats.Enqueued,
                    ScheduledCount = stats.Scheduled,
                    ProcessingCount = stats.Processing,
                    SucceededCount = stats.Succeeded,
                    FailedCount = stats.Failed,
                    DeletedCount = stats.Deleted,
                    ServerCount = servers.Count,
                    Servers = servers.Select(s => $"Server: {s.Name}, Workers: {s.WorkersCount}, Started: {(s.StartedAt.HasValue ? s.StartedAt.Value.ToString("o") : "N/A")}").ToList(),
                    Queues = queues,
                    RecurringJobs = hangfireRecurringJobs.Select(job => new HangfireRecurringJobDto
                    {
                        Id = job.Id,
                        Cron = job.Cron,
                        Queue = job.Queue,
                        NextExecution = job.NextExecution?.ToString("o"),
                        LastExecution = job.LastExecution?.ToString("o"), // Corrected property name
                        CreatedAt = job.CreatedAt?.ToString("o"),
                        Removed = job.Removed,
                        Error = job.Error,
                        // Constructing Method string from Hangfire.Common.Job
                        Method = job.Job != null ? $"{job.Job.Type.ToGenericTypeString()}.{job.Job.Method.Name}" : "N/A"
                    }).ToList()
                };

                _logger.LogInformation("Hangfire status retrieved successfully.");
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Hangfire status.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while checking Hangfire status.");
            }
        }
    }
}
