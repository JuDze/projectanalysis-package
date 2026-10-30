using System;
using Judze.ProjectAnalysis.Editor.Settings;
using Judze.ProjectAnalysis.Editor.WebConnection;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Forma rediģē melnrakstu; datņu glabāšana tiek pieslēgta ar atsevišķu darbību.
    internal sealed class WebConnectionView : IDisposable
    {
        private readonly TextField _urlField, _projectUuidField, _tokenField;
        private readonly Toggle _showToken;
        private readonly Button _save, _cancel, _reset, _test;
        private readonly Label _status, _connectionStatus, _connectionHint;
        private readonly HelpBox _validation, _projectValidation, _loadWarning;
        private readonly WebConnectionSettingsDraft _draft;
        private readonly Action<WebConnectionSettings> _onSave;
        private readonly IWebConnectionClient _client;
        private IDisposable _operation;
        private WebConnectionResult _result;
        private int _requestGeneration;
        private bool _requiresSave, _testing, _disposed, _cancelled;

        public WebConnectionView(VisualElement root, WebConnectionSettingsDraft draft,
            Action<WebConnectionSettings> onSave, IWebConnectionClient client, string loadWarning = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _draft = draft ?? throw new ArgumentNullException(nameof(draft));
            _onSave = onSave ?? throw new ArgumentNullException(nameof(onSave));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _requiresSave = !string.IsNullOrEmpty(loadWarning);
            _urlField = Require<TextField>(root, "server-url-field");
            _projectUuidField = Require<TextField>(root, "server-project-uuid-field");
            _tokenField = Require<TextField>(root, "server-token-field");
            _showToken = Require<Toggle>(root, "show-server-token-toggle");
            _save = Require<Button>(root, "save-server-settings-button");
            _cancel = Require<Button>(root, "cancel-server-settings-button");
            _reset = Require<Button>(root, "reset-server-settings-button");
            _test = Require<Button>(root, "test-server-connection-button");
            _connectionHint = Require<Label>(root, "server-connection-hint");
            _status = Require<Label>(root, "server-settings-status");
            _connectionStatus = Require<Label>(root, "server-connection-status");
            _validation = new HelpBox("", HelpBoxMessageType.Error);
            Require<VisualElement>(root, "server-address-group").Add(_validation);
            _projectValidation = new HelpBox("", HelpBoxMessageType.Error);
            Require<VisualElement>(root, "server-project-group").Add(_projectValidation);
            _loadWarning = new HelpBox(loadWarning ?? "", HelpBoxMessageType.Warning);
            _loadWarning.EnableInClassList("pa-hidden", !_requiresSave);
            Require<VisualElement>(root, "server-settings-content").Insert(0, _loadWarning);

            _showToken.RegisterValueChangedCallback(e => _tokenField.isPasswordField = !e.newValue);
            _urlField.RegisterValueChangedCallback(e => { InvalidateCheck(); _draft.ChangeServerUrl(e.newValue); ShowDraft(); });
            _projectUuidField.RegisterValueChangedCallback(e => { InvalidateCheck(); _draft.WebProjectUuid = e.newValue; Refresh(); });
            _tokenField.RegisterValueChangedCallback(e => { InvalidateCheck(); _draft.AccessToken = e.newValue; Refresh(); });
            _save.clicked += Save;
            _cancel.clicked += () => { InvalidateCheck(); _draft.CancelChanges(); ShowDraft(); };
            _reset.clicked += () => { InvalidateCheck(); _draft.ResetToDefaults(); ShowDraft(); };
            _test.clicked += TestConnection;
            ShowDraft();
        }

        private void ShowDraft()
        {
            _urlField.SetValueWithoutNotify(_draft.ServerUrl);
            _projectUuidField.SetValueWithoutNotify(_draft.WebProjectUuid);
            _tokenField.SetValueWithoutNotify(_draft.AccessToken);
            _tokenField.isPasswordField = true;
            _showToken.SetValueWithoutNotify(false);
            Refresh();
        }

        private void Refresh()
        {
            bool valid = Validate(out _, out string urlError, out string projectError);
            _validation.text = urlError ?? "";
            _validation.EnableInClassList("pa-hidden", urlError == null);
            _projectValidation.text = projectError ?? "";
            _projectValidation.EnableInClassList("pa-hidden", projectError == null);
            _save.SetEnabled(!_testing && valid && (_draft.HasChanges || _requiresSave));
            _cancel.SetEnabled(!_testing && _draft.HasChanges);
            _reset.SetEnabled(!_testing && !_draft.HasDefaultValues);
            _status.text = !valid ? "Check the connection settings." : _draft.HasChanges ? "Unsaved changes"
                : _requiresSave ? "Defaults loaded — not saved" : "No changes";
            bool configured = WebConnectionRequest.TryCreate(_draft.ServerUrl, _draft.WebProjectUuid,
                _draft.AccessToken, out _, out string checkError);
            _urlField.SetEnabled(!_testing);
            _projectUuidField.SetEnabled(!_testing);
            _tokenField.SetEnabled(!_testing);
            _showToken.SetEnabled(!_testing);
            _test.SetEnabled(_testing || configured);
            _test.text = _testing ? "Cancel test" : "Test connection";
            _connectionStatus.text = _testing ? "Checking…" : _result != null
                ? (_result.Connected ? "Connected" : "Failed") : configured ? "Not checked" : "Not configured";
            _connectionHint.text = _testing ? "Checking the token and project access…"
                : _result != null ? _result.Message
                : _cancelled ? "Connection check cancelled."
                : !configured ? checkError
                : "Test the current form values without saving settings or sending a report.";
        }

        private void Save()
        {
            if (_testing || !Validate(out _, out _, out _) || !(_draft.HasChanges || _requiresSave)) return;
            var snapshot = _draft.CreateSnapshot();
            try
            {
                // Nesaglabāto stāvokli pieņem tikai pēc veiksmīga ieraksta; tokens paliek atmiņā.
                _onSave(snapshot);
                _draft.AcceptSavedSnapshot(snapshot);
                _requiresSave = false;
                _loadWarning.AddToClassList("pa-hidden");
                ShowDraft();
                _status.text = string.IsNullOrEmpty(_draft.AccessToken)
                    ? "Connection settings saved. No token stored for this connection."
                    : "Connection settings saved. Token kept until Unity exits.";
            }
            catch
            {
                // Kļūdas paziņojumā neiekļauj ievadīto adresi vai tokenu.
                Refresh();
                _status.text = "Could not save connection settings. Your changes remain in the form.";
            }
        }

        private bool Validate(out string url, out string urlError, out string projectError)
        {
            bool validUrl = WebConnectionUrl.TryNormalize(_draft.ServerUrl, out url, out urlError);
            bool validProject = WebProjectUuidValidator.TryNormalize(_draft.WebProjectUuid, out _, out projectError);
            if (validUrl && string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(_draft.AccessToken))
            {
                urlError = "Enter a server URL before saving a token.";
                validUrl = false;
            }
            return validUrl && validProject;
        }

        private void TestConnection()
        {
            if (_disposed) return;
            if (_testing)
            {
                InvalidateCheck();
                _cancelled = true;
                Refresh();
                return;
            }
            if (!WebConnectionRequest.TryCreate(_draft.ServerUrl, _draft.WebProjectUuid,
                _draft.AccessToken, out var request, out _)) { Refresh(); return; }
            InvalidateCheck();
            int generation = _requestGeneration;
            _testing = true;
            Refresh();
            try
            {
                // Pārbauda melnraksta vērtības; saglabāšana joprojām ir atsevišķa darbība.
                var operation = _client.Check(request, result =>
                {
                    if (_disposed || generation != _requestGeneration || !_testing) return;
                    _testing = false;
                    _operation?.Dispose();
                    _operation = null;
                    _result = result ?? new WebConnectionResult(WebConnectionStatus.InvalidResponse);
                    Refresh();
                });
                // Saskarne pieļauj arī tūlītēju atbildi, piemēram, testu klientā.
                if (_testing && generation == _requestGeneration && !_disposed) _operation = operation;
                else operation?.Dispose();
            }
            catch
            {
                InvalidateCheck();
                _result = new WebConnectionResult(WebConnectionStatus.NetworkError);
                Refresh();
            }
        }

        private void InvalidateCheck()
        {
            // Novēlotas atbildes pēc atcelšanas vai ievades maiņas ignorē.
            _requestGeneration++;
            _testing = false;
            _operation?.Dispose();
            _operation = null;
            _result = null;
            _cancelled = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            InvalidateCheck();
            ClearSensitiveFields();
        }

        public void ClearSensitiveFields()
        {
            _tokenField.SetValueWithoutNotify("");
            _tokenField.isPasswordField = true;
            _showToken.SetValueWithoutNotify(false);
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UI element '{name}'.");
    }
}
