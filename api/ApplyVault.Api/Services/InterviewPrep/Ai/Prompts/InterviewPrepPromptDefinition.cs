namespace ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;

public sealed record InterviewPrepPromptDefinition(
    InterviewPrepAiOperation Operation,
    string Name,
    string Version,
    string SystemInstruction,
    string UserPromptTemplate);

public interface IInterviewPrepPromptRegistry
{
    InterviewPrepPromptDefinition Get(InterviewPrepAiOperation operation);

    IReadOnlyCollection<InterviewPrepPromptDefinition> List();
}
