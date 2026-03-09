namespace IqraAIWebSessionMiddlewareApp.Services.Interfaces
{
    public record IpValidationResult(bool IsValid, string Reason);

    public interface IIpInfoService
    {
        Task<IpValidationResult> ValidateIpAsync(string ipAddress);
    }
}
