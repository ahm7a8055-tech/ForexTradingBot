using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProMonitoringLogController : ControllerBase
    {
        private readonly IProMonitoringLogRepository _repo;
        public ProMonitoringLogController(IProMonitoringLogRepository repo)
        {
            _repo = repo;
        }

        #region Create
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProMonitoringLog log, CancellationToken cancellationToken)
        {
            await _repo.AddAsync(log, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = log.Id }, log);
        }
        #endregion

        #region Read
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            ProMonitoringLog? log = await _repo.GetByIdAsync(id, cancellationToken);
            return log == null ? NotFound() : Ok(log);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            List<ProMonitoringLog> logs = await _repo.GetAllAsync(cancellationToken);
            return Ok(logs);
        }
        #endregion

        #region Update
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ProMonitoringLog log, CancellationToken cancellationToken)
        {
            if (id != log.Id)
            {
                return BadRequest();
            }

            await _repo.UpdateAsync(log, cancellationToken);
            return NoContent();
        }
        #endregion

        #region Delete
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _repo.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        #endregion
    }
}