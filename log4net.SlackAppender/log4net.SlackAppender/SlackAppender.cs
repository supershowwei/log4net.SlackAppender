using System;
using System.Collections.Generic;
using log4net.Core;
using log4net.SlackAppender;
using log4net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
#if NET452
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
#else
using RestSharp;
#endif
namespace log4net.Appender
{
    public class SlackAppender : AppenderSkeleton
    {
        private static readonly Uri ApiUri = new Uri("https://slack.com/api/chat.postMessage");

        private static readonly JsonSerializerSettings SlackJsonSettings = new JsonSerializerSettings
                                                                           {
                                                                               ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                                                               NullValueHandling = NullValueHandling.Ignore
                                                                           };

        public string Token { get; set; }

        public string Channel { get; set; }
#if NET452
        // 2012R2 SChannel 缺 Slack 要求的 cipher suite，改由 BouncyCastle 處理 TLS
        private static readonly HttpClient HttpClient = new HttpClient(new BouncyCastleHttpHandler());
#endif
        protected override void Append(LoggingEvent loggingEvent)
        {
            var payload = this.GeneratePayload(loggingEvent);
            var json = JsonConvert.SerializeObject(payload, SlackJsonSettings);

#if NET452
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, ApiUri) { Content = new StringContent(json, Encoding.UTF8, "application/json") })
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Token);

                    using (var response = HttpClient.SendAsync(request).GetAwaiter().GetResult())
                    {
                        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        if (!IsSuccess(response.IsSuccessStatusCode, content))
                        {
                            LogLog.Debug(typeof(SlackAppender), $"Failed to post message to Slack. StatusCode={(int)response.StatusCode}, Content={content}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // TLS 交握失敗、憑證驗證失敗、Socket 逾時等都會走到這裡
                LogLog.Debug(typeof(SlackAppender), "Failed to post message to Slack.", ex);
            }
#else
            var client = new RestClient(ApiUri.GetLeftPart(UriPartial.Authority));

            var request = new RestRequest(ApiUri.PathAndQuery, Method.POST);
            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            request.AddHeader("Authorization", $"Bearer {this.Token}");
            request.AddParameter("application/json; charset=utf-8", json, ParameterType.RequestBody);

            var response = client.Execute(request);

            if (!IsSuccess(response.IsSuccessful, response.Content))
            {
                LogLog.Debug(typeof(SlackAppender), $"Failed to post message to Slack. ResponseStatus={response.ResponseStatus}, StatusCode={(int)response.StatusCode}, Content={response.Content}", response.ErrorException);
            }
#endif
        }

        private static string GetEmoji(Level level)
        {
            switch (level.DisplayName.ToLowerInvariant())
            {
                case "warn": return ":warning:";
                case "error": return ":rotating_light:";
                case "fatal": return ":fire:";
                default: return ":information_source:";
            }
        }

        private static bool IsSuccess(bool isHttpSuccess, string content)
        {
            if (!isHttpSuccess || string.IsNullOrEmpty(content)) return false;

            try
            {
                return (bool?)JObject.Parse(content)["ok"] == true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private Payload GeneratePayload(LoggingEvent loggingEvent)
        {
            var emoji = GetEmoji(loggingEvent.Level);
            var renderedMessage = this.RenderLoggingEvent(loggingEvent);

            var header = $"{emoji} {loggingEvent.Level.DisplayName} from {loggingEvent.LoggerName} in {GlobalContext.Properties["ApplicationName"]} on {GlobalContext.Properties["log4net:HostName"]}";

            return new Payload
                   {
                       Channel = this.Channel,
                       Text = $"{header}\n{renderedMessage.SmsTruncate()}",
                       Blocks = new List<Payload.Block>
                                {
                                    new Payload.HeaderBlock { Text = new Payload.TextObject { Type = "plain_text", Text = header } },
                                    new Payload.MarkdownBlock { Text = $"```\n{renderedMessage}\n```" }
                                }
                   };
        }
    }
}