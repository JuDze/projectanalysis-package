using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Judze.ProjectAnalysis.Editor.WebConnection
{
    // Kopīga nelielu API atbilžu lasīšana; nepieņem dublētus laukus vai papildu JSON dokumentus.
    internal static class WebResponseJson
    {
        public const int MaximumBytes = 65536;

        public static bool IsJson(string contentType)
        {
            string mediaType = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
            return mediaType == "application/json" ||
                (mediaType.StartsWith("application/", System.StringComparison.Ordinal) &&
                 mediaType.EndsWith("+json", System.StringComparison.Ordinal));
        }

        public static XElement ReadObject(string body)
        {
            if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > MaximumBytes || !HasSingleObject(body)) return null;
            try
            {
                using var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(body),
                    new XmlDictionaryReaderQuotas { MaxDepth = 16, MaxStringContentLength = MaximumBytes, MaxArrayLength = MaximumBytes });
                var root = XElement.Load(reader);
                return root.Attribute("type")?.Value == "object" ? root : null;
            }
            catch { return null; }
        }

        public static XElement Field(XElement parent, string name, string type)
        {
            var fields = parent?.Elements(name).ToArray();
            return fields?.Length == 1 && fields[0].Attribute("type")?.Value == type ? fields[0] : null;
        }

        public static string ReadString(XElement parent, string name) => Field(parent, name, "string")?.Value;

        private static bool HasSingleObject(string body)
        {
            // Unity Mono lasītājs var ignorēt tekstu aiz pirmā objekta; pārbauda arī dokumenta robežu.
            string value = body.Trim();
            if (value.Length == 0 || value[0] != '{') return false;
            int depth = 0;
            bool inString = false, escaped = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return i == value.Length - 1;
            }
            return false;
        }
    }
}
