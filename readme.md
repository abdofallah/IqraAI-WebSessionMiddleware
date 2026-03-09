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
    *   **IP Validation:** Detects and blocks VPNs, Proxies, and Tor nodes using `ipapi.is`.
    *   **Rate Limiting:** configurable hourly and daily request limits per IP address.
*   **Scalable Architecture:** Built on .NET and Redis, capable of running across multiple server instances (stateless API).

---

## 📋 Prerequisites

Before running the application, ensure you have the following installed:

1.  **.NET SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/98.0))
2.  **Redis Server** (Required for caching, queueing, and distributed locking).
    *   *Local:* `docker run --name redis-dev -p 6379:6379 -d redis`
3.  **API Keys:**
    *   Voice AI Platform (Iqra.bot) API Token.
    *   IPAPI.is API Key (Free tier available).

---

## ⚙️ Configuration

All configuration is managed via `appsettings.json`. You must configure these values before starting the application.

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
    "BusinessId": "1",
    "WebCampaignId": "YOUR_WEB_CAMPAIGN_ID",
    "DefaultRegionId": "us-east-1"
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

### 1. Request a Session
**Endpoint:** `POST /api/session/request`  
**Description:** Main entry point for the widget.

**Request Body:**
```json
{
  "clientIdentifier": "user-123",
  "dynamicVariables": {
    "FirstName": "John"
  },
  "metadata": {
    "Source": "Landing Page"
  }
}
```

**Response (Success - 200 OK):**
```json
{
  "webSocketUrl": "wss://voice-api.iqra.bot/..."
}
```

**Response (Queued - 202 Accepted):**
```json
{
  "status": "queued",
  "uniqueRequestId": "guid-string",
  "queuePosition": 5
}
```

### 2. Webhook (Session Ended)
**Endpoint:** `POST /api/webhook/session-ended`  
**Description:** Callback URL configured in the Voice AI Platform. Triggered when a call ends to free up a slot.

### 3. Queue Real-time Hub
**Endpoint:** `/sessionHub` (SignalR)  
**Description:** WebSocket endpoint for queued clients to listen for updates.

---

## 🚀 Deployment

### 1. Publish the Application
Run the following command to generate the production binaries:
```bash
dotnet publish -c Release -o ./publish
```

### 2. Environment Variables
In a production environment (like Azure App Service, Docker, or Linux), it is recommended to override sensitive `appsettings.json` values using Environment Variables:

*   `VoiceAiPlatform__ApiSecretToken`
*   `IpApi__ApiKey`
*   `RedisConnectionString`

### 3. Webhook Configuration
Ensure your Voice AI Platform (Iqra.bot) is configured to send the "End Call" webhook to your deployed middleware URL:
`https://your-middleware-domain.com/api/webhook/session-ended`

---

## 🧪 Troubleshooting

*   **Redis Connection Error:** Ensure Redis is running and the `RedisConnectionString` is correct. If using Docker, ensure the middleware container can reach the Redis container.
*   **CORS Errors:** If the widget fails to connect, check the `AllowedOrigins` array in `appsettings.json`. It must include the domain where the widget is hosted.
*   **Concurrency Issues:** If users are stuck in the queue, ensure the **Webhook** is correctly configured and reaching the middleware. The queue only moves when the webhook signals that a call has ended.