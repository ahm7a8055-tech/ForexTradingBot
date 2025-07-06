using Application.DTOs.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Application.Interfaces
{
    public interface IDiagnosticsService
    {
        Task<ConnectivityStatusDto> CheckConnectivityAsync(CancellationToken cancellationToken = default);
    }
}
