using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace IqraAIWebSessionMiddlewareApp.Services
{
    public class QueueService : IQueueService
    {
        private readonly IDatabase _redisDb;
        private const string QueueKey = "session-request-queue";

        public QueueService(IConnectionMultiplexer redis)
        {
            _redisDb = redis.GetDatabase();
        }

        public async Task<long> EnqueueAsync(QueueEntry entry)
        {
            var serializedEntry = JsonSerializer.Serialize(entry);
            // LPUSH adds to the "left" or head of the list.
            return await _redisDb.ListLeftPushAsync(QueueKey, serializedEntry);
        }

        public async Task RequeueAsync(QueueEntry entry)
        {
            var serializedEntry = JsonSerializer.Serialize(entry);
            // RPUSH adds to the "right" or tail of the list, placing it at the front of the queue to be DEQUEUED next.
            await _redisDb.ListRightPushAsync(QueueKey, serializedEntry);
        }

        public async Task<QueueEntry?> DequeueAsync()
        {
            // RPOP removes from the "right" or tail of the list, creating a FIFO queue.
            var redisValue = await _redisDb.ListRightPopAsync(QueueKey);
            if (redisValue.IsNullOrEmpty)
            {
                return null;
            }
            return JsonSerializer.Deserialize<QueueEntry>(redisValue.ToString());
        }

        public Task<long> GetQueueLengthAsync()
        {
            return _redisDb.ListLengthAsync(QueueKey);
        }
    }
}
