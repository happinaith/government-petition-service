using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace PetitionService.Server.AI;

public class GeminiPetitionAssistant : IGeminiPetitionAssistant
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiPetitionAssistant(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PetitionAiDraftSuggestion> BuildDraftAsync(PetitionAiDraftRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new GeminiIntegrationException("Gemini API key is not configured.", StatusCodes.Status503ServiceUnavailable);
        }

        var prompt = BuildPrompt(request);
        var endpoint = BuildEndpoint();
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var response = await SendWithRetriesAsync(endpoint, payloadJson, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            throw new GeminiIntegrationException(
                $"Gemini request failed with status {statusCode}.",
                statusCode >= 500 ? StatusCodes.Status502BadGateway : StatusCodes.Status424FailedDependency);
        }

        var generatedJson = ExtractGeneratedText(responseBody);
        if (string.IsNullOrWhiteSpace(generatedJson))
        {
            throw new GeminiIntegrationException("Gemini returned an empty response.", StatusCodes.Status502BadGateway);
        }

        GeminiDraftRaw? raw;
        try
        {
            raw = JsonSerializer.Deserialize<GeminiDraftRaw>(generatedJson, JsonOptions);
        }
        catch (JsonException)
        {
            throw new GeminiIntegrationException("Gemini response could not be parsed.", StatusCodes.Status502BadGateway);
        }

        if (raw is null)
        {
            throw new GeminiIntegrationException("Gemini response is missing draft payload.", StatusCodes.Status502BadGateway);
        }

        return Normalize(raw, request);
    }

    private string BuildEndpoint()
    {
        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        return $"{baseUrl}/models/{_options.Model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
    }

    private static string BuildPrompt(PetitionAiDraftRequest request)
    {
        var safeContent = request.Content.Trim();
        var safeTitleHint = request.TitleHint?.Trim();
        var safeCategoryHint = request.CategoryHint?.Trim();

        return $"""
You are helping with a civic e-petition draft in Russian.
Return STRICT JSON object only with keys: title, content, category, summary.
Rules:
- title: 3..200 chars, specific and civic style.
- content: 10..5000 chars, improve clarity and structure, no markdown.
- category: up to 100 chars, may be null.
- summary: 20..240 chars.
- Keep original meaning. Do not invent legal claims.

Hints:
- titleHint: {safeTitleHint}
- categoryHint: {safeCategoryHint}

User draft content:
{safeContent}
""";
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(string endpoint, string payloadJson, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _options.MaxRetries + 1);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

            using var currentRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            try
            {
                var response = await _httpClient.SendAsync(currentRequest, cts.Token);
                if (!ShouldRetry(response.StatusCode) || attempt == attempts)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == attempts)
                {
                    throw new GeminiIntegrationException("Gemini request timeout exceeded.", StatusCodes.Status504GatewayTimeout);
                }
            }
            catch (HttpRequestException)
            {
                if (attempt == attempts)
                {
                    throw new GeminiIntegrationException("Gemini network request failed.", StatusCodes.Status502BadGateway);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
        }

        throw new GeminiIntegrationException("Gemini request failed unexpectedly.", StatusCodes.Status502BadGateway);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        return statusCode == HttpStatusCode.TooManyRequests || numeric >= 500;
    }

    private static string ExtractGeneratedText(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
            {
                sb.AppendLine(text.GetString());
            }
        }

        var raw = sb.ToString().Trim();
        if (raw.StartsWith("```", StringComparison.Ordinal))
        {
            raw = Regex.Replace(raw, "^```(?:json)?\\s*|\\s*```$", string.Empty, RegexOptions.Multiline).Trim();
        }

        return raw;
    }

    private PetitionAiDraftSuggestion Normalize(GeminiDraftRaw raw, PetitionAiDraftRequest source)
    {
        var title = Sanitize(raw.Title, 3, 200, source.TitleHint, "Черновик петиции");
        var content = Sanitize(raw.Content, 10, 5000, source.Content, source.Content);
        var category = SanitizeOptional(raw.Category, 100, source.CategoryHint);
        var summary = Sanitize(raw.Summary, 20, 240, source.Content, "Краткое описание петиции.");

        return new PetitionAiDraftSuggestion(
            title,
            content,
            category,
            summary,
            "Google Gemini",
            _options.Model);
    }

    private static string Sanitize(string? value, int min, int max, string? fallback, string hardFallback)
    {
        var candidate = (value ?? string.Empty).Trim();
        if (candidate.Length < min)
        {
            candidate = (fallback ?? hardFallback).Trim();
        }

        if (candidate.Length < min)
        {
            candidate = hardFallback;
        }

        if (candidate.Length > max)
        {
            candidate = candidate[..max].Trim();
        }

        if (candidate.Length < min)
        {
            candidate = hardFallback;
            if (candidate.Length > max)
            {
                candidate = candidate[..max];
            }
        }

        return candidate;
    }

    private static string? SanitizeOptional(string? value, int max, string? fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value;
        candidate = candidate?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (candidate.Length > max)
        {
            return candidate[..max].Trim();
        }

        return candidate;
    }

    private sealed class GeminiDraftRaw
    {
        public string? Title { get; init; }
        public string? Content { get; init; }
        public string? Category { get; init; }
        public string? Summary { get; init; }
    }
}
