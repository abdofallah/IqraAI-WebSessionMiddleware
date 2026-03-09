namespace IqraAIWebSessionMiddlewareApp.Dtos.VoiceAi
{
    public class VoiceAiFunctionReturnResult<T> : VoiceAiFunctionReturnResult
    {
        public T? Data { get; set; }
    }

    public class VoiceAiFunctionReturnResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Code { get; set; }
    }
}
