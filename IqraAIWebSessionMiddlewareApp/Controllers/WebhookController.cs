using IqraAIWebSessionMiddlewareApp.Dtos.Webhook;
using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using IqraAIWebSessionMiddlewareApp.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IqraAIWebSessionMiddlewareApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IRateLimitService _rateLimitService;
        private readonly SecuritySettings _securitySettings;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            IRateLimitService rateLimitService,
            IOptions<SecuritySettings> securitySettings,
            ILogger<WebhookController> logger)
        {
            _rateLimitService = rateLimitService;
            _securitySettings = securitySettings.Value;
            _logger = logger;
        }

        [HttpPost("session-ended")]
        public async Task<IActionResult> SessionEnded([FromBody] SessionEndedPayload payload)
        {
            var providedToken = Request.Headers["X-Api-Token"].FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(_securitySettings.WebhookApiToken) || providedToken != _securitySettings.WebhookApiToken)
            {
                _logger.LogWarning("Unauthorized webhook access attempt.");
                return Unauthorized();
            }

            if (!string.IsNullOrEmpty(payload.WebSessionId))
            {
                var ipAddress = await _rateLimitService.GetIpForSessionAsync(payload.WebSessionId);
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    await _rateLimitService.DecrementConcurrentAsync(ipAddress);
                }
            }

            return Ok();
        }
    }
}
