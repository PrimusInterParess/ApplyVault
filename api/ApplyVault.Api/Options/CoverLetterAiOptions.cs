using System.ComponentModel.DataAnnotations;

namespace ApplyVault.Api.Options;

public sealed class CoverLetterAiOptions
{
    public const string SectionName = "CoverLetter";

    public const string OllamaProvider = "Ollama";

    public const string GoogleProvider = "Google";

    public const string DefaultModel = "qwen3:32b";

    public bool Enabled { get; set; }

    [Required]
    public string Provider { get; set; } = OllamaProvider;

    [Required]
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";

    [Required]
    public string Model { get; set; } = DefaultModel;

    [Range(30, 600)]
    public int TimeoutSeconds { get; set; } = 180;
}
