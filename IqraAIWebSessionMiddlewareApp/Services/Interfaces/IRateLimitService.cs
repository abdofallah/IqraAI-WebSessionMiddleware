using IqraAIWebSessionMiddlewareApp.Settings;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace IqraAIWebSessionMiddlewareApp.Services.Interfaces
{
    public interface IRateLimitService
    {
        Task<RateLimitCheckResult> CheckAndAcquireAsync(string ipAddress);
        Task DecrementConcurrentAsync(string ipAddress);
        Task MapSessionToIpAsync(string sessionId, string ipAddress);
        Task<string?> GetIpForSessionAsync(string sessionId);
    }

    public class RateLimitCheckResult
    {
        public bool IsAllowed { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
