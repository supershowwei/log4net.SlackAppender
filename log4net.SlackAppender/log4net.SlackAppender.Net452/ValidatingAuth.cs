using System;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Tls;

namespace log4net.SlackAppender
{
    internal class ValidatingAuth : TlsAuthentication
    {
        private readonly string _host;

        public ValidatingAuth(string host) => this._host = host;

        public TlsCredentials GetClientCredentials(CertificateRequest r) => null;

        public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
        {
            var certs = serverCertificate.Certificate;
            if (certs == null || certs.IsEmpty) throw new TlsFatalAlert(AlertDescription.bad_certificate);

            var leafBytes = certs.GetCertificateAt(0).GetEncoded();
            var leaf = new X509Certificate2(leafBytes);

            // 4.5.2 的 X509Chain 還不是 IDisposable，不能用 using
            var chain = new X509Chain();
            for (int i = 1; i < certs.Length; i++)
                chain.ChainPolicy.ExtraStore.Add(new X509Certificate2(certs.GetCertificateAt(i).GetEncoded()));

            if (!chain.Build(leaf)) throw new TlsFatalAlert(AlertDescription.bad_certificate);

            if (!this.MatchesHost(leafBytes)) throw new TlsFatalAlert(AlertDescription.bad_certificate);
        }

        private bool MatchesHost(byte[] leafBytes)
        {
            var bcCert = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(leafBytes);
            var sans = bcCert.GetSubjectAlternativeNames();
            if (sans == null) return false;

            foreach (System.Collections.IList entry in sans)
            {
                if ((int)entry[0] != 2) continue; // 2 = dNSName

                var name = (string)entry[1];
                if (string.Equals(name, this._host, StringComparison.OrdinalIgnoreCase)) return true;

                // *.slack.com 只比對一層子網域
                if (name.StartsWith("*.", StringComparison.Ordinal))
                {
                    var dot = this._host.IndexOf('.');
                    if (dot > 0 && string.Equals(this._host.Substring(dot + 1), name.Substring(2), StringComparison.OrdinalIgnoreCase)) return true;
                }
            }

            return false;
        }
    }
}