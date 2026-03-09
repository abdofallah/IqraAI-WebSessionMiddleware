namespace IqraAIWebSessionMiddlewareApp.Settings
{
    public class SecuritySettings
    {
        public int RateLimitHourly { get; set; }
        public int RateLimitDaily { get; set; }
        public bool BlockVpn { get; set; }
        public bool BlockProxy { get; set; }
        public bool BlockDatacenter { get; set; }
    }
}
