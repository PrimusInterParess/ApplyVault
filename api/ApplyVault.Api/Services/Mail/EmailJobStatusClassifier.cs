namespace ApplyVault.Api.Services;

public sealed class EmailJobStatusClassifier(
    IInterviewScheduleExtractor interviewScheduleExtractor) : IEmailJobStatusClassifier
{
    private const string AcknowledgementKind = "acknowledgement";

    public EmailClassification? Classify(GmailMessage message)
    {
        var searchText = MailTextNormalizer.BuildSearchText(message);
        var rejectionMatches = CountRuleMatches(searchText, (ruleSet) => ruleSet.StrongRejectionPhrases);
        var softRejectionMatches = CountRuleMatches(searchText, (ruleSet) => ruleSet.SoftRejectionPhrases);

        if (rejectionMatches >= 1 || softRejectionMatches >= 2)
        {
            return new EmailClassification(JobStatusKinds.Rejection, 0.9 + Math.Min(0.09, rejectionMatches * 0.01), null);
        }

        var acknowledgementMatches = CountRuleMatches(searchText, (ruleSet) => ruleSet.AcknowledgementPhrases);
        var processDescriptionMatches = CountRuleMatches(searchText, (ruleSet) => ruleSet.ProcessDescriptionPhrases);
        var interviewInvitationMatches = CountRuleMatches(searchText, (ruleSet) => ruleSet.InterviewInvitationPhrases);
        var interviewAvailabilityMatches = CountRuleMatches(searchText, (ruleSet) => ruleSet.InterviewAvailabilityPhrases);
        var interviewGeneralMatches = CountRuleMatches(searchText, (ruleSet) => ruleSet.InterviewGeneralPhrases);
        var hasMeetingLink = ContainsAny(searchText, EmailClassificationRules.InterviewMeetingLinkIndicators);
        var hasInterviewIntent = interviewInvitationMatches > 0 || interviewAvailabilityMatches > 0;
        var hasAcknowledgementSignals = acknowledgementMatches > 0 || processDescriptionMatches >= 2;

        if (!hasInterviewIntent && !hasMeetingLink && hasAcknowledgementSignals)
        {
            return new EmailClassification(AcknowledgementKind, 0.95, null);
        }

        if (!interviewScheduleExtractor.TryExtractSchedule(message, out var schedule))
        {
            return null;
        }

        if (!hasInterviewIntent && interviewGeneralMatches == 0 && !hasMeetingLink)
        {
            return null;
        }

        return new EmailClassification(
            JobStatusKinds.Interview,
            hasInterviewIntent ? 0.92 : hasMeetingLink ? 0.86 : 0.82,
            schedule);
    }

    internal static bool IsAcknowledgement(EmailClassification classification) =>
        string.Equals(classification.Kind, AcknowledgementKind, StringComparison.Ordinal);

    private static int CountMatches(string searchText, IEnumerable<string> phrases) =>
        phrases.Count((phrase) => searchText.Contains(MailTextNormalizer.Normalize(phrase), StringComparison.Ordinal));

    private static int CountRuleMatches(
        string searchText,
        Func<MailClassificationRuleSet, IEnumerable<string>> selector) =>
        EmailClassificationRules.RuleSets.Sum((ruleSet) => CountMatches(searchText, selector(ruleSet)));

    private static bool ContainsAny(string searchText, IEnumerable<string> phrases) =>
        phrases.Any((phrase) => searchText.Contains(MailTextNormalizer.Normalize(phrase), StringComparison.Ordinal));
}
