namespace PetitionService.Server.AI;

public interface IGeminiPetitionAssistant
{
    Task<PetitionAiDraftSuggestion> BuildDraftAsync(PetitionAiDraftRequest request, CancellationToken cancellationToken = default);
}
