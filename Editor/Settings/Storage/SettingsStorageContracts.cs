namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Nosaka iestatījumu ielādes un saglabāšanas darbības.
    internal interface IRuleSettingsStorage
    {
        RuleSettings Load();
        void Save(RuleSettings settings);
    }

    internal interface IWebConnectionSettingsStorage
    {
        WebConnectionSettings Load();
        void Save(WebConnectionSettings settings);
    }

    // Tokenu glabāšana ir nodalīta no serializējamiem savienojuma iestatījumiem.
    internal interface IAccessTokenStore
    {
        string Load(string serverUrl);
        void Save(string serverUrl, string token);
    }
}
