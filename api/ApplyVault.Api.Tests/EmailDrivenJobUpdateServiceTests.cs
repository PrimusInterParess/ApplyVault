using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Tests;

public sealed class EmailDrivenJobUpdateServiceTests
{
    [Fact]
    public async Task TryApplyAsync_EnglishRejection_MarksMatchedJobAsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Contoso",
            jobTitle: "Backend Developer",
            sourceHostname: "contoso.com");

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Update on your Backend Developer application at Contoso",
            From: "jobs@contoso.com",
            Snippet: "Thank you for your interest in Contoso.",
            BodyText: "Thank you for your interest in the Backend Developer role at Contoso. We regret to inform you that we will not be moving forward.",
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 12, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults.SingleAsync();
        Assert.True(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Rejection, updatedJob.LastStatusKind);
        Assert.Equal(JobStatusSources.Gmail, updatedJob.LastStatusSource);
        Assert.Equal(message.Subject, updatedJob.LastStatusEmailSubject);
    }

    [Fact]
    public async Task TryApplyAsync_DanishRejection_MarksMatchedJobAsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Nordic Works",
            jobTitle: "Data Engineer",
            sourceHostname: "nordicworks.dk");

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Opdatering om din ansogning hos Nordic Works",
            From: "talent@nordicworks.dk",
            Snippet: "Tak for din interesse for stillingen som Data Engineer.",
            BodyText: "Tak for din interesse for stillingen som Data Engineer hos Nordic Works. Vi beklager at meddele, at vi er gaaet videre med andre kandidater.",
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 13, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults.SingleAsync();
        Assert.True(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Rejection, updatedJob.LastStatusKind);
        Assert.Equal(JobStatusSources.Gmail, updatedJob.LastStatusSource);
    }

    [Fact]
    public async Task TryApplyAsync_DanishInterview_ParsesScheduleAndLocation()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "BlaaTech",
            jobTitle: "Softwareudvikler",
            sourceHostname: "blaatech.dk");

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Invitation til jobsamtale hos BlåTech",
            From: "rekruttering@blaatech.dk",
            Snippet: "Vi vil gerne invitere dig til en jobsamtale.",
            BodyText: """
                Hej,

                Vi vil gerne invitere dig til jobsamtale som Softwareudvikler hos BlåTech den 15. april 2026 kl. 13.30-14.15 CEST.
                Lokation: BlåTech HQ, København

                Venlig hilsen
                BlåTech
                """,
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 14, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults
            .Include((result) => result.InterviewEvent)
            .SingleAsync();

        Assert.False(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Interview, updatedJob.LastStatusKind);
        Assert.NotNull(updatedJob.InterviewEvent);
        Assert.Equal("BlåTech HQ, København", updatedJob.InterviewEvent!.Location);
        Assert.Equal("W. Europe Standard Time", updatedJob.InterviewEvent.TimeZone);

        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 4, 15, 13, 30, 0, DateTimeKind.Unspecified),
            zone);
        var expectedEndUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 4, 15, 14, 15, 0, DateTimeKind.Unspecified),
            zone);

        Assert.Equal(expectedStartUtc, updatedJob.InterviewEvent.StartUtc.UtcDateTime);
        Assert.Equal(expectedEndUtc, updatedJob.InterviewEvent.EndUtc.UtcDateTime);
        Assert.Equal(DateOnly.FromDateTime(expectedStartUtc), updatedJob.InterviewDate);
    }

    [Fact]
    public async Task TryApplyAsync_Interview_SyncsCalendarAfterPersistingInterviewEvent()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Contoso",
            jobTitle: "Platform Engineer",
            sourceHostname: "contoso.com");

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var calendarSync = new SpyEmailDrivenInterviewCalendarSyncService();
        var service = CreateService(dbContext, calendarSync);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Interview for Platform Engineer at Contoso",
            From: "jobs@contoso.com",
            Snippet: "Friday, April 10 · 11:00 - 11:45am. Time zone: Europe/Copenhagen.",
            BodyText: """
                Hi Yordan,

                Interview for Platform Engineer at Contoso
                Friday, April 10 · 11:00 - 11:45am
                Time zone: Europe/Copenhagen
                """,
            ReceivedAt: new DateTimeOffset(2026, 4, 5, 10, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);
        var syncRequest = Assert.Single(calendarSync.Requests);
        Assert.Equal(user.Id, syncRequest.UserId);
        Assert.Equal(job.Id, syncRequest.ScrapeResultId);
    }

    [Fact]
    public async Task TryApplyAsync_EnglishRealWorldRejection_MarksMatchedJobAsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Netcompany",
            jobTitle: "Software Developer",
            sourceHostname: "netcompany.com");

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Update on your application at Netcompany",
            From: "recruitment@netcompany.com",
            Snippet: "After reviewing your profile, we regret to inform you that we will not be proceeding with your application at this time.",
            BodyText: """
                Dear Yordan,

                Thank you for your application and for your interest in Netcompany.

                After reviewing your profile, we regret to inform you that we will not be proceeding with your application at this time, as we currently do not have a position that matches your qualifications and experience.

                We truly appreciate the time and effort you put into your application, and we thank you for considering Netcompany as a potential employer.

                We wish you all the best in your continued job search and future career.

                Best regards,

                Julie Stentebjerg Larsen
                Netcompany Recruitment
                """,
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 15, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults.SingleAsync();
        Assert.True(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Rejection, updatedJob.LastStatusKind);
    }

    [Fact]
    public async Task TryApplyAsync_EnglishMoveForwardWithOtherCandidatesRejection_MarksMatchedJobAsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "DevriX",
            jobTitle: "Full Stack Engineer",
            sourceHostname: "devrix.com");

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Update on your Full Stack Engineer application at DevriX",
            From: "hiring@devrix.com",
            Snippet: "We have decided to move forward with other candidates who more closely align with our current needs.",
            BodyText: """
                Dear Candidate,

                Thank you for applying for the Full Stack Engineer (PHP/Python) position at DevriX. We appreciate the time and effort you invested in your application and enjoyed reviewing your qualifications.

                After careful consideration, we have decided to move forward with other candidates who more closely align with our current needs and the specific skills required for the role.

                We encourage you to apply for other future opportunities at DevriX that match your skills and experience. We will also keep your resume on file and reach out if a position becomes available that we believe would be a good fit for you.

                Thank you once again for your interest in joining our team. We wish you all the best in your job search and future professional endeavors.

                Warm regards,
                DevriX Hiring Team
                """,
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 15, 30, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults.SingleAsync();
        Assert.True(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Rejection, updatedJob.LastStatusKind);
    }

    [Fact]
    public async Task TryApplyAsync_DanishRealWorldRejection_MarksMatchedJobAsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Example Company",
            jobTitle: "Systemudvikler",
            sourceHostname: "example.dk");
        job.HiringManagerContacts.Add(new ScrapeResultContactEntity
        {
            Type = "email",
            Value = "hr@example.dk",
            Label = "Recruiter"
        });
        job.HiringManagerContacts.Add(new ScrapeResultContactEntity
        {
            Type = "name",
            Value = "Kathrine V. Klausen",
            Label = "Recruiter"
        });

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Svar pa din ansogning",
            From: "hr@example.dk",
            Snippet: "Vi maa desvaerre meddele dig, at vi har valgt ikke at gaa videre med dit kandidatur.",
            BodyText: """
                Kære Yordan Borisov

                Endnu engang tak for din ansøgning til stillingen, som vi har læst med stor interesse. Vi sætter pris på den tid du har brugt i processen.

                Du har været oppe imod et stærkt felt af kandidater og vi må desværre meddele dig, at vi har valgt ikke at gå videre med dit kandidatur.

                Hvis du har spørgsmål i den forbindelse, er du meget velkommen til at kontakte mig.

                Vi vil meget gerne beholde dine oplysninger i vores kandidatbank til senere brug. Hvis du ikke ønsker dette, bedes du venligst sende os en mail med oplysning herom.

                Vi takker for din interesse og ønsker dig held og lykke med din eventuelle videre jobsøgning.

                Venlig hilsen,

                Kathrine V. Klausen
                """,
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 16, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults.SingleAsync();
        Assert.True(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Rejection, updatedJob.LastStatusKind);
    }

    [Fact]
    public async Task TryApplyAsync_DanishApplicationConfirmation_DoesNotApplyStatusUpdate()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "twoday",
            jobTitle: "Udvikler",
            sourceHostname: "twoday.dk");

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Vi har modtaget din ansøgning",
            From: "career@twoday.dk",
            Snippet: "Vi har modtaget din ansøgning, og vi ser frem til at læse den.",
            BodyText: """
                Hej Yordan,

                Vi er glade for, at du ønsker at være en del af twoday. Vi har modtaget din ansøgning, og vi ser frem til at læse den!

                Vores 3000 medarbejdere og 8000 kunder arbejder hver dag på at udvikle og konsultere for at få verden til at køre endnu mere gnidningsløst.

                Hvis vi ser, at din profil er et godt match, vil vi invitere dig til næste trin i vores rekrutteringsproces. Vores rekrutteringsproces indebærer typisk logiske færdighedstests, interviews, arbejdsprøver og referencekontroller for at nå det endelige mål: et jobtilbud!

                Vi værdsætter den tid, du bruger med os, inden du og vi træffer beslutningen.

                Indtil da, hav en fantastisk dag!
                """,
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 17, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.False(applied);

        var updatedJob = await dbContext.ScrapeResults
            .Include((result) => result.InterviewEvent)
            .SingleAsync();

        Assert.False(updatedJob.IsRejected);
        Assert.Null(updatedJob.LastStatusKind);
        Assert.Null(updatedJob.InterviewEvent);
        Assert.Null(updatedJob.InterviewDate);
    }

    [Fact]
    public async Task TryApplyAsync_DanishMjoelnerRejection_MarksMatchedJobAsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Mjoelner Informatics",
            jobTitle: "Software Developer",
            sourceHostname: "mjolner.dk");
        job.HiringManagerContacts.Add(new ScrapeResultContactEntity
        {
            Type = "email",
            Value = "rekruttering@mjolner.dk",
            Label = "Recruitment"
        });

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Opdatering pa din ansogning hos Mjoelner Informatics",
            From: "rekruttering@mjolner.dk",
            Snippet: "Desvaerre maa vi meddele, at vi ikke har valgt at gaa videre med dig i denne omgang.",
            BodyText: """
                Kære Yordan,

                Endnu en gang tak for din ansøgning og interesse i at blive en del af holdet hos Mjølner Informatics. Vi sætter pris på, at du har taget dig tid til at søge jobbet hos os.

                Hos Mjølner tror vi på, at alle har noget unikt at bidrage med, og derfor har vi læst din ansøgning igennem med stor interesse.

                Desværre må vi meddele, at vi ikke har valgt at gå videre med dig i denne omgang, da vi har modtaget andre ansøgninger, hvoriblandt vi mener der er et bedre match til lige netop denne stilling. Du er meget velkommen til at kontakte undertegnede, såfremt du ønsker en uddybende forklaring.

                Vi ønsker dig held og lykke med jobsøgningen.
                """,
            ReceivedAt: new DateTimeOffset(2026, 4, 4, 18, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults.SingleAsync();
        Assert.True(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Rejection, updatedJob.LastStatusKind);
    }

    [Fact]
    public async Task TryApplyAsync_InterviewAvailabilityRequest_DoesNotApplyStatusWithoutScheduledTime()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Lifebonder",
            jobTitle: "Volunteer Product Role",
            sourceHostname: "lifebonder.com");
        job.HiringManagerContacts.Add(new ScrapeResultContactEntity
        {
            Type = "email",
            Value = "valentinam@lifebonder.com",
            Label = "Recruiter"
        });

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Interview invitation and availability request",
            From: "valentinam@lifebonder.com",
            Snippet: "We would like to invite you for an interview. Please let me know your availability next week.",
            BodyText: """
                Hi Yordan,

                Thank you for your application and for expressing interest in joining our startup. After reviewing your CV, I am pleased to inform you that we would like to invite you for an interview to discuss the opportunity further.

                Before we proceed, I wanted to ensure that you are aware of the current nature of the position. As we are in the early stages of launching our startup, this is an unpaid, part-time, volunteer role.

                To provide you with further context, we are a multicultural team of over 30+ dedicated individuals from various backgrounds (experienced professionals, freelancers, and students), working remotely from across the globe. Our team is united by the vision of building a successful platform, and we are currently in the process of seeking funding.

                With this in mind, I would be happy to arrange a time for us to connect and discuss this further. Please let me know your availability next week for a roughly 20 minutes video call.
                """,
            ReceivedAt: new DateTimeOffset(2026, 3, 26, 16, 0, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.False(applied);

        var updatedJob = await dbContext.ScrapeResults
            .Include((result) => result.InterviewEvent)
            .SingleAsync();

        Assert.False(updatedJob.IsRejected);
        Assert.Null(updatedJob.LastStatusKind);
        Assert.Null(updatedJob.InterviewEvent);
        Assert.Null(updatedJob.InterviewDate);
    }

    [Fact]
    public async Task TryApplyAsync_GoogleMeetInterviewInvite_CreatesInterviewEvent()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var job = CreateJob(
            user,
            companyName: "Lifebonder",
            jobTitle: "Angular Developer",
            sourceHostname: "lifebonder.com");
        job.HiringManagerContacts.Add(new ScrapeResultContactEntity
        {
            Type = "email",
            Value = "valentinam@lifebonder.com",
            Label = "Recruiter"
        });

        dbContext.Users.Add(user);
        dbContext.ScrapeResults.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var message = new GmailMessage(
            Id: Guid.NewGuid().ToString("N"),
            HistoryId: null,
            Subject: "Interview with Angular Developer Yordan Borisov",
            From: "valentinam@lifebonder.com",
            Snippet: "Wednesday, April 1 · 3:00 – 3:30pm. Time zone: Europe/Copenhagen. Google Meet joining info.",
            BodyText: """
                Hi Yordan,

                Thank you for taking the time to speak with us, we are looking forward to getting to know you. You will have the opportunity to meet Jesper (CEO&Founder) and have the chance to talk more about your experience, as well as your expectations.

                Here is the meeting link:

                Interview with Angular Developer Yordan Borisov
                Wednesday, April 1 · 3:00 – 3:30pm
                Time zone: Europe/Copenhagen
                Google Meet joining info
                """,
            ReceivedAt: new DateTimeOffset(2026, 3, 27, 13, 24, 0, TimeSpan.Zero));

        var applied = await service.TryApplyAsync(user, message);

        Assert.True(applied);

        var updatedJob = await dbContext.ScrapeResults
            .Include((result) => result.InterviewEvent)
            .SingleAsync();

        Assert.False(updatedJob.IsRejected);
        Assert.Equal(JobStatusKinds.Interview, updatedJob.LastStatusKind);
        Assert.NotNull(updatedJob.InterviewEvent);
        Assert.Equal("W. Europe Standard Time", updatedJob.InterviewEvent!.TimeZone);

        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var expectedStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 4, 1, 15, 0, 0, DateTimeKind.Unspecified),
            zone);
        var expectedEndUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 4, 1, 15, 30, 0, DateTimeKind.Unspecified),
            zone);

        Assert.Equal(expectedStartUtc, updatedJob.InterviewEvent.StartUtc.UtcDateTime);
        Assert.Equal(expectedEndUtc, updatedJob.InterviewEvent.EndUtc.UtcDateTime);
        Assert.Equal(DateOnly.FromDateTime(expectedStartUtc), updatedJob.InterviewDate);
    }

    private static ApplyVaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplyVaultDbContext(options);
    }

    private static EmailDrivenJobUpdateService CreateService(
        ApplyVaultDbContext dbContext,
        IEmailDrivenInterviewCalendarSyncService? calendarSyncService = null)
    {
        var scheduleExtractor = new InterviewScheduleExtractor();
        var classifier = new EmailJobStatusClassifier(scheduleExtractor);
        var matcher = new ScrapeResultEmailMatcher();
        return new EmailDrivenJobUpdateService(
            dbContext,
            classifier,
            matcher,
            calendarSyncService ?? new SpyEmailDrivenInterviewCalendarSyncService());
    }

    private static AppUserEntity CreateUser()
    {
        var utcNow = DateTimeOffset.UtcNow;

        return new AppUserEntity
        {
            Id = Guid.NewGuid(),
            SupabaseUserId = Guid.NewGuid().ToString("N"),
            Email = "user@example.com",
            DisplayName = "Test User",
            CreatedAt = utcNow,
            LastSeenAt = utcNow
        };
    }

    private static ScrapeResultEntity CreateJob(
        AppUserEntity user,
        string companyName,
        string jobTitle,
        string sourceHostname)
    {
        return new ScrapeResultEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SavedAt = DateTimeOffset.UtcNow,
            IsRejected = false,
            IsDeleted = false,
            Title = jobTitle,
            Url = $"https://{sourceHostname}/jobs/{Guid.NewGuid():N}",
            Text = $"{companyName} is hiring a {jobTitle}.",
            TextLength = 32,
            ExtractedAt = DateTimeOffset.UtcNow.ToString("O"),
            SourceHostname = sourceHostname,
            DetectedPageType = "jobPosting",
            JobTitle = jobTitle,
            CompanyName = companyName,
            CaptureReviewStatus = CaptureReviewStatuses.NotRequired
        };
    }

    private sealed class SpyEmailDrivenInterviewCalendarSyncService : IEmailDrivenInterviewCalendarSyncService
    {
        public List<(Guid UserId, Guid ScrapeResultId)> Requests { get; } = [];

        public Task SyncAsync(
            AppUserEntity user,
            Guid scrapeResultId,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((user.Id, scrapeResultId));
            return Task.CompletedTask;
        }
    }
}
