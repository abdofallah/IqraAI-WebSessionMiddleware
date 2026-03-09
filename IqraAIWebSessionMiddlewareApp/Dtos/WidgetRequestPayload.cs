using IqraAIWebSessionMiddlewareApp.Dtos.Enums;

namespace IqraAIWebSessionMiddlewareApp.Dtos
{
    public class WidgetRequestPayload
    {
        public string CampaignId { get; set; } = string.Empty;
        public string RegionId { get; set; } = string.Empty;
        public VoiceAiWebSessionTransportTypeEnum TransportType { get; set; }
        public string? ClientIdentifier { get; set; }
        public Dictionary<string, string> DynamicVariables { get; set; } = null!;
        public Dictionary<string, string> Metadata { get; set; } = null!;
        public WidgetRequestPayloadAudioConfiguration AudioConfiguration { get; set; } = null!;
    }

    public class WidgetRequestPayloadAudioConfiguration
    {
        public VoiceAiAudioEncodingTypeEnum InputEncodingType { get; set; }
        public int InputSampleRate { get; set; }
        public int InputBitsPerSample { get; set; }

        public VoiceAiAudioEncodingTypeEnum OutputEncodingType { get; set; }
        public int OutputSampleRate { get; set; }
        public int OutputBitsPerSample { get; set; }
    }
}
