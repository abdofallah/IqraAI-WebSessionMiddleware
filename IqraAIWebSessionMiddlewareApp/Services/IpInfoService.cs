using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using IqraAIWebSessionMiddlewareApp.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IqraAIWebSessionMiddlewareApp.Services
{
    public class IpInfoService : IIpInfoService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDistributedCache _cache;
        private readonly IpApiSettings _ipApiSettings;
        private readonly SecuritySettings _securitySettings;

        public IpInfoService(
            IHttpClientFactory httpClientFactory,
            IDistributedCache cache,
            IOptions<IpApiSettings> ipApiSettings,
            IOptions<SecuritySettings> securitySettings)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _ipApiSettings = ipApiSettings.Value;
            _securitySettings = securitySettings.Value;
        }

        public async Task<IpValidationResult> ValidateIpAsync(string ipAddress)
        {
            // For local development, often the IP is ::1 which is not valid for lookup.
            if (ipAddress == "::1")
            {
                return new IpValidationResult(true, "Local development IP.");
            }

            string cacheKey = $"ipinfo:{ipAddress}";
            var cachedResult = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                // Found in cache, return the deserialized result
                return JsonSerializer.Deserialize<IpValidationResult>(cachedResult)
                    ?? new IpValidationResult(false, "Failed to deserialize cached IP info.");
            }

            // Not in cache, call the external API
            var client = _httpClientFactory.CreateClient();
            var requestUrl = $"{_ipApiSettings.BaseUrl}?q={ipAddress}&key={_ipApiSettings.ApiKey}";

            try
            {
                var response = await client.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                // Check against our security settings
                if (_securitySettings.BlockVpn && root.TryGetProperty("is_vpn", out var isVpn) && isVpn.GetBoolean())
                    return await CacheAndReturn(cacheKey, new IpValidationResult(false, "VPN detected."));

                if (_securitySettings.BlockProxy && root.TryGetProperty("is_proxy", out var isProxy) && isProxy.GetBoolean())
                    return await CacheAndReturn(cacheKey, new IpValidationResult(false, "Proxy detected."));

                if (_securitySettings.BlockDatacenter && root.TryGetProperty("is_datacenter", out var isDc) && isDc.GetBoolean())
                    return await CacheAndReturn(cacheKey, new IpValidationResult(false, "Datacenter IP detected."));

                // If all checks pass
                return await CacheAndReturn(cacheKey, new IpValidationResult(true, "IP is valid."));
            }
            catch (Exception ex)
            {
                // If the API fails, we can choose to either allow or deny the request.
                // For now, we'll log it and deny to be safe.
                Console.WriteLine($"Error validating IP {ipAddress}: {ex.Message}");
                return new IpValidationResult(false, "Could not verify IP address.");
            }
        }

        private async Task<IpValidationResult> CacheAndReturn(string key, IpValidationResult result)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                // Cache the result for 24 hours to respect API limits
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(result), cacheOptions);
            return result;
        }
    }
}
