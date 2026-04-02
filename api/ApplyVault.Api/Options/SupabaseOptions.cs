namespace ApplyVault.Api.Options;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;

    public string Audience { get; set; } = "authenticated";
}
