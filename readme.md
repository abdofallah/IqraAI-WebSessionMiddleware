# Voice AI Middleware (C# ASP.NET Core)

A robust, scalable middleware solution designed to act as a secure bridge between client-side web widgets and the Voice AI Agent Platform. It handles authentication, concurrency management, user queuing, rate limiting, and security validation.

## 🌐 Official Web Client (JS Widget)

While this middleware exposes a standard REST and SignalR API allowing you to build your own custom client-side integration, we have created an official, framework-agnostic JavaScript widget that connects to this middleware out of the box. 

👉 **[Get the Iqra AI Web Widget (JavaScript SDK) here &rarr;](https://github.com/abdofallah/IqraAIWebMiddlewareJSWidget)**

---

## 🚀 Features

*   **Secure Proxy:** Hides sensitive API keys (Iqra.bot tokens) from the client-side browser.
*   **Concurrency Management:** Tracks active calls in real-time. If the concurrency limit is reached, users are automatically placed in a queue.
*   **Distributed Queueing:** Uses **Redis** and **SignalR** to manage a First-In-First-Out (FIFO) queue. Users are notified in real-time when a slot becomes available.
*   **Smart Security:**
    *   **IP Validation:** Detects and blocks VPNs, Proxies, and Tor nodes using `ipapi.is`. Can be toggled on/off, and queries can be aggressively cached to save API costs.
    *   **Rate Limiting:** Configurable concurrent, hourly and daily request limits per IP address (0 for unlimited).
*   **Scalable Architecture:** Built on .NET, Docker, and Redis, capable of running across multiple server instances (stateless API).

---

## 📋 Prerequisites

Before running the application, ensure you have the following:

1.  **Docker & Docker Compose** (Recommended for easiest deployment).
2.  *Alternative (Manual Setup):* **.NET 9 SDK** and a running **Redis Server**.
3.  **API Keys:**
    *   Voice AI Platform (Iqra.bot) API Token.
    *   IPAPI.is API Key (If using IP validation, Free tier available).

---

## ⚙️ Configuration

Configuration is managed via `appsettings.json` or Environment Variables.

### `appsettings.json` Structure

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  // CORS: Add your client domains here
  "AllowedOrigins": [
    "https://your-client-website.com",
    "http://localhost:5173" 
  ],

  // Redis Connection (e.g., "localhost:6379" or a connection string)
  "RedisConnectionString": "localhost:6379",

  // Voice AI Platform Configuration
  "VoiceAiPlatform": {
    "ApiSecretToken": "YOUR_IQRA_BOT_SECRET_TOKEN",
    "BaseUrl": "https://app.iqra.bot/api/v1/",
    "Campaigns": {
        "ExampleCampaign": {
          "BusinessId": "1",
          "WebCampaignId": "YOUR_WEB_CAMPAIGN_ID",
          "AllowedRegionIds": ["us-east-1"]
        }
    }
  },

  // IP Validation Service
  "IpApi": {
    "ApiKey": "YOUR_IPAPI_KEY",
    "BaseUrl": "https://api.ipapi.is/"
  },

  // Security & Rate Limiting Rules
  "Security": {
    "RateLimitHourly": 20,     // Max requests per IP per hour
    "RateLimitDaily": 100,     // Max requests per IP per day
    "RateLimitConcurrency": 1, // Max concurrent sessions per IP
    "EnableIpApiCheck": true,  // External IP check toggle
    "EnableIpApiCache": true,  // Cache ipapi.is results to save costs
    "IpApiCacheDurationDays": 14, // How many days to cache the IP result for
    "WebhookApiToken": "YOUR_SECRET_WEBHOOK_TOKEN",
    "BlockVpn": true,          // Reject known VPNs
    "BlockProxy": true,        // Reject known Proxies
    "BlockDatacenter": false   // Reject Datacenter IPs (AWS, Azure, etc.)
  }
}
```

---

## 🏗 Architecture Overview

1.  **Request Flow:**
    *   The Client Widget sends a POST request to `/api/session/request`.
    *   Middleware checks Redis for Rate Limits.
    *   Middleware validates IP against `ipapi.is`.
    *   Middleware checks current concurrency against `Iqra.bot` limits.

2.  **Concurrency Handling:**
    *   **If Slot Available:** The middleware requests a WebSocket URL from the AI Platform and returns it to the client immediately.
    *   **If Full:** The user is added to a **Redis List** (Queue). The client connects to a **SignalR Hub** and waits.

3.  **Queue Processing:**
    *   When a call finishes, the AI Platform sends a webhook to `/api/webhook/session-ended`.
    *   The middleware processes the queue, takes the next user, initiates a session for them, and sends the WebSocket URL via SignalR.

---

## 🔌 API Reference

For a complete breakdown of payloads, endpoints, WebSockets, error codes, and SignalR Queue integration for building custom clients:
👉 **[Read the API Reference here &rarr;](readme-api-reference.md)**

---

## 🚀 Deployment

### 1. Docker Compose (Recommended)
The official Docker image is automatically built and published to Docker Hub at `abdofallah/iqraai-websessionmiddleware:latest`.

The easiest way to deploy the middleware and its required Redis instance is using the included `docker-compose.yml` file.

1. Download or clone the repository.
2. Create a copy of the `appsettings.json.example` and edit to match your configuration.
2. Edit the `/host/path/to/appsettings.json` path in `docker-compose.yml` to match the path to your `appsettings.json` file.
3. Run the following command in the terminal:
   ```bash
   docker-compose up -d
   ```
The middleware will now be running on port `8080`.

### 2. Manual Deployment (Bare Metal / IIS)
If you prefer not to use Docker, you can publish the binaries directly:
```bash
dotnet publish -c Release -o ./publish
```
*Note: Ensure you have a Redis instance running locally or remotely, and update your `appsettings.json` with the `RedisConnectionString`.*

### 3. Webhook Configuration (Crucial)
Ensure your Voice AI Platform (Iqra.bot) is configured to send the "End Call" webhook to your deployed middleware URL so the queue can advance and each ip concurrency is lowered after conversation ends:
1. Create a tool with the following configuration:
Endpoint: `https://your-middleware-domain.com/api/webhook/session-ended`
Request Type: Post
Input Schema: Create a string argurment with id `conversationsessionid` with requried checked, and name/description of your choice.
Body:
```
{
  "ConversationSessionId": "{{conversationsessionid}}"
}
```
2. In your web campaign action:
  a. Add the tool to the "End Call" webhook action
  b. Include the `conversationsessionid` arguement and map it to the `Conversation Session Id`

This will ensure when a conversation ends, a webhook is sent to the middleware with the conversation session id so the middleware knows to clear the local concurrency of that session.

---

## 🧪 Troubleshooting

*   **Redis Connection Error:** Ensure Redis is running and the `RedisConnectionString` is correct. If using Docker Compose, the connection string should simply be `redis:6379`.
*   **CORS Errors:** If the widget fails to connect, check the `AllowedOrigins` array in `appsettings.json` (or the `AllowedOrigins__0` environment variable). It must include the exact domain where the widget is hosted.
*   **Concurrency Issues:** If users are stuck in the queue, ensure the **Webhook** is correctly configured on the Iqra dashboard and reaching the middleware successfully.