using System;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Datnē glabā savienojuma adresi un Web projekta UUID; tokens šajā modelī nav iekļauts.
    [Serializable]
    internal sealed class WebConnectionSettings
    {
        public const string DefaultServerUrl = "https://unityqawebsystem-production-5r5dcg.laravel.cloud";
        public string ServerUrl = DefaultServerUrl;
        public string WebProjectUuid = "";
    }

    // Savienojuma melnraksts un tokens dzīvo atsevišķi no serializējamā modeļa.
    internal sealed class WebConnectionSettingsDraft
    {
        private string _savedUrl;
        private string _savedProjectUuid;
        private string _savedToken = "";
        public string SavedServerUrl => _savedUrl;
        public string ServerUrl { get; set; }
        public string WebProjectUuid { get; set; }
        public string AccessToken { get; set; } = "";
        public bool HasChanges => ServerUrl != _savedUrl || WebProjectUuid != _savedProjectUuid || AccessToken != _savedToken;
        public bool HasDefaultValues => ServerUrl == WebConnectionSettings.DefaultServerUrl
            && string.IsNullOrEmpty(WebProjectUuid) && string.IsNullOrEmpty(AccessToken);

        public WebConnectionSettingsDraft(WebConnectionSettings settings, string savedToken = "")
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ServerUrl = _savedUrl = WebConnectionUrl.Normalize(settings.ServerUrl);
            WebProjectUuid = _savedProjectUuid = WebProjectUuidValidator.Normalize(settings.WebProjectUuid);
            AccessToken = _savedToken = string.IsNullOrEmpty(ServerUrl) ? "" : savedToken ?? "";
        }

        public void ChangeServerUrl(string value)
        {
            // Mainot serveri, iepriekšējo tokenu jaunajai adresei nepārnes.
            bool sameAddress = WebConnectionUrl.TryNormalize(ServerUrl, out string previous, out _)
                && WebConnectionUrl.TryNormalize(value, out string next, out _) && previous == next;
            if (!sameAddress) AccessToken = "";
            ServerUrl = value;
        }

        public WebConnectionSettings CreateSnapshot() => new WebConnectionSettings
        {
            ServerUrl = WebConnectionUrl.Normalize(ServerUrl),
            WebProjectUuid = WebProjectUuidValidator.Normalize(WebProjectUuid)
        };

        public void AcceptSavedSnapshot(WebConnectionSettings snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            ServerUrl = _savedUrl = snapshot.ServerUrl;
            WebProjectUuid = _savedProjectUuid = snapshot.WebProjectUuid;
            // Pieņemtā vērtība ļauj atcelt nesaglabātas izmaiņas šajā formā.
            _savedToken = AccessToken;
        }

        public void CancelChanges() { ServerUrl = _savedUrl; WebProjectUuid = _savedProjectUuid; AccessToken = _savedToken; }
        public void ResetToDefaults() { ServerUrl = WebConnectionSettings.DefaultServerUrl; WebProjectUuid = ""; AccessToken = ""; }
        public void ClearToken() { AccessToken = ""; _savedToken = ""; }
    }
}
