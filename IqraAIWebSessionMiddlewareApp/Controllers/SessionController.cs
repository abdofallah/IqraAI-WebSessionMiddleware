using IqraAIWebSessionMiddlewareApp.Dtos;
using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using IqraAIWebSessionMiddlewareApp.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IqraAIWebSessionMiddlewareApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private readonly IIpInfoService _ipInfoService;
        private readonly IConcurrencyService _concurrencyService;
        private readonly IVoiceAiPlatformService _voiceAiPlatformService;
        private readonly IQueueService _queueService;
        private readonly ILogger<SessionController> _logger;
        private readonly VoiceAiPlatformSettings _platformSettings;

        public SessionController(
            IIpInfoService ipInfoService,
            IConcurrencyService concurrencyService,
            IVoiceAiPlatformService voiceAiPlatformService,
            IQueueService queueService,
            IOptions<VoiceAiPlatformSettings> platformSettings,
            ILogger<SessionController> logger)
        {
            _ipInfoService = ipInfoService;
            _concurrencyService = concurrencyService;
            _voiceAiPlatformService = voiceAiPlatformService;
            _queueService = queueService;
            _platformSettings = platformSettings.Value;
            _logger = logger;
        }

        [HttpPost("request")]
        public async Task<IActionResult> RequestSession([FromBody] WidgetRequestPayload payload)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ipAddress))
            {
                return BadRequest(new { message = "Could not determine client IP address." });
            }

            var ipValidationResult = await _ipInfoService.ValidateIpAsync(ipAddress);
            if (!ipValidationResult.IsValid)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ipValidationResult.Reason });
            }

            // Concurrency Check
            var status = await _concurrencyService.GetStatusAsync();
            if (status.Current >= status.Max)
            {
                var uniqueRequestId = Guid.NewGuid().ToString();
                var queueEntry = new QueueEntry(uniqueRequestId, payload);

                var queuePosition = await _queueService.EnqueueAsync(queueEntry);
                _logger.LogInformation("Concurrency full. Added request {RequestId} to queue at position {Position}.", uniqueRequestId, queuePosition);

                return Accepted(new
                {
                    status = "queued",
                    uniqueRequestId = uniqueRequestId,
                    queuePosition = queuePosition
                });
            }

            try
            {
                var config = new WebSessionConfig(
                    TransportType: payload.TransportType,
                    WebCampaignId: _platformSettings.WebCampaignId,
                    RegionId: _platformSettings.DefaultRegionId,
                    ClientIdentifier: string.IsNullOrEmpty(payload.ClientIdentifier) ? ipAddress : $"{payload.ClientIdentifier}_{ipAddress}",
                    DynamicVariables: payload.DynamicVariables,
                    Metadata: payload.Metadata,
                    new WebSessionAudioConfiguration(
                        InputEncodingType: payload.AudioConfiguration.InputEncodingType,
                        InputSampleRate: payload.AudioConfiguration.InputSampleRate,
                        InputBitsPerSample: payload.AudioConfiguration.InputBitsPerSample,
                        OutputEncodingType: payload.AudioConfiguration.OutputEncodingType,
                        OutputSampleRate: payload.AudioConfiguration.OutputSampleRate,
                        OutputBitsPerSample: payload.AudioConfiguration.OutputBitsPerSample
                    )
                );

                var webSocketUrl = await _voiceAiPlatformService.InitiateWebSessionAsync(config);
                await _concurrencyService.IncrementCurrentAsync();
                return Ok(new { webSocketUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating web session directly.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while trying to initiate the session." });
            }
        }
    }
}
