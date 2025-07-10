using Domain.Entities;

namespace Application.Common.Interfaces
{
    public interface IAiApiConfigurationRepository
    {
        Task<AiApiConfiguration?> GetByProviderAndStatusAsync(string providerName, bool isEnabled, CancellationToken cancellationToken = default);
        Task<AiApiConfiguration?> GetByProviderNameAsync(string providerName, CancellationToken cancellationToken = default);
        Task<IEnumerable<AiApiConfiguration>> GetAllEnabledAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<AiApiConfiguration>> GetAllByProviderAndStatusAsync(string providerName, bool isEnabled, CancellationToken cancellationToken = default);
        Task<IEnumerable<AiApiConfiguration>> GetAllByProviderAndStatusAndKeyNameAsync(string providerName, bool isEnabled, string? apiKeyName = null, CancellationToken cancellationToken = default);
        Task<AiApiConfiguration> AddAsync(AiApiConfiguration configuration, CancellationToken cancellationToken = default);
        Task UpdateAsync(AiApiConfiguration configuration, CancellationToken cancellationToken = default);
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string providerName, CancellationToken cancellationToken = default);
    }
}