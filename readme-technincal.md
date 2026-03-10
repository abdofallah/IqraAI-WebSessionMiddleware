# Voice AI Middleware Core SDK - Technician Guide

## 📚 Overview

This documentation is intended for .NET developers who wish to consume the **Voice AI Middleware logic** directly within their own ASP.NET Core applications or Worker Services, rather than deploying the middleware as a standalone API.

The core business logic is decoupled from the HTTP Controllers, allowing you to use the **Services Layer** as an internal SDK.

## 📦 Integration Prerequisites

To integrate the SDK into your .NET 8 project:

1.  **Dependencies:** Ensure your project references the following NuGet packages:
    *   `Microsoft.Extensions.Caching.StackExchangeRedis`
    *   `StackExchange.Redis`
    *   `RedLock.net`
    *   `RedLock.net.SERedis`
    *   `Microsoft.Extensions.Http`
    *   `Microsoft.AspNetCore.SignalR.Client` (Optional, if using client-side features)

2.  **Source Code:** Copy the following folders from the Middleware solution into your project:
    *   `/Services` (Contains all business logic)
    *   `/Settings` (Contains configuration models)
    *   `/Hubs` (If you need the real-time signaling)

---

## 🔧 Dependency Injection Setup

To use the services, you must register them in your `Program.cs`.

```csharp
using VoiceAiMiddleware.Services;
using VoiceAiMiddleware.Settings;
using StackExchange.Redis;

// 1. Configure Settings Injection
builder.Services.Configure<VoiceAiPlatformSettings>(builder.Configuration.GetSection("VoiceAiPlatform"));
builder.Services.Configure<IpApiSettings>(builder.Configuration.GetSection("IpApi"));
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));

// 2. Configure Redis (Required for Concurrency & Queuing)
var redisConn = builder.Configuration.GetValue<string>("RedisConnectionString");
var muxer = ConnectionMultiplexer.Connect(redisConn);
builder.Services.AddSingleton<IConnectionMultiplexer>(muxer);
builder.Services.AddStackExchangeRedisCache(o => {
    o.Configuration = redisConn;
    o.InstanceName = "VoiceAiSdk_";
});

// 3. Register Core Services
builder.Services.AddHttpClient("VoiceAiClient"); // Named client for platform communication
builder.Services.AddScoped<IIpInfoService, IpInfoService>();
builder.Services.AddScoped<IVoiceAiPlatformService, VoiceAiPlatformService>();
builder.Services.AddSingleton<IConcurrencyService, ConcurrencyService>(); // Must be Singleton
builder.Services.AddSingleton<IQueueService, QueueService>();
builder.Services.AddScoped<IQueueProcessor, QueueProcessor>();
```

---

## 🛠 Service Reference

### 1. `IVoiceAiPlatformService`
*The primary bridge to the Voice AI Platform API.*

**Use this service to:** Initiate calls directly and fetch billing/usage data.

```csharp
public interface IVoiceAiPlatformService
{
    // Fetches current active calls and max allowed concurrency from the platform
    Task<(int Current, int Max)> GetConcurrencyDataAsync();

    // Initiates a session and returns the WebSocket URL
    Task<string> InitiateWebSessionAsync(WebSessionConfig config);
}
```

**Configuration Object (`WebSessionConfig`):**
```csharp
public record WebSessionConfig(
    VoiceAiWebSessionTransportTypeEnum TransportType,
    string BusinessId,
    string WebCampaignId,
    string RegionId,
    string? ClientIdentifier,
    Dictionary<string, string> DynamicVariables,
    Dictionary<string, string> Metadata,
    WebSessionAudioConfiguration AudioConfiguration
);
```

### 2. `IConcurrencyService`
*Manages the distributed state of active calls using Redis.*

**Use this service to:** Check if a slot is available before attempting a call, or to manually increment/decrement counters if you are building custom logic.

```csharp
public interface IConcurrencyService
{
    // Gets the cached local snapshot of concurrency
    Task<ConcurrencyStatus> GetStatusAsync();

    // Atomic increment (Call when a session starts)
    Task<long> IncrementCurrentAsync();

    // Atomic decrement (Call when a session ends)
    Task<long> DecrementCurrentAsync();
}
```

### 3. `IIpInfoService`
*Validates IP addresses against security rules.*

**Use this service to:** Pre-validate a user request before processing it.

```csharp
public interface IIpInfoService
{
    // Returns true if IP is clean. Returns false if VPN/Proxy/Datacenter detected.
    Task<IpValidationResult> ValidateIpAsync(string ipAddress);
}
```

### 4. `IQueueService`
*Manages the FIFO queue logic.*

**Use this service to:** Add users to the waiting line or check queue depth.

```csharp
public interface IQueueService
{
    // Adds a user request to the tail of the Redis list
    Task<long> EnqueueAsync(QueueEntry entry);

    // Removes and returns the user request from the head of the Redis list
    Task<QueueEntry?> DequeueAsync();

    // Gets the total number of waiting users
    Task<long> GetQueueLengthAsync();
}
```

---

## 💻 Code Example: Implementing a Custom Controller

If you are importing this SDK into your own custom application, here is how you would use it in a Controller.

```csharp
[ApiController]
[Route("my-custom-api")]
public class CustomVoiceController : ControllerBase
{
    private readonly IVoiceAiPlatformService _voiceService;
    private readonly IConcurrencyService _concurrency;

    // Inject the SDK services
    public CustomVoiceController(
        IVoiceAiPlatformService voiceService, 
        IConcurrencyService concurrency)
    {
        _voiceService = voiceService;
        _concurrency = concurrency;
    }

    [HttpPost("start-call")]
    public async Task<IActionResult> StartCall([FromBody] MyRequestModel model)
    {
        // 1. Check Logic
        var status = await _concurrency.GetStatusAsync();
        if (status.Current >= status.Max) 
        {
            return StatusCode(503, "System busy");
        }

        // 2. Prepare Config
        var config = new WebSessionConfig(
            TransportType: VoiceAiWebSessionTransportTypeEnum.WebSocket,
            BusinessId: "your-business-id",
            WebCampaignId: "your-campaign-id",
            RegionId: "us-east-1",
            ClientIdentifier: model.UserId,
            DynamicVariables: new Dictionary<string, string> { { "Name", model.UserName } },
            Metadata: new Dictionary<string, string>(),
            AudioConfiguration: new WebSessionAudioConfiguration(...)
        );

        // 3. Call Platform
        try {
            var (sessionId, url) = await _voiceService.InitiateWebSessionAsync(config);
            
            // 4. Update Concurrency State
            await _concurrency.IncrementCurrentAsync();
            
            return Ok(new { url });
        }
        catch(Exception ex) {
            return StatusCode(500, ex.Message);
        }
    }
}
```

---

## ⚙️ Required Configuration Settings

You must add the following sections to your project's `appsettings.json` for the services to function correctly.

```json
{
  "RedisConnectionString": "localhost:6379",
  "VoiceAiPlatform": {
    "ApiSecretToken": "...",
    "BaseUrl": "https://app.iqra.bot/api/v1/",
    "Campaigns": {
        "Default": {
          "BusinessId": "...",
          "WebCampaignId": "...",
          "AllowedRegionIds": ["us-east-1"]
        }
    }
  },
  "IpApi": {
    "ApiKey": "...",
    "BaseUrl": "https://api.ipapi.is/"
  },
  "Security": {
    "RateLimitHourly": 20,
    "RateLimitDaily": 100,
    "RateLimitConcurrency": 1,
    "EnableIpApiCheck": true,
    "EnableIpApiCache": true,
    "IpApiCacheDurationDays": 14,
    "WebhookApiToken": "YOUR_SECRET_WEBHOOK_TOKEN",
    "BlockVpn": true,
    "BlockProxy": true,
    "BlockDatacenter": false
  }
}
```