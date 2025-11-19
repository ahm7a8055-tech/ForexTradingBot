using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/gemini-test")]
    [Authorize]
    public class GeminiTestController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly ILogger<GeminiTestController> _logger;

        public GeminiTestController(IGeminiService geminiService, ILogger<GeminiTestController> logger)
        {
            _geminiService = geminiService;
            _logger = logger;
        }

        #region Request Models
        public class TestEnhancementRequest
        {
            public string Message { get; set; } = string.Empty;
            public string? ApiKeyName { get; set; }
        }

        public class TestResponse
        {
            public string OriginalMessage { get; set; } = string.Empty;
            public string? EnhancedMessage { get; set; }
            public string? JobId { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
        #endregion

        /// <summary>
        /// Test message enhancement with immediate result
        /// </summary>
        [HttpPost("enhance")]
        [ProducesResponseType(typeof(TestResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TestEnhancement([FromBody] TestEnhancementRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { Message = "Message is required" });
            }

            TestResponse response = new()
            {
                OriginalMessage = request.Message,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Testing Gemini enhancement for message: {MessageLength} chars", request.Message.Length);

                string? result = await _geminiService.EnhanceMessageAsync(request.Message, ct, request.ApiKeyName);

                if (!string.IsNullOrWhiteSpace(result) && !result.StartsWith("Job enqueued"))
                {
                    // Immediate enhancement succeeded
                    response.EnhancedMessage = result;
                    response.Status = "SUCCESS_IMMEDIATE";
                    _logger.LogInformation("Immediate enhancement successful. Enhanced length: {EnhancedLength}", result?.Length ?? 0);
                }
                else if (!string.IsNullOrWhiteSpace(result) && result.StartsWith("Job enqueued"))
                {
                    // Background job was enqueued
                    string jobId = result.Replace("Job enqueued successfully. JobId: ", "");
                    response.JobId = jobId;
                    response.Status = "JOB_ENQUEUED";
                    _logger.LogInformation("Background job enqueued. JobId: {JobId}", jobId);

                    // Wait a bit and check the result
                    await Task.Delay(2000, ct); // Wait 2 seconds
                    string? jobResult = await _geminiService.GetJobResultAsync(jobId, ct);

                    if (jobResult is not "JOB_RUNNING" and not "JOB_NOT_FOUND")
                    {
                        response.EnhancedMessage = jobResult;
                        response.Status = "SUCCESS_BACKGROUND";
                    }
                    else
                    {
                        response.Status = $"BACKGROUND_{jobResult}";
                    }
                }
                else
                {
                    response.Status = "FAILED";
                    response.Error = "No result returned from service";
                }
            }
            catch (Exception ex)
            {
                response.Status = "ERROR";
                response.Error = ex.Message;
                _logger.LogError(ex, "Error during Gemini enhancement test");
            }

            return Ok(response);
        }

        /// <summary>
        /// Test with the specific GOLD trading message
        /// </summary>
        [HttpPost("test-gold-message")]
        public async Task<IActionResult> TestGoldMessage(CancellationToken ct)
        {
            string testMessage = @"GOLD SELL NOW 
3295 - 3297

SL : 3300

TP1: 3293
TP2: 3291
TP3: 3289";

            TestEnhancementRequest request = new() { Message = testMessage };
            return await TestEnhancement(request, ct);
        }

        /// <summary>
        /// Check service configuration status
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetServiceStatus(CancellationToken ct)
        {
            try
            {
                // Test with a simple message to check if service is working
                string testMessage = "Test message for service status check";
                string? result = await _geminiService.EnhanceMessageAsync(testMessage, ct);

                var status = new
                {
                    ServiceAvailable = true,
                    LastTestTime = DateTime.UtcNow,
                    TestResult = result?.StartsWith("Job enqueued") == true ? "Background Job" : "Immediate Result",
                    MessageLength = result?.Length ?? 0
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                var status = new
                {
                    ServiceAvailable = false,
                    LastTestTime = DateTime.UtcNow,
                    Error = ex.Message
                };

                return Ok(status);
            }
        }

        /// <summary>
        /// Test job result retrieval
        /// </summary>
        [HttpGet("job/{jobId}")]
        public async Task<IActionResult> GetJobResult(string jobId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest(new { Message = "Job ID is required" });
            }

            try
            {
                string? result = await _geminiService.GetJobResultAsync(jobId, ct);

                var response = new
                {
                    JobId = jobId,
                    Status = result,
                    IsCompleted = result is not "JOB_RUNNING" and not "JOB_NOT_FOUND",
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}