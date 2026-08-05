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
}
