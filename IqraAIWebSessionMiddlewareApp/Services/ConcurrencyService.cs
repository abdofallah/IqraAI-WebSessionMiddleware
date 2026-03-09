using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using StackExchange.Redis;

namespace IqraAIWebSessionMiddlewareApp.Services
{
    public class ConcurrencyService : IConcurrencyService
    {
        private readonly IDatabase _redisDb;
        private const string CurrentKey = "concurrency:current";
        private const string MaxKey = "concurrency:max";

        public ConcurrencyService(IConnectionMultiplexer redisConnection)
        {
            // Get the underlying Redis database for atomic operations
            _redisDb = redisConnection.GetDatabase();
        }

        public async Task<ConcurrencyStatus> GetStatusAsync()
        {
            var values = await _redisDb.StringGetAsync(new RedisKey[] { CurrentKey, MaxKey });
            decimal.TryParse(values[0], out var current);
            decimal.TryParse(values[1], out var max);
            return new ConcurrencyStatus(current, max);
        }

        public async Task UpdateStatusAsync(decimal current, decimal max)
        {
            var batch = _redisDb.CreateBatch();
            batch.StringSetAsync(CurrentKey, current.ToString());
            batch.StringSetAsync(MaxKey, max.ToString());
            batch.Execute();
            await Task.CompletedTask; // CreateBatch is synchronous in execution
        }

        public Task<long> IncrementCurrentAsync()
        {
            // INCR is an atomic operation in Redis
            return _redisDb.StringIncrementAsync(CurrentKey);
        }

        public Task<long> DecrementCurrentAsync()
        {
            // DECR is an atomic operation in Redis
            return _redisDb.StringDecrementAsync(CurrentKey);
        }
    }
}
