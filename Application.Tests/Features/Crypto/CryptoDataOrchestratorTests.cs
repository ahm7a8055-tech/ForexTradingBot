using Application.Common.Interfaces;
using Application.DTOs.CoinGecko;
using Application.DTOs.Crypto.Dtos;
using Application.DTOs.Fmp;
using Application.Features.Crypto.Interfaces;
using Application.Features.Crypto.Services;
using Application.Features.Fmp.Interfaces; // Assuming IFmpService is here
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Results;

namespace Application.Tests.Features.Crypto
{
    public class CryptoDataOrchestratorTests
    {
        private readonly Mock<ICoinGeckoService> _mockCoinGeckoService;
        private readonly Mock<IFmpService> _mockFmpService;
        private readonly Mock<ILogger<CryptoDataOrchestrator>> _mockLogger;
        private readonly Mock<IMemoryCacheService<object>> _mockCache;
        private readonly Mock<ICryptoSymbolMapper> _mockSymbolMapper;
        private readonly CryptoDataOrchestrator _orchestrator;

        public CryptoDataOrchestratorTests()
        {
            _mockCoinGeckoService = new Mock<ICoinGeckoService>();
            _mockFmpService = new Mock<IFmpService>();
            _mockLogger = new Mock<ILogger<CryptoDataOrchestrator>>();
            _mockCache = new Mock<IMemoryCacheService<object>>();
            _mockSymbolMapper = new Mock<ICryptoSymbolMapper>();

            _orchestrator = new CryptoDataOrchestrator(
                _mockCoinGeckoService.Object,
                _mockFmpService.Object,
                _mockLogger.Object,
                _mockCache.Object,
                _mockSymbolMapper.Object
            );
        }

        // Helper to setup cache TryGetValue
        private void SetupCacheTryGetValue<T>(string cacheKey, T? value, bool returnsValue) where T : class
        {
            object? outValue = value;
            _mockCache.Setup(c => c.TryGetValue(cacheKey, out outValue))
                      .Returns(returnsValue);
        }

        [Fact]
        public async Task GetCryptoListAsync_ReturnsFromCache_IfAvailable()
        {
            // Arrange
            var page = 1;
            var perPage = 10;
            var cacheKey = $"CryptoList_Page{page}";
            var cachedList = new List<UnifiedCryptoDto> { new UnifiedCryptoDto { Id = "bitcoin", Name = "Bitcoin" } };
            SetupCacheTryGetValue(cacheKey, cachedList, true);

            // Act
            var result = await _orchestrator.GetCryptoListAsync(page, perPage, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Same(cachedList, result.Data);
            _mockCoinGeckoService.Verify(s => s.GetCoinMarketsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCryptoListAsync_FetchesFromCoinGecko_AndCaches_IfNotInCache_AndCoinGeckoSucceeds()
        {
            // Arrange
            var page = 1;
            var perPage = 10;
            var cacheKey = $"CryptoList_Page{page}";
            var coinGeckoData = new List<CoinMarketDto>
            {
                new CoinMarketDto { Id = "bitcoin", Symbol = "btc", Name = "Bitcoin", CurrentPrice = 50000 }
            };
            SetupCacheTryGetValue<List<UnifiedCryptoDto>>(cacheKey, null, false);
            _mockCoinGeckoService.Setup(s => s.GetCoinMarketsAsync(page, perPage, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<List<CoinMarketDto>>.Success(coinGeckoData));

            // Act
            var result = await _orchestrator.GetCryptoListAsync(page, perPage, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal("bitcoin", result.Data[0].Id);
            _mockCache.Verify(c => c.Set(cacheKey, It.IsAny<List<UnifiedCryptoDto>>(), TimeSpan.FromMinutes(2)), Times.Once);
            _mockFmpService.Verify(s => s.GetTopCryptosAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetCryptoListAsync_FetchesFromFmp_AndCaches_IfCoinGeckoFails_AndFmpSucceeds()
        {
            // Arrange
            var page = 1;
            var perPage = 10;
            var cacheKey = $"CryptoList_Page{page}";
            var fmpData = new List<FmpQuoteDto>
            {
                new FmpQuoteDto { Symbol = "BTCUSD", Name = "Bitcoin USD", Price = 51000 }
            };
            SetupCacheTryGetValue<List<UnifiedCryptoDto>>(cacheKey, null, false);
            _mockCoinGeckoService.Setup(s => s.GetCoinMarketsAsync(page, perPage, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<List<CoinMarketDto>>.Failure("CoinGecko down"));
            _mockFmpService.Setup(s => s.GetTopCryptosAsync(20, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Result<List<FmpQuoteDto>>.Success(fmpData));

            // Act
            var result = await _orchestrator.GetCryptoListAsync(page, perPage, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal("BTCUSD", result.Data[0].Id); // FMP DTO uses Symbol as Id
            Assert.Equal("FMP", result.Data[0].PriceDataSource);
            _mockCache.Verify(c => c.Set(cacheKey, It.IsAny<List<UnifiedCryptoDto>>(), TimeSpan.FromMinutes(2)), Times.Once);
        }

        [Fact]
        public async Task GetCryptoListAsync_ReturnsFailure_IfBothCoinGeckoAndFmpFail()
        {
            // Arrange
            var page = 1;
            var perPage = 10;
            var cacheKey = $"CryptoList_Page{page}";
            SetupCacheTryGetValue<List<UnifiedCryptoDto>>(cacheKey, null, false);
            _mockCoinGeckoService.Setup(s => s.GetCoinMarketsAsync(page, perPage, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<List<CoinMarketDto>>.Failure("CoinGecko down"));
            _mockFmpService.Setup(s => s.GetTopCryptosAsync(20, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Result<List<FmpQuoteDto>>.Failure("FMP down"));

            // Act
            var result = await _orchestrator.GetCryptoListAsync(page, perPage, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("CoinGecko down", result.Errors);
            Assert.Contains("FMP down", result.Errors);
        }


        [Fact]
        public async Task GetCryptoDetailsAsync_ReturnsFromCache_IfAvailable()
        {
            // Arrange
            var coinSymbol = "btc";
            var cacheKey = $"CryptoDetails_Unified_{coinSymbol}";
            var cachedDetails = new UnifiedCryptoDto { Id = coinSymbol, Name = "Bitcoin (cached)" };
            SetupCacheTryGetValue(cacheKey, cachedDetails, true);

            // Act
            var result = await _orchestrator.GetCryptoDetailsAsync(coinSymbol, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.Same(cachedDetails, result.Data);
            _mockSymbolMapper.Verify(m => m.GetCoinGeckoId(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetCryptoDetailsAsync_FetchesAndMerges_WhenNotInCache_CoinGeckoSucceeds()
        {
            // Arrange
            var coinSymbol = "btc";
            var coinGeckoId = "bitcoin";
            var fmpSymbol = "BTCUSD";
            var cacheKey = $"CryptoDetails_Unified_{coinSymbol}";

            SetupCacheTryGetValue<UnifiedCryptoDto>(cacheKey, null, false);
            _mockSymbolMapper.Setup(m => m.GetCoinGeckoId(coinSymbol)).Returns(coinGeckoId);
            _mockSymbolMapper.Setup(m => m.GetFmpSymbol(coinSymbol)).Returns(fmpSymbol);

            var coinGeckoDetails = new CoinDetailsDto { Id = coinGeckoId, Symbol = "btc", Name = "Bitcoin CG", Description = new Dictionary<string, string>{{"en", "CG Desc"}}, MarketData = new MarketDataDto { CurrentPrice = new Dictionary<string, double?>{{"usd", 50000}}, PriceChangePercentage24h = 1.5 } };
            _mockCoinGeckoService.Setup(s => s.GetCryptoDetailsAsync(coinGeckoId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<CoinDetailsDto>.Success(coinGeckoDetails));
            // FMP can succeed or fail, CG data should be prioritized
            _mockFmpService.Setup(s => s.GetCryptoDetailsAsync(fmpSymbol, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Result<FmpQuoteDto>.Failure("FMP no data for this test"));


            // Act
            var result = await _orchestrator.GetCryptoDetailsAsync(coinSymbol, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Equal(coinSymbol, result.Data.Id); // Should use original symbol for ID
            Assert.Equal("Bitcoin CG", result.Data.Name);
            Assert.Equal(50000, result.Data.Price);
            Assert.Equal("CoinGecko", result.Data.PriceDataSource);
            _mockCache.Verify(c => c.Set(cacheKey, It.IsAny<UnifiedCryptoDto>(), TimeSpan.FromMinutes(5)), Times.Once);
        }

        [Fact]
        public async Task GetCryptoDetailsAsync_FetchesAndMerges_WhenNotInCache_FmpSucceeds_CoinGeckoFailsAfterMapping()
        {
            // Arrange
            var coinSymbol = "eth";
            var coinGeckoId = "ethereum"; // Mapper gives ID
            var fmpSymbol = "ETHUSD";
            var cacheKey = $"CryptoDetails_Unified_{coinSymbol}";

            SetupCacheTryGetValue<UnifiedCryptoDto>(cacheKey, null, false);
            _mockSymbolMapper.Setup(m => m.GetCoinGeckoId(coinSymbol)).Returns(coinGeckoId);
            _mockSymbolMapper.Setup(m => m.GetFmpSymbol(coinSymbol)).Returns(fmpSymbol);

            _mockCoinGeckoService.Setup(s => s.GetCryptoDetailsAsync(coinGeckoId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<CoinDetailsDto>.Failure("CG API Error")); // CG fetch fails
            var fmpDetails = new FmpQuoteDto { Symbol = fmpSymbol, Name = "Ethereum FMP", Price = 3000, ChangesPercentage = -0.5m };
            _mockFmpService.Setup(s => s.GetCryptoDetailsAsync(fmpSymbol, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Result<FmpQuoteDto>.Success(fmpDetails));

            // Act
            var result = await _orchestrator.GetCryptoDetailsAsync(coinSymbol, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Equal(coinSymbol, result.Data.Id);
            Assert.Equal("Ethereum FMP", result.Data.Name);
            Assert.Equal(3000, result.Data.Price);
            Assert.Equal("FMP", result.Data.PriceDataSource);
            _mockCache.Verify(c => c.Set(cacheKey, It.IsAny<UnifiedCryptoDto>(), TimeSpan.FromMinutes(5)), Times.Once);
        }


        [Fact]
        public async Task GetCryptoDetailsAsync_ReturnsFailure_IfCoinGeckoMappingFails_AndFmpFails()
        {
            // Arrange
            var coinSymbol = "unknown";
            var cacheKey = $"CryptoDetails_Unified_{coinSymbol}";

            SetupCacheTryGetValue<UnifiedCryptoDto>(cacheKey, null, false);
            _mockSymbolMapper.Setup(m => m.GetCoinGeckoId(coinSymbol)).Returns((string?)null); // CG Mapping fails (returns null)
            _mockSymbolMapper.Setup(m => m.GetFmpSymbol(coinSymbol)).Returns("UNKNOWNFMP"); // FMP might have a symbol
            _mockFmpService.Setup(s => s.GetCryptoDetailsAsync("UNKNOWNFMP", It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Result<FmpQuoteDto>.Failure("FMP also failed")); // FMP fetch fails

            // Act
            var result = await _orchestrator.GetCryptoDetailsAsync(coinSymbol, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("No CoinGecko mapping available", result.Errors.First()); // From the orchestrator's own error message
        }

        [Fact]
        public async Task GetCryptoDetailsAsync_ReturnsFailure_IfBothServicesFail()
        {
            // Arrange
            var coinSymbol = "failcoin";
            var coinGeckoId = "failcg";
            var fmpSymbol = "FAILFMP";
            var cacheKey = $"CryptoDetails_Unified_{coinSymbol}";

            SetupCacheTryGetValue<UnifiedCryptoDto>(cacheKey, null, false);
            _mockSymbolMapper.Setup(m => m.GetCoinGeckoId(coinSymbol)).Returns(coinGeckoId);
            _mockSymbolMapper.Setup(m => m.GetFmpSymbol(coinSymbol)).Returns(fmpSymbol);

            _mockCoinGeckoService.Setup(s => s.GetCryptoDetailsAsync(coinGeckoId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<CoinDetailsDto>.Failure("CG Down"));
            _mockFmpService.Setup(s => s.GetCryptoDetailsAsync(fmpSymbol, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(Result<FmpQuoteDto>.Failure("FMP Down"));

            // Act
            var result = await _orchestrator.GetCryptoDetailsAsync(coinSymbol, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Could not retrieve data", result.Errors.First());
        }
    }
}
