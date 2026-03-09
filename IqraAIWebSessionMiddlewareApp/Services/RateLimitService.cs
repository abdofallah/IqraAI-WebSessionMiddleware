using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using IqraAIWebSessionMiddlewareApp.Settings;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace IqraAIWebSessionMiddlewareApp.Services
{
    public class RateLimitService : IRateLimitService
    {
        private readonly IDatabase _redisDb;
        private readonly SecuritySettings _securitySettings;

        public RateLimitService(IConnectionMultiplexer redisConnection, IOptions<SecuritySettings> securitySettings)
        {
            _redisDb = redisConnection.GetDatabase();
            _securitySettings = securitySettings.Value;
        }

        public async Task<RateLimitCheckResult> CheckAndAcquireAsync(string ipAddress)
        {
            var now = DateTime.UtcNow;
            var dailyKey = $"ratelimit:daily:{ipAddress}:{now:yyyyMMdd}";
            var hourlyKey = $"ratelimit:hourly:{ipAddress}:{now:yyyyMMddHH}";
            var concurrentKey = $"ratelimit:concurrent:{ipAddress}";

            // We use a transaction/script to check and increment atomically if allowed,
            // but actually we can just GET, check, and INCR. To avoid race conditions,
            // a small Lua script is best, or we can just INCR and check if it exceeds, 
            // if so DECR back. This is simpler without Lua.

            if (_securitySettings.RateLimitConcurrency > 0)
            {
                var currentConcurrent = (long)await _redisDb.StringGetAsync(concurrentKey);
                if (currentConcurrent >= _securitySettings.RateLimitConcurrency)
                {
                    return new RateLimitCheckResult { IsAllowed = false, Reason = "Too many concurrent sessions from your IP." };
                }
            }

            if (_securitySettings.RateLimitDaily > 0)
            {
                var currentDaily = (long)await _redisDb.StringGetAsync(dailyKey);
                if (currentDaily >= _securitySettings.RateLimitDaily)
                {
                    return new RateLimitCheckResult { IsAllowed = false, Reason = "Daily rate limit exceeded." };
                }
            }

            if (_securitySettings.RateLimitHourly > 0)
            {
                var currentHourly = (long)await _redisDb.StringGetAsync(hourlyKey);
                if (currentHourly >= _securitySettings.RateLimitHourly)
                {
                    return new RateLimitCheckResult { IsAllowed = false, Reason = "Hourly rate limit exceeded." };
                }
            }

            // All checks passed, perform increments
            if (_securitySettings.RateLimitConcurrency > 0)
            {
                await _redisDb.StringIncrementAsync(concurrentKey);
            }

            if (_securitySettings.RateLimitDaily > 0)
            {
                var dailyCount = await _redisDb.StringIncrementAsync(dailyKey);
                if (dailyCount == 1) await _redisDb.KeyExpireAsync(dailyKey, TimeSpan.FromHours(24));
            }

            if (_securitySettings.RateLimitHourly > 0)
            {
                var hourlyCount = await _redisDb.StringIncrementAsync(hourlyKey);
                if (hourlyCount == 1) await _redisDb.KeyExpireAsync(hourlyKey, TimeSpan.FromHours(1));
            }

            return new RateLimitCheckResult { IsAllowed = true };
        }

        public async Task DecrementConcurrentAsync(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress)) return;
            
            var concurrentKey = $"ratelimit:concurrent:{ipAddress}";
            var current = (long)await _redisDb.StringGetAsync(concurrentKey);
            
            if (current > 0)
            {
                await _redisDb.StringDecrementAsync(concurrentKey);
            }
        }

        public async Task MapSessionToIpAsync(string sessionId, string ipAddress)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(ipAddress)) return;
            var key = $"sessionip:{sessionId}";
            // Store mapping for 2 hours, should be more than enough for any web session
            await _redisDb.StringSetAsync(key, ipAddress, TimeSpan.FromHours(2));
        }

        public async Task<string?> GetIpForSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return null;
            var key = $"sessionip:{sessionId}";
            var value = await _redisDb.StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
    }
}
