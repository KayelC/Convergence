using System.Globalization;
using System.Text;
using JRPGPrototype.Data.Definitions;

namespace JRPGPrototype.Logic.Core
{
    internal static class LegacyContentIdCodec
    {
        public static ContentId Encode(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes((value ?? string.Empty).Trim());
            var builder = new StringBuilder("legacy_");
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return ContentId.Parse(builder.ToString());
        }

        public static string Decode(ContentId id)
        {
            string value = id.ToString();
            const string prefix = "legacy_";
            if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
                (value.Length - prefix.Length) % 2 != 0)
            {
                return value;
            }

            byte[] bytes = new byte[(value.Length - prefix.Length) / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string hex = value.Substring(prefix.Length + i * 2, 2);
                bytes[i] = byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
