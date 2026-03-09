using IqraAIWebSessionMiddlewareApp.Hubs;
using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using IqraAIWebSessionMiddlewareApp.Settings;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace IqraAIWebSessionMiddlewareApp.Services
{
    public class QueueProcessor : IQueueProcessor
    {
        private readonly IQueueService _queueService;
        private readonly IVoiceAiPlatformService _voiceAiPlatformService;
        private readonly IConcurrencyService _concurrencyService;
        private readonly IRateLimitService _rateLimitService;
        private readonly IHubContext<SessionHub> _hubContext;
        private readonly ILogger<QueueProcessor> _logger;
        private readonly RedLockFactory _redLockFactory;
        private readonly VoiceAiPlatformSettings _platformSettings;

        public QueueProcessor(
            IQueueService queueService,
            IVoiceAiPlatformService voiceAiPlatformService,
            IConcurrencyService concurrencyService,
            IRateLimitService rateLimitService,
            IHubContext<SessionHub> hubContext,
            ILogger<QueueProcessor> logger,
            IConnectionMultiplexer redisConnection,
            IOptions<VoiceAiPlatformSettings> platformSettings)
        {
            _queueService = queueService;
            _voiceAiPlatformService = voiceAiPlatformService;
            _concurrencyService = concurrencyService;
            _rateLimitService = rateLimitService;
            _hubContext = hubContext;
            _logger = logger;
            _platformSettings = platformSettings.Value;

            // Initialize the distributed lock factory
            _redLockFactory = RedLockFactory.Create(new List<RedLockMultiplexer> { new RedLockMultiplexer(redisConnection) });
        }

        public async Task ProcessNextInQueueAsync()
        {
            // Use a distributed lock to ensure only one instance of this processor
            // runs at a time across all servers.
            var lockKey = "queue-processor-lock";
            var expiry = TimeSpan.FromSeconds(10);
            var wait = TimeSpan.FromSeconds(1);
            var retry = TimeSpan.FromSeconds(1);

            await using (var redLock = await _redLockFactory.CreateLockAsync(lockKey, expiry, wait, retry))
            {
                if (!redLock.IsAcquired)
                {
                    _logger.LogWarning("Could not acquire queue processor lock. Another process is likely running.");
                    return;
                }

                var (current, max) = await _voiceAiPlatformService.GetConcurrencyDataAsync();

                if (current >= max)
                {
                    _logger.LogInformation("Queue processor ran, but no concurrency slots are available ({Current}/{Max}).", current, max);
                    return;
                }

                var queueEntry = await _queueService.DequeueAsync();
                if (queueEntry == null)
                {
                    _logger.LogInformation("Queue processor ran, but the queue is empty.");
                    return;
                }

                _logger.LogInformation("Processing request {RequestId} from queue.", queueEntry.UniqueRequestId);

                try
                {
                    if (string.IsNullOrEmpty(queueEntry.Payload.CampaignId) || !_platformSettings.Campaigns.TryGetValue(queueEntry.Payload.CampaignId, out var campaignConfig))
                    {
                        throw new InvalidOperationException($"Invalid or missing CampaignId: {queueEntry.Payload.CampaignId}");
                    }

                    var config = new WebSessionConfig(
                        TransportType: queueEntry.Payload.TransportType,
                        BusinessId: campaignConfig.BusinessId,
                        WebCampaignId: campaignConfig.WebCampaignId,
                        RegionId: queueEntry.Payload.RegionId,
                        ClientIdentifier: string.IsNullOrEmpty(queueEntry.Payload.ClientIdentifier) ? queueEntry.IpAddress : $"{queueEntry.Payload.ClientIdentifier}_{queueEntry.IpAddress}",
                        DynamicVariables: queueEntry.Payload.DynamicVariables,
                        Metadata: queueEntry.Payload.Metadata,
                        new WebSessionAudioConfiguration(
                            InputEncodingType: queueEntry.Payload.AudioConfiguration.InputEncodingType,
                            InputSampleRate: queueEntry.Payload.AudioConfiguration.InputSampleRate,
                            InputBitsPerSample: queueEntry.Payload.AudioConfiguration.InputBitsPerSample,
                            OutputEncodingType: queueEntry.Payload.AudioConfiguration.OutputEncodingType,
                            OutputSampleRate: queueEntry.Payload.AudioConfiguration.OutputSampleRate,
                            OutputBitsPerSample: queueEntry.Payload.AudioConfiguration.OutputBitsPerSample
                        )
                    );

                    var sessionResult = await _voiceAiPlatformService.InitiateWebSessionAsync(config);
                    
                    await _rateLimitService.MapSessionToIpAsync(sessionResult.SessionId, queueEntry.IpAddress);

                    // Notify the specific waiting client via SignalR
                    await _hubContext.Clients.Group(queueEntry.UniqueRequestId)
                                     .SendAsync("SessionReady", new { webSocketUrl = sessionResult.WebSocketUrl });

                    _logger.LogInformation("Successfully processed and sent WebSocket URL to client for request {RequestId}", queueEntry.UniqueRequestId);

                    // Increment our internal tracker
                    await _concurrencyService.IncrementCurrentAsync();
                }
                catch (Exception ex)
                {
                    // If queue processing errors out before mapping, we need to revert the IP rate limit currency
                    await _rateLimitService.DecrementConcurrentAsync(queueEntry.IpAddress);

                    _logger.LogError(ex, "Failed to process request {RequestId} from queue.", queueEntry.UniqueRequestId);

                    // Notify the client that something went wrong so they aren't stuck waiting
                    await _hubContext.Clients.Group(queueEntry.UniqueRequestId)
                                     .SendAsync("SessionFailed", new { message = "An error occurred while creating your session. Please try again." });
                }
            }
        }
    }
}
