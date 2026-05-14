using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using log4net.Core;
using log4net.SlackAppender;

namespace log4net.Appender
{
    public class SlackAppender : AppenderSkeleton
    {
        private static readonly Uri ApiUri = new Uri("https://slack.com/api/chat.postMessage");

        private static readonly JsonSerializerOptions SlackJsonSettings = new JsonSerializerOptions
                                                                          {
                                                                              PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                                              Converters = { new BlockConverter() }
                                                                          };

        public string Token { get; set; }

        public string Channel { get; set; }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var payload = this.GeneratePayload(loggingEvent);

            var json = JsonSerializer.Serialize<object>(payload, SlackJsonSettings);

            var request = new HttpRequestMessage(HttpMethod.Post, ApiUri.PathAndQuery)
                          {
                              Content = new StringContent(json, Encoding.UTF8, "application/json")
                          };

            var httpClient = HttpClientFactory.Instance.CreateClient(new Uri(ApiUri.GetLeftPart(UriPartial.Authority)), this.Token);

            httpClient.SendAsync(request).GetAwaiter().GetResult();
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