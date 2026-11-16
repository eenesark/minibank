namespace MiniBank.Common;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = "MiniBankSuperSecretKeyForJwtAuthentication2026!+MustBeLongEnough";
    public string Issuer { get; set; } = "MiniBankServer";
    public string Audience { get; set; } = "MiniBankClient";
    public double ExpireMinutes { get; set; } = 1440;
}

