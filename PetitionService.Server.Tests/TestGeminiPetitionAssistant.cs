using PetitionService.Server.AI;

namespace PetitionService.Server.Tests;

public sealed class TestGeminiPetitionAssistant : IGeminiPetitionAssistant
{
    private Func<PetitionAiDraftRequest, CancellationToken, Task<PetitionAiDraftSuggestion>> _handler;

    public TestGeminiPetitionAssistant()
    {
        Reset();
    }

    public void Reset()
    {
        _handler = (request, _) => Task.FromResult(CreateDefaultSuggestion(request));
    }

    public void ConfigureSuccess(PetitionAiDraftSuggestion suggestion)
    {
        _handler = (_, _) => Task.FromResult(suggestion);
    }

    public void ConfigureFailure(int statusCode, string message = "Gemini integration failed.")
    {
        _handler = (_, _) => Task.FromException<PetitionAiDraftSuggestion>(new GeminiIntegrationException(message, statusCode));
    }

    public Task<PetitionAiDraftSuggestion> BuildDraftAsync(PetitionAiDraftRequest request, CancellationToken cancellationToken = default)
    {
        return _handler(request, cancellationToken);
    }

    private static PetitionAiDraftSuggestion CreateDefaultSuggestion(PetitionAiDraftRequest request)
    {
        var title = string.IsNullOrWhiteSpace(request.TitleHint) ? "Безопасные дороги" : request.TitleHint.Trim();
        var category = string.IsNullOrWhiteSpace(request.CategoryHint) ? "Инфраструктура" : request.CategoryHint.Trim();

        return new PetitionAiDraftSuggestion(
            title,
            request.Content.Trim(),
            category,
            "Краткое описание черновика для тестового окружения.",
            "Google Gemini",
            "gemini-test-model");
    }
}