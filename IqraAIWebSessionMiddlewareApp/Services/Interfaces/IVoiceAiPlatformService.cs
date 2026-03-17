using IqraAIWebSessionMiddlewareApp.Dtos.Enums;

namespace IqraAIWebSessionMiddlewareApp.Services.Interfaces
{
    public record WebSessionConfig(
        VoiceAiWebSessionTransportTypeEnum TransportType,
        string BusinessId,
        string WebCampaignId,
        string RegionId,
        string? ClientIdentifier, // Can be null
        Dictionary<string, string> DynamicVariables,
        Dictionary<string, string> Metadata,
        WebSessionAudioConfiguration AudioConfiguration
    );

    public record WebSessionAudioConfiguration(
        VoiceAiAudioEncodingTypeEnum InputEncodingType,
        int InputSampleRate,
        int InputBitsPerSample,
        VoiceAiAudioEncodingTypeEnum OutputEncodingType,
        int OutputSampleRate,
        int OutputBitsPerSample
    );

    public interface IVoiceAiPlatformService
    {
        Task<(decimal Current, decimal Max)> GetConcurrencyDataAsync();
        Task<(string WebSessionId, string ConversationSessionId, string WebSocketUrl)> InitiateWebSessionAsync(WebSessionConfig config);
    }
}
