using Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IProMonitoringLogRepository
    {
        Task AddAsync(ProMonitoringLog log, CancellationToken cancellationToken = default);
    }
} 