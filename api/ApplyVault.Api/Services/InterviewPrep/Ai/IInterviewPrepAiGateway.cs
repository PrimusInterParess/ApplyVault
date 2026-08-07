using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;

namespace ApplyVault.Api.Services.InterviewPrep.Ai;

public interface IInterviewPrepAiGateway
{
    Task<InterviewPrepAiExecutionResult<CreateInterviewBriefResponse>> CreateInterviewBriefAsync(
        CreateInterviewBriefRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<PlanInterviewResponse>> PlanInterviewAsync(
        PlanInterviewRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<GenerateOpeningResponse>> GenerateOpeningAsync(
        GenerateOpeningRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<AssessAnswerResponse>> AssessAnswerAsync(
        AssessAnswerRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<SelectNextActionResponse>> SelectNextActionAsync(
        SelectNextActionRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<GenerateInterviewerMessageResponse>> GenerateInterviewerMessageAsync(
        GenerateInterviewerMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<SummarizeConversationResponse>> SummarizeConversationAsync(
        SummarizeConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<EvaluateStageResponse>> EvaluateStageAsync(
        EvaluateStageRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<GenerateFeedbackResponse>> GenerateFeedbackAsync(
        GenerateFeedbackRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<CompareAnswerRetryResponse>> CompareAnswerRetryAsync(
        CompareAnswerRetryRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<ExtractProfessionalStoryResponse>> ExtractProfessionalStoryAsync(
        ExtractProfessionalStoryRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<PlanFullLoopResponse>> PlanFullLoopAsync(
        PlanFullLoopRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAiExecutionResult<GeneratePanelDebriefResponse>> GeneratePanelDebriefAsync(
        GeneratePanelDebriefRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a durable study Interview Prep brief body (topics + gap, sample questions, talking points).
    /// Hard-fails when AI is unavailable (no safe fallback) so callers can map to
    /// <c>503 interview_prep_brief_ai_unavailable</c>.
    /// </summary>
    Task<InterviewPrepAiExecutionResult<GenerateInterviewPrepStudyBriefResponse>> GenerateInterviewPrepStudyBriefAsync(
        GenerateInterviewPrepStudyBriefRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate Answer review Model answer + delivery coaching tips (ADR-0026).
    /// Uses safe fallback when AI is unavailable (short non-empty modelAnswer).
    /// Does not replace session-level <see cref="GenerateFeedbackAsync"/>.
    /// </summary>
    Task<InterviewPrepAiExecutionResult<GenerateAnswerReviewResponse>> GenerateAnswerReviewAsync(
        GenerateAnswerReviewRequest request,
        CancellationToken cancellationToken = default);
}
