using IqraAIWebSessionMiddlewareApp.Dtos;

namespace IqraAIWebSessionMiddlewareApp.Services.Interfaces
{
    public record QueueEntry(string UniqueRequestId, string IpAddress, WidgetRequestPayload Payload);

    public interface IQueueService
    {
        Task<long> EnqueueAsync(QueueEntry entry);
        Task<QueueEntry?> DequeueAsync();
        Task<long> GetQueueLengthAsync();
    }
}
