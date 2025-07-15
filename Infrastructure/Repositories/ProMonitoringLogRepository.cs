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

         public async Task<List<ProMonitoringLog>> GetRecentPagedAsync(int limit, int offset, CancellationToken cancellationToken = default)
    {
        // Basic validation
        if (limit <= 0) limit = 10;
        if (offset < 0) offset = 0;

        return await _db.ProMonitoringLogs
            .AsNoTracking() // Performance boost for read-only queries
            .OrderByDescending(log => log.Timestamp) // IMPORTANT: Order first!
            .Skip(offset)                             // Then, skip the records from previous pages
            .Take(limit)                              // Finally, take the records for the current page
            .ToListAsync(cancellationToken);
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

        public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            // This is the most efficient way to delete all records in a table with EF Core 7+
            return await _db.ProMonitoringLogs.ExecuteDeleteAsync(cancellationToken);
        }
        #endregion
    }
} 