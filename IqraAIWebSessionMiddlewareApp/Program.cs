using IqraAIWebSessionMiddlewareApp.Hubs;
using IqraAIWebSessionMiddlewareApp.Services;
using IqraAIWebSessionMiddlewareApp.Services.Interfaces;
using IqraAIWebSessionMiddlewareApp.Settings;
using IqraAIWebSessionMiddlewareApp.Workers;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;

namespace IqraAIWebSessionMiddlewareApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.Configure<VoiceAiPlatformSettings>(builder.Configuration.GetSection("VoiceAiPlatform"));
            builder.Services.Configure<IpApiSettings>(builder.Configuration.GetSection("IpApi"));
            builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));

            var redisConnectionString = builder.Configuration.GetValue<string>("RedisConnectionString");

            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "VoiceAiMiddleware_";
            });


            builder.Services.AddHttpClient();
            builder.Services.AddHttpClient("VoiceAiClient", (serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<VoiceAiPlatformSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", settings.ApiSecretToken);
            });

            builder.Services.AddScoped<IIpInfoService, IpInfoService>();
            builder.Services.AddScoped<IVoiceAiPlatformService, VoiceAiPlatformService>();
            builder.Services.AddSingleton<IConcurrencyService, ConcurrencyService>();
            builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
            builder.Services.AddSingleton<IQueueService, QueueService>();
            builder.Services.AddScoped<IQueueProcessor, QueueProcessor>();

            builder.Services.AddHostedService<ConcurrencyPollingWorker>();

            builder.Services.AddSignalR();
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();

            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new string[0];
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    policy =>
                    {
                        policy.WithOrigins(allowedOrigins)
                              .AllowAnyHeader()
                              .AllowAnyMethod();
                    });
            });

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();

                // 1. Load Known Proxies (Single IPs)
                var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
                if (knownProxies != null)
                {
                    foreach (var proxy in knownProxies)
                    {
                        if (IPAddress.TryParse(proxy, out var ip))
                        {
                            options.KnownProxies.Add(ip);
                        }
                    }
                }

                // 2. Load Known Networks (CIDR notations like 172.19.0.0/16)
                var knownNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();
                if (knownNetworks != null)
                {
                    foreach (var network in knownNetworks)
                    {
                        var parts = network.Split('/');
                        if (parts.Length == 2 &&
                            IPAddress.TryParse(parts[0], out var ip) &&
                            int.TryParse(parts[1], out var prefix))
                        {
                            options.KnownIPNetworks.Add(new System.Net.IPNetwork(ip, prefix));
                        }
                    }
                }
            });

            var app = builder.Build();

            app.UseForwardedHeaders();

            app.UseCors("AllowSpecificOrigins");

            app.UseAuthorization();

            app.MapHub<SessionHub>("/sessionHub");

            app.MapControllers();

            app.Run();
        }
    }
}
