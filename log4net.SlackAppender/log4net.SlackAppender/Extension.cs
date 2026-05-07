using System.Text;

namespace log4net.SlackAppender
{
    internal static class Extension
    {
        public static string SmsTruncate(this string value, int maxWidth = 70, string ellipsis = "...")
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.GetSmsWidth() <= maxWidth) return value;

            var budget = maxWidth - ellipsis.GetSmsWidth();
            if (budget <= 0) return string.Empty;

            var sb = new StringBuilder(value.Length);
            var width = 0;

            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                var isPair = char.IsHighSurrogate(ch) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]);
                var w = ch < 128 ? 1 : 2;

                if (width + w > budget) break;

                sb.Append(ch);
                if (isPair) sb.Append(value[++i]);
                width += w;
            }

            return sb.Append(ellipsis).ToString();
        }

        private static int GetSmsWidth(this string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            var width = 0;
            for (var i = 0; i < value.Length; i++)
            {
                // surrogate pair（emoji、罕用 CJK 擴展區）一律視為寬度 2 並一起跳過
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    width += 2;
                    i++;
                }
                else
                {
                    width += value[i] < 128 ? 1 : 2;
                }
            }

            return width;
        }
    }
}