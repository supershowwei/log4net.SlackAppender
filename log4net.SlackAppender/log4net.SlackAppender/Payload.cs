using System.Collections.Generic;

namespace log4net.Appender
{
    internal class Payload
    {
        public string Channel { get; set; }

        public string Username { get; set; }

        public string Text { get; set; }

        public List<Block> Blocks { get; set; }

        internal class Block
        {
            public string Type { get; set; }

            public TextObject Text { get; set; }
        }

        internal class TextObject
        {
            public string Type { get; set; }

            public string Text { get; set; }
        }
    }
}