using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace log4net.SlackAppender
{
    /// <summary>
    ///     HttpClient 用的 handler，TLS 改用 BouncyCastle 處理，
    ///     完全繞過 Windows SChannel，避開 Server 2012 R2 cipher suite 不夠新的問題。
    ///     用法：new HttpClient(new BouncyCastleHttpHandler())
    /// </summary>
    public class BouncyCastleHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.Run(() => this.SendCore(request), cancellationToken);
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }

                if (match) return i;
            }

            return -1;
        }

        private HttpResponseMessage SendCore(HttpRequestMessage request)
        {
            var uri = request.RequestUri;
            if (uri.Scheme != "https")
                throw new NotSupportedException("Only HTTPS is supported");

            using (var tcp = new TcpClient())
            {
                tcp.ReceiveTimeout = 60000;
                tcp.SendTimeout = 60000;
                tcp.Connect(uri.Host, uri.Port);

                var crypto = new BcTlsCrypto(new SecureRandom());
                var tlsClient = new SniTlsClient(crypto, uri.Host);
                var protocol = new TlsClientProtocol(tcp.GetStream());
                protocol.Connect(tlsClient);

                try
                {
                    var stream = protocol.Stream;
                    this.WriteRequest(stream, request, uri);
                    return this.ReadResponse(stream);
                }
                finally
                {
                    try
                    {
                        protocol.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void WriteRequest(Stream stream, HttpRequestMessage request, Uri uri)
        {
            byte[] body = null;
            if (request.Content != null)
                body = request.Content.ReadAsByteArrayAsync().Result;

            var sb = new StringBuilder();
            sb.Append(request.Method.Method).Append(' ').Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n");
            sb.Append("Host: ").Append(uri.Host);
            if (!uri.IsDefaultPort) sb.Append(':').Append(uri.Port);
            sb.Append("\r\n");

            foreach (var h in request.Headers)
                foreach (var v in h.Value)
                    sb.Append(h.Key).Append(": ").Append(v).Append("\r\n");

            if (request.Content != null)
            {
                foreach (var h in request.Content.Headers)
                    foreach (var v in h.Value)
                        sb.Append(h.Key).Append(": ").Append(v).Append("\r\n");

                if (body != null && !request.Content.Headers.Contains("Content-Length"))
                    sb.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            }

            if (!request.Headers.Contains("User-Agent"))
                sb.Append("User-Agent: BouncyCastleHttpHandler/1.0\r\n");

            sb.Append("Connection: close\r\n\r\n");

            var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (body != null && body.Length > 0)
                stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private HttpResponseMessage ReadResponse(Stream stream)
        {
            // 因為送了 Connection: close，server 回完會關連線，直接讀到 EOF
            byte[] raw;
            using (var ms = new MemoryStream())
            {
                var buf = new byte[8192];
                try
                {
                    int n;
                    while ((n = stream.Read(buf, 0, buf.Length)) > 0)
                        ms.Write(buf, 0, n);
                }
                catch (IOException)
                {
                    /* EOF */
                }

                raw = ms.ToArray();
            }

            // 切出 header / body
            int headerEnd = IndexOf(raw, new byte[] { 0x0D, 0x0A, 0x0D, 0x0A });
            if (headerEnd < 0)
                throw new HttpRequestException("Malformed HTTP response");

            var headerText = Encoding.ASCII.GetString(raw, 0, headerEnd);
            var bodyBytes = new byte[raw.Length - headerEnd - 4];
            Buffer.BlockCopy(raw, headerEnd + 4, bodyBytes, 0, bodyBytes.Length);

            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);

            // 第一行: HTTP/1.1 200 OK
            var statusParts = lines[0].Split(new[] { ' ' }, 3);
            int statusCode = int.Parse(statusParts[1]);

            var response = new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                ReasonPhrase = statusParts.Length >= 3 ? statusParts[2] : string.Empty,
                Version = new Version(1, 1)
            };

            var content = new ByteArrayContent(bodyBytes);

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;

                var name = line.Substring(0, colon).Trim();
                var value = line.Substring(colon + 1).Trim();

                // 先試 response header，失敗就放 content header
                if (!response.Headers.TryAddWithoutValidation(name, value))
                    content.Headers.TryAddWithoutValidation(name, value);
            }

            response.Content = content;
            return response;
        }
    }

    internal class SniTlsClient : DefaultTlsClient
    {
        private readonly string _hostname;

        public SniTlsClient(BcTlsCrypto crypto, string hostname)
            : base(crypto) =>
            this._hostname = hostname;

        public override TlsAuthentication GetAuthentication() => new ValidatingAuth(this._hostname);

        protected override System.Collections.IList GetSniServerNames() =>
            new System.Collections.ArrayList { new ServerName(NameType.host_name, Encoding.UTF8.GetBytes(this._hostname)) };
    }

    internal class AcceptAllAuth : TlsAuthentication
    {
        public TlsCredentials GetClientCredentials(CertificateRequest r) => null;

        public void NotifyServerCertificate(TlsServerCertificate c)
        {
            /* accept */
        }
    }
}