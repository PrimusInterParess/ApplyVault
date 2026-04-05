using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class EmailJobStatusClassifierTests
{
    private static readonly EmailDrivenInterviewSchedule DefaultSchedule = new(
        new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 4, 10, 9, 30, 0, TimeSpan.Zero),
        "UTC",
        "Video call");

    public static TheoryData<GmailMessage> StrongRejectionMessages => new()
    {
        CreateMessage(
            subject: "Update on your application at Contoso",
            snippet: "We regret to inform you that we will not be moving forward.",
            body: "Thank you for your time. We regret to inform you that we will not be moving forward with your application."),
        CreateMessage(
            subject: "Backend Engineer role",
            snippet: "We have decided to move forward with other candidates.",
            body: "After review, we have decided to move forward with other candidates whose experience more closely aligns with our needs."),
        CreateMessage(
            subject: "Application status",
            snippet: "The position has been filled.",
            body: "Thank you for applying. The position has been filled and your application has been unsuccessful."),
        CreateMessage(
            subject: "Opdatering pa din ansogning",
            snippet: "Vi beklager at meddele, at vi er gaaet videre med andre kandidater.",
            body: "Tak for din interesse. Vi beklager at meddele, at vi er gaaet videre med andre kandidater."),
        CreateMessage(
            subject: "Svar pa ansogning",
            snippet: "Vi har valgt ikke at gaa videre med dit kandidatur.",
            body: "Du har vaeret oppe imod et staerkt felt af kandidater, men vi har valgt ikke at gaa videre med dit kandidatur.")
    };

    public static TheoryData<GmailMessage> SoftRejectionMessages => new()
    {
        CreateMessage(
            subject: "Thanks for applying",
            snippet: "Unfortunately, this decision was difficult.",
            body: "Unfortunately, this decision was difficult. We wish you all the best in your job search."),
        CreateMessage(
            subject: "Your application",
            snippet: "Thank you for your interest in our team.",
            body: "Thank you for your interest. We encourage you to apply for future opportunities."),
        CreateMessage(
            subject: "Application follow-up",
            snippet: "We appreciate the time and effort you invested.",
            body: "We appreciate the time and effort you invested in your application. We will keep your resume on file."),
        CreateMessage(
            subject: "Svar pa ansogning",
            snippet: "Desvaerre og held og lykke med jobsoegningen.",
            body: "Desvaerre er vi ikke kommet videre. Vi oensker dig held og lykke med jobsoegningen."),
        CreateMessage(
            subject: "Tak for din ansogning",
            snippet: "Tak for din interesse for stillingen.",
            body: "Tak for din interesse. Vi vil gerne beholde dine oplysninger i vores kandidatbank.")
    };

    public static TheoryData<GmailMessage> AcknowledgementMessages => new()
    {
        CreateMessage(
            subject: "We received your application",
            snippet: "Thank you for applying to Fabrikam.",
            body: "Thank you for applying. We received your application and look forward to reviewing it."),
        CreateMessage(
            subject: "Application received",
            snippet: "Your application has been received.",
            body: "Your application has been received. We look forward to reading your application."),
        CreateMessage(
            subject: "Vi har modtaget din ansogning",
            snippet: "Tak for din ansogning.",
            body: "Vi har modtaget din ansogning og ser frem til at laese den."),
        CreateMessage(
            subject: "Tak for at du har sogt",
            snippet: "Vi har modtaget din ansogning.",
            body: "Tak for at du har soegt stillingen. Vi har modtaget din ansogning."),
        CreateMessage(
            subject: "Your application to Northwind",
            snippet: "Thank you for your application.",
            body: "Thank you for your application. We look forward to reviewing your application soon.")
    };

    public static TheoryData<GmailMessage> InterviewIntentMessages => new()
    {
        CreateMessage(
            subject: "Interview invitation",
            snippet: "We would like to invite you for an interview on April 10 at 10:00.",
            body: "We would like to invite you for an interview on April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Next step is an interview",
            snippet: "The next step is an interview with the hiring team.",
            body: "The next step is an interview. Friday, April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Availability for interview",
            snippet: "Please let us know your availability.",
            body: "Please let us know your availability. We can meet on 10/04/2026 10:00 UTC."),
        CreateMessage(
            subject: "Invitation til jobsamtale",
            snippet: "Vi vil gerne invitere dig til jobsamtale.",
            body: "Vi vil gerne invitere dig til jobsamtale den 10. april 2026 kl. 10.00 UTC."),
        CreateMessage(
            subject: "Interview invitation and availability",
            snippet: "Send your availability for an interview.",
            body: "Could you send your availability? We would like to invite you for an interview on April 10 2026 at 10:00 UTC.")
    };

    public static TheoryData<GmailMessage> MeetingLinkMessages => new()
    {
        CreateMessage(
            subject: "Chat with our team",
            snippet: "Google Meet joining info",
            body: "Friday, April 10 2026 at 10:00 UTC. Google Meet joining info."),
        CreateMessage(
            subject: "Hiring conversation",
            snippet: "Join here: https://meet.google.com/abc-defg-hij",
            body: "Please join here: https://meet.google.com/abc-defg-hij on April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Online meeting",
            snippet: "Join Microsoft Teams meeting",
            body: "Use https://teams.microsoft.com/l/meetup-join/... April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Video meeting details",
            snippet: "Zoom details enclosed",
            body: "Interview details: https://zoom.us/j/123 April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Calendar invitation",
            snippet: "calendar.google.com event",
            body: "Open this event in calendar.google.com. Friday, April 10 2026 at 10:00 UTC.")
    };

    public static TheoryData<GmailMessage> GeneralInterviewMessages => new()
    {
        CreateMessage(
            subject: "Phone screen details",
            snippet: "Phone screen on April 10 at 10:00 UTC.",
            body: "Phone screen on April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Technical screen",
            snippet: "Technical screen scheduled",
            body: "Your technical screen is booked for April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Meeting with the team",
            snippet: "Meeting with the team on April 10 at 10:00 UTC.",
            body: "We scheduled a meeting with the team on April 10 2026 at 10:00 UTC."),
        CreateMessage(
            subject: "Virtuel samtale",
            snippet: "Virtuel samtale den 10. april 2026 kl. 10.00 UTC.",
            body: "Virtuel samtale den 10. april 2026 kl. 10.00 UTC."),
        CreateMessage(
            subject: "Naeste skridt",
            snippet: "Naeste skridt er et moede med teamet.",
            body: "Naeste skridt er et moede med teamet den 10. april 2026 kl. 10.00 UTC.")
    };

    [Theory]
    [MemberData(nameof(StrongRejectionMessages))]
    public void Classify_StrongRejectionVariants_ReturnsRejection(GmailMessage message)
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Rejection, classification!.Kind);
        Assert.Null(classification.InterviewSchedule);
        Assert.True(classification.Confidence >= 0.91);
        Assert.Equal(0, extractor.CallCount);
    }

    [Theory]
    [MemberData(nameof(SoftRejectionMessages))]
    public void Classify_TwoSoftRejectionSignals_ReturnsRejection(GmailMessage message)
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Rejection, classification!.Kind);
        Assert.Equal(0.9, classification.Confidence);
        Assert.Null(classification.InterviewSchedule);
        Assert.Equal(0, extractor.CallCount);
    }

    [Theory]
    [MemberData(nameof(AcknowledgementMessages))]
    public void Classify_AcknowledgementVariants_ReturnsAcknowledgement(GmailMessage message)
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: false);
        var classifier = new EmailJobStatusClassifier(extractor);

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal("acknowledgement", classification!.Kind);
        Assert.Equal(0.95, classification.Confidence);
        Assert.Null(classification.InterviewSchedule);
        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public void Classify_TwoProcessDescriptionSignals_ReturnsAcknowledgement()
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: false);
        var classifier = new EmailJobStatusClassifier(extractor);
        var message = CreateMessage(
            subject: "What happens next",
            snippet: "Our recruitment process includes a short screen.",
            body: "Our recruitment process includes a short screen. The next step in our recruitment process is a team review.");

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal("acknowledgement", classification!.Kind);
        Assert.Equal(0.95, classification.Confidence);
        Assert.Equal(0, extractor.CallCount);
    }

    [Theory]
    [MemberData(nameof(InterviewIntentMessages))]
    public void Classify_InterviewIntentWithSchedule_ReturnsInterviewAtHighConfidence(GmailMessage message)
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Interview, classification!.Kind);
        Assert.Equal(0.92, classification.Confidence);
        Assert.Equal(DefaultSchedule, classification.InterviewSchedule);
        Assert.Equal(1, extractor.CallCount);
    }

    [Theory]
    [MemberData(nameof(MeetingLinkMessages))]
    public void Classify_MeetingLinkWithSchedule_ReturnsInterview(GmailMessage message)
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Interview, classification!.Kind);
        Assert.Equal(0.86, classification.Confidence);
        Assert.Equal(DefaultSchedule, classification.InterviewSchedule);
        Assert.Equal(1, extractor.CallCount);
    }

    [Theory]
    [MemberData(nameof(GeneralInterviewMessages))]
    public void Classify_GeneralInterviewLanguageWithSchedule_ReturnsInterview(GmailMessage message)
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Interview, classification!.Kind);
        Assert.Equal(0.82, classification.Confidence);
        Assert.Equal(DefaultSchedule, classification.InterviewSchedule);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public void Classify_InterviewIntentWithoutSchedule_ReturnsNull()
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: false);
        var classifier = new EmailJobStatusClassifier(extractor);
        var message = CreateMessage(
            subject: "Interview invitation",
            snippet: "We would like to invite you for an interview.",
            body: "We would like to invite you for an interview next week. Please send your availability.");

        var classification = classifier.Classify(message);

        Assert.Null(classification);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public void Classify_GeneralInterviewLanguageWithoutSchedule_ReturnsNull()
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: false);
        var classifier = new EmailJobStatusClassifier(extractor);
        var message = CreateMessage(
            subject: "Phone screen details",
            snippet: "Phone screen with the team.",
            body: "We would love to continue with a phone screen once we settle the timing.");

        var classification = classifier.Classify(message);

        Assert.Null(classification);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public void Classify_SingleSoftRejectionSignal_DoesNotClassify()
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: false);
        var classifier = new EmailJobStatusClassifier(extractor);
        var message = CreateMessage(
            subject: "Application update",
            snippet: "Unfortunately, we are still reviewing applications.",
            body: "Unfortunately, the team needs more time to review all applications.");

        var classification = classifier.Classify(message);

        Assert.Null(classification);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public void Classify_AcknowledgementWithInterviewIntent_ReturnsInterviewInsteadOfAcknowledgement()
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);
        var message = CreateMessage(
            subject: "Thanks for applying",
            snippet: "Thank you for your application.",
            body: "Thank you for your application. We would like to invite you for an interview on April 10 2026 at 10:00 UTC.");

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Interview, classification!.Kind);
        Assert.Equal(0.92, classification.Confidence);
        Assert.Equal(1, extractor.CallCount);
    }

    [Fact]
    public void Classify_RejectionSignalsTakePrecedenceOverInterviewSignals()
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);
        var message = CreateMessage(
            subject: "Application update",
            snippet: "We regret to inform you.",
            body: "We regret to inform you that we will not be moving forward. The calendar.google.com link below is no longer needed.");

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Rejection, classification!.Kind);
        Assert.Null(classification.InterviewSchedule);
        Assert.Equal(0, extractor.CallCount);
    }

    [Fact]
    public void Classify_MultipleStrongRejectionSignals_CapsConfidenceIncrease()
    {
        var extractor = new SpyInterviewScheduleExtractor(shouldExtract: true, DefaultSchedule);
        var classifier = new EmailJobStatusClassifier(extractor);
        var message = CreateMessage(
            subject: "Update on your application",
            snippet: "We regret to inform you that the position has been filled.",
            body: """
                We regret to inform you that we will not be moving forward.
                We have decided to move forward with other candidates.
                The position has been filled.
                Your application has been unsuccessful.
                You were not selected.
                """);

        var classification = classifier.Classify(message);

        Assert.NotNull(classification);
        Assert.Equal(JobStatusKinds.Rejection, classification!.Kind);
        Assert.Equal(0.99, classification.Confidence);
    }

    private static GmailMessage CreateMessage(
        string subject,
        string snippet,
        string body,
        string from = "jobs@example.com") =>
        new(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: subject,
            From: from,
            Snippet: snippet,
            BodyText: body,
            ReceivedAt: new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero));

    private sealed class SpyInterviewScheduleExtractor(
        bool shouldExtract,
        EmailDrivenInterviewSchedule? schedule = null) : IInterviewScheduleExtractor
    {
        public int CallCount { get; private set; }

        public bool TryExtractSchedule(GmailMessage message, out EmailDrivenInterviewSchedule? extractedSchedule)
        {
            CallCount++;
            extractedSchedule = shouldExtract ? schedule : null;
            return shouldExtract;
        }
    }
}
