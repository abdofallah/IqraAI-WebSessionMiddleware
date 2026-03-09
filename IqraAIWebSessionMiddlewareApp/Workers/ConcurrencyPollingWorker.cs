using IqraAIWebSessionMiddlewareApp.Services.Interfaces;

namespace IqraAIWebSessionMiddlewareApp.Workers
{
    public class ConcurrencyPollingWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConcurrencyPollingWorker> _logger;

        public ConcurrencyPollingWorker(IServiceProvider serviceProvider, ILogger<ConcurrencyPollingWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Concurrency Polling Worker starting.");

            // Wait a few seconds on startup before the first poll
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Create a scope to resolve scoped services like IVoiceAiPlatformService
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var voiceAiService = scope.ServiceProvider.GetRequiredService<IVoiceAiPlatformService>();
                        var concurrencyService = scope.ServiceProvider.GetRequiredService<IConcurrencyService>();

                        var (current, max) = await voiceAiService.GetConcurrencyDataAsync();
                        await concurrencyService.UpdateStatusAsync(current, max);

                        _logger.LogInformation("Successfully polled and updated concurrency: {Current}/{Max}", current, max);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while polling for concurrency data.");
                }

                // Wait for 15 seconds before the next poll
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }

            _logger.LogInformation("Concurrency Polling Worker stopping.");
        }
    }
}
