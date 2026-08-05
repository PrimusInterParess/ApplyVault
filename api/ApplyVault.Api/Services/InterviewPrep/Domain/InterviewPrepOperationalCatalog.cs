namespace ApplyVault.Api.Services.InterviewPrep.Domain;



/// <summary>

/// Operational surface: enums may include future values, but create/prepare only accepts these sets.

/// M8 adds problemSolvingCase mode and barRaiser persona with explicit mode×persona pairs.
/// M9 adds languagePractice mode (recruiter/hiringManager/seniorPeer).

/// </summary>

public static class InterviewPrepOperationalCatalog

{

    private static readonly HashSet<InterviewPrepMode> OperationalModes =

    [

        InterviewPrepMode.ScreeningAndMotivation,

        InterviewPrepMode.BehavioralAndCulture,

        InterviewPrepMode.RoleAndDomainDepth,

        InterviewPrepMode.ProcessAndSystems,

        InterviewPrepMode.ProblemSolvingCase,

        InterviewPrepMode.LanguagePractice,

        InterviewPrepMode.FullLoop

    ];



    private static readonly HashSet<InterviewPrepPersona> OperationalPersonas =

    [

        InterviewPrepPersona.Recruiter,

        InterviewPrepPersona.HiringManager,

        InterviewPrepPersona.SeniorPeer,

        InterviewPrepPersona.BarRaiser

    ];



    private static readonly HashSet<(InterviewPrepMode Mode, InterviewPrepPersona Persona)> OperationalPairs =

    [

        (InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.Recruiter),

        (InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.HiringManager),

        (InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.SeniorPeer),

        (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.Recruiter),

        (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.HiringManager),

        (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.SeniorPeer),

        (InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.BarRaiser),

        (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.Recruiter),

        (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.HiringManager),

        (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.SeniorPeer),

        (InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.BarRaiser),

        (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.Recruiter),

        (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.HiringManager),

        (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.SeniorPeer),

        (InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.BarRaiser),

        (InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.HiringManager),

        (InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.SeniorPeer),

        (InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.BarRaiser),

        (InterviewPrepMode.LanguagePractice, InterviewPrepPersona.Recruiter),

        (InterviewPrepMode.LanguagePractice, InterviewPrepPersona.HiringManager),

        (InterviewPrepMode.LanguagePractice, InterviewPrepPersona.SeniorPeer),

        (InterviewPrepMode.FullLoop, InterviewPrepPersona.HiringManager)

    ];



    public static bool IsOperationalMode(InterviewPrepMode mode) => OperationalModes.Contains(mode);



    public static bool IsOperationalPersona(InterviewPrepPersona persona) => OperationalPersonas.Contains(persona);



    public static bool IsOperationalPair(InterviewPrepMode mode, InterviewPrepPersona persona) =>

        OperationalPairs.Contains((mode, persona));



    public static void EnsureOperationalCreate(InterviewPrepMode mode, InterviewPrepPersona persona)

    {

        if (!IsOperationalMode(mode))

        {

            throw new InterviewPrepValidationException(

                $"Interview mode '{InterviewPrepEnumNames.ToWire(mode)}' is not available yet.");

        }



        if (!IsOperationalPersona(persona))

        {

            throw new InterviewPrepValidationException(

                $"Interview persona '{InterviewPrepEnumNames.ToWire(persona)}' is not available yet.");

        }



        if (!IsOperationalPair(mode, persona))

        {

            throw new InterviewPrepValidationException(

                $"Interview mode '{InterviewPrepEnumNames.ToWire(mode)}' is not available with persona "

                + $"'{InterviewPrepEnumNames.ToWire(persona)}' yet.");

        }

    }

}


