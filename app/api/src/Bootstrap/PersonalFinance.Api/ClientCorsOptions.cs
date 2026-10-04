namespace PersonalFinance.Api;

/// <summary>
/// Binds the <c>Cors</c> configuration section.
/// </summary>
public sealed class ClientCorsOptions {
    public const string SectionName = "Cors";
    public const string PolicyName = "client";

    public string[] AllowedOrigins { get; set; } = [];
}
