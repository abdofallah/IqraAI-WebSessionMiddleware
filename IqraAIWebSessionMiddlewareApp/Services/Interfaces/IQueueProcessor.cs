namespace IqraAIWebSessionMiddlewareApp.Services.Interfaces
{
    public interface IQueueProcessor
    {
        Task ProcessNextInQueueAsync();
    }
}
