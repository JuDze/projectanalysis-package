using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Pārveido iestatījumus JSON formātā un pārbauda ielādētos datus.
    internal static class RuleSettingsJson
    {
        private const int CurrentVersion = 4;

        [Serializable]
        private sealed class Document
        {
            public int Version;
            public RuleSettings Settings;
        }

        public static string Serialize(RuleSettings settings)
        {
            Validate(settings);
            var document = new Document
            {
                Version = CurrentVersion,
                Settings = settings
            };
            return JsonUtility.ToJson(document, true);
        }

        public static RuleSettings Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The rule settings file is empty.");

            ValidateDocumentStructure(json);
            var document = new Document { Settings = new RuleSettings() };
            JsonUtility.FromJsonOverwrite(json, document);
            if (document.Version != CurrentVersion)
                throw new InvalidDataException("Unsupported rule settings file version.");

            Validate(document.Settings);
            return document.Settings;
        }

        private static void ValidateDocumentStructure(string json)
        {
            // Unity var aizstāt null objektus ar noklusējumiem, tāpēc pārbauda sākotnējo JSON.
            using var reader = JsonReaderWriterFactory.CreateJsonReader(
                Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max);
            bool hasSettings = false;
            bool hasVersion = false;
            bool inSettings = false;
            Type ruleType = null;
            var sections = new HashSet<string>(StringComparer.Ordinal);
            var properties = new HashSet<string>(StringComparer.Ordinal);

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                string type = reader.GetAttribute("type");

                if (reader.Depth == 0 && type != "object")
                    throw new InvalidDataException("The settings document must be an object.");

                if (reader.Depth == 1)
                {
                    inSettings = reader.LocalName == nameof(Document.Settings);
                    if (inSettings)
                    {
                        if (hasSettings || type != "object")
                            throw new InvalidDataException("Settings must be a single JSON object.");
                        hasSettings = true;
                    }
                    else if (reader.LocalName == nameof(Document.Version))
                    {
                        if (hasVersion || type != "number")
                            throw new InvalidDataException("Version must be a single number.");
                        hasVersion = true;
                    }
                    else throw new InvalidDataException($"Unknown settings document field: {reader.LocalName}.");
                }
                else if (inSettings && reader.Depth > 1)
                {
                    // Pieņem tikai pašreizējo iestatījumu klašu laukus.
                    const BindingFlags fields = BindingFlags.Public | BindingFlags.Instance;
                    if (reader.Depth == 2)
                    {
                        var section = typeof(RuleSettings).GetField(reader.LocalName, fields);
                        if (section == null)
                            throw new InvalidDataException($"Unknown rule settings section: {reader.LocalName}.");
                        if (!sections.Add(reader.LocalName))
                            throw new InvalidDataException($"Duplicate rule settings section: {reader.LocalName}.");
                        ruleType = section.FieldType;
                        properties.Clear();
                    }
                    else if (reader.Depth == 3)
                    {
                        if (ruleType?.GetField(reader.LocalName, fields) == null)
                            throw new InvalidDataException($"Unknown rule setting: {reader.LocalName}.");
                        // Divas vienāda lauka vērtības nedrīkst atšķirīgi interpretēt ielādes laikā.
                        if (!properties.Add(reader.LocalName))
                            throw new InvalidDataException($"Duplicate rule setting: {reader.LocalName}.");
                    }

                    // Noteikumu sadaļas ir objekti; to lauki ir skaitļi vai loģiskas vērtības.
                    string expectedType = reader.Depth == 2 ? "object"
                        : reader.LocalName == "Enabled" ? "boolean" : "number";
                    if (reader.Depth > 3 || type != expectedType)
                        throw new InvalidDataException($"Invalid JSON value for {reader.LocalName}.");
                }
            }

            if (!hasSettings || !hasVersion)
                throw new InvalidDataException("Version and Settings are required.");
        }

        private static void Validate(RuleSettings settings)
        {
            if (settings == null)
                throw new InvalidDataException("The file does not contain rule settings.");

            // Visām formām nepieciešami atbilstošie iestatījumu objekti.
            object[] rules =
            {
                settings.Tex001, settings.Tex002, settings.Tex003, settings.Tex004,
                settings.Mod001, settings.Mod002, settings.Mod003,
                settings.Mod004,
                settings.Aud001, settings.Aud002, settings.Aud003, settings.Aud004, settings.Aud005
            };
            if (Array.Exists(rules, rule => rule == null))
                throw new InvalidDataException("A rule settings section is missing.");

            // No datnes var saņemt arī saskarnē nepieejamas izvēles.
            RequireEnumValue(settings.Aud001.Goal, "AUD001 goal");
            RequireEnumValue(settings.Aud002.TargetFormat, "AUD002 format");

            // Robežvērtībām izmanto jau esošo pārbaudi.
            var issues = new RuleSettingsValidator().Validate(settings);
            if (issues.Count > 0)
                throw new InvalidDataException(
                    $"Invalid {issues[0].RuleId} settings: {issues[0].Message}");
        }

        private static void RequireEnumValue<T>(T value, string name)
            where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new InvalidDataException($"Unsupported value for {name}.");
        }
    }
}
