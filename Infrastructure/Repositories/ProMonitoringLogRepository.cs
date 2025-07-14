using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class ProMonitoringLogRepository : IProMonitoringLogRepository
    {
        private readonly AppDbContext _db;
        public ProMonitoringLogRepository(AppDbContext db) => _db = db;

        #region Create
        public async Task AddAsync(ProMonitoringLog log, CancellationToken cancellationToken = default)
        {
            _db.ProMonitoringLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);
        }
        #endregion

        #region Read
        public async Task<ProMonitoringLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _db.ProMonitoringLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<List<ProMonitoringLog>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _db.ProMonitoringLogs.AsNoTracking().ToListAsync(cancellationToken);
        }
        #endregion

        #region Update
        public async Task UpdateAsync(ProMonitoringLog log, CancellationToken cancellationToken = default)
        {
            _db.ProMonitoringLogs.Update(log);
            await _db.SaveChangesAsync(cancellationToken);
        }
        #endregion

        #region Delete
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var log = await _db.ProMonitoringLogs.FindAsync(new object[] { id }, cancellationToken);
            if (log != null)
            {
                _db.ProMonitoringLogs.Remove(log);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        #endregion
    }
} 