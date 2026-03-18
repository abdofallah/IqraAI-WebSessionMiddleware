# Voice AI Middleware (C# ASP.NET Core)

A robust, scalable middleware solution designed to act as a secure bridge between client-side web widgets and the Voice AI Agent Platform. It handles authentication, concurrency management, user queuing, rate limiting, and security validation.

---

## 🌐 Official Web Client (JS Widget)

While this middleware exposes a standard REST and SignalR API allowing you to build your own custom client-side integration, we have created an official, framework-agnostic JavaScript widget that connects to this middleware out of the box. 

👉 **[Get the Iqra AI Web Widget (JavaScript SDK) here &rarr;](https://github.com/abdofallah/IqraAIWebMiddlewareJSWidget)**

---

## 🔌 API Reference

For a complete breakdown of payloads, endpoints, WebSockets, error codes, and SignalR Queue integration for building custom clients:
👉 **[Read the API Reference here &rarr;](readme-api-reference.md)**

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
2.  *Alternative (Manual Setup):* **.NET 10 SDK** and a running **Redis Server**.
3.  **API Keys:**
    *   Voice AI Platform (Iqra.bot) API Token.
    *   IPAPI.is API Key (If using IP validation, Free tier available).

---

## 🐳 Docker Deployment

We provide an [official Docker image](https://hub.docker.com/r/abdofallah/iqraai-websessionmiddleware) for easy deployment.

### Using Docker Compose (Recommended)
The easiest way to deploy the middleware and its required Redis instance is using the included [`docker-compose.yml`](https://github.com/abdofallah/IqraAI-WebSessionMiddleware/blob/master/docker-compose.yml) file.

1.  Download or clone the repository.
2.  Create a copy of the [`IqraAIWebSessionMiddlewareApp/appsettings.json.example`](https://github.com/abdofallah/IqraAI-WebSessionMiddleware/blob/master/IqraAIWebSessionMiddlewareApp/appsettings.json.example) (name it `appsettings.json`) and edit it to match your configuration.
3.  Open `docker-compose.yml` and edit the volume mapping path (`/host/path/to/appsettings.json`) to point to your newly created `appsettings.json` file.
4.  Run the following command in the terminal:
    ```bash
    docker-compose up -d
    ```
    The middleware will now be running on port `8080`.

---

## 🛠 Manual Deployment (Bare Metal / IIS)

If you prefer not to use Docker, you can publish the binaries directly to your server:

```bash
dotnet publish -c Release -o ./publish
```
*Note: Ensure you have a Redis instance running locally or remotely, and copy & update your [`appsettings.json`](https://github.com/abdofallah/IqraAI-WebSessionMiddleware/blob/master/IqraAIWebSessionMiddlewareApp/appsettings.json.example) with the correct `RedisConnectionString`.*

---

## 🪝 Webhook Configuration (Crucial)

Ensure your Voice AI Platform (Iqra.bot) is configured to send the **"End Call"** webhook to your deployed middleware URL. This ensures that when a conversation ends, the queue advances and the local concurrency for that IP/session is cleared.

### Step 1: Create the Webhook Tool
In your Iqra.bot dashboard, create a tool with the following configuration:
*   **Endpoint:** `https://your-middleware-domain.com/api/webhook/session-ended`
*   **Request Type:** `POST`
*   **Input Schema:** Create a string argument with the ID `conversationsessionid`. Check the "Required" box and provide a name/description of your choice.
*   **Body:**
    ```json
    {
      "ConversationSessionId": "{{conversationsessionid}}"
    }
    ```

### Step 2: Map to the Web Campaign
In your specific Web Campaign configuration:
1.  Add the newly created tool to the **"End Call"** webhook action.
2.  Include the `conversationsessionid` argument and map it directly to the platform's **Conversation Session Id** variable.

---

## 🌍 Nginx Reverse Proxy & IP Forwarding

Because this middleware relies heavily on the user's IP address for Rate Limiting and Security Checks (VPN/Proxy detection), it must accurately read the client's real IP. 

If you are running this app behind a reverse proxy like **Nginx** (especially alongside Docker), you must configure Nginx to forward the real IP, and configure the .NET App to trust your Nginx proxy.

### 1. Nginx Configuration Template
Ensure your Nginx site configuration forwards the necessary headers and supports WebSockets (required for SignalR).

```nginx
server {
    listen 443 ssl http2;
    server_name api.yourdomain.com;

    # SSL Certificates go here...

    location / {
        # Point to your Docker container port (e.g., 8080)
        proxy_pass http://127.0.0.1:8080; 

        # Forward the real IP and Scheme
        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # WebSocket Support (Crucial for SignalR Queueing)
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 2. Trusting the Proxy in `appsettings.json`
By default, ASP.NET Core drops the `X-Forwarded-For` header for security reasons unless the request comes from a known proxy. If you are using Docker, Nginx acts as a proxy via the Docker Bridge Gateway (usually `172.17.0.1` or `172.19.0.1`).

Find the Docker gateway IP on your host machine using `ifconfig` or `ip addr`, and add it to the `KnownProxies` array in your `appsettings.json`:

```json
  "ForwardedHeaders": {
    "KnownProxies": [ "127.0.0.1", "::1", "172.19.0.1" ]
  }
```

---

## 🧪 Troubleshooting

*   **Error validating IP `::ffff:172.x.x.x`: 400 Bad Request:** This means your app is reading the internal Docker gateway IP instead of the user's real IP. Ensure you have properly configured the `KnownProxies` in your `appsettings.json` and added the `X-Forwarded-For` headers in Nginx as detailed in the Reverse Proxy section above.
*   **Redis Connection Error:** Ensure Redis is running and the `RedisConnectionString` is correct. If using Docker Compose, the connection string should simply be `redis:6379`.
*   **CORS Errors:** If the widget fails to connect, check the `AllowedOrigins` array in your mapped `appsettings.json`. It must include the exact domain where the widget is hosted.
*   **Concurrency Issues:** If users are stuck in the queue, ensure the **Webhook** is correctly configured on the Iqra dashboard (following the steps above) and is successfully reaching the middleware.