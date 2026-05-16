namespace PetitionService.Server.AI;

public record PetitionAiDraftRequest(string Content, string? TitleHint, string? CategoryHint);

public record PetitionAiDraftSuggestion(
    string Title,
    string Content,
    string? Category,
    string Summary,
    string Provider,
    string Model);
