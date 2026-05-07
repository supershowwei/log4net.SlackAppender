using System;
using System.Collections.Generic;
using log4net.Core;
using log4net.SlackAppender;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;

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

        protected override void Append(LoggingEvent loggingEvent)
        {
            var client = new RestClient(ApiUri.GetLeftPart(UriPartial.Authority));

            var request = new RestRequest(ApiUri.PathAndQuery, Method.POST);
            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            request.AddHeader("Authorization", $"Bearer {this.Token}");

            var payload = this.GeneratePayload(loggingEvent);

            request.AddParameter("application/json; charset=utf-8", JsonConvert.SerializeObject(payload, SlackJsonSettings), ParameterType.RequestBody);

            client.Execute(request);
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

            return new Payload
                   {
                       Channel = this.Channel,
                       Username = $"{GlobalContext.Properties["log4net:HostName"]}.{GlobalContext.Properties["ApplicationName"]}",
                       Text = $"{emoji} {loggingEvent.Level.DisplayName} on {loggingEvent.LoggerName}\n{renderedMessage.SmsTruncate()}",
                       Blocks = new List<Payload.Block>
                                {
                                    new Payload.Block
                                    {
                                        Type = "header",
                                        Text = new Payload.TextObject
                                               {
                                                   Type = "plain_text",
                                                   Text = $"{emoji} {loggingEvent.Level.DisplayName} on {loggingEvent.LoggerName}"
                                               }
                                    },
                                    new Payload.Block
                                    {
                                        Type = "section",
                                        Text = new Payload.TextObject { Type = "mrkdwn", Text = $"```\n{renderedMessage}\n```" }
                                    }
                                }
                   };
        }
    }
}