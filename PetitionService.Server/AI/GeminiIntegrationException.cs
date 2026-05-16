namespace PetitionService.Server.AI;

public class GeminiIntegrationException : Exception
{
    public int StatusCode { get; }

    public GeminiIntegrationException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
