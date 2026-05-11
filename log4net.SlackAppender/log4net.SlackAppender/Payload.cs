using System.Collections.Generic;

namespace log4net.Appender
{
    internal class Payload
    {
        public string Channel { get; set; }

        public string Text { get; set; }

        public List<Block> Blocks { get; set; }

        internal abstract class Block
        {
        }

        internal class HeaderBlock : Block
        {
            public string Type => "header";

            public TextObject Text { get; set; }
        }

        internal class TextObject
        {
            public string Type { get; set; }

            public string Text { get; set; }
        }

        internal class MarkdownBlock : Block
        {
            public string Type => "markdown";

            public string Text { get; set; }
        }
    }
}