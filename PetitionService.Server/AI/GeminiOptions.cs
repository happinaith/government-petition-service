namespace PetitionService.Server.AI;

public class GeminiOptions
{
    public string ApiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string Model { get; set; } = "gemini-1.5-flash";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 2;
}
