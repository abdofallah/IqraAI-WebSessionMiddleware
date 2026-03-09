namespace IqraAIWebSessionMiddlewareApp.Settings
{
    public class SecuritySettings
    {
        public int RateLimitHourly { get; set; }
        public int RateLimitDaily { get; set; }
        public int RateLimitConcurrency { get; set; }
        public bool EnableIpApiCheck { get; set; } = true;
        public bool EnableIpApiCache { get; set; } = true;
        public int IpApiCacheDurationDays { get; set; } = 14;
        public bool BlockVpn { get; set; }
        public bool BlockProxy { get; set; }
        public bool BlockDatacenter { get; set; }
    }
}
