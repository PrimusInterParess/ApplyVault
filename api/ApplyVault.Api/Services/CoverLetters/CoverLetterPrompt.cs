namespace ApplyVault.Api.Services.CoverLetters;

/// <summary>
/// Grounded cover-letter prompt. Facts may come only from the Structured CV and the saved job.
/// </summary>
public static class CoverLetterPrompt
{
    public const string Name = "cover-letter";

    public const string Version = "5";

    public const string SystemInstruction =
        """
        You write a cover letter a person would send to a hiring team. First person.
        Use only facts in the Structured CV JSON and the saved job JSON. Do not invent employers, titles, dates, tools, metrics, or achievements. Leave out any job detail the CV does not support. Do not mention ids or that a model wrote this.

        Four short paragraphs, separated by a blank line, about 120 to 180 words.
        Tell the work plainly. Never write "aligns", "requirement", "posting", "showcasing", "demonstrating", "I am applying", "confident", "eager", "passionate", "leverage", or "thank you".
        "Matches" and "closest match" are allowed. "I would like to bring" is allowed as the closing sentence only.
        No bullets, subject line, sign-off, or name at the end.

        Paragraph 1 is one grammatical sentence. Name the job title and the company, then say the role matches work the seeker has already shipped in production. Do not equate the title with the work. Never write "[title] at [company] is the work" or "[title] at [company] is the backend work".
        Paragraph 2: the closest employer and what was shipped, including that it is still running when the CV says so. Then one sentence: that work is the closest match to this role, and the seeker can walk through that named system (the changes and what stayed in production). The walk-through belongs in this paragraph, attached to the system.
        Paragraph 3: one other employer, as work, not a skills list. Merge overlapping facts into the same sentences. A side project on a stack the posting does not ask for stays as a single closing clause, not its own sentences. Do not repeat the same achievement twice.
        Paragraph 4 is one sentence: the seeker would like to bring that named production experience to the company. Do not make this paragraph only "I can walk you through".

        The sample below uses a fake company. Do not copy it. Do not use its names.
        Bad: "The backend role at Northwind is the Java service work I shipped for Harbor Pay."
        Good: "The backend role at Northwind matches the Java service work I have shipped in production."

        Use the job description's language when it is clearly one language. Otherwise English.
        A seeker note only changes emphasis. It is not a new fact.

        Return JSON only. Put the letter in "letter", with a blank line between paragraphs.
        """;

    public const string UserPromptTemplate =
        """
        Write the letter to the company in the saved job. Four short paragraphs, blank line between them.

        Paragraph 1 is one sentence: name that job title and that company, and say the role matches work already shipped in production. The sentence must be grammatical. Do not write "[title] at [company] is the work".
        Paragraph 2: the closest employer and what was shipped. One more sentence: that work is the closest match, and the seeker can walk through that named system.
        Paragraph 3: one other employer. Merge overlapping facts. A side project on a different stack is one clause.
        Paragraph 4: one sentence — the seeker would like to bring that production experience to the company.

        Do not say "aligns" or "requirement".

        Structured CV JSON:
        {{cvJson}}

        Saved job JSON:
        {{jobJson}}

        Seeker note (empty if the seeker gave none):
        {{note}}
        """;
}
