using Application.Common.Interfaces;
using Application.DTOs.CryptoPay;
using Application.Interfaces;
using Application.Services;
using AutoMapper;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Results;
using System.Text.Json;

namespace Application.Tests
{
    public class PaymentServiceTests
    {
        private readonly Mock<ICryptoPayApiClient> _mockCryptoPayApiClient;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ISubscriptionRepository> _mockSubscriptionRepository;
        private readonly Mock<ITransactionRepository> _mockTransactionRepository;
        private readonly Mock<IAppDbContext> _mockDbContext;
        private readonly Mock<IMapper> _mockMapper; // Though not directly used by methods under test, it's a dependency
        private readonly Mock<ILogger<PaymentService>> _mockLogger;
        private readonly PaymentService _paymentService;

        // Known Plan IDs from PaymentService
        private static readonly Guid PremiumPlanId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid BestPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002");


        public PaymentServiceTests()
        {
            _mockCryptoPayApiClient = new Mock<ICryptoPayApiClient>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockSubscriptionRepository = new Mock<ISubscriptionRepository>();
            _mockTransactionRepository = new Mock<ITransactionRepository>();
            _mockDbContext = new Mock<IAppDbContext>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<PaymentService>>();

            _paymentService = new PaymentService(
                _mockCryptoPayApiClient.Object,
                _mockUserRepository.Object,
                _mockSubscriptionRepository.Object,
                _mockTransactionRepository.Object,
                _mockDbContext.Object,
                _mockMapper.Object,
                _mockLogger.Object
            );
        }

        [Theory]
        [InlineData("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000001", "USDT", 10)] // Invalid UserId
        [InlineData("10000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000", "USDT", 10)] // Invalid PlanId
        [InlineData("10000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000001", "", 10)]         // Invalid Asset
        [InlineData("10000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000001", "USDT", 0)]      // Invalid Amount
        public async Task CreateCryptoPaymentInvoiceAsync_ReturnsFailure_ForInvalidParameters(string userIdStr, string planIdStr, string asset, decimal amount)
        {
            // Arrange
            var userId = Guid.Parse(userIdStr);
            var planId = Guid.Parse(planIdStr);

            // Act
            var result = await _paymentService.CreateCryptoPaymentInvoiceAsync(userId, planId, asset, amount);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Invalid parameters", result.Errors.First());
        }

        [Fact]
        public async Task CreateCryptoPaymentInvoiceAsync_ReturnsFailure_WhenUserNotFound()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            // Act
            var result = await _paymentService.CreateCryptoPaymentInvoiceAsync(userId, PremiumPlanId, "USDT", 10m);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("User not found", result.Errors.First());
        }

        [Fact]
        public async Task CreateCryptoPaymentInvoiceAsync_ReturnsFailure_WhenPlanIdIsUnknown()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var unknownPlanId = Guid.NewGuid(); // Not Premium or Best
            var user = new User { Id = userId, Username = "testuser" };
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

            // Act
            var result = await _paymentService.CreateCryptoPaymentInvoiceAsync(userId, unknownPlanId, "USDT", 10m);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Selected plan is not valid", result.Errors.First());
        }

        [Fact]
        public async Task CreateCryptoPaymentInvoiceAsync_ReturnsFailure_WhenCryptoPayApiFails()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { Id = userId, Username = "testuser" };
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _mockCryptoPayApiClient.Setup(c => c.CreateInvoiceAsync(It.IsAny<CreateCryptoPayInvoiceRequestDto>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<CryptoPayInvoiceDto>.Failure("API Error"));

            // Act
            var result = await _paymentService.CreateCryptoPaymentInvoiceAsync(userId, PremiumPlanId, "USDT", 10m);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("API Error", result.Errors.First());
            _mockTransactionRepository.Verify(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateCryptoPaymentInvoiceAsync_CreatesInvoiceAndPendingTransaction_OnSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var planId = PremiumPlanId;
            var asset = "USDT";
            var amount = 15.5m;
            var user = new User { Id = userId, Username = "testuser" };
            var invoiceId = 12345L;
            var createdInvoiceDto = new CryptoPayInvoiceDto { InvoiceId = invoiceId, Status = "active", PayUrl = "http://pay.url" };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _mockCryptoPayApiClient.Setup(c => c.CreateInvoiceAsync(It.IsAny<CreateCryptoPayInvoiceRequestDto>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<CryptoPayInvoiceDto>.Success(createdInvoiceDto));
            _mockTransactionRepository.Setup(t => t.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
                                    .Returns(Task.CompletedTask);
            _mockDbContext.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _paymentService.CreateCryptoPaymentInvoiceAsync(userId, planId, asset, amount);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Equal(invoiceId, result.Data.InvoiceId);

            _mockCryptoPayApiClient.Verify(c => c.CreateInvoiceAsync(
                It.Is<CreateCryptoPayInvoiceRequestDto>(r =>
                    r.Asset == asset &&
                    r.Amount == amount.ToString("F8", CultureInfo.InvariantCulture) &&
                    r.Description.Contains("Premium Plan") &&
                    JsonSerializer.Deserialize<JsonElement>(r.Payload!).GetProperty("UserId").GetGuid() == userId &&
                    JsonSerializer.Deserialize<JsonElement>(r.Payload!).GetProperty("PlanId").GetGuid() == planId
                ), It.IsAny<CancellationToken>()), Times.Once);

            _mockTransactionRepository.Verify(t => t.AddAsync(
                It.Is<Transaction>(tr =>
                    tr.UserId == userId &&
                    tr.Amount == amount &&
                    tr.Currency == asset &&
                    tr.Status == "Pending" &&
                    tr.PaymentGatewayInvoiceId == invoiceId.ToString() &&
                    tr.Description.Contains("Premium Plan")
                ), It.IsAny<CancellationToken>()), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckInvoiceStatusAsync_ReturnsFailure_ForInvalidInvoiceId()
        {
            // Act
            var result = await _paymentService.CheckInvoiceStatusAsync(0);
            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Invalid invoice ID", result.Errors.First());
        }

        [Fact]
        public async Task CheckInvoiceStatusAsync_ReturnsFailure_WhenApiFailsOrInvoiceNotFound()
        {
            // Arrange
            var invoiceId = 123L;
            _mockCryptoPayApiClient.Setup(c => c.GetInvoicesAsync(It.Is<GetCryptoPayInvoicesRequestDto>(r => r.InvoiceIds == invoiceId.ToString()), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<IEnumerable<CryptoPayInvoiceDto>>.Failure("API Error or Not Found"));

            // Act
            var result = await _paymentService.CheckInvoiceStatusAsync(invoiceId);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("API Error or Not Found", result.Errors.First());
        }

        [Fact]
        public async Task CheckInvoiceStatusAsync_ReturnsSuccess_WhenInvoiceFound()
        {
            // Arrange
            var invoiceId = 456L;
            var invoiceDto = new CryptoPayInvoiceDto { InvoiceId = invoiceId, Status = "paid", Asset = "USDT", Amount = "10.00" };
            var apiResponse = new List<CryptoPayInvoiceDto> { invoiceDto };
            _mockCryptoPayApiClient.Setup(c => c.GetInvoicesAsync(It.Is<GetCryptoPayInvoicesRequestDto>(r => r.InvoiceIds == invoiceId.ToString()), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(Result<IEnumerable<CryptoPayInvoiceDto>>.Success(apiResponse));

            // Act
            var result = await _paymentService.CheckInvoiceStatusAsync(invoiceId);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Data);
            Assert.Equal(invoiceId, result.Data.InvoiceId);
            Assert.Equal("paid", result.Data.Status);
        }
    }
}
