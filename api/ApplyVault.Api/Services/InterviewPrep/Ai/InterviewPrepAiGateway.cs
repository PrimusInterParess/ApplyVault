using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.InterviewPrep.Ai;

public sealed class InterviewPrepAiGateway(
    IInterviewPrepAiProvider provider,
    IInterviewPrepPromptRegistry promptRegistry,
    IOptions<InterviewPrepAiOptions> aiOptions,
    IOptions<GoogleAiOptions> googleAiOptions,
    ILogger<InterviewPrepAiGateway> logger,
    IInterviewPrepDebugTraceContext debugTraceContext,
    IInterviewPrepDebugFileTraceLogger debugFileTraceLogger) : IInterviewPrepAiGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<InterviewPrepAiExecutionResult<CreateInterviewBriefResponse>> CreateInterviewBriefAsync(
        CreateInterviewBriefRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<CreateInterviewBriefRequest, CreateInterviewBriefResponse>(
            InterviewPrepAiOperation.CreateInterviewBrief,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<PlanInterviewResponse>> PlanInterviewAsync(
        PlanInterviewRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<PlanInterviewRequest, PlanInterviewResponse>(
            InterviewPrepAiOperation.PlanInterview,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<GenerateOpeningResponse>> GenerateOpeningAsync(
        GenerateOpeningRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<GenerateOpeningRequest, GenerateOpeningResponse>(
            InterviewPrepAiOperation.GenerateOpening,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<AssessAnswerResponse>> AssessAnswerAsync(
        AssessAnswerRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            InterviewPrepAiOperation.AssessAnswer,
            request,
            fallback: () => FakeDeterministicInterviewPrepAiProvider.SafeAssessFallback(request),
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<SelectNextActionResponse>> SelectNextActionAsync(
        SelectNextActionRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<SelectNextActionRequest, SelectNextActionResponse>(
            InterviewPrepAiOperation.SelectNextAction,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<GenerateInterviewerMessageResponse>> GenerateInterviewerMessageAsync(
        GenerateInterviewerMessageRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            InterviewPrepAiOperation.GenerateInterviewerMessage,
            request,
            fallback: () => FakeDeterministicInterviewPrepAiProvider.SafeMessageFallback(request),
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<SummarizeConversationResponse>> SummarizeConversationAsync(
        SummarizeConversationRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<SummarizeConversationRequest, SummarizeConversationResponse>(
            InterviewPrepAiOperation.SummarizeConversation,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<EvaluateStageResponse>> EvaluateStageAsync(
        EvaluateStageRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<EvaluateStageRequest, EvaluateStageResponse>(
            InterviewPrepAiOperation.EvaluateStage,
            request,
            fallback: () => FakeDeterministicInterviewPrepAiProvider.SafeStageFallback(request),
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<GenerateFeedbackResponse>> GenerateFeedbackAsync(
        GenerateFeedbackRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<GenerateFeedbackRequest, GenerateFeedbackResponse>(
            InterviewPrepAiOperation.GenerateFeedback,
            request,
            fallback: () => FakeDeterministicInterviewPrepAiProvider.SafeFeedbackFallback(request),
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<CompareAnswerRetryResponse>> CompareAnswerRetryAsync(
        CompareAnswerRetryRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<CompareAnswerRetryRequest, CompareAnswerRetryResponse>(
            InterviewPrepAiOperation.CompareAnswerRetry,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<ExtractProfessionalStoryResponse>> ExtractProfessionalStoryAsync(
        ExtractProfessionalStoryRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<ExtractProfessionalStoryRequest, ExtractProfessionalStoryResponse>(
            InterviewPrepAiOperation.ExtractProfessionalStory,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<PlanFullLoopResponse>> PlanFullLoopAsync(
        PlanFullLoopRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<PlanFullLoopRequest, PlanFullLoopResponse>(
            InterviewPrepAiOperation.PlanFullLoop,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<GeneratePanelDebriefResponse>> GeneratePanelDebriefAsync(
        GeneratePanelDebriefRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<GeneratePanelDebriefRequest, GeneratePanelDebriefResponse>(
            InterviewPrepAiOperation.GeneratePanelDebrief,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<GenerateInterviewPrepStudyBriefResponse>> GenerateInterviewPrepStudyBriefAsync(
        GenerateInterviewPrepStudyBriefRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<GenerateInterviewPrepStudyBriefRequest, GenerateInterviewPrepStudyBriefResponse>(
            InterviewPrepAiOperation.GenerateInterviewPrepStudyBrief,
            request,
            fallback: null,
            cancellationToken);

    public Task<InterviewPrepAiExecutionResult<GenerateAnswerReviewResponse>> GenerateAnswerReviewAsync(
        GenerateAnswerReviewRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            InterviewPrepAiOperation.GenerateAnswerReview,
            request,
            fallback: () => FakeDeterministicInterviewPrepAiProvider.SafeAnswerReviewFallback(request),
            cancellationToken);

    private async Task<InterviewPrepAiExecutionResult<TResponse>> ExecuteAsync<TRequest, TResponse>(
        InterviewPrepAiOperation operation,
        TRequest request,
        Func<TResponse>? fallback,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var prompt = promptRegistry.Get(operation);
        var options = aiOptions.Value;
        var maxAttempts = Math.Max(1, options.MaxRetries + 1);
        string? lastErrorCode = InterviewPrepAiErrorCodes.TransportFailed;
        string? lastErrorMessage = "Interview Prep AI call failed.";
        var opName = InterviewPrepDebugTraceLabels.AiOperation(operation);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                logger.LogInformation(
                    "InterviewPrep AI call start: operation={Operation} attempt={Attempt} provider={Provider} requestType={RequestType}",
                    operation,
                    attempt,
                    provider.ProviderName,
                    typeof(TRequest).Name);

                Trace(sessionId => InterviewPrepAiGatewayDebugTrace.LogRequestSummary(
                    debugFileTraceLogger,
                    sessionId,
                    operation,
                    request!));

                Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                    $"AI {opName} start attempt={attempt}/{maxAttempts} provider={provider.ProviderName} prompt={prompt.Name}@{prompt.Version}"));

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var timeoutSeconds = options.TimeoutSeconds ?? googleAiOptions.Value.TimeoutSeconds;
                if (timeoutSeconds > 0)
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                }

                var providerResult = await provider.CompleteAsync(prompt, request!, timeoutCts.Token);
                if (!providerResult.Succeeded)
                {
                    lastErrorCode = providerResult.ErrorCode ?? InterviewPrepAiErrorCodes.TransportFailed;
                    lastErrorMessage = providerResult.ErrorMessage ?? "Provider returned failure.";
                    Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                        $"AI {opName} providerFailed attempt={attempt} error={InterviewPrepDebugTraceLabels.AiErrorCode(providerResult.ErrorCode)} wireCode={providerResult.ErrorCode ?? "null"}"));
                }
                else if (string.IsNullOrWhiteSpace(providerResult.ResponseJson))
                {
                    lastErrorCode = InterviewPrepAiErrorCodes.InvalidStructuredOutput;
                    lastErrorMessage = "Provider returned empty JSON.";
                    Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                        $"AI {opName} emptyJson attempt={attempt}"));
                }
                else
                {
                    TResponse? parsed;
                    try
                    {
                        parsed = JsonSerializer.Deserialize<TResponse>(providerResult.ResponseJson, SerializerOptions);
                    }
                    catch (JsonException ex)
                    {
                        lastErrorCode = InterviewPrepAiErrorCodes.InvalidStructuredOutput;
                        lastErrorMessage = $"Invalid JSON: {ex.Message}";
                        parsed = null;
                        Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                            $"AI {opName} jsonParseFailed attempt={attempt} errorPreview='{InterviewPrepDebugTraceLabels.Preview(ex.Message, 120)}'"));
                    }

                    if (parsed is not null)
                    {
                        var validation = InterviewPrepAiResponseValidator.Validate(operation, parsed);
                        if (validation.IsValid)
                        {
                            Trace(sessionId =>
                            {
                                InterviewPrepAiGatewayDebugTrace.LogResponseOutput(
                                    debugFileTraceLogger,
                                    sessionId,
                                    operation,
                                    parsed,
                                    usedFallback: false,
                                    attempt);
                                debugFileTraceLogger.Log(sessionId, $"AI {opName} success attempt={attempt}");
                            });

                            return InterviewPrepAiExecutionResult<TResponse>.Success(parsed, prompt, attempt);
                        }

                        lastErrorCode = InterviewPrepAiErrorCodes.ValidationFailed;
                        lastErrorMessage = validation.ErrorMessage ?? "Structured output failed validation.";
                        Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                            $"AI {opName} validationFailed attempt={attempt} errorPreview='{InterviewPrepDebugTraceLabels.Preview(validation.ErrorMessage, 140)}'"));
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastErrorCode = InterviewPrepAiErrorCodes.Timeout;
                lastErrorMessage = "Interview Prep AI call timed out.";
                Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                    $"AI {opName} timeout attempt={attempt}"));
            }
            catch (OperationCanceledException)
            {
                Trace(sessionId => debugFileTraceLogger.Log(sessionId, $"AI {opName} cancelled attempt={attempt}"));
                return InterviewPrepAiExecutionResult<TResponse>.Failure(
                    prompt,
                    attempt,
                    InterviewPrepAiErrorCodes.Cancelled,
                    "Interview Prep AI call was cancelled.");
            }
            catch (InterviewPrepAiException ex)
            {
                lastErrorCode = ex.ErrorCode;
                lastErrorMessage = ex.Message;
                Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                    $"AI {opName} exception attempt={attempt} error={InterviewPrepDebugTraceLabels.AiErrorCode(ex.ErrorCode)}"));
            }
            catch (Exception ex)
            {
                lastErrorCode = InterviewPrepAiErrorCodes.TransportFailed;
                lastErrorMessage = ex.Message;
                Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                    $"AI {opName} transportException attempt={attempt} errorPreview='{InterviewPrepDebugTraceLabels.Preview(ex.Message, 120)}'"));
            }

            if (attempt < maxAttempts && options.RetryDelayMilliseconds > 0)
            {
                Trace(sessionId => debugFileTraceLogger.Log(sessionId,
                    $"AI {opName} retryScheduled delayMs={options.RetryDelayMilliseconds} nextAttempt={attempt + 1}"));
                await Task.Delay(options.RetryDelayMilliseconds, cancellationToken);
            }
        }

        if (options.AllowSafeFallback && fallback is not null)
        {
            var safe = fallback();
            var validation = InterviewPrepAiResponseValidator.Validate(operation, safe);
            if (validation.IsValid)
            {
                Trace(sessionId =>
                {
                    InterviewPrepAiGatewayDebugTrace.LogResponseOutput(
                        debugFileTraceLogger,
                        sessionId,
                        operation,
                        safe,
                        usedFallback: true,
                        maxAttempts);
                    debugFileTraceLogger.Log(sessionId, $"AI {opName} safeFallbackUsed attempts={maxAttempts}");
                });

                return InterviewPrepAiExecutionResult<TResponse>.Success(safe, prompt, maxAttempts, usedFallback: true);
            }
        }

        Trace(sessionId => debugFileTraceLogger.Log(sessionId,
            $"AI {opName} finalFailed attempts={maxAttempts} lastError={InterviewPrepDebugTraceLabels.AiErrorCode(lastErrorCode)} wireCode={lastErrorCode ?? "null"} messagePreview='{InterviewPrepDebugTraceLabels.Preview(lastErrorMessage, 160)}'"));

        return InterviewPrepAiExecutionResult<TResponse>.Failure(
            prompt,
            maxAttempts,
            lastErrorCode ?? InterviewPrepAiErrorCodes.TransportFailed,
            lastErrorMessage ?? "Interview Prep AI call failed.");
    }

    private void Trace(Action<Guid> write)
    {
        if (debugTraceContext.CurrentSessionId is Guid sessionId)
        {
            write(sessionId);
        }
    }
}
