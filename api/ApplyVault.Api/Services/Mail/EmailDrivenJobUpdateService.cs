using System.Text;
using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class EmailDrivenJobUpdateService(
    ApplyVaultDbContext dbContext,
    IEmailJobStatusClassifier classifier,
    IScrapeResultEmailMatcher matcher,
    IEmailDrivenInterviewCalendarSyncService interviewCalendarSyncService) : IEmailDrivenJobUpdateService
{
    public async Task<bool> TryApplyAsync(
        AppUserEntity user,
        GmailMessage message,
        CancellationToken cancellationToken = default)
    {
        var classification = classifier.Classify(message);

        if (classification is null || classification.Confidence < 0.8)
        {
            return false;
        }

        if (EmailJobStatusClassifier.IsAcknowledgement(classification))
        {
            return false;
        }

        var candidates = await dbContext.ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Where((result) => !result.IsDeleted && (result.UserId == user.Id || result.UserId == null))
            .OrderByDescending((result) => result.SavedAt)
            .ToArrayAsync(cancellationToken);

        var match = matcher.FindBestMatch(candidates, message);

        if (match is null)
        {
            return false;
        }

        var shouldSyncInterviewCalendar = false;

        if (string.Equals(classification.Kind, JobStatusKinds.Rejection, StringComparison.Ordinal))
        {
            match.IsRejected = true;
            ScrapeResultStatusUpdater.ApplyStatusSyncMetadata(match, message, JobStatusKinds.Rejection, JobStatusSources.Gmail);
        }
        else if (classification.InterviewSchedule is not null)
        {
            ApplyInterviewUpdate(match, message, classification.InterviewSchedule);
            shouldSyncInterviewCalendar = true;
        }
        else
        {
            return false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (shouldSyncInterviewCalendar)
        {
            await interviewCalendarSyncService.SyncAsync(user, match.Id, cancellationToken);
        }

        return true;
    }

    private static void ApplyInterviewUpdate(
        ScrapeResultEntity match,
        GmailMessage message,
        EmailDrivenInterviewSchedule schedule)
    {
        match.InterviewDate = DateOnly.FromDateTime(schedule.StartUtc.UtcDateTime);
        match.InterviewEvent ??= new InterviewEventEntity
        {
            ScrapeResultId = match.Id,
            TimeZone = schedule.TimeZone
        };
        match.InterviewEvent.StartUtc = schedule.StartUtc;
        match.InterviewEvent.EndUtc = schedule.EndUtc;
        match.InterviewEvent.TimeZone = schedule.TimeZone;
        match.InterviewEvent.Location = schedule.Location;
        match.InterviewEvent.Notes = BuildInterviewNotes(message);
        ScrapeResultStatusUpdater.ApplyStatusSyncMetadata(match, message, JobStatusKinds.Interview, JobStatusSources.Gmail);
    }

    private static string BuildInterviewNotes(GmailMessage message)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Auto-synced from Gmail.");

        if (!string.IsNullOrWhiteSpace(message.From))
        {
            builder.AppendLine($"From: {message.From}");
        }

        if (!string.IsNullOrWhiteSpace(message.Subject))
        {
            builder.AppendLine($"Subject: {message.Subject}");
        }

        if (!string.IsNullOrWhiteSpace(message.Snippet))
        {
            builder.AppendLine();
            builder.AppendLine(message.Snippet);
        }

        return builder.ToString().Trim();
    }
}
