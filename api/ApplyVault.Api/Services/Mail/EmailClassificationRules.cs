namespace ApplyVault.Api.Services;

internal sealed record MailClassificationRuleSet(
    IReadOnlyList<string> StrongRejectionPhrases,
    IReadOnlyList<string> SoftRejectionPhrases,
    IReadOnlyList<string> AcknowledgementPhrases,
    IReadOnlyList<string> ProcessDescriptionPhrases,
    IReadOnlyList<string> InterviewInvitationPhrases,
    IReadOnlyList<string> InterviewAvailabilityPhrases,
    IReadOnlyList<string> InterviewGeneralPhrases);

internal static class EmailClassificationRules
{
    public static readonly MailClassificationRuleSet[] RuleSets =
    [
        new(
            StrongRejectionPhrases:
            [
                "regret to inform",
                "regret to let you know",
                "not moving forward",
                "not be moving forward",
                "no longer moving forward",
                "move forward with other candidates",
                "moving forward with other candidates",
                "move forward with other applicants",
                "moving forward with other applicants",
                "move forward with another candidate",
                "moving forward with another candidate",
                "move forward with candidates whose experience more closely aligns",
                "move forward with candidates who more closely align with our needs",
                "more closely align with our current needs",
                "better align with our current needs",
                "qualifications more closely align with our needs",
                "experience more closely aligns with our needs",
                "background more closely aligns with our needs",
                "proceed with other candidates",
                "proceeding with other candidates",
                "proceed with another candidate",
                "continue with other candidates",
                "continuing with other candidates",
                "moving ahead with other candidates",
                "pursue other candidates",
                "position has been filled",
                "role has been filled",
                "vacancy has been filled",
                "position has been closed",
                "application was not selected",
                "application has been unsuccessful",
                "your application has been unsuccessful",
                "declined to proceed",
                "unable to move your application forward",
                "unable to advance your application",
                "unable to advance your candidacy",
                "unable to progress your application",
                "will not be moving forward with your application",
                "will not be proceeding with your application",
                "have decided not to move forward with your application",
                "have chosen not to move forward with your application",
                "have decided not to proceed with your application",
                "have chosen to move forward with other candidates",
                "have chosen to move forward with another candidate",
                "have decided to move forward with other candidates",
                "have decided to move forward with another candidate",
                "we will not be taking your application further",
                "will not be taking your application further",
                "you were not selected",
                "not selected for the position",
                "not selected to move forward",
                "not selected to proceed",
                "not shortlisted",
                "you have not been shortlisted",
                "were unsuccessful on this occasion",
                "unsuccessful on this occasion",
                "cannot offer you the position",
                "unable to offer you the role"
            ],
            SoftRejectionPhrases:
            [
                "unfortunately",
                "thank you for your interest",
                "thank you again for your interest",
                "thank you once again for your interest",
                "thank you for the time and effort you invested",
                "thank you for the time and effort you put into your application",
                "we appreciate the time and effort you invested",
                "we appreciate the time and effort you put into your application",
                "we appreciate the time and effort you invested in your application",
                "we appreciate the time and effort you have invested",
                "this decision was difficult",
                "difficult decision",
                "number of highly qualified applicants",
                "encourage you to apply for future opportunities",
                "encourage you to apply for other future opportunities",
                "we encourage you to apply for future opportunities",
                "we will keep your resume on file",
                "we will keep your cv on file",
                "we will keep your application on file",
                "reach out if a position becomes available",
                "should another suitable opportunity arise",
                "all the best in your job search",
                "all the best in your continued job search",
                "wish you all the best",
                "wish you all the best in your job search",
                "wish you all the best in your future endeavors",
                "wish you the very best",
                "wish you every success in your job search",
                "best of luck in your job search"
            ],
            AcknowledgementPhrases:
            [
                "received your application",
                "application has been received",
                "thank you for applying",
                "thank you for your application",
                "we look forward to reviewing your application",
                "we look forward to reading your application"
            ],
            ProcessDescriptionPhrases:
            [
                "recruitment process",
                "if your profile is a good match",
                "if your profile is a match",
                "next step in our recruitment process",
                "process includes",
                "typically includes"
            ],
            InterviewInvitationPhrases:
            [
                "would like to invite you",
                "invite you to an interview",
                "invite you for an interview",
                "interview invitation",
                "next step is an interview"
            ],
            InterviewAvailabilityPhrases:
            [
                "share your availability",
                "send your availability",
                "what is your availability",
                "available for an interview",
                "please let us know your availability"
            ],
            InterviewGeneralPhrases:
            [
                "interview",
                "schedule",
                "availability",
                "phone screen",
                "technical screen",
                "virtual interview",
                "onsite",
                "meeting with the team",
                "next step"
            ]),
        new(
            StrongRejectionPhrases:
            [
                "beklager at meddele",
                "maa desvaerre meddele",
                "gaar ikke videre",
                "gaaet videre med andre kandidater",
                "gaaet videre med andre ansoegere",
                "vi er gaaet videre med andre kandidater",
                "vi har valgt at gaa videre med andre kandidater",
                "vi har valgt at gaa videre med en anden kandidat",
                "vi gaar videre med andre kandidater",
                "vi gaar videre med en anden kandidat",
                "ikke at gaa videre",
                "har valgt ikke at gaa videre",
                "har valgt ikke at gaa videre med dit kandidatur",
                "har valgt ikke at gaa videre med din ansoegning",
                "ikke har valgt at gaa videre",
                "stillingen er besat",
                "stillingen er blevet besat",
                "rollen er blevet besat",
                "ansoegning blev ikke udvalgt",
                "ikke udvalgt",
                "du er ikke udvalgt",
                "du blev ikke udvalgt",
                "ikke gaa videre",
                "ikke kommet videre",
                "ikke gaaet videre",
                "ikke kommet i betragtning",
                "ikke kommet i betragtning til stillingen",
                "ikke har valgt dig",
                "ikke valgt at gaa videre med dig",
                "valgt en anden kandidat",
                "valgt andre kandidater",
                "kan desvaerre ikke tilbyde dig stillingen",
                "kan ikke tilbyde dig stillingen",
                "ikke tilbyde dig stillingen"
            ],
            SoftRejectionPhrases:
            [
                "desvaerre",
                "tak for din interesse",
                "tak igen for din interesse",
                "tak for din ansoegning og interesse",
                "vi saetter pris paa den tid du har brugt",
                "vi saetter pris paa den tid og interesse",
                "du har vaeret oppe imod et staerkt felt af kandidater",
                "vi vil gerne beholde dine oplysninger i vores kandidatbank",
                "vi gemmer gerne dine oplysninger til fremtidige stillinger",
                "du er velkommen til at soege igen",
                "soeg gerne igen",
                "vi opfordrer dig til at soege igen",
                "vi oensker dig held og lykke",
                "held og lykke med jobsoegningen",
                "held og lykke med din videre jobsoegning",
                "held og lykke i din jobsoegning",
                "held og lykke med din eventuelle videre jobsoegning"
            ],
            AcknowledgementPhrases:
            [
                "vi har modtaget din ansoegning",
                "modtaget din ansoegning",
                "ser frem til at laese den",
                "tak for din ansoegning",
                "tak for at du har soegt"
            ],
            ProcessDescriptionPhrases:
            [
                "rekrutteringsproces",
                "naeste trin i vores rekrutteringsproces",
                "hvis vi ser at din profil er et godt match",
                "vores rekrutteringsproces indebaerer typisk",
                "typisk logiske faerdighedstests",
                "referencekontroller"
            ],
            InterviewInvitationPhrases:
            [
                "vi vil gerne invitere dig",
                "invitere dig til jobsamtale",
                "invitere dig til en samtale",
                "invitation til jobsamtale",
                "naeste trin er en samtale"
            ],
            InterviewAvailabilityPhrases:
            [
                "send din tilgaengelighed",
                "hvilke tidspunkter passer dig",
                "har du mulighed for",
                "hvad er din tilgaengelighed",
                "kan du deltage"
            ],
            InterviewGeneralPhrases:
            [
                "samtale",
                "jobsamtale",
                "telefonsamtale",
                "teknisk samtale",
                "virtuel samtale",
                "moede med teamet",
                "naeste skridt",
                "tilgaengelighed",
                "interviews"
            ])
    ];

    public static readonly string[] InterviewMeetingLinkIndicators =
    [
        "meet.google.com",
        "google meet joining info",
        "teams.microsoft.com",
        "zoom.us",
        "whereby.com",
        "calendar.google.com"
    ];
}
