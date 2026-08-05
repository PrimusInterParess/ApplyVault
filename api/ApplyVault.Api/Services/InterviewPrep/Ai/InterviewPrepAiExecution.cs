using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;

namespace ApplyVault.Api.Services.InterviewPrep.Ai;

public sealed record InterviewPrepAiExecutionMeta(
    string Operation,
    string PromptName,
    string PromptVersion,
    int AttemptCount,
    bool UsedFallback,
    string? ErrorCode,
    string? ErrorMessage);

public sealed class InterviewPrepAiExecutionResult<T>
{
    private InterviewPrepAiExecutionResult(
        bool succeeded,
        T? value,
        InterviewPrepAiExecutionMeta meta)
    {
        Succeeded = succeeded;
        Value = value;
        Meta = meta;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public InterviewPrepAiExecutionMeta Meta { get; }

    public static InterviewPrepAiExecutionResult<T> Success(
        T value,
        InterviewPrepPromptDefinition prompt,
        int attemptCount,
        bool usedFallback = false) =>
        new(
            true,
            value,
            new InterviewPrepAiExecutionMeta(
                InterviewPrepAiOperationNames.ToName(prompt.Operation),
                prompt.Name,
                prompt.Version,
                attemptCount,
                usedFallback,
                null,
                null));

    public static InterviewPrepAiExecutionResult<T> Failure(
        InterviewPrepPromptDefinition prompt,
        int attemptCount,
        string errorCode,
        string errorMessage,
        bool usedFallback = false) =>
        new(
            false,
            default,
            new InterviewPrepAiExecutionMeta(
                InterviewPrepAiOperationNames.ToName(prompt.Operation),
                prompt.Name,
                prompt.Version,
                attemptCount,
                usedFallback,
                errorCode,
                errorMessage));
}

public sealed record InterviewPrepAiProviderResult(
    bool Succeeded,
    string? ResponseJson,
    string? ErrorCode,
    string? ErrorMessage);

public interface IInterviewPrepAiProvider
{
    string ProviderName { get; }

    Task<InterviewPrepAiProviderResult> CompleteAsync(
        InterviewPrepPromptDefinition prompt,
        object request,
        CancellationToken cancellationToken = default);
}
