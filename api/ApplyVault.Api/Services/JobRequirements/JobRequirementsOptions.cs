using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Services.JobRequirements;

internal sealed class JobRequirementsOptions
{
    public const string SectionName = "JobRequirements";

    public bool Enabled { get; set; } = true;

    [Range(1, 100)]
    public int BatchSize { get; set; } = 5;

    [Range(1, 3600)]
    public int PollIntervalSeconds { get; set; } = 30;

    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;

    [Range(1, 86400)]
    public int RetryDelaySeconds { get; set; } = 60;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 60;

    // A lease comfortably outlives the bounded provider operation.
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(TimeoutSeconds + 60);
}
