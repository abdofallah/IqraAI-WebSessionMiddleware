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

        private const string RateLimitScript = @"
local concurrentKey = KEYS[1]
local hourlyKey = KEYS[2]
local dailyKey = KEYS[3]

local maxConcurrent = tonumber(ARGV[1])
local maxHourly = tonumber(ARGV[2])
local maxDaily = tonumber(ARGV[3])

local now = tonumber(ARGV[4])
local hourlyWindowStart = tonumber(ARGV[5])
local dailyWindowStart = tonumber(ARGV[6])
local token = ARGV[7]

-- Check Concurrent
if maxConcurrent > 0 then
    local currentConcurrent = tonumber(redis.call('GET', concurrentKey) or '0')
    if currentConcurrent >= maxConcurrent then
        return 'CONCURRENT_LIMIT'
    end
end

-- Check Hourly
if maxHourly > 0 then
    redis.call('ZREMRANGEBYSCORE', hourlyKey, '-inf', hourlyWindowStart)
    local currentHourly = tonumber(redis.call('ZCARD', hourlyKey) or '0')
    if currentHourly >= maxHourly then
        return 'HOURLY_LIMIT'
    end
end

-- Check Daily
if maxDaily > 0 then
    redis.call('ZREMRANGEBYSCORE', dailyKey, '-inf', dailyWindowStart)
    local currentDaily = tonumber(redis.call('ZCARD', dailyKey) or '0')
    if currentDaily >= maxDaily then
        return 'DAILY_LIMIT'
    end
end

-- All checks passed, apply increments
if maxConcurrent > 0 then
    redis.call('INCR', concurrentKey)
end

if maxHourly > 0 then
    redis.call('ZADD', hourlyKey, now, token)
    redis.call('EXPIRE', hourlyKey, 3600)
end

if maxDaily > 0 then
    redis.call('ZADD', dailyKey, now, token)
    redis.call('EXPIRE', dailyKey, 86400)
end

return 'OK'
";

        private const string RevertScript = @"
local concurrentKey = KEYS[1]
local hourlyKey = KEYS[2]
local dailyKey = KEYS[3]
local token = ARGV[1]

local currentConcurrent = tonumber(redis.call('GET', concurrentKey) or '0')
if currentConcurrent > 0 then
    redis.call('DECR', concurrentKey)
end

redis.call('ZREM', hourlyKey, token)
redis.call('ZREM', dailyKey, token)
return 'OK'
";

        public async Task<RateLimitCheckResult> CheckAndAcquireAsync(string ipAddress)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var hourlyWindowStart = now - TimeSpan.FromHours(1).TotalMilliseconds;
            var dailyWindowStart = now - TimeSpan.FromDays(1).TotalMilliseconds;
            var token = Guid.NewGuid().ToString();

            var keys = new RedisKey[]
            {
                $"ratelimit:concurrent:{ipAddress}",
                $"ratelimit:hourly_sliding:{ipAddress}",
                $"ratelimit:daily_sliding:{ipAddress}"
            };

            var args = new RedisValue[]
            {
                _securitySettings.RateLimitConcurrency,
                _securitySettings.RateLimitHourly,
                _securitySettings.RateLimitDaily,
                now,
                hourlyWindowStart,
                dailyWindowStart,
                token
            };

            var result = (string?)await _redisDb.ScriptEvaluateAsync(RateLimitScript, keys, args);

            return result switch
            {
                "CONCURRENT_LIMIT" => new RateLimitCheckResult { IsAllowed = false, Reason = "Too many concurrent sessions from your IP." },
                "HOURLY_LIMIT" => new RateLimitCheckResult { IsAllowed = false, Reason = "Hourly rate limit exceeded." },
                "DAILY_LIMIT" => new RateLimitCheckResult { IsAllowed = false, Reason = "Daily rate limit exceeded." },
                _ => new RateLimitCheckResult { IsAllowed = true, RevertToken = token }
            };
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

        public async Task RevertRateLimitsAsync(string ipAddress, string revertToken)
        {
            if (string.IsNullOrEmpty(ipAddress) || string.IsNullOrEmpty(revertToken)) return;

            var keys = new RedisKey[]
            {
                $"ratelimit:concurrent:{ipAddress}",
                $"ratelimit:hourly_sliding:{ipAddress}",
                $"ratelimit:daily_sliding:{ipAddress}"
            };

            var args = new RedisValue[] { revertToken };

            await _redisDb.ScriptEvaluateAsync(RevertScript, keys, args);
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
