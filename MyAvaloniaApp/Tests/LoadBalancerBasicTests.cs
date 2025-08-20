using System;
using System.Threading.Tasks;
using Xunit;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.Tests
{
    public class LoadBalancerBasicTests
    {
        [Fact]
        public void RateLimiter_Singleton_ReturnsInstance()
        {
            // Act
            var instance1 = RateLimiter.Instance;
            var instance2 = RateLimiter.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2); // Should be same instance
        }

        [Fact]
        public void UserCacheManager_Singleton_ReturnsInstance()
        {
            // Act
            var instance1 = UserCacheManager.Instance;
            var instance2 = UserCacheManager.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2); // Should be same instance
        }

        [Fact]
        public void RequestQueueManager_Singleton_ReturnsInstance()
        {
            // Act
            var instance1 = RequestQueueManager.Instance;
            var instance2 = RequestQueueManager.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2); // Should be same instance
        }

        [Fact]
        public void PerformanceMonitor_Singleton_ReturnsInstance()
        {
            // Act
            var instance1 = PerformanceMonitor.Instance;
            var instance2 = PerformanceMonitor.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2); // Should be same instance
        }

        [Fact]
        public void JwtService_Singleton_ReturnsInstance()
        {
            // Act
            var instance1 = JwtService.Instance;
            var instance2 = JwtService.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2); // Should be same instance
        }

        [Fact]
        public void ConnectionPoolManager_Singleton_ReturnsInstance()
        {
            // Act
            var instance1 = ConnectionPoolManager.Instance;
            var instance2 = ConnectionPoolManager.Instance;

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.Same(instance1, instance2); // Should be same instance
        }

        [Fact]
        public async Task RateLimiter_BasicFunctionality_Works()
        {
            // Arrange
            var rateLimiter = RateLimiter.Instance;
            var clientId = "basic_test";

            // Act
            var result = await rateLimiter.CanMakeRequestAsync(clientId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RequestQueue_BasicFunctionality_Works()
        {
            // Arrange
            var queueManager = RequestQueueManager.Instance;

            // Act
            var result = await queueManager.EnqueueRequestAsync(async () =>
            {
                await Task.Delay(10);
                return "test_result";
            });

            // Assert
            Assert.Equal("test_result", result);
        }

        [Fact]
        public void UserCache_BasicFunctionality_Works()
        {
            // Arrange
            var cacheManager = UserCacheManager.Instance;
            var user = new Models.User
            {
                Id = 999,
                Username = "basictest",
                PasswordHash = "hash",
                Salt = "salt",
                IsActive = true,
                Role = Models.UserRole.User
            };

            // Act
            cacheManager.CacheUser(user);
            var cachedUser = cacheManager.GetCachedUserByUsername("basictest");

            // Assert
            Assert.NotNull(cachedUser);
            Assert.Equal(user.Id, cachedUser.Id);
            Assert.Equal(user.Username, cachedUser.Username);

            // Cleanup
            cacheManager.InvalidateUser("basictest");
        }

        [Fact]
        public async Task PerformanceMonitor_BasicFunctionality_Works()
        {
            // Arrange
            var monitor = PerformanceMonitor.Instance;
            var operationName = "basic_test_op";

            // Act
            using (var measurement = monitor.StartMeasurement(operationName))
            {
                await Task.Delay(10);
            }

            // Assert
            var metric = monitor.GetMetric(operationName);
            Assert.NotNull(metric);
            Assert.Equal(1, metric.TotalCalls);
            Assert.Equal(1, metric.SuccessfulCalls);
        }
    }
}
