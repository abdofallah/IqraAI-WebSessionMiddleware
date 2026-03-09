using IqraAIWebSessionMiddlewareApp.Dtos.Enums;

namespace IqraAIWebSessionMiddlewareApp.Dtos.VoiceAi
{
    public class VoiceAiInitiateWebSessionRequest
    {
        public VoiceAiWebSessionTransportTypeEnum TransportType { get; set; }
        public string WebCampaignId { get; set; } = null!;
        public string RegionId { get; set; } = null!;
        public string ClientIdentifier { get; set; } = null!;

        public VoiceAiInitiateWebSessionRequestAudioInputConfiguration AudioInputConfiguration { get; set; } = new VoiceAiInitiateWebSessionRequestAudioInputConfiguration();
        public VoiceAiInitiateWebSessionRequestAudioOutputConfiguration AudioOutputConfiguration { get; set; } = new VoiceAiInitiateWebSessionRequestAudioOutputConfiguration();

        public Dictionary<string, string>? DynamicVariables { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    public class VoiceAiInitiateWebSessionRequestAudioInputConfiguration
    {
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public VoiceAiAudioEncodingTypeEnum AudioEncodingType { get; set; }
        public VoiceAiAudioEncoderFallbackOptimizationMode AudioEncodingFallbackMode { get; set; }
    }

    public class VoiceAiInitiateWebSessionRequestAudioOutputConfiguration
    {
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public VoiceAiAudioEncodingTypeEnum AudioEncodingType { get; set; }
        public VoiceAiAudioEncoderFallbackOptimizationMode AudioEncodingFallbackMode { get; set; }
        public int FrameDurationMs { get; set; }
    }
}
