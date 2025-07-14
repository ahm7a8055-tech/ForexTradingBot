using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class ProMonitoringLogRepository : IProMonitoringLogRepository
    {
        private readonly AppDbContext _db;
        public ProMonitoringLogRepository(AppDbContext db) => _db = db;
        public async Task AddAsync(ProMonitoringLog log, CancellationToken cancellationToken = default)
        {
            _db.ProMonitoringLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
} 