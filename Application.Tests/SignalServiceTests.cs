using Application.Common.Interfaces;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using AutoMapper;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests
{
    public class SignalServiceTests
    {
        private readonly Mock<ISignalRepository> _mockSignalRepository;
        private readonly Mock<ISignalCategoryRepository> _mockCategoryRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IAppDbContext> _mockDbContext;
        private readonly Mock<ILogger<SignalService>> _mockLogger;
        private readonly SignalService _signalService;

        public SignalServiceTests()
        {
            _mockSignalRepository = new Mock<ISignalRepository>();
            _mockCategoryRepository = new Mock<ISignalCategoryRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockDbContext = new Mock<IAppDbContext>();
            _mockLogger = new Mock<ILogger<SignalService>>();

            _signalService = new SignalService(
                _mockSignalRepository.Object,
                _mockCategoryRepository.Object,
                _mockMapper.Object,
                _mockDbContext.Object,
                _mockLogger.Object
            );
        }

        private Signal CreateSampleSignal(Guid id, Guid categoryId, string symbol = "EURUSD") => new()
        {
            Id = id,
            Symbol = symbol,
            SignalProvider = "TestProvider",
            SignalTime = DateTime.UtcNow.AddMinutes(-10),
            EntryPrice = 1.1000m,
            StopLoss = 1.0900m,
            TakeProfit1 = 1.1100m,
            PublishedAt = DateTime.UtcNow,
            Status = Domain.Enums.SignalStatus.Active,
            Type = Domain.Enums.SignalType.Buy,
            CategoryId = categoryId,
            Category = new SignalCategory { Id = categoryId, Name = "Majors" }
        };

        private SignalDto CreateSampleSignalDto(Signal signal) => new()
        {
            Id = signal.Id,
            Symbol = signal.Symbol,
            SignalProvider = signal.SignalProvider,
            SignalTime = signal.SignalTime,
            EntryPrice = signal.EntryPrice,
            StopLoss = signal.StopLoss,
            TakeProfit1 = signal.TakeProfit1,
            PublishedAt = signal.PublishedAt,
            Status = signal.Status,
            Type = signal.Type,
            Category = _mockMapper.Object.Map<SignalCategoryDto>(signal.Category) // Assume mapper handles this
        };

        [Fact]
        public async Task CreateSignalAsync_ThrowsException_WhenCategoryNotFound()
        {
            // Arrange
            var createDto = new CreateSignalDto { CategoryId = Guid.NewGuid(), Symbol = "EURUSD" };
            _mockCategoryRepository.Setup(repo => repo.GetByIdAsync(createDto.CategoryId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync((SignalCategory?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => _signalService.CreateSignalAsync(createDto));
            Assert.Contains(createDto.CategoryId.ToString(), ex.Message); // Check if category ID is in message
        }

        [Fact]
        public async Task CreateSignalAsync_CreatesAndReturnsSignalDto_WhenCategoryFound()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var createDto = new CreateSignalDto { CategoryId = categoryId, Symbol = "GBPUSD", SignalProvider = "TestProvider" };
            var category = new SignalCategory { Id = categoryId, Name = "Test Category" };

            var mappedSignal = new Signal { Id = Guid.NewGuid(), CategoryId = categoryId, Symbol = createDto.Symbol }; // Simplified mapping result
            var createdSignalWithDetails = CreateSampleSignal(mappedSignal.Id, categoryId, createDto.Symbol); // Simulate what repo returns
            createdSignalWithDetails.Category = category;

            var expectedSignalDto = CreateSampleSignalDto(createdSignalWithDetails);

            _mockCategoryRepository.Setup(repo => repo.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(category);
            _mockMapper.Setup(m => m.Map<Signal>(createDto)).Returns(mappedSignal);
            _mockSignalRepository.Setup(repo => repo.AddAsync(mappedSignal, It.IsAny<CancellationToken>()))
                                 .Returns(Task.CompletedTask);
            _mockDbContext.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockSignalRepository.Setup(repo => repo.GetByIdWithDetailsAsync(mappedSignal.Id, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(createdSignalWithDetails);
            _mockMapper.Setup(m => m.Map<SignalDto>(createdSignalWithDetails)).Returns(expectedSignalDto);


            // Act
            var result = await _signalService.CreateSignalAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedSignalDto.Id, result.Id);
            Assert.Equal(createDto.Symbol, result.Symbol);
            _mockSignalRepository.Verify(repo => repo.AddAsync(mappedSignal, It.IsAny<CancellationToken>()), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetSignalByIdAsync_ReturnsNull_WhenSignalNotFound()
        {
            // Arrange
            var signalId = Guid.NewGuid();
            _mockSignalRepository.Setup(repo => repo.GetByIdWithDetailsAsync(signalId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync((Signal?)null);

            // Act
            var result = await _signalService.GetSignalByIdAsync(signalId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetSignalByIdAsync_ReturnsSignalDto_WhenSignalFound()
        {
            // Arrange
            var signalId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var signal = CreateSampleSignal(signalId, categoryId);
            var expectedDto = CreateSampleSignalDto(signal);

            _mockSignalRepository.Setup(repo => repo.GetByIdWithDetailsAsync(signalId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(signal);
            _mockMapper.Setup(m => m.Map<SignalDto>(signal)).Returns(expectedDto);

            // Act
            var result = await _signalService.GetSignalByIdAsync(signalId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedDto.Id, result.Id);
        }

        [Fact]
        public async Task UpdateSignalAsync_ThrowsException_WhenSignalNotFound()
        {
            // Arrange
            var signalId = Guid.NewGuid();
            var updateDto = new UpdateSignalDto { Symbol = "AUDUSD" };
            _mockSignalRepository.Setup(repo => repo.GetByIdAsync(signalId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync((Signal?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => _signalService.UpdateSignalAsync(signalId, updateDto));
            Assert.Contains(signalId.ToString(), ex.Message);
        }

        [Fact]
        public async Task UpdateSignalAsync_ThrowsException_WhenNewCategoryNotFound()
        {
            // Arrange
            var signalId = Guid.NewGuid();
            var currentCategoryId = Guid.NewGuid();
            var newCategoryId = Guid.NewGuid();
            var updateDto = new UpdateSignalDto { CategoryId = newCategoryId, Symbol = "CADJPY" };
            var signal = CreateSampleSignal(signalId, currentCategoryId);

            _mockSignalRepository.Setup(repo => repo.GetByIdAsync(signalId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(signal);
            _mockCategoryRepository.Setup(repo => repo.GetByIdAsync(newCategoryId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync((SignalCategory?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => _signalService.UpdateSignalAsync(signalId, updateDto));
            Assert.Contains(newCategoryId.ToString(), ex.Message);
        }

        [Fact]
        public async Task UpdateSignalAsync_UpdatesSignalSuccessfully()
        {
            // Arrange
            var signalId = Guid.NewGuid();
            var currentCategoryId = Guid.NewGuid();
            var newCategoryId = Guid.NewGuid(); // Optional: can be same or different
            var updateDto = new UpdateSignalDto { Symbol = "NZDUSD", EntryPrice = 0.6500m, CategoryId = newCategoryId };

            var signal = CreateSampleSignal(signalId, currentCategoryId);
            var newCategory = new SignalCategory { Id = newCategoryId, Name = "Commdolls" };

            _mockSignalRepository.Setup(repo => repo.GetByIdAsync(signalId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(signal);
            if (updateDto.CategoryId.HasValue)
            {
                _mockCategoryRepository.Setup(repo => repo.GetByIdAsync(updateDto.CategoryId.Value, It.IsAny<CancellationToken>()))
                                     .ReturnsAsync(newCategory);
            }
            // IMapper.Map(updateDto, signal) is a void method, Moq handles it by default.
            // We verify its effect by checking the entity's properties if needed, or trust AutoMapper config.
            _mockDbContext.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _signalService.UpdateSignalAsync(signalId, updateDto);

            // Assert
            _mockSignalRepository.Verify(repo => repo.UpdateAsync(It.Is<Signal>(s => s.Id == signalId), It.IsAny<CancellationToken>()), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            // Verify AutoMapper was implicitly called by checking mapped properties
            // For example, if the mock mapper was set up to modify the signal:
            // Assert.Equal(updateDto.Symbol, signal.Symbol);
            // Assert.Equal(updateDto.EntryPrice, signal.EntryPrice);
            // Assert.Equal(newCategoryId, signal.CategoryId);
        }

        [Fact]
        public async Task DeleteSignalAsync_ThrowsException_WhenSignalNotFound()
        {
            // Arrange
            var signalId = Guid.NewGuid();
            _mockSignalRepository.Setup(repo => repo.GetByIdAsync(signalId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync((Signal?)null);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _signalService.DeleteSignalAsync(signalId));
        }

        [Fact]
        public async Task DeleteSignalAsync_DeletesSignalSuccessfully()
        {
            // Arrange
            var signalId = Guid.NewGuid();
            var signal = CreateSampleSignal(signalId, Guid.NewGuid());
            _mockSignalRepository.Setup(repo => repo.GetByIdAsync(signalId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(signal);
            _mockSignalRepository.Setup(repo => repo.DeleteAsync(signal, It.IsAny<CancellationToken>()))
                                 .Returns(Task.CompletedTask);
            _mockDbContext.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _signalService.DeleteSignalAsync(signalId);

            // Assert
            _mockSignalRepository.Verify(repo => repo.DeleteAsync(signal, It.IsAny<CancellationToken>()), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
