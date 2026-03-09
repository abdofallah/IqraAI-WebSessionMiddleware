namespace IqraAIWebSessionMiddlewareApp.Services.Interfaces
{
    public record ConcurrencyStatus(decimal Current, decimal Max);

    public interface IConcurrencyService
    {
        Task<ConcurrencyStatus> GetStatusAsync();
        Task UpdateStatusAsync(decimal current, decimal max);
        Task<long> IncrementCurrentAsync();
        Task<long> DecrementCurrentAsync();
    }
}
