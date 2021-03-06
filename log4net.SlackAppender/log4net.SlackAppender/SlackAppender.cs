﻿using System.Collections.Generic;
using log4net.Core;
using log4net.SlackAppender;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp;

namespace log4net.Appender
{
    public class SlackAppender : AppenderSkeleton
    {
        public string WebhookUrl { get; set; }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var client = new RestClient(this.WebhookUrl);

            var request = new RestRequest(Method.POST);

            var payload = this.GeneratePayload(loggingEvent);

            request.AddParameter(
                "payload",
                JsonConvert.SerializeObject(
                    payload,
                    new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));

            client.Execute(request);
        }

        private static string GetColor(Level level)
        {
            switch (level.DisplayName.ToLowerInvariant())
            {
                case "warn":
                    return "warning";
                case "error":
                case "fatal":
                    return "danger";
                default:
                    return "good";
            }
        }

        private Payload GeneratePayload(LoggingEvent loggingEvent)
        {
            return new Payload
                       {
                           Username =
                               string.Format(
                                   "{0}.{1}",
                                   GlobalContext.Properties["log4net:HostName"],
                                   GlobalContext.Properties["ApplicationName"]),
                           Fallback = string.Format("Occurs {0}", loggingEvent.Level.DisplayName),
                           Color = GetColor(loggingEvent.Level),
                           Fields =
                               new List<Field>
                                   {
                                       new Field
                                           {
                                               Title =
                                                   string.Format(
                                                       "{0} in {1}",
                                                       loggingEvent.Level.DisplayName,
                                                       loggingEvent.LoggerName),
                                               Value = this.RenderLoggingEvent(loggingEvent),
                                               Short = false
                                           }
                                   }
                       };
        }
    }
}