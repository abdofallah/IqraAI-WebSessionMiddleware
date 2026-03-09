namespace IqraAIWebSessionMiddlewareApp.Dtos.Webhook
{
    public class SessionEndedPayload
    {
        public string ClientIdentifier { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string WebSessionId { get; set; } = string.Empty;
    }
}
