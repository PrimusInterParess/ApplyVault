using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep;

public interface IInterviewPrepQuestionBank
{
    IReadOnlyList<InterviewPrepBankQuestion> GetQuestions(
        InterviewPrepMode mode,
        InterviewPrepPersona persona);
}

public sealed record InterviewPrepBankQuestion(
    string Text,
    string CompetencyTag);

public sealed class FixedInterviewPrepQuestionBank : IInterviewPrepQuestionBank
{
    public IReadOnlyList<InterviewPrepBankQuestion> GetQuestions(
        InterviewPrepMode mode,
        InterviewPrepPersona persona)
    {
        return (mode, persona) switch
        {
            (InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.Recruiter) =>
            [
                new("Tell me briefly about yourself and what you are looking for next.", "motivation"),
                new("What attracted you to this role and company?", "motivation"),
                new("Walk me through a recent achievement you are proud of.", "impact"),
                new("What are your salary expectations and preferred start timing?", "logistics")
            ],
            (InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.HiringManager) =>
            [
                new("Summarize your background as it relates to this team's work.", "motivation"),
                new("Why this role now in your career?", "motivation"),
                new("Describe a problem you owned end-to-end recently.", "ownership"),
                new("What would success look like in your first 90 days?", "planning")
            ],
            (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.Recruiter) =>
            [
                new("Tell me about a time you handled conflicting priorities.", "prioritization"),
                new("Describe a situation where you gave or received difficult feedback.", "communication"),
                new("Give an example of collaborating with a stakeholder who disagreed with you.", "collaboration"),
                new("How do you typically adapt when requirements change mid-project?", "adaptability")
            ],
            (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.HiringManager) =>
            [
                new("Tell me about a time you influenced a technical or product decision.", "influence"),
                new("Describe a failure and what you changed afterward.", "learning"),
                new("Give an example of mentoring or raising the bar for teammates.", "leadership"),
                new("How do you handle ambiguity when goals are unclear?", "ambiguity")
            ],
            (InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.SeniorPeer) =>
            [
                new("What kinds of technical problems energize you in this kind of role?", "roleDepth"),
                new("Describe a recent system or feature you shaped from idea to production.", "execution"),
                new("How do you stay current with the domain this team works in?", "motivation"),
                new("What trade-offs would you expect in the first projects here?", "problemSolving")
            ],
            (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.SeniorPeer) =>
            [
                new("Tell me about a time you disagreed with a design direction on the team.", "collaboration"),
                new("Describe how you raised the quality bar for code or architecture.", "leadership"),
                new("Give an example of debugging a complex production issue under pressure.", "problemSolving"),
                new("How do you balance speed with maintainability when shipping?", "execution")
            ],
            (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.Recruiter) =>
            [
                new("How does your background map to the core responsibilities in the job description?", "roleDepth"),
                new("Which domain concepts for this role are you strongest in today?", "roleDepth"),
                new("Describe a project where domain knowledge made the difference.", "execution"),
                new("What would you want to learn in the first months in this domain?", "motivation")
            ],
            (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.HiringManager) =>
            [
                new("Walk me through the most complex domain problem you solved in a similar role.", "roleDepth"),
                new("How do you validate that your technical choices fit the business context?", "problemSolving"),
                new("Describe depth in a toolchain or stack listed in the job snapshot.", "roleDepth"),
                new("Where have you been the go-to person for this domain on a team?", "ownership")
            ],
            (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.SeniorPeer) =>
            [
                new("How would you approach the main technical challenge described in the posting?", "roleDepth"),
                new("What design alternatives did you consider on a comparable system?", "problemSolving"),
                new("Tell me about a time you had to deepen domain knowledge quickly.", "roleDepth"),
                new("How do you review others' work in this problem space?", "leadership")
            ],
            (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.Recruiter) =>
            [
                new("How do you typically organize work when multiple teams depend on you?", "collaboration"),
                new("Describe a process you improved that helped delivery.", "execution"),
                new("Tell me about coordinating stakeholders across functions.", "communication"),
                new("What does good operational hygiene look like in your current role?", "execution")
            ],
            (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.HiringManager) =>
            [
                new("Describe a system or workflow you scaled as load or scope grew.", "execution"),
                new("How do you design feedback loops and metrics for a team process?", "problemSolving"),
                new("Tell me about a cross-team initiative you drove end-to-end.", "leadership"),
                new("Give an example of reducing toil or incident load through systemic fixes.", "ownership")
            ],
            (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.SeniorPeer) =>
            [
                new("How would you evolve our deployment or release process based on your experience?", "execution"),
                new("Describe a systemic failure mode you eliminated and how you proved it stayed fixed.", "problemSolving"),
                new("Tell me about standardizing patterns across services or repos.", "collaboration"),
                new("What observability or SLO practices do you insist on for new systems?", "execution")
            ],
            (InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.HiringManager) =>
            [
                new("Walk me through how you would structure your initial diagnosis for this case.", "problemSolving"),
                new("What trade-offs would you consider under the stated constraints?", "problemSolving"),
                new("How would you prioritize actions for the next 90 days?", "execution"),
                new("What metrics would you track to know your plan is working?", "execution")
            ],
            (InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.SeniorPeer) =>
            [
                new("How would you decompose this case into testable hypotheses?", "problemSolving"),
                new("Which data would you request first before recommending changes?", "problemSolving"),
                new("Describe risks in your proposed approach and how you would mitigate them.", "ownership"),
                new("Summarize your final recommendation with clear success criteria.", "execution")
            ],
            (InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.BarRaiser) =>
            [
                new("Before we decide, how do your assumptions align with the facts we have shared?", "communication"),
                new("Where might your recommendation conflict with the constraints you were given?", "problemSolving"),
                new("Help me reconcile any gaps between your earlier points and your final plan.", "communication"),
                new("What would change your recommendation if one key fact were different?", "problemSolving")
            ],
            (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.BarRaiser) =>
            [
                new("Earlier you mentioned impact — can you align that with the timeline you described?", "communication"),
                new("Walk me through how your role differed from the team's on that project.", "ownership"),
                new("What evidence would convince you that your approach was the right call?", "problemSolving"),
                new("Is there anything from your earlier answers you would clarify now?", "communication")
            ],
            (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.BarRaiser) =>
            [
                new("You cited depth in this domain — how does that match the scope you personally owned?", "ownership"),
                new("Help me connect your technical choice to the business outcome you claimed.", "problemSolving"),
                new("Where might a skeptical reviewer push back on your assessment?", "communication"),
                new("What would you revise if we found a contradiction in the evidence so far?", "problemSolving")
            ],
            (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.BarRaiser) =>
            [
                new("How do your process changes reconcile with the constraints the team had?", "problemSolving"),
                new("You described scale — what proof would you offer that it sustained?", "execution"),
                new("Is there tension between speed and reliability in your story?", "communication"),
                new("What single metric best validates your systems approach?", "execution")
            ],
            (InterviewPrepMode.LanguagePractice, InterviewPrepPersona.Recruiter) =>
            [
                new("Describe your background in clear, concise interview language.", "languageFluency"),
                new("Why are you interested in this opportunity?", "motivation"),
                new("Give a short example of how you communicate with stakeholders.", "communication"),
                new("How would you introduce yourself in a screening call?", "languageFluency")
            ],
            (InterviewPrepMode.LanguagePractice, InterviewPrepPersona.HiringManager) =>
            [
                new("Summarize a recent project using structured interview phrasing.", "languageFluency"),
                new("Explain a technical decision in plain language for a mixed audience.", "communication"),
                new("Describe ownership on a deliverable without jargon overload.", "languageFluency"),
                new("What would you clarify if the interviewer misunderstood your role?", "communication")
            ],
            (InterviewPrepMode.LanguagePractice, InterviewPrepPersona.SeniorPeer) =>
            [
                new("Walk through a technical example with clear situation and outcome.", "languageFluency"),
                new("How do you keep answers precise when discussing trade-offs?", "communication"),
                new("Practice a concise answer about a challenge you solved.", "languageFluency"),
                new("Rephrase a complex topic as you would in a peer interview.", "languageFluency")
            ],
            _ => throw new InterviewPrepValidationException(
                $"No fixed question bank for mode={InterviewPrepEnumNames.ToWire(mode)}, persona={InterviewPrepEnumNames.ToWire(persona)}.")
        };
    }
}
