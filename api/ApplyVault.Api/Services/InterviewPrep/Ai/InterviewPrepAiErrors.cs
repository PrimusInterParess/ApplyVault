namespace ApplyVault.Api.Services.InterviewPrep.Ai;

public static class InterviewPrepAiErrorCodes
{
    public const string Disabled = "interview_prep_ai_disabled";
    public const string ProviderUnavailable = "interview_prep_ai_provider_unavailable";
    public const string Timeout = "interview_prep_ai_timeout";
    public const string InvalidStructuredOutput = "interview_prep_ai_invalid_structured_output";
    public const string ValidationFailed = "interview_prep_ai_validation_failed";
    public const string TransportFailed = "interview_prep_ai_transport_failed";
    public const string OperationNotImplemented = "interview_prep_ai_operation_not_implemented";
    public const string Cancelled = "interview_prep_ai_cancelled";
}

public sealed class InterviewPrepAiException : Exception
{
    public InterviewPrepAiException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
