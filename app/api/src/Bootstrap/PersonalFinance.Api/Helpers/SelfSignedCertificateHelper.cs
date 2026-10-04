using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PersonalFinance.Api.Helpers;

/// <summary>
/// Loads the self-signed localhost HTTPS certificate from a directory, generating and persisting it when missing or close to expiry.
/// </summary>
internal static class SelfSignedCertificateHelper {
    public const string PfxFileName = "personal-finance.pfx";
    public const string CrtFileName = "personal-finance.crt";
    public const int ValidityDays = 397;
    public const int RenewBeforeDays = 30;

    public static (X509Certificate2 Certificate, bool Generated) LoadOrCreate(string directory, DateTimeOffset now) {
        var pfxPath = Path.Combine(directory, PfxFileName);
        if(File.Exists(pfxPath)) {
            var existing = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, null);
            if(existing.NotAfter.ToUniversalTime() - now.UtcDateTime > TimeSpan.FromDays(RenewBeforeDays)) {
                return (existing, false);
            }
            existing.Dispose();
        }
        Directory.CreateDirectory(directory);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        using var created = request.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(ValidityDays));
        File.WriteAllBytes(pfxPath, created.Export(X509ContentType.Pfx));
        File.WriteAllText(Path.Combine(directory, CrtFileName), created.ExportCertificatePem());
        return (X509CertificateLoader.LoadPkcs12FromFile(pfxPath, null), true);
    }
}
