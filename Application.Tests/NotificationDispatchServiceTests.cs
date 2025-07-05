using Application.Common.Interfaces;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Hangfire; // For JobCancellationToken only, actual Hangfire not used in unit tests
using Microsoft.Extensions.Logging;
using Moq;
using Polly.CircuitBreaker; // For CircuitState and BrokenCircuitException
using StackExchange.Redis;
using System.Text.Json;

namespace Application.Tests
{
    public class NotificationDispatchServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<INotificationJobScheduler> _mockJobScheduler;
        private readonly Mock<ILogger<NotificationDispatchService>> _mockLogger;
        private readonly Mock<INewsItemRepository> _mockNewsItemRepository;
        private readonly Mock<IConnectionMultiplexer> _mockRedisConnection;
        private readonly Mock<IDatabase> _mockRedisDb;
        private readonly Mock<INotificationRateLimiter> _mockRateLimiter;

        private NotificationDispatchService _notificationDispatchService;
        private AsyncCircuitBreakerPolicy _testCircuitBreaker; // To control its state in tests

        public NotificationDispatchServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockJobScheduler = new Mock<INotificationJobScheduler>();
            _mockLogger = new Mock<ILogger<NotificationDispatchService>>();
            _mockNewsItemRepository = new Mock<INewsItemRepository>();
            _mockRedisConnection = new Mock<IConnectionMultiplexer>();
            _mockRedisDb = new Mock<IDatabase>();
            _mockRateLimiter = new Mock<INotificationRateLimiter>();

            _mockRedisConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockRedisDb.Object);

            // Setup a real circuit breaker for testing its state, but it won't actually break connections to a mock.
            // The service creates its own, so we can't directly inject this one unless we refactor the service.
            // For now, we'll test behavior when the service's internal breaker *would* be open by mocking RedisException.
            _testCircuitBreaker = Policy.Handle<RedisException>().CircuitBreakerAsync(1, TimeSpan.FromMinutes(1));


            _notificationDispatchService = new NotificationDispatchService(
                _mockNewsItemRepository.Object,
                _mockUserRepository.Object,
                _mockJobScheduler.Object,
                _mockLogger.Object,
                _mockRedisConnection.Object,
                _mockRateLimiter.Object
            );
        }

        private NewsItem CreateSampleNewsItem(Guid id, Guid? categoryId = null, bool isVip = false) => new()
        {
            Id = id, Title = "Test News", Content = "Test Content", AssociatedSignalCategoryId = categoryId, IsVipOnly = isVip
        };

        private List<User> CreateSampleUsers(int count, bool enableRssNotifications = true)
        {
            var users = new List<User>();
            for (int i = 0; i < count; i++)
            {
                users.Add(new User {
                    Id = Guid.NewGuid(),
                    TelegramId = (1000 + i).ToString(), // Ensure parseable long
                    EnableRssNewsNotifications = enableRssNotifications,
                    EnableGeneralNotifications = true
                });
            }
            return users;
        }

        [Fact]
        public async Task DispatchNewsNotificationAsync_Skips_IfGlobalLockNotAcquired()
        {
            // Arrange
            var newsItemId = Guid.NewGuid();
            _mockJobScheduler.Setup(js => js.TryAcquireLockAsync("global-dispatch-orchestration-lock", It.IsAny<TimeSpan>()))
                             .ReturnsAsync(false); // Global lock fails

            // Act
            await _notificationDispatchService.DispatchNewsNotificationAsync(newsItemId);

            // Assert
            _mockNewsItemRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockJobScheduler.Verify(js => js.Enqueue<INotificationDispatchService>(x => x.ProcessNotificationChunkAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())), Times.Never);
        }

        [Fact]
        public async Task DispatchNewsNotificationAsync_Skips_IfItemLockNotAcquired()
        {
            // Arrange
            var newsItemId = Guid.NewGuid();
            _mockJobScheduler.Setup(js => js.TryAcquireLockAsync("global-dispatch-orchestration-lock", It.IsAny<TimeSpan>()))
                             .ReturnsAsync(true); // Global lock succeeds
            _mockRedisDb.Setup(db => db.StringSetAsync($"dispatch-lock:{newsItemId}", "locked", TimeSpan.FromHours(1), When.NotExists, CommandFlags.None))
                        .ReturnsAsync(false); // Item lock fails

            // Act
            await _notificationDispatchService.DispatchNewsNotificationAsync(newsItemId);

            // Assert
            _mockNewsItemRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync("global-dispatch-orchestration-lock"), Times.Once); // Global lock should be released
        }

        // To truly test circuit breaker state effect, NotificationDispatchService would need to accept
        // the policy via DI, or we'd need to force RedisException from _mockRedisDb on specific calls.
        // Forcing RedisException from StringSetAsync to simulate breaker opening:
        [Fact]
        public async Task DispatchNewsNotificationAsync_Skips_IfRedisThrowsAndCircuitWouldOpen()
        {
            // Arrange
            var newsItemId = Guid.NewGuid();
            _mockJobScheduler.Setup(js => js.TryAcquireLockAsync("global-dispatch-orchestration-lock", It.IsAny<TimeSpan>()))
                             .ReturnsAsync(true);
            _mockRedisDb.Setup(db => db.StringSetAsync($"dispatch-lock:{newsItemId}", "locked", TimeSpan.FromHours(1), When.NotExists, CommandFlags.None))
                        .ReturnsAsync(true); // Item lock acquired initially

            // Simulate Redis being down for caching user list which would trip the breaker in real scenario
            _mockNewsItemRepository.Setup(r => r.GetByIdAsync(newsItemId, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(CreateSampleNewsItem(newsItemId));
            _mockUserRepository.Setup(r => r.GetUsersForNewsNotificationAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(CreateSampleUsers(1));
             _mockRateLimiter.Setup(rl => rl.IsUserOverLimitAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(false); // User is not over limit

            // This setup will cause the ExecuteAsync to throw RedisException
            _mockRedisDb.Setup(db => db.StringSetAsync($"dispatch:users:{newsItemId}", It.IsAny<RedisValue>(), TimeSpan.FromHours(24), When.Always, CommandFlags.None))
                        .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.InternalFailure, "Simulated Redis down"));

            // Act
            // The internal circuit breaker in NotificationDispatchService should catch this.
            // We expect it to log a warning/error and not enqueue, and release locks.
            await _notificationDispatchService.DispatchNewsNotificationAsync(newsItemId);


            // Assert
             _mockJobScheduler.Verify(js => js.Enqueue<INotificationDispatchService>(x => x.ProcessNotificationChunkAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())),
                Times.Never, "Jobs should not be enqueued if Redis fails in a way that opens the circuit.");
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync("global-dispatch-orchestration-lock"), Times.Once);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync($"dispatch-lock:{newsItemId}"), Times.Once);
        }


        [Fact]
        public async Task DispatchNewsNotificationAsync_Skips_IfNewsItemNotFound()
        {
            // Arrange
            var newsItemId = Guid.NewGuid();
            _mockJobScheduler.Setup(js => js.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
            _mockRedisDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
            _mockNewsItemRepository.Setup(r => r.GetByIdAsync(newsItemId, It.IsAny<CancellationToken>())).ReturnsAsync((NewsItem?)null);

            // Act
            await _notificationDispatchService.DispatchNewsNotificationAsync(newsItemId);

            // Assert
            _mockUserRepository.Verify(r => r.GetUsersForNewsNotificationAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync("global-dispatch-orchestration-lock"), Times.Once);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync($"dispatch-lock:{newsItemId}"), Times.Once);
        }

        [Fact]
        public async Task DispatchNewsNotificationAsync_Skips_IfNoEligibleUsersFound()
        {
            // Arrange
            var newsItemId = Guid.NewGuid();
            _mockJobScheduler.Setup(js => js.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
            _mockRedisDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
            _mockNewsItemRepository.Setup(r => r.GetByIdAsync(newsItemId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateSampleNewsItem(newsItemId));
            _mockUserRepository.Setup(r => r.GetUsersForNewsNotificationAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(new List<User>()); // No users

            // Act
            await _notificationDispatchService.DispatchNewsNotificationAsync(newsItemId);

            // Assert
            _mockRedisDb.Verify(db => db.StringSetAsync($"dispatch:users:{newsItemId}", It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync("global-dispatch-orchestration-lock"), Times.Once);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync($"dispatch-lock:{newsItemId}"), Times.Once);
        }

        [Fact]
        public async Task DispatchNewsNotificationAsync_Skips_IfAllUsersRateLimited()
        {
            var newsItemId = Guid.NewGuid();
            var users = CreateSampleUsers(2); // 2 users
            _mockJobScheduler.Setup(js => js.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
            _mockRedisDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
            _mockNewsItemRepository.Setup(r => r.GetByIdAsync(newsItemId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateSampleNewsItem(newsItemId));
            _mockUserRepository.Setup(r => r.GetUsersForNewsNotificationAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(users);
            _mockRateLimiter.Setup(rl => rl.IsUserOverLimitAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(true); // All users are over limit

            await _notificationDispatchService.DispatchNewsNotificationAsync(newsItemId);

            _mockRedisDb.Verify(db => db.StringSetAsync($"dispatch:users:{newsItemId}", It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
            _mockJobScheduler.Verify(js => js.Enqueue<INotificationDispatchService>(x => x.ProcessNotificationChunkAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())), Times.Never);
        }


        [Fact]
        public async Task DispatchNewsNotificationAsync_EnqueuesChunkJobs_ForEligibleUsers()
        {
            // Arrange
            var newsItemId = Guid.NewGuid();
            var users = CreateSampleUsers(3); // 3 users
            var eligibleUserIds = users.Select(u => long.Parse(u.TelegramId!)).ToList();
            var serializedUserIds = JsonSerializer.Serialize(eligibleUserIds);

            _mockJobScheduler.Setup(js => js.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync(true);
            _mockRedisDb.Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>())).ReturnsAsync(true); // Both locks succeed
            _mockNewsItemRepository.Setup(r => r.GetByIdAsync(newsItemId, It.IsAny<CancellationToken>())).ReturnsAsync(CreateSampleNewsItem(newsItemId));
            _mockUserRepository.Setup(r => r.GetUsersForNewsNotificationAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(users);
            _mockRateLimiter.Setup(rl => rl.IsUserOverLimitAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(false); // No users are rate limited
            _mockRedisDb.Setup(db => db.StringSetAsync($"dispatch:users:{newsItemId}", serializedUserIds, TimeSpan.FromHours(24), When.Always, CommandFlags.None))
                        .ReturnsAsync(true);


            // Act
            await _notificationDispatchService.DispatchNewsNotificationAsync(newsItemId);

            // Assert
            _mockRedisDb.Verify(db => db.StringSetAsync($"dispatch:users:{newsItemId}", serializedUserIds, TimeSpan.FromHours(24), When.Always, CommandFlags.None), Times.Once);
            // Assuming chunkSize is 500, so 1 chunk for 3 users
            _mockJobScheduler.Verify(js => js.Enqueue<INotificationDispatchService>(
                x => x.ProcessNotificationChunkAsync(newsItemId, $"dispatch:users:{newsItemId}", 0, 3, It.IsAny<CancellationToken>())), Times.Once);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync("global-dispatch-orchestration-lock"), Times.Once);
            _mockJobScheduler.Verify(js => js.ReleaseLockAsync($"dispatch-lock:{newsItemId}"), Times.Once);
        }

        [Fact]
        public async Task ProcessNotificationChunkAsync_SchedulesIndividualJobs_WithStaggeredDelay()
        {
            // Arrange
            var newsItemId = Guid.NewGuid();
            var userListCacheKey = $"dispatch:users:{newsItemId}";
            var chunkStartIndex = 0;
            var chunkSize = 3; // 3 jobs to schedule
            var expectedDelayMs = 40; // From service

            // Act
            await _notificationDispatchService.ProcessNotificationChunkAsync(newsItemId, userListCacheKey, chunkStartIndex, chunkSize, CancellationToken.None);

            // Assert
            for (int i = 0; i < chunkSize; i++)
            {
                int currentUserIndex = chunkStartIndex + i;
                TimeSpan expectedIndividualDelay = TimeSpan.FromMilliseconds(i * expectedDelayMs);
                _mockJobScheduler.Verify(js => js.Schedule<INotificationSendingService>(
                    service => service.ProcessNotificationFromCacheAsync(newsItemId, userListCacheKey, currentUserIndex, JobCancellationToken.Null),
                    expectedIndividualDelay), Times.Once);
            }
        }

        [Fact]
        public async Task DispatchBatchNewsNotificationAsync_Skips_IfNoNewsItemIds()
        {
            // Act
            await _notificationDispatchService.DispatchBatchNewsNotificationAsync(null!);
            await _notificationDispatchService.DispatchBatchNewsNotificationAsync(new List<Guid>());

            // Assert
            _mockNewsItemRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DispatchBatchNewsNotificationAsync_Skips_IfFirstNewsItemNotFound()
        {
            // Arrange
            var newsItemIds = new List<Guid> { Guid.NewGuid() };
            _mockNewsItemRepository.Setup(r => r.GetByIdAsync(newsItemIds.First(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync((NewsItem?)null);

            // Act
            await _notificationDispatchService.DispatchBatchNewsNotificationAsync(newsItemIds);

            // Assert
            _mockUserRepository.Verify(r => r.GetUsersForNewsNotificationAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DispatchBatchNewsNotificationAsync_EnqueuesJobs_ForEligibleUsers()
        {
            // Arrange
            var newsItemId1 = Guid.NewGuid();
            var newsItemId2 = Guid.NewGuid();
            var batchNewsItemIds = new List<Guid> { newsItemId1, newsItemId2 };

            var firstNewsItem = CreateSampleNewsItem(newsItemId1, Guid.NewGuid(), false);
            var users = CreateSampleUsers(2); // 2 users
            var eligibleTelegramIdLong1 = long.Parse(users[0].TelegramId!);
            var eligibleTelegramIdLong2 = long.Parse(users[1].TelegramId!);


            _mockNewsItemRepository.Setup(r => r.GetByIdAsync(newsItemId1, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(firstNewsItem);
            _mockUserRepository.Setup(r => r.GetUsersForNewsNotificationAsync(firstNewsItem.AssociatedSignalCategoryId, firstNewsItem.IsVipOnly, It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(users);
            _mockRateLimiter.Setup(rl => rl.IsUserOverLimitAsync(It.IsAny<long>(), 1, TimeSpan.FromHours(1), It.IsAny<CancellationToken>()))
                            .ReturnsAsync(false); // Users are not rate limited for batch

            // Act
            await _notificationDispatchService.DispatchBatchNewsNotificationAsync(batchNewsItemIds);

            // Assert
            _mockJobScheduler.Verify(js => js.Enqueue<INotificationSendingService>(
                service => service.ProcessBatchNotificationForUserAsync(eligibleTelegramIdLong1, batchNewsItemIds)), Times.Once);
            _mockJobScheduler.Verify(js => js.Enqueue<INotificationSendingService>(
                service => service.ProcessBatchNotificationForUserAsync(eligibleTelegramIdLong2, batchNewsItemIds)), Times.Once);
        }
    }
}
