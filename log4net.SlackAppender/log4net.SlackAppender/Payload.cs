using System.Collections.Generic;
using log4net.SlackAppender;

namespace log4net.Appender
{
    internal class Payload
    {
        public string Color { get; set; }

        public string Fallback { get; set; }

        public List<Field> Fields { get; set; }

        public string Username { get; set; }
    }
}