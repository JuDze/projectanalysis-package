using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Serializācijā iekļauj adresi, projekta UUID un formāta versiju, bet ne tokenu.
    internal static class WebConnectionSettingsJson
    {
        [Serializable] private sealed class Document
        {
            public int Version;
            public string ServerUrl;
            public string WebProjectUuid;
        }

        public static string Serialize(WebConnectionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return JsonUtility.ToJson(new Document { Version = 1,
                ServerUrl = WebConnectionUrl.Normalize(settings.ServerUrl),
                WebProjectUuid = WebProjectUuidValidator.Normalize(settings.WebProjectUuid) }, true);
        }

        private static void ValidateStructure(string json)
        {
            // JsonUtility var pārvērst null par tukšu virkni, tāpēc vispirms pārbauda sākotnējos tipus.
            using var reader = JsonReaderWriterFactory.CreateJsonReader(
                Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max);
            bool hasUrl = false, hasVersion = false, hasProjectUuid = false;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                string type = reader.GetAttribute("type");
                if (reader.Depth == 0)
                {
                    if (type != "object") throw new InvalidDataException("Connection settings must be an object.");
                }
                else if (reader.Depth == 1 && reader.LocalName == nameof(Document.ServerUrl))
                {
                    if (hasUrl || type != "string") throw new InvalidDataException("ServerUrl must be a single string.");
                    hasUrl = true;
                }
                else if (reader.Depth == 1 && reader.LocalName == nameof(Document.Version))
                {
                    if (hasVersion || type != "number") throw new InvalidDataException("Version must be a single number.");
                    hasVersion = true;
                }
                else if (reader.Depth == 1 && reader.LocalName == nameof(Document.WebProjectUuid))
                {
                    if (hasProjectUuid || type != "string") throw new InvalidDataException("WebProjectUuid must be a single string.");
                    hasProjectUuid = true;
                }
                else throw new InvalidDataException("Unexpected connection settings field.");
            }
            if (!hasUrl || !hasVersion) throw new InvalidDataException("Version and ServerUrl are required.");
        }

        public static WebConnectionSettings Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Connection settings are empty.");
            ValidateStructure(json);
            var document = JsonUtility.FromJson<Document>(json);
            if (document == null || document.Version != 1 || document.ServerUrl == null)
                throw new InvalidDataException("Connection settings are invalid or unsupported.");
            return new WebConnectionSettings
            {
                ServerUrl = WebConnectionUrl.Normalize(document.ServerUrl),
                WebProjectUuid = WebProjectUuidValidator.Normalize(document.WebProjectUuid)
            };
        }
    }
}
