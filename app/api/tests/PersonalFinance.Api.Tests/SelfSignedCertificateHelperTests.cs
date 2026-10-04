using System.Net;
using System.Security.Cryptography.X509Certificates;
using PersonalFinance.Api.Helpers;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class SelfSignedCertificateHelperTests : IDisposable {
    private static readonly DateTimeOffset now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string directory = Path.Combine(Path.GetTempPath(), "pf-cert-" + Guid.NewGuid().ToString("N"));

    public void Dispose() {
        if(Directory.Exists(directory)) {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void LoadOrCreate_generates_a_localhost_server_certificate_when_directory_is_empty() {
        var (certificate, generated) = SelfSignedCertificateHelper.LoadOrCreate(directory, now);
        using(certificate) {
            Assert.True(generated);
            Assert.True(File.Exists(Path.Combine(directory, SelfSignedCertificateHelper.PfxFileName)));
            Assert.True(File.Exists(Path.Combine(directory, SelfSignedCertificateHelper.CrtFileName)));
            Assert.Equal("CN=localhost", certificate.Subject);
            Assert.True(certificate.HasPrivateKey);
            var san = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
            Assert.Contains("localhost", san.EnumerateDnsNames());
            Assert.Contains(IPAddress.Loopback, san.EnumerateIPAddresses());
            var eku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
            Assert.Contains(eku.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>(), oid => oid.Value == "1.3.6.1.5.5.7.3.1");
            var expectedNotAfter = now.AddDays(SelfSignedCertificateHelper.ValidityDays);
            Assert.InRange((certificate.NotAfter.ToUniversalTime() - expectedNotAfter.UtcDateTime).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
            Assert.True(certificate.NotBefore.ToUniversalTime() <= now.UtcDateTime);
        }
    }

    [Fact]
    public void LoadOrCreate_writes_a_public_only_pem_crt_matching_the_pfx() {
        var (certificate, _) = SelfSignedCertificateHelper.LoadOrCreate(directory, now);
        using(certificate) {
            var crtPath = Path.Combine(directory, SelfSignedCertificateHelper.CrtFileName);
            Assert.StartsWith("-----BEGIN CERTIFICATE-----", File.ReadAllText(crtPath));
            using var publicOnly = X509CertificateLoader.LoadCertificateFromFile(crtPath);
            Assert.False(publicOnly.HasPrivateKey);
            Assert.Equal(certificate.Thumbprint, publicOnly.Thumbprint);
        }
    }

    [Fact]
    public void LoadOrCreate_reuses_the_existing_certificate_on_a_second_call() {
        var (first, firstGenerated) = SelfSignedCertificateHelper.LoadOrCreate(directory, now);
        var (second, secondGenerated) = SelfSignedCertificateHelper.LoadOrCreate(directory, now);
        using(first)
        using(second) {
            Assert.True(firstGenerated);
            Assert.False(secondGenerated);
            Assert.Equal(first.Thumbprint, second.Thumbprint);
        }
    }

    [Fact]
    public void LoadOrCreate_regenerates_when_fewer_than_renew_days_remain() {
        var (first, _) = SelfSignedCertificateHelper.LoadOrCreate(directory, now);
        var later = now.AddDays(SelfSignedCertificateHelper.ValidityDays - SelfSignedCertificateHelper.RenewBeforeDays + 1);
        var (second, generated) = SelfSignedCertificateHelper.LoadOrCreate(directory, later);
        using(first)
        using(second) {
            Assert.True(generated);
            Assert.NotEqual(first.Thumbprint, second.Thumbprint);
        }
    }

    [Fact]
    public void LoadOrCreate_still_reuses_with_more_than_renew_days_remaining() {
        var (first, _) = SelfSignedCertificateHelper.LoadOrCreate(directory, now);
        var later = now.AddDays(SelfSignedCertificateHelper.ValidityDays - SelfSignedCertificateHelper.RenewBeforeDays - 1);
        var (second, generated) = SelfSignedCertificateHelper.LoadOrCreate(directory, later);
        using(first)
        using(second) {
            Assert.False(generated);
            Assert.Equal(first.Thumbprint, second.Thumbprint);
        }
    }
}
