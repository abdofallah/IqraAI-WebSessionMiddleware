namespace IqraAIWebSessionMiddlewareApp.Settings
{
    public class CampaignConfig
    {
        public string BusinessId { get; set; } = string.Empty;
        public string WebCampaignId { get; set; } = string.Empty;
        public List<string> AllowedRegionIds { get; set; } = new();
    }

    public class VoiceAiPlatformSettings
    {
        public string ApiSecretToken { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public Dictionary<string, CampaignConfig> Campaigns { get; set; } = new();
    }
}
