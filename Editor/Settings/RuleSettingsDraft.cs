using System;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Rediģējamo iestatījumu kopija un sākotnējā stāvokļa salīdzināšana.
    internal sealed class RuleSettingsDraft
    {
        private string _savedJson;
        private readonly string _defaultsJson;

        public RuleSettings Values { get; private set; }

        public bool HasChanges => !string.Equals(_savedJson, JsonUtility.ToJson(Values), StringComparison.Ordinal);
        public bool HasDefaultsValues => string.Equals(_defaultsJson, JsonUtility.ToJson(Values), StringComparison.Ordinal);

        public RuleSettingsDraft(RuleSettings initialSettings)
        {
            if (initialSettings == null)
                throw new ArgumentNullException(nameof(initialSettings));

            // Sākotnējās vērtības saglabā tikai atmiņā.
            _savedJson = JsonUtility.ToJson(initialSettings);
            _defaultsJson = JsonUtility.ToJson(new RuleSettings());
            Values = Deserialize(_savedJson);
        }

        public RuleSettings CreateSavedSnapshot()
        {
            // Analīze izmanto apstiprinātos noteikumus, nevis nesaglabāto melnrakstu.
            return Deserialize(_savedJson);
        }

        public RuleSettings CreateSnapshot()
        {
            // Izveido pašreizējo vērtību neatkarīgu kopiju.
            return Deserialize(JsonUtility.ToJson(Values));
        }

        private static RuleSettings Deserialize(string json)
        {
            return JsonUtility.FromJson<RuleSettings>(json)
                ?? throw new InvalidOperationException("Could not create a rule settings copy.");
        }

        public void AcceptSavedSnapshot(RuleSettings snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            // Saglabā pieņemto momentuzņēmumu turpmākai izmaiņu salīdzināšanai.
            _savedJson = JsonUtility.ToJson(snapshot);
        }

        // Cancel atjauno pēdējās saglabātās vērtības.
        public void CancelChanges()
        {
            Values = Deserialize(_savedJson);
        }

        // Reset atjauno noklusējuma vērtības tikai melnrakstā.
        public void ResetToDefaults()
        {
            // Atiestata tikai noteikumus; projekta būves mērķis netiek mainīts.
            Values = new RuleSettings();
        }
    }
}
