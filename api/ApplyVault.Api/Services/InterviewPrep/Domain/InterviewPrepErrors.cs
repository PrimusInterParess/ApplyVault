namespace ApplyVault.Api.Services.InterviewPrep.Domain;

public sealed class InterviewPrepValidationException(string message) : Exception(message);
public sealed class InterviewPrepNotFoundException() : Exception("Preparation pack was not found.");
public sealed class InterviewPrepConflictException(string message, Guid? existingPackId = null) : Exception(message)
{
    public Guid? ExistingPackId { get; } = existingPackId;
}
public sealed class InterviewPrepAiUnavailableException(string message) : Exception(message)
{
    public string? FailureCode { get; init; }
}
