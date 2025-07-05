using Application.Common.Interfaces;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using AutoMapper;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Extensions; // For ILoggingSanitizer, assuming it's here or a similar namespace

namespace Application.Tests
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ITokenWalletRepository> _mockTokenWalletRepository;
        private readonly Mock<ISubscriptionRepository> _mockSubscriptionRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IAppDbContext> _mockDbContext;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<ILoggingSanitizer> _mockLogSanitizer;
        private readonly UserService _userService;

        public UserServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockTokenWalletRepository = new Mock<ITokenWalletRepository>();
            _mockSubscriptionRepository = new Mock<ISubscriptionRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockDbContext = new Mock<IAppDbContext>();
            _mockLogger = new Mock<ILogger<UserService>>();
            _mockCacheService = new Mock<ICacheService>();
            _mockLogSanitizer = new Mock<ILoggingSanitizer>();

            // Setup mock log sanitizer to return input for simplicity in tests
            _mockLogSanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns((string s) => s);

            _userService = new UserService(
                _mockLogSanitizer.Object,
                _mockUserRepository.Object,
                _mockTokenWalletRepository.Object,
                _mockSubscriptionRepository.Object,
                _mockMapper.Object,
                _mockDbContext.Object,
                _mockCacheService.Object,
                _mockLogger.Object
            );
        }

        private User CreateSampleUser(string telegramId = "testTelegramId", Guid? userId = null)
        {
            return new User
            {
                Id = userId ?? Guid.NewGuid(),
                TelegramId = telegramId,
                Username = "testUser",
                Email = "test@example.com",
                TokenWallet = new TokenWallet { Id = Guid.NewGuid(), Balance = 100 },
                Subscriptions = new List<Subscription>()
            };
        }

        private UserDto CreateSampleUserDto(User user, SubscriptionDto? activeSubscription = null)
        {
            return new UserDto
            {
                Id = user.Id,
                TelegramId = user.TelegramId,
                Username = user.Username,
                Email = user.Email,
                TokenBalance = user.TokenWallet?.Balance ?? 0,
                ActiveSubscription = activeSubscription
            };
        }

        [Fact]
        public async Task GetUserByTelegramIdAsync_ReturnsNull_IfTelegramIdIsNullOrWhitespace()
        {
            // Act
            var resultNull = await _userService.GetUserByTelegramIdAsync(null!);
            var resultEmpty = await _userService.GetUserByTelegramIdAsync("");
            var resultWhitespace = await _userService.GetUserByTelegramIdAsync("   ");

            // Assert
            Assert.Null(resultNull);
            Assert.Null(resultEmpty);
            Assert.Null(resultWhitespace);
        }

        [Fact]
        public async Task GetUserByTelegramIdAsync_ReturnsUserDtoFromCache_IfFound()
        {
            // Arrange
            var telegramId = "cachedUser";
            var cachedUser = CreateSampleUser(telegramId);
            var cachedUserDto = CreateSampleUserDto(cachedUser);
            string cacheKey = $"user:telegram_id:{telegramId}";

            _mockCacheService.Setup(c => c.GetAsync<UserDto>(cacheKey, It.IsAny<CancellationToken>()))
                             .ReturnsAsync(cachedUserDto);

            // Act
            var result = await _userService.GetUserByTelegramIdAsync(telegramId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(cachedUserDto.Id, result.Id);
            _mockUserRepository.Verify(r => r.GetByTelegramIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetUserByTelegramIdAsync_FetchesFromRepositoryAndCaches_IfNotInCache()
        {
            // Arrange
            var telegramId = "dbUser";
            var userFromDb = CreateSampleUser(telegramId);
            var userDtoFromMapper = CreateSampleUserDto(userFromDb);
            string cacheKey = $"user:telegram_id:{telegramId}";

            _mockCacheService.Setup(c => c.GetAsync<UserDto>(cacheKey, It.IsAny<CancellationToken>()))
                             .ReturnsAsync((UserDto?)null); // Cache miss
            _mockUserRepository.Setup(r => r.GetByTelegramIdAsync(telegramId, It.IsAny<CancellationToken>()))
                               .ReturnsAsync(userFromDb);
            _mockMapper.Setup(m => m.Map<UserDto>(userFromDb)).Returns(userDtoFromMapper);
            _mockSubscriptionRepository.Setup(sr => sr.GetActiveSubscriptionByUserIdAsync(userFromDb.Id, It.IsAny<CancellationToken>()))
                                       .ReturnsAsync((Subscription?)null); // No active sub for simplicity here

            // Act
            var result = await _userService.GetUserByTelegramIdAsync(telegramId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userDtoFromMapper.Id, result.Id);
            _mockUserRepository.Verify(r => r.GetByTelegramIdAsync(telegramId, It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheService.Verify(c => c.SetAsync(cacheKey, userDtoFromMapper, TimeSpan.FromHours(1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetUserByTelegramIdAsync_ReturnsNull_IfUserNotFoundInDb()
        {
            // Arrange
            var telegramId = "nonExistentUser";
            string cacheKey = $"user:telegram_id:{telegramId}";
             _mockCacheService.Setup(c => c.GetAsync<UserDto>(cacheKey, It.IsAny<CancellationToken>()))
                             .ReturnsAsync((UserDto?)null);
            _mockUserRepository.Setup(r => r.GetByTelegramIdAsync(telegramId, It.IsAny<CancellationToken>()))
                               .ReturnsAsync((User?)null);

            // Act
            var result = await _userService.GetUserByTelegramIdAsync(telegramId);

            // Assert
            Assert.Null(result);
            _mockCacheService.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<UserDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RegisterUserAsync_ThrowsArgumentNullException_IfRegisterDtoIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _userService.RegisterUserAsync(null!));
        }

        [Fact]
        public async Task RegisterUserAsync_ThrowsArgumentException_IfUserEntityToRegisterIsNull()
        {
            // Arrange
            var registerDto = new RegisterUserDto { TelegramId = "test", Email = "test@example.com", Username = "test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _userService.RegisterUserAsync(registerDto, userEntityToRegister: null));
        }

        [Fact]
        public async Task RegisterUserAsync_ThrowsInvalidOperationException_IfUserEmailExists()
        {
            // Arrange
            var registerDto = new RegisterUserDto { Email = "existing@example.com", TelegramId = "newTelegramId", Username = "newUser" };
            var userEntity = CreateSampleUser(registerDto.TelegramId); // User entity is pre-constructed

            _mockUserRepository.Setup(r => r.ExistsByEmailAsync(registerDto.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _userService.RegisterUserAsync(registerDto, userEntityToRegister: userEntity));
        }


        [Fact]
        public async Task RegisterUserAsync_SuccessfullyRegistersUser_AndCachesResult()
        {
            // Arrange
            var telegramId = "registerMe";
            var email = "register@example.com";
            var userEntityToRegister = new User
            {
                Id = Guid.NewGuid(),
                TelegramId = telegramId,
                Email = email,
                Username = "toBeRegistered",
                TokenWallet = new TokenWallet { Id = Guid.NewGuid(), Balance = 0, UserId = Guid.NewGuid() /* This would be userEntity.Id */ }
            };
            userEntityToRegister.TokenWallet.UserId = userEntityToRegister.Id; // Link wallet to user

            var registerDto = new RegisterUserDto { TelegramId = telegramId, Email = email, Username = "toBeRegistered" };
            var registeredUserDto = new UserDto { Id = userEntityToRegister.Id, TelegramId = telegramId, Email = email, Username = "toBeRegistered" };

            _mockUserRepository.Setup(r => r.ExistsByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _mockUserRepository.Setup(r => r.AddAsync(userEntityToRegister, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            // _mockTokenWalletRepository.Setup(t => t.AddAsync(userEntityToRegister.TokenWallet, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask); // Assuming wallet added with user or separately
            _mockDbContext.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _mockMapper.Setup(m => m.Map<UserDto>(userEntityToRegister)).Returns(registeredUserDto); // This mapping is simplified in the actual code

            string cacheKeyId = $"user:id:{userEntityToRegister.Id}";
            string cacheKeyTelegramId = $"user:telegram_id:{telegramId}";

            // Act
            var result = await _userService.RegisterUserAsync(registerDto, userEntityToRegister: userEntityToRegister);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userEntityToRegister.Id, result.Id);
            _mockUserRepository.Verify(r => r.AddAsync(userEntityToRegister, It.IsAny<CancellationToken>()), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheService.Verify(c => c.RemoveAsync(cacheKeyTelegramId, It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheService.Verify(c => c.RemoveAsync(cacheKeyId, It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheService.Verify(c => c.SetAsync(cacheKeyTelegramId, registeredUserDto, TimeSpan.FromHours(1), It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheService.Verify(c => c.SetAsync(cacheKeyId, registeredUserDto, TimeSpan.FromHours(1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateUserAsync_ThrowsArgumentNullException_IfUpdateDtoIsNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _userService.UpdateUserAsync(Guid.NewGuid(), null!));
        }

        [Fact]
        public async Task UpdateUserAsync_ThrowsInvalidOperationException_IfUserNotFound()
        {
            var userId = Guid.NewGuid();
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
            var updateDto = new UpdateUserDto { Email = "new@example.com" };

            await Assert.ThrowsAsync<InvalidOperationException>(() => _userService.UpdateUserAsync(userId, updateDto));
        }

        [Fact]
        public async Task UpdateUserAsync_SuccessfullyUpdatesUser_AndInvalidatesCache()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingUser = CreateSampleUser("oldTelegramId", userId);
            existingUser.Email = "old@example.com";
            var updateDto = new UpdateUserDto { Email = "new@example.com", Username = "updatedUser" };

            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(existingUser);
            _mockUserRepository.Setup(r => r.ExistsByEmailAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _mockUserRepository.Setup(r => r.UpdateAsync(existingUser, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockDbContext.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            // IMapper.Map(updateDto, existingUser) will be called internally. We don't need to mock the void return of Map.

            string cacheKey = $"user:telegram_id:{existingUser.TelegramId}";

            // Act
            await _userService.UpdateUserAsync(userId, updateDto);

            // Assert
            _mockUserRepository.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Id == userId && u.Email == "new@example.com" && u.Username == "updatedUser"), It.IsAny<CancellationToken>()), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheService.Verify(c => c.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
             Assert.Equal("new@example.com", existingUser.Email); // Check if mapper was applied (indirectly)
            Assert.Equal("updatedUser", existingUser.Username);
        }

        [Fact]
        public async Task DeleteUserAsync_HandlesUserNotFoundGracefully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            // Act
            await _userService.DeleteUserAsync(userId); // Should not throw

            // Assert
            _mockUserRepository.Verify(r => r.DeleteAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            _mockCacheService.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteUserAsync_DeletesUserAndInvalidatesCache_IfUserFound()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var userToDelete = CreateSampleUser("deleteThisUser", userId);
            _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(userToDelete);
            _mockUserRepository.Setup(r => r.DeleteAsync(userToDelete, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockDbContext.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            string cacheKey = $"user:telegram_id:{userToDelete.TelegramId}";

            // Act
            await _userService.DeleteUserAsync(userId);

            // Assert
            _mockUserRepository.Verify(r => r.DeleteAsync(userToDelete, It.IsAny<CancellationToken>()), Times.Once);
            _mockDbContext.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockCacheService.Verify(c => c.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
