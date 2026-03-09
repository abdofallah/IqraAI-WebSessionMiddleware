using Microsoft.AspNetCore.SignalR;

namespace IqraAIWebSessionMiddlewareApp.Hubs
{
    public class SessionHub : Hub
    {
        private readonly ILogger<SessionHub> _logger;

        public SessionHub(ILogger<SessionHub> logger)
        {
            _logger = logger;
        }

        // A client will call this method immediately after connecting to the hub
        // to register itself with its unique request ID.
        public async Task Register(string uniqueRequestId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, uniqueRequestId);
            _logger.LogInformation("Client with ConnectionId {ConnectionId} registered for RequestId {RequestId}", Context.ConnectionId, uniqueRequestId);
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            // Here you could add logic to remove a user from the queue if they disconnect,
            // but that can be complex. For now, we'll just log it.
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
