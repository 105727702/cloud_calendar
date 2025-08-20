using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyAvaloniaApp.Services
{
    public class RateLimiter
    {
        private static RateLimiter? _instance;
        public static RateLimiter Instance => _instance ??= new RateLimiter();

        private readonly ConcurrentDictionary<string, ClientInfo> _clients;
        private readonly Timer _cleanupTimer;
        private readonly int _maxRequestsPerMinute;
        private readonly int _maxRequestsPer10Seconds;

        private RateLimiter()
        {
            _clients = new ConcurrentDictionary<string, ClientInfo>();
            _maxRequestsPerMinute = 30;     // Tối đa 30 requests/phút
            _maxRequestsPer10Seconds = 10;  // Tối đa 10 requests/10 giây
            
            // Cleanup expired entries every minute
            _cleanupTimer = new Timer(CleanupExpiredClients, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public Task<bool> CanMakeRequestAsync(string clientId = "default")
        {
            var now = DateTime.UtcNow;
            var client = _clients.GetOrAdd(clientId, _ => new ClientInfo());

            lock (client)
            {
                // Cleanup old requests (older than 1 minute)
                client.RequestTimes.RemoveAll(time => now - time > TimeSpan.FromMinutes(1));

                // Check rate limits
                var requestsInLastMinute = client.RequestTimes.Count;
                var requestsInLast10Seconds = client.RequestTimes.Count(time => now - time <= TimeSpan.FromSeconds(10));

                if (requestsInLastMinute >= _maxRequestsPerMinute)
                {
                    Console.WriteLine($"Rate limit exceeded for client {clientId}: {requestsInLastMinute} requests in last minute");
                    return Task.FromResult(false);
                }

                if (requestsInLast10Seconds >= _maxRequestsPer10Seconds)
                {
                    Console.WriteLine($"Rate limit exceeded for client {clientId}: {requestsInLast10Seconds} requests in last 10 seconds");
                    return Task.FromResult(false);
                }

                // Record this request
                client.RequestTimes.Add(now);
                client.LastRequestTime = now;
                return Task.FromResult(true);
            }
        }

        public TimeSpan GetRetryAfter(string clientId = "default")
        {
            if (!_clients.TryGetValue(clientId, out var client))
                return TimeSpan.Zero;

            lock (client)
            {
                var now = DateTime.UtcNow;
                var requestsInLast10Seconds = client.RequestTimes.Count(time => now - time <= TimeSpan.FromSeconds(10));
                
                if (requestsInLast10Seconds >= _maxRequestsPer10Seconds)
                {
                    var oldestRecentRequest = client.RequestTimes
                        .Where(time => now - time <= TimeSpan.FromSeconds(10))
                        .Min();
                    return TimeSpan.FromSeconds(10) - (now - oldestRecentRequest);
                }

                var requestsInLastMinute = client.RequestTimes.Count;
                if (requestsInLastMinute >= _maxRequestsPerMinute)
                {
                    var oldestRequest = client.RequestTimes.Min();
                    return TimeSpan.FromMinutes(1) - (now - oldestRequest);
                }

                return TimeSpan.Zero;
            }
        }

        private void CleanupExpiredClients(object? state)
        {
            var now = DateTime.UtcNow;
            var expiredClients = new List<string>();

            foreach (var kvp in _clients)
            {
                var client = kvp.Value;
                lock (client)
                {
                    // Remove clients that haven't made requests in the last 5 minutes
                    if (now - client.LastRequestTime > TimeSpan.FromMinutes(5))
                    {
                        expiredClients.Add(kvp.Key);
                    }
                    else
                    {
                        // Cleanup old request times
                        client.RequestTimes.RemoveAll(time => now - time > TimeSpan.FromMinutes(1));
                    }
                }
            }

            foreach (var clientId in expiredClients)
            {
                _clients.TryRemove(clientId, out _);
            }

            if (expiredClients.Count > 0)
            {
                Console.WriteLine($"Cleaned up {expiredClients.Count} expired rate limiter clients");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _clients.Clear();
        }
    }

    public class ClientInfo
    {
        public List<DateTime> RequestTimes { get; } = new List<DateTime>();
        public DateTime LastRequestTime { get; set; } = DateTime.UtcNow;
    }
}
