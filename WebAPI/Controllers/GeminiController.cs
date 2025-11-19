using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/gemini")]
    [Authorize]
    public class GeminiController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly ILogger<GeminiController> _logger;

        public GeminiController(IGeminiService geminiService, ILogger<GeminiController> logger)
        {
            _geminiService = geminiService;
            _logger = logger;
        }

        #region Request Models
        public class EnhanceMessageRequest
        {
            public string Text { get; set; } = string.Empty;
            public string? ApiKeyName { get; set; }
        }

        public class BatchEnhanceRequest
        {
            public List<string> Texts { get; set; } = [];
            public string? ApiKeyName { get; set; }
        }

        public class JobResultResponse
        {
            public string JobId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Result { get; set; }
            public DateTime Timestamp { get; set; }
        }
        #endregion

        #region API Endpoints

        /// <summary>
        /// Enqueue a message enhancement job
        /// </summary>
        [HttpPost("enhance")]
        [ProducesResponseType(typeof(JobResultResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EnhanceMessage([FromBody] EnhanceMessageRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { Message = "Text is required" });
            }

            try
            {
                string? jobId = await _geminiService.EnhanceMessageAsync(request.Text, ct, request.ApiKeyName);

                _logger.LogInformation("Message enhancement job enqueued. JobId: {JobId}", jobId);

                JobResultResponse response = new()
                {
                    JobId = jobId ?? "UNKNOWN",
                    Status = "ENQUEUED",
                    Timestamp = DateTime.UtcNow
                };

                return Accepted(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue enhancement job");
                return StatusCode(500, new { Message = "Failed to enqueue job", Error = ex.Message });
            }
        }

        /// <summary>
        /// Get the result of a background job
        /// </summary>
        [HttpGet("job/{jobId}")]
        [ProducesResponseType(typeof(JobResultResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetJobResult(string jobId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new { Message = "Job ID is required" });
            }

            try
            {
                string? result = await _geminiService.GetJobResultAsync(jobId, ct);

                if (result == "JOB_NOT_FOUND")
                {
                    return NotFound(new { Message = "Job not found", JobId = jobId });
                }

                JobResultResponse response = new()
                {
                    JobId = jobId,
                    Status = result == "JOB_RUNNING" ? "RUNNING" : "COMPLETED",
                    Result = result == "JOB_RUNNING" ? null : result,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                string sanitizedJobId = jobId.Replace("\n", "").Replace("\r", "");
                _logger.LogError(ex, "Failed to get job result for JobId: {JobId}", sanitizedJobId);
                return StatusCode(500, new { Message = "Failed to get job result", Error = ex.Message });
            }
        }

        /// <summary>
        /// Enqueue multiple message enhancement jobs
        /// </summary>
        [HttpPost("enhance/batch")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EnhanceMessagesBatch([FromBody] BatchEnhanceRequest request, CancellationToken ct)
        {
            if (request.Texts == null || !request.Texts.Any())
            {
                return BadRequest(new { Message = "At least one text is required" });
            }

            if (request.Texts.Count > 100) // Limit batch size
            {
                return BadRequest(new { Message = "Batch size cannot exceed 100 items" });
            }

            try
            {
                List<string> jobIds = await _geminiService.EnhanceMessagesBatchAsync(request.Texts, ct, request.ApiKeyName);

                _logger.LogInformation("Batch enhancement jobs enqueued. Count: {Count}, JobIds: {JobIds}",
                    jobIds.Count, string.Join(", ", jobIds));

                return Accepted(new
                {
                    Message = "Batch jobs enqueued successfully",
                    JobCount = jobIds.Count,
                    JobIds = jobIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue batch enhancement jobs");
                return StatusCode(500, new { Message = "Failed to enqueue batch jobs", Error = ex.Message });
            }
        }

        /// <summary>
        /// Get status of multiple jobs
        /// </summary>
        [HttpPost("jobs/status")]
        [ProducesResponseType(typeof(List<JobResultResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetJobsStatus([FromBody] List<string> jobIds, CancellationToken ct)
        {
            if (jobIds == null || !jobIds.Any())
            {
                return BadRequest(new { Message = "At least one job ID is required" });
            }

            if (jobIds.Count > 50) // Limit status check size
            {
                return BadRequest(new { Message = "Status check cannot exceed 50 jobs" });
            }

            try
            {
                List<JobResultResponse> results = [];

                foreach (string jobId in jobIds)
                {
                    string? result = await _geminiService.GetJobResultAsync(jobId, ct);

                    results.Add(new JobResultResponse
                    {
                        JobId = jobId,
                        Status = result switch
                        {
                            "JOB_NOT_FOUND" => "NOT_FOUND",
                            "JOB_RUNNING" => "RUNNING",
                            _ => "COMPLETED"
                        },
                        Result = result is "JOB_RUNNING" or "JOB_NOT_FOUND" ? null : result,
                        Timestamp = DateTime.UtcNow
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get jobs status");
                return StatusCode(500, new { Message = "Failed to get jobs status", Error = ex.Message });
            }
        }

        #endregion
    }
}