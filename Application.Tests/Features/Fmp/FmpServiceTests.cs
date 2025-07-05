using Application.Common.Interfaces; // For IFmpApiClient
using Application.DTOs.Fmp;
using Application.Features.Fmp.Interfaces;
using Application.Features.Fmp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Results;

namespace Application.Tests.Features.Fmp
{
    public class FmpServiceTests
    {
        private readonly Mock<IFmpApiClient> _mockApiClient;
        private readonly Mock<ILogger<FmpService>> _mockLogger;
        private readonly FmpService _fmpService;

        public FmpServiceTests()
        {
            _mockApiClient = new Mock<IFmpApiClient>();
            _mockLogger = new Mock<ILogger<FmpService>>();
            _fmpService = new FmpService(_mockApiClient.Object, _mockLogger.Object);
        }

        private List<FmpQuoteDto> CreateSampleQuotes()
        {
            return new List<FmpQuoteDto>
            {
                new FmpQuoteDto { Symbol = "BTCUSD", Name = "Bitcoin", MarketCap = 1000, Price = 50000 },
                new FmpQuoteDto { Symbol = "ETHUSD", Name = "Ethereum", MarketCap = 500, Price = 3000 },
                new FmpQuoteDto { Symbol = "ADAUSD", Name = "Cardano", MarketCap = 300, Price = 1.5m },
                new FmpQuoteDto { Symbol = "SOLUSD", Name = "Solana", MarketCap = 400, Price = 150 },
                new FmpQuoteDto { Symbol = "XRPUSD", Name = "Ripple", MarketCap = null, Price = 0.5m }, // Null MarketCap
                new FmpQuoteDto { Symbol = "DOGEUSD", Name = "Dogecoin", MarketCap = 0, Price = 0.1m }, // Zero MarketCap
                new FmpQuoteDto { Symbol = "SHIBUSD", Name = null, MarketCap = 50, Price = 0.00001m } // Null Name
            };
        }

        [Fact]
        public async Task GetTopCryptosAsync_ReturnsFailure_WhenApiClientFails()
        {
            // Arrange
            var errors = new List<string> { "API Error" };
            _mockApiClient.Setup(c => c.GetFullCryptoQuoteListAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync(Result<List<FmpQuoteDto>>.Failure(errors));

            // Act
            var result = await _fmpService.GetTopCryptosAsync(5, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal(errors, result.Errors);
        }

        [Fact]
        public async Task GetTopCryptosAsync_ReturnsFailure_WhenApiClientReturnsNullData()
        {
            // Arrange
            _mockApiClient.Setup(c => c.GetFullCryptoQuoteListAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync(Result<List<FmpQuoteDto>>.Success(null!)); // Null data

            // Act
            var result = await _fmpService.GetTopCryptosAsync(5, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded); // Should propagate as failure if data is null
        }

        [Fact]
        public async Task GetTopCryptosAsync_ReturnsEmptyList_WhenApiClientReturnsEmptyData()
        {
            // Arrange
            _mockApiClient.Setup(c => c.GetFullCryptoQuoteListAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync(Result<List<FmpQuoteDto>>.Success(new List<FmpQuoteDto>()));

            // Act
            var result = await _fmpService.GetTopCryptosAsync(5, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data);
        }


        [Fact]
        public async Task GetTopCryptosAsync_FiltersSortsAndTakesCorrectCount()
        {
            // Arrange
            var allQuotes = CreateSampleQuotes();
            _mockApiClient.Setup(c => c.GetFullCryptoQuoteListAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync(Result<List<FmpQuoteDto>>.Success(allQuotes));
            var expectedCount = 2;

            // Expected valid items: BTCUSD (1000), ETHUSD (500), SOLUSD (400), ADAUSD (300)
            // Sorted: BTCUSD, ETHUSD, SOLUSD, ADAUSD
            // Taken 2: BTCUSD, ETHUSD

            // Act
            var result = await _fmpService.GetTopCryptosAsync(expectedCount, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Equal(expectedCount, result.Data.Count);
            Assert.Equal("BTCUSD", result.Data[0].Symbol); // Highest market cap
            Assert.Equal("ETHUSD", result.Data[1].Symbol); // Second highest
        }

        [Fact]
        public async Task GetTopCryptosAsync_ReturnsFewerThanCount_IfNotEnoughValidItems()
        {
            // Arrange
            var limitedQuotes = new List<FmpQuoteDto>
            {
                new FmpQuoteDto { Symbol = "BTCUSD", Name = "Bitcoin", MarketCap = 1000, Price = 50000 },
                new FmpQuoteDto { Symbol = "NMCUSD", Name = "NoMarketCapCoin", Price = 10 } // Only one valid by MarketCap
            };
            _mockApiClient.Setup(c => c.GetFullCryptoQuoteListAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync(Result<List<FmpQuoteDto>>.Success(limitedQuotes));

            // Act
            var result = await _fmpService.GetTopCryptosAsync(5, CancellationToken.None); // Request 5

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data); // But only 1 is valid and returned
            Assert.Equal("BTCUSD", result.Data[0].Symbol);
        }

        [Fact]
        public async Task GetCryptoDetailsAsync_ReturnsSuccess_WhenApiClientSucceeds()
        {
            // Arrange
            var fmpSymbol = "BTCUSD";
            var quoteDto = new FmpQuoteDto { Symbol = fmpSymbol, Name = "Bitcoin", Price = 50000 };
            _mockApiClient.Setup(c => c.GetFullCryptoQuoteAsync(fmpSymbol, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(Result<FmpQuoteDto>.Success(quoteDto));

            // Act
            var result = await _fmpService.GetCryptoDetailsAsync(fmpSymbol, CancellationToken.None);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Equal(fmpSymbol, result.Data.Symbol);
        }

        [Fact]
        public async Task GetCryptoDetailsAsync_ReturnsFailure_WhenApiClientFails()
        {
            // Arrange
            var fmpSymbol = "UNKNOWN";
            var errors = new List<string> { "Not found" };
            _mockApiClient.Setup(c => c.GetFullCryptoQuoteAsync(fmpSymbol, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(Result<FmpQuoteDto>.Failure(errors));

            // Act
            var result = await _fmpService.GetCryptoDetailsAsync(fmpSymbol, CancellationToken.None);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Equal(errors, result.Errors);
        }
    }
}
