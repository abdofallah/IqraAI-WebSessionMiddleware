namespace IqraAIWebSessionMiddlewareApp.Dtos.VoiceAi
{
    public class VoiceAiInitiateWebSessionResponse
    {
        public string SessionId { get; set; } = null!;
        public string SessionWebSocketURL { get; set; } = null!;
    }
}
