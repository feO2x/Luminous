namespace Luminous.InSign.Tests.InSignTriangulation;

/// <summary>
/// Settings for the inSign sandbox tests. The sandbox URL and credentials are public (see
/// https://getinsign.github.io/insign-getting-started/) and therefore have defaults; the signer
/// email addresses are personal and must be provided via .NET User Secrets or environment
/// variables (keys "InSign:LicensorEmail" and "InSign:LicenseeEmail"). The session id of a
/// previously created signing session can be provided via "InSign:SessionId" to reload it.
/// </summary>
public sealed record InSignOptions(
    string BaseUrl,
    string UserName,
    string Password,
    string? LicensorEmail,
    string? LicenseeEmail,
    string? SessionId
)
{
    public static InSignOptions Load()
    {
        var configuration = TestSettings.Configuration;

        return new InSignOptions(
            configuration["InSign:BaseUrl"] ?? "https://sandbox.test.getinsign.show",
            configuration["InSign:UserName"] ?? "controller",
            configuration["InSign:Password"] ?? "pwd.insign.sandbox.4561",
            NullIfWhiteSpace(configuration["InSign:LicensorEmail"]),
            NullIfWhiteSpace(configuration["InSign:LicenseeEmail"]),
            NullIfWhiteSpace(configuration["InSign:SessionId"])
        );
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
