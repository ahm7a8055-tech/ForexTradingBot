using Domain.Entities;

namespace Application.Common.Interfaces
{
    public interface IProMonitoringLogRepository
    {
        #region Create
        Task AddAsync(ProMonitoringLog log, CancellationToken cancellationToken = default);
        #endregion

        #region Read
        Task<ProMonitoringLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<ProMonitoringLog>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<ProMonitoringLog>> GetRecentPagedAsync(int limit, int offset, CancellationToken cancellationToken = default);
        #endregion

        #region Update
        Task UpdateAsync(ProMonitoringLog log, CancellationToken cancellationToken = default);
        #endregion

        #region Delete
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
        #endregion
    }
}