# Iqra AI Web Session Middleware - API Reference

This document provides a detailed technical API reference for developers building custom client-side integrations (e.g., custom web widgets, mobile apps) that connect to the Iqra AI Web Session Middleware.

---

## 🔗 Base URL
All requests should be made to the root URL where this middleware is deployed.
Example: `https://your-middleware-domain.com/`

---

## 1. Request a Session 

**Endpoint:** `POST /api/session/request`  
**Description:** This is the primary entry point for a client to request a voice AI session. The middleware handles rate limiting, IP security (VPN/Proxy checks), and concurrency queuing before issuing a session.

### Request Payload

The request body must be a JSON object containing the `WidgetRequestPayload`:

| Field | Type | Required | Description |
| :--- | :--- | :--- | :--- |
| `campaignId` | `string` | **Yes** | The ID of the campaign configured in the middleware's `appsettings.json` (e.g., `"IqraWebsiteCampaign"`). |
| `regionId` | `string` | **Yes** | The geographic region to route the call through (e.g., `"EU-DE"`, `"us-east-1"`). Must be allowed by the campaign configuration. |
| `transportType` | `integer` | **Yes** | Enum value defining the transport type. Typically `0` for WebSocket (`VoiceAiWebSessionTransportTypeEnum.WebSocket`). |
| `clientIdentifier` | `string` | No | An optional unique identifier for the user (e.g., user ID, session cookie). If null, the middleware falls back to tracking the user's IP address. |
| `dynamicVariables` | `object` | **Yes** | A key-value dictionary of strings to inject runtime variables into the AI prompt (e.g., `{"FirstName": "John", "AccountType": "Premium"}`). If none, pass an empty object `{}`. |
| `metadata` | `object` | **Yes** | A key-value dictionary of strings to attach tracking/metadata to the call record. If none, pass an empty object `{}`. |
| `audioConfiguration` | `object` | **Yes** | Codec and sampling rates for the audio streams. See structure below. |

#### `audioConfiguration` Structure

| Field | Type | Description |
| :--- | :--- | :--- |
| `inputEncodingType` | `integer` | Enum value for Input Encoding. Typically `1` for PCM / Linear16. |
| `inputSampleRate` | `integer` | Usually `16000` or `24000` Hz. |
| `inputBitsPerSample` | `integer` | Usually `16` bits. |
| `outputEncodingType` | `integer` | Enum value for Output Encoding. Typically `1` for PCM / Linear16. |
| `outputSampleRate` | `integer` | Usually `16000` or `24000` Hz. |
| `outputBitsPerSample` | `integer` | Usually `16` bits. |

**Example Request:**
```json
{
  "campaignId": "IqraWebsiteCampaign",
  "regionId": "EU-DE",
  "transportType": 0,
  "clientIdentifier": "user-abcdef-12345",
  "dynamicVariables": {
    "userName": "Abdullah",
    "language": "en"
  },
  "metadata": {
    "source": "homepage_widget"
  },
  "audioConfiguration": {
    "inputEncodingType": 1,
    "inputSampleRate": 16000,
    "inputBitsPerSample": 16,
    "outputEncodingType": 1,
    "outputSampleRate": 16000,
    "outputBitsPerSample": 16
  }
}
```

### Responses

#### 🟢 200 OK (Session Immediately Available)
If there is available concurrency, the middleware instantly acquires a session from the AI platform and returns a WebSocket URL.

```json
{
  "webSocketUrl": "wss://app.iqra.bot/ws/path..."
}
```
**Next Action:** The client should establish a WebSocket connection to the provided URL to begin audio streaming.

#### 🟡 202 Accepted (Concurrency Full - Queued)
If the concurrency limit is reached, the user is placed into a Redis-backed queue.

```json
{
  "status": "queued",
  "uniqueRequestId": "edbdf408-db05-4ebd-a3c3-376df1f5b021",
  "queuePosition": 3
}
```
**Next Action:** The client MUST connect to the SignalR Hub (`/sessionHub`) using the provided `uniqueRequestId` as their connection group identifier to wait in line.

#### 🔴 Client Errors
- **`400 Bad Request`**: Malformed payload, missing `campaignId`, or an invalid `regionId`.
- **`403 Forbidden`**: The client's IP address failed security validation (e.g., detected as a VPN, Proxy, or Datacenter IP, if enabled).
- **`429 Too Many Requests`**: The client exceeded the allowed concurrent, hourly, or daily rate limits defined in the configuration.

---

## 2. SignalR Real-Time Queue Hub 

**Endpoint:** `/sessionHub` (SignalR Protocol)  
**Description:** Used exclusively by clients who receive a `202 Accepted` response from the session request endpoint. 

### Connection Flow

1. **Connect:** Use a standard SignalR client library (e.g., `@microsoft/signalr` in JS) to connect to `wss://your-middleware-domain.com/sessionHub`.
2. **Register:** Immediately after connection, the client must invoke the `Register` method on the server, passing the `uniqueRequestId` received from the `202` response.

**Example JavaScript Client Setup:**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://your-middleware-domain.com/sessionHub")
    .withAutomaticReconnect()
    .build();

await connection.start();

// Tell the hub who we are so we receive our specific updates
await connection.invoke("Register", "edbdf408-db05-4ebd-a3c3-376df1f5b021");
```

### Server-to-Client Events (Listen for these)

While waiting in the queue, the server will emit specific events to the client.

#### `SessionReady`
Fired when a slot opens up, the queue advances, and the middleware successfully secures a WebSocket URL for the queued client.

**Payload received:**
```json
{
  "webSocketUrl": "wss://app.iqra.bot/ws/path..."
}
```
**Next Action:** Disconnect from SignalR (optional) and open the audio streaming WebSocket URL.

#### `SessionFailed`
Fired if an unexpected error occurs on the server while trying to process the user's turn in the queue (e.g., the Iqra platform API fails).

**Payload received:**
```json
{
  "message": "An error occurred while creating your session. Please try again."
}
```

---

## 3. Webhook (Internal Use Only)
> *Note: This endpoint is strictly for the Voice AI Agent platform to communicate with the middleware. Clients do not call this.*

**Endpoint:** `POST /api/webhook/session-ended`  
**Description:** This endpoint is configured natively within the Voice AI project's Webhook Settings. When a call drops, the platform hits this endpoint, alerting the middleware to decrement the concurrent session count and proactively process the next client in the Queue.

**Payload Expected from Platform:**
```json
{
  "WebSessionId": "ses_xxxxxxxx"
}
```
