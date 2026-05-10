using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace bestgen.Services.Zatca;

/// <summary>
/// Supplies the ECDSA P-256 certificate used to cryptographically stamp
/// invoices. In production, point at a ZATCA-issued PCSID via configuration
/// (path to PEM/PFX). For dev, a self-signed certificate is auto-generated
/// once and persisted under <c>App_Data/zatca</c> so signatures stay stable
/// across restarts.
/// </summary>
public class ZatcaCertificateProvider
{
    private readonly string _certDir;
    private X509Certificate2? _cached;
    private readonly object _lock = new();

    public ZatcaCertificateProvider(IWebHostEnvironment env)
    {
        _certDir = Path.Combine(env.ContentRootPath, "App_Data", "zatca");
        Directory.CreateDirectory(_certDir);
    }

    public X509Certificate2 GetCertificate()
    {
        if (_cached is not null) return _cached;
        lock (_lock)
        {
            if (_cached is not null) return _cached;

            var pfxPath = Path.Combine(_certDir, "dev-csid.pfx");
            if (File.Exists(pfxPath))
            {
                _cached = new X509Certificate2(File.ReadAllBytes(pfxPath), "bestgen-dev",
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
                return _cached;
            }

            // Generate a self-signed ECDSA P-256 certificate. Real ZATCA
            // PCSIDs share the same shape (EC P-256, X.509), so swapping
            // this out later is just a config change.
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var req = new CertificateRequest(
                "CN=Bestgen Dev CSID, O=Bestgen ERP, C=SA",
                key,
                HashAlgorithmName.SHA256);

            var cert = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(2));

            var pfx = cert.Export(X509ContentType.Pfx, "bestgen-dev");
            File.WriteAllBytes(pfxPath, pfx);

            _cached = new X509Certificate2(pfx, "bestgen-dev",
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet);
            return _cached;
        }
    }

    /// <summary>Public key bytes (DER-encoded SubjectPublicKeyInfo) for QR tag 8.</summary>
    public byte[] GetPublicKeyDer()
    {
        var cert = GetCertificate();
        return cert.GetPublicKey();
    }

    /// <summary>SHA-256 of the certificate (DER) — used as Tag 9 in Phase 2 QR.</summary>
    public byte[] GetCertificateSignatureBytes()
    {
        var cert = GetCertificate();
        var rawCertBytes = cert.RawData;
        return SHA256.HashData(rawCertBytes);
    }

    /// <summary>Sign a SHA-256 hash with ECDSA P-256 and return the signature bytes.</summary>
    public byte[] SignHash(byte[] hash)
    {
        var cert = GetCertificate();
        using var ecdsa = cert.GetECDsaPrivateKey() ?? throw new InvalidOperationException("Certificate has no EC private key.");
        return ecdsa.SignHash(hash);
    }
}
