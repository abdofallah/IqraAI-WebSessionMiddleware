namespace IqraAIWebSessionMiddlewareApp.Dtos.VoiceAi
{
    public class VoiceAiInitiateWebSessionResponse
    {
        public string WebSessionId { get; set; } = null!;
        public string ConversationSessionId { get; set; } = null!;
        public string SessionWebSocketURL { get; set; } = null!;
    }
}
