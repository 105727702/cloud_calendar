using System;
using System.Threading.Tasks;
using Xunit;
using MyAvaloniaApp.Services;

namespace MyAvaloniaApp.Tests
{
    public class RateLimiterTests
    {
        [Fact]
        public async Task CanMakeRequest_WithinLimits_ReturnsTrue()
        {
            // Arrange
            var rateLimiter = RateLimiter.Instance;
            var clientId = "test_client_1";

            // Act
            var result = await rateLimiter.CanMakeRequestAsync(clientId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanMakeRequest_ExceedsPerSecondLimit_ReturnsFalse()
        {
            // Arrange
            var rateLimiter = RateLimiter.Instance;
            var clientId = "test_client_2";

            // Act - Make 11 requests (exceeds 10 per 10 seconds limit)
            for (int i = 0; i < 10; i++)
            {
                await rateLimiter.CanMakeRequestAsync(clientId);
            }
            var result = await rateLimiter.CanMakeRequestAsync(clientId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanMakeRequest_ExceedsPerMinuteLimit_ReturnsFalse()
        {
            // Arrange
            var rateLimiter = RateLimiter.Instance;
            var clientId = "test_client_3";

            // Act - Make 31 requests (exceeds 30 per minute limit)
            for (int i = 0; i < 30; i++)
            {
                await rateLimiter.CanMakeRequestAsync(clientId);
                await Task.Delay(100); // Small delay to avoid 10-second limit
            }
            var result = await rateLimiter.CanMakeRequestAsync(clientId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetRetryAfter_WhenRateLimited_ReturnsValidTimeSpan()
        {
            // Arrange
            var rateLimiter = RateLimiter.Instance;
            var clientId = "test_client_4";

            // Act - Exceed rate limit
            for (int i = 0; i < 11; i++)
            {
                await rateLimiter.CanMakeRequestAsync(clientId);
            }
            var retryAfter = rateLimiter.GetRetryAfter(clientId);

            // Assert
            Assert.True(retryAfter > TimeSpan.Zero);
            Assert.True(retryAfter <= TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task CanMakeRequest_DifferentClients_IndependentLimits()
        {
            // Arrange
            var rateLimiter = RateLimiter.Instance;
            var clientId1 = "test_client_5a";
            var clientId2 = "test_client_5b";

            // Act - Exhaust limit for client 1
            for (int i = 0; i < 10; i++)
            {
                await rateLimiter.CanMakeRequestAsync(clientId1);
            }
            var client1Result = await rateLimiter.CanMakeRequestAsync(clientId1);
            var client2Result = await rateLimiter.CanMakeRequestAsync(clientId2);

            // Assert
            Assert.False(client1Result); // Client 1 should be rate limited
            Assert.True(client2Result);  // Client 2 should still be allowed
        }
    }
}
