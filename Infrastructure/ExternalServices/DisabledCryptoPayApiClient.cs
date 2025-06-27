// File: Infrastructure/ExternalServices/DisabledCryptoPayApiClient.cs

using Application.Common.Interfaces;
using Application.DTOs.CryptoPay;
using Microsoft.Extensions.Logging;
using Shared.Results;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.ExternalServices
{
    /// <summary>
    /// A "Null Object" implementation of the CryptoPay API client.
    /// This is used when the CryptoPay feature is disabled. Its methods do nothing
    /// except log a warning and return a safe, empty/failed result, preventing the app from crashing.
    /// </summary>
    public class DisabledCryptoPayApiClient : ICryptoPayApiClient
    {
        private readonly ILogger<DisabledCryptoPayApiClient> _logger;

        public DisabledCryptoPayApiClient(ILogger<DisabledCryptoPayApiClient> logger)
        {
            _logger = logger;
            _logger.LogWarning("CryptoPay feature is DISABLED. The DisabledCryptoPayApiClient is active. All payment operations will be skipped.");
        }

        public Task<Result<CryptoPayInvoiceDto>> CreateInvoiceAsync(CreateCryptoPayInvoiceRequestDto request, CancellationToken cancellationToken = default)
        {
            const string errorMessage = "Cannot create invoice: CryptoPay feature is disabled.";
            _logger.LogWarning(errorMessage);
            return Task.FromResult(Result<CryptoPayInvoiceDto>.Failure(errorMessage));
        }

        public Task<Result<IEnumerable<CryptoPayInvoiceDto>>> GetInvoicesAsync(GetCryptoPayInvoicesRequestDto? request = null, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("GetInvoicesAsync was called, but the CryptoPay feature is disabled. Skipping operation.");
            // A success result with an empty list is a safe default for collection-based methods.
            return Task.FromResult(Result<IEnumerable<CryptoPayInvoiceDto>>.Success(Enumerable.Empty<CryptoPayInvoiceDto>()));
        }

        public Task<Result<CryptoPayAppInfoDto>> GetMeAsync(CancellationToken cancellationToken = default)
        {
            const string errorMessage = "Cannot get app info: CryptoPay feature is disabled.";
            _logger.LogWarning(errorMessage);
            return Task.FromResult(Result<CryptoPayAppInfoDto>.Failure(errorMessage));
        }

        public Task<Result<IEnumerable<CryptoPayBalanceDto>>> GetBalanceAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("GetBalanceAsync was called, but the CryptoPay feature is disabled. Skipping operation.");
            return Task.FromResult(Result<IEnumerable<CryptoPayBalanceDto>>.Success(Enumerable.Empty<CryptoPayBalanceDto>()));
        }
    }
}