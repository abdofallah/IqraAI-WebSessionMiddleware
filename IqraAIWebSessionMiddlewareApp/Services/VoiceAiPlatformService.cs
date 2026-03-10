using IqraAIWebSessionMiddlewareApp.Dtos.Enums;
using IqraAIWebSessionMiddlewareApp.Dtos.VoiceAi;
using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using IqraAIWebSessionMiddlewareApp.Settings;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace IqraAIWebSessionMiddlewareApp.Services
{
    public class VoiceAiPlatformService : IVoiceAiPlatformService
    {
        private readonly HttpClient _httpClient;
        private readonly VoiceAiPlatformSettings _settings;

        public VoiceAiPlatformService(IHttpClientFactory httpClientFactory, IOptions<VoiceAiPlatformSettings> settings)
        {
            _settings = settings.Value;
            _httpClient = httpClientFactory.CreateClient("VoiceAiClient");
        }

        public async Task<(decimal Current, decimal Max)> GetConcurrencyDataAsync()
        {
            // Concurrently fetch both current usage and max features
            var usageTask = GetCurrentUsageAsync();
            var featuresTask = GetActiveFeaturesAsync();

            await Task.WhenAll(usageTask, featuresTask);

            var currentUsage = usageTask.Result;
            var maxFeatures = featuresTask.Result;

            // Safely extract "Call_Concurrency" value, defaulting to 0 if not found
            currentUsage.TryGetValue("Call_Concurrency", out var currentConcurrency);
            maxFeatures.TryGetValue("Call_Concurrency", out var maxConcurrency);

            return ((decimal)currentConcurrency, (decimal)maxConcurrency);
        }

        public async Task<(string SessionId, string WebSocketUrl)> InitiateWebSessionAsync(WebSessionConfig config)
        {
            var requestUri = $"business/{config.BusinessId}/websession/initiate";

            var requestData = new VoiceAiInitiateWebSessionRequest
            {
                TransportType = config.TransportType,
                WebCampaignId = config.WebCampaignId,
                RegionId = config.RegionId,
                ClientIdentifier = config.ClientIdentifier ?? "Unknown",
                DynamicVariables = config.DynamicVariables,
                Metadata = config.Metadata,
                AudioInputConfiguration = new()
                {
                    AudioEncodingType = config.AudioConfiguration.InputEncodingType,
                    SampleRate = config.AudioConfiguration.InputSampleRate,
                    BitsPerSample = config.AudioConfiguration.InputBitsPerSample
                },
                AudioOutputConfiguration = new()
                {
                    AudioEncodingType = config.AudioConfiguration.OutputEncodingType,
                    SampleRate = config.AudioConfiguration.OutputSampleRate,
                    BitsPerSample = config.AudioConfiguration.OutputBitsPerSample,
                    FrameDurationMs = 30
                }
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(requestUri, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Api call failed with status code {response.StatusCode}: {errorResponse}");
            }

            var responseData = await response.Content.ReadFromJsonAsync<VoiceAiFunctionReturnResult<VoiceAiInitiateWebSessionResponse>>();
            if (responseData == null)
            {
                throw new InvalidOperationException("Failed to deserialize API response.");
            }

            if (!responseData.Success)
            {
                throw new InvalidOperationException($"[{responseData.Code}] {responseData.Message}");
            }

            return (responseData.Data!.SessionId, responseData.Data!.SessionWebSocketURL);
        }

        private async Task<Dictionary<string, decimal>> GetCurrentUsageAsync()
        {
            var response = await _httpClient.GetAsync("user/billing/usage");
            response.EnsureSuccessStatusCode();
            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            if (doc.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                throw new Exception($"Error Occured when retrieving current usage: {doc.RootElement.GetProperty("message").GetString()}");
            }

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("currentUsage", out var currentUsage) &&
                currentUsage.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<Dictionary<string, decimal>>(currentUsage.GetRawText()) ?? new();
            }

            return new Dictionary<string, decimal>();
        }

        private async Task<Dictionary<string, decimal>> GetActiveFeaturesAsync()
        {
            var response = await _httpClient.GetAsync("user/billing/featuresactivequantity");
            response.EnsureSuccessStatusCode();
            var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            if (doc.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                throw new Exception($"Error Occured when retrieving current active features: {doc.RootElement.GetProperty("message").GetString()}");
            }

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object)
            {
                return JsonSerializer.Deserialize<Dictionary<string, decimal>>(data.GetRawText()) ?? new();
            }
            return new Dictionary<string, decimal>();
        }
    }
}
