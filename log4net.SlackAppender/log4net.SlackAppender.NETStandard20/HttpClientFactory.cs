using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace log4net.SlackAppender
{
    internal class HttpClientFactory
    {
        private static readonly Lazy<HttpClientFactory> Lazy = new Lazy<HttpClientFactory>(() => new HttpClientFactory());
        private static readonly Dictionary<Uri, HttpClient> HttpClientContainer = new Dictionary<Uri, HttpClient>();

        private HttpClientFactory()
        {
        }

        public static HttpClientFactory Instance => Lazy.Value;

        public HttpClient CreateClient(Uri baseAddress, string token)
        {
            if (HttpClientContainer.TryGetValue(baseAddress, out var client)) return client;

            lock (HttpClientContainer)
            {
                if (HttpClientContainer.TryGetValue(baseAddress, out client)) return client;

                client = new HttpClient
                         {
                             BaseAddress = baseAddress,
                             DefaultRequestHeaders =
                             {
                                 Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token)
                             }
                         };

                ServicePointManager.FindServicePoint(baseAddress).ConnectionLeaseTimeout = 60000;

                HttpClientContainer[baseAddress] = client;

                return client;
            }
        }
    }
}