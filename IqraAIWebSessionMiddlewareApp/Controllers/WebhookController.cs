using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IqraAIWebSessionMiddlewareApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IQueueProcessor _queueProcessor;
        private readonly IConcurrencyService _concurrencyService;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(IQueueProcessor queueProcessor, IConcurrencyService concurrencyService, ILogger<WebhookController> logger)
        {
            _queueProcessor = queueProcessor;
            _concurrencyService = concurrencyService;
            _logger = logger;
        }

        // This endpoint should be secured, e.g., by checking a secret token from the header
        // For now, we'll keep it simple.
        [HttpPost("session-ended")]
        public async Task<IActionResult> SessionEnded([FromBody] object payload) // Define a proper payload model later
        {
            _logger.LogInformation("Received session-ended webhook.");

            // It's crucial to decrement our counter when a session ends.
            await _concurrencyService.DecrementCurrentAsync();

            // Now, trigger the queue processor to see if a waiting user can take the freed slot.
            _ = _queueProcessor.ProcessNextInQueueAsync();

            // Return 200 OK immediately. Don't wait for the queue processor to finish.
            // The `_ =` syntax starts the task but doesn't wait for it to complete.
            return Ok();
        }
    }
}
