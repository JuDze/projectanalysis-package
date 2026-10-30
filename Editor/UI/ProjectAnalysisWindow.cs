using Judze.ProjectAnalysis.Editor.Settings;
using Judze.ProjectAnalysis.Editor.WebConnection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.IO;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Reporting;
using Judze.ProjectAnalysis.Editor.Fixes;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;

namespace Judze.ProjectAnalysis.Editor.UI
{
    public class ProjectAnalysisWindow : EditorWindow
    {
        private const string LayoutPath =
            "Packages/com.judze.projectanalysis/Editor/UI/ProjectAnalysisWindow.uxml";

        private RuleSettingsView _ruleSettingsView;

        // Pašlaik izmantotie noteikumu iestatījumi.
        private RuleSettings _activeRuleSettings = new RuleSettings();
        // Atsevišķa kopija rediģēšanai atvērtajā logā.
        private RuleSettingsDraft _ruleSettingsDraft;

        // Saglabājam atsauci, lai atjaunotu no projekta nolasīto informāciju.
        private AnalysisContextView _analysisContextView;
        private WebConnectionView _webConnectionView;
        [NonSerialized] internal IWebConnectionClient ConnectionClient;
        [NonSerialized] internal IWebReportUploadClient ReportUploadClient;
        private ReportUploadController _reportUploadController;
        // Tokens nedrīkst nonākt EditorWindow domēna pārlādes serializācijā.
        [NonSerialized] private WebConnectionSettingsDraft _webConnectionDraft;
        private IWebConnectionSettingsStorage _webConnectionStorage;
        private IAccessTokenStore _accessTokenStore;
        private string _webConnectionLoadWarning;
        private IFixSessionStore _fixState;
        private IReportFileWriter _reportFileWriter = new JsonReportFileWriter();
        internal Func<string, string, string, string, string> SelectReportSavePath = EditorUtility.SaveFilePanel;
        // Testos apstiprinājumu var aizstāt, neizsaucot operētājsistēmas dialogu.
        internal Func<string, string, string, bool> ConfirmChanges = (title, message, action) =>
            EditorUtility.DisplayDialog(title, message, action, "Cancel");
        private ReportsTabView _reportsTabView;
        private ReportWorkflowController _reportController;
        private AnalysisWorkflowController _analysisController;
        // JSON kopijas saglabājas loga pārbūvē un domēna pārlādē; tās nemaina vēlākie labojumi.
        [SerializeField, HideInInspector] private List<string> _reportJsons = new List<string>();
        [SerializeField, HideInInspector] private string _reportBaselineJson;
        private AnalysisTabWindow _analysisTabWindow;
        // Iestatījumu glabāšanu izmanto caur kopīgo saskarni.
        private IRuleSettingsStorage _ruleSettingsStorage;
        private string _ruleSettingsLoadWarning;
        private HelpBox _ruleSettingsWarningBox;
        private HelpBox _analysisSettingsWarningBox;

        [MenuItem("Tools/Project Analysis")]
        private static void Open()
        {
            var window = GetWindow<ProjectAnalysisWindow>();
            window.titleContent = new GUIContent("Project Analysis");
        }
        private void InitializeRuleSettingsView()
        {
            var rulesRoot = rootVisualElement.Q<VisualElement>("rules-settings-view");
            string projectDirectory = Path.GetDirectoryName(Application.dataPath);
            string filePath = Path.Combine(
                projectDirectory, "UserSettings", "ProjectAnalysis", "RuleSettings.json");

            _ruleSettingsStorage ??= new JsonRuleSettingsStorage(filePath);

            if (_ruleSettingsDraft == null)
            {
                _ruleSettingsLoadWarning = null;
                try
                {
                    _activeRuleSettings = _ruleSettingsStorage.Load();
                }
                catch (Exception exception)
                {
                    // Kļūdainas ielādes gadījumā turpina ar noklusējuma vērtībām.
                    _activeRuleSettings = new RuleSettings();
                    _ruleSettingsLoadWarning =
                        "Could not load saved settings. Default values are being used. " +
                        "The original file has not been changed. Open Rules and press Save to store these settings.";
                    Debug.LogWarning(
                        $"Project Analysis could not load settings from {filePath}: {exception.Message}");
                }

                // Melnrakstu izveido arī tad, ja ielādēšana neizdevās.
                _ruleSettingsDraft = new RuleSettingsDraft(_activeRuleSettings);
            }

            _ruleSettingsView = new RuleSettingsView(
                rulesRoot, _ruleSettingsDraft, SaveRuleSettings,
                requiresSave: _ruleSettingsLoadWarning != null);

            // Brīdinājums saglabājas līdz veiksmīgai saglabāšanai.
            _ruleSettingsWarningBox = null;
            if (_ruleSettingsLoadWarning != null)
            {
                _ruleSettingsWarningBox = new HelpBox(
                    _ruleSettingsLoadWarning, HelpBoxMessageType.Warning);
                rulesRoot.Insert(0, _ruleSettingsWarningBox);
            }
        }

        private void SaveRuleSettings(RuleSettings snapshot)
        {
            _ruleSettingsStorage.Save(snapshot);
            _activeRuleSettings = snapshot;
            SettingsFileSaved();
        }

        private void SettingsFileSaved()
        {
            _analysisTabWindow?.MarkResultsOutdated();

            // Brīdinājumu noņem tikai pēc veiksmīgas ierakstīšanas.
            _ruleSettingsLoadWarning = null;
            _ruleSettingsWarningBox?.RemoveFromHierarchy();
            _ruleSettingsWarningBox = null;
            _analysisSettingsWarningBox?.RemoveFromHierarchy();
            _analysisSettingsWarningBox = null;
            _ruleSettingsView?.MarkSettingsFileSaved();
        }

        private void InitializeWebConnectionView()
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "UserSettings", "ProjectAnalysis", "WebConnection.json");
            _webConnectionStorage ??= new JsonWebConnectionSettingsStorage(path);
            _accessTokenStore ??= new SessionStateAccessTokenStore(Path.GetDirectoryName(Application.dataPath));
            if (_webConnectionDraft == null)
            {
                WebConnectionSettings settings;
                _webConnectionLoadWarning = null;
                try { settings = _webConnectionStorage.Load(); }
                catch
                {
                    settings = new WebConnectionSettings();
                    _webConnectionLoadWarning = "Could not load saved connection settings. Default values are in use. " +
                        "The original file has not been changed. Press Save to store these settings.";
                }
                _webConnectionDraft = new WebConnectionSettingsDraft(settings, _accessTokenStore.Load(settings.ServerUrl));
            }
            _webConnectionView = new WebConnectionView(rootVisualElement.Q("server-settings-view"),
                _webConnectionDraft, settings =>
                {
                    _webConnectionStorage.Save(settings);
                    // Atiestatot arī servera adresi, izdzēš iepriekšējā savienojuma sesijas tokenu.
                    if (_webConnectionDraft.HasDefaultValues && _webConnectionDraft.SavedServerUrl != settings.ServerUrl)
                        _accessTokenStore.Save(_webConnectionDraft.SavedServerUrl, "");
                    // Atiestatīšanu apstiprina ar Save; tukšs tokens dzēš iepriekšējo sesijas vērtību.
                    _accessTokenStore.Save(string.IsNullOrEmpty(settings.ServerUrl)
                        ? _webConnectionDraft.SavedServerUrl : settings.ServerUrl, _webConnectionDraft.AccessToken);
                    _webConnectionLoadWarning = null;
                }, ConnectionClient ??= new UnityWebConnectionClient(), _webConnectionLoadWarning);
        }

        private void OnDisable()
        {
            _analysisController?.Dispose();
            _analysisController = null;
            _reportController?.Dispose();
            _reportController = null;
            _reportUploadController?.Dispose();
            _reportUploadController = null;
            // Notīra formas kopiju; SessionState saglabātā vērtība paliek līdz Unity aizvēršanai.
            _webConnectionDraft?.ClearToken();
            _webConnectionDraft = null;
            _webConnectionView?.Dispose();
            _webConnectionView = null;
        }

        public void CreateGUI()
        {
            // Editor loga minimālo izmēru nosaka ar EditorWindow API, nevis USS.
            minSize = new Vector2(700, 500);
            // Pārbūvējot logu, pārtrauc iepriekšējās augšupielādes un savienojuma pārbaudes.
            _analysisController?.Dispose();
            _analysisController = null;
            _reportController?.Dispose();
            _reportController = null;
            _reportUploadController?.Dispose();
            _reportUploadController = null;
            _webConnectionView?.Dispose();
            _webConnectionView = null;
            rootVisualElement.Clear();

            // UXML izveido struktūru un pieslēdz USS; C# piesaista tikai darbības un datus.
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null)
            {
                rootVisualElement.Add(new HelpBox(
                    "Could not load the Project Analysis layout. Check that ProjectAnalysisWindow.uxml exists in the package.",
                    HelpBoxMessageType.Error));
                return;
            }
            layout.CloneTree(rootVisualElement);
            InitializeRuleSettingsView();
            InitializeAnalysisContext();
            InitializeWebConnectionView();
            _fixState ??= new JsonFixSessionStore(Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "UserSettings", "ProjectAnalysis", "FixState"));
            InitializeReportsView();
            InitializeAnalysisView();
        }

        private void InitializeAnalysisContext()
        {
            var analysisRoot = rootVisualElement.Q("analysis-settings-view");
            var analysisSettings = new AnalysisContextView(
                analysisRoot, UnityBuildTargetReader.Read());
            _analysisContextView = analysisSettings;

            _analysisSettingsWarningBox = null;
            if (_ruleSettingsLoadWarning != null)
            {
                _analysisSettingsWarningBox = new HelpBox(
                    _ruleSettingsLoadWarning, HelpBoxMessageType.Warning);
                analysisRoot.Insert(0, _analysisSettingsWarningBox);
            }
        }

        private void InitializeReportsView()
        {
            // Saraksts satur tikai lietotāja izveidotos momentuzņēmumus.
            _reportsTabView = new ReportsTabView(
                rootVisualElement.Q("reports-view"), Array.Empty<ReportPreviewRow>());

            _reportController = new ReportWorkflowController(rootVisualElement, _reportsTabView, _reportJsons,
                () => _reportBaselineJson, json => _reportBaselineJson = json, _fixState, _reportFileWriter,
                (title, directory, name, extension) => SelectReportSavePath(title, directory, name, extension),
                (title, message, action) => ConfirmChanges(title, message, action));
            _reportUploadController = new ReportUploadController(_reportsTabView, _webConnectionDraft,
                ReportUploadClient ??= new UnityWebReportUploadClient());
        }

        private void InitializeAnalysisView()
        {
            var mainTabs = rootVisualElement.Q<TabView>("main-tabs");
            var rulesSettingsTab = rootVisualElement.Q<Tab>("rules-settings-tab");

            var fixChangeTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.judze.projectanalysis/Editor/UI/Analysis/FixPreviewChangeItem.uxml");
            if (fixChangeTemplate == null)
            {
                rootVisualElement.Q("analysis-view").Add(new HelpBox(
                    "Could not load FixPreviewChangeItem.uxml. Check that the preview template exists in the package.",
                    HelpBoxMessageType.Error));
                return;
            }

            var propertyChangeTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.judze.projectanalysis/Editor/UI/Analysis/FixPreviewPropertyChangeItem.uxml");
            if (propertyChangeTemplate == null)
            {
                rootVisualElement.Q("analysis-view").Add(new HelpBox(
                    "Could not load FixPreviewPropertyChangeItem.uxml. Check that the preview template exists in the package.",
                    HelpBoxMessageType.Error));
                return;
            }

            // Saīsne atver noteikumu cilni galvenajā rindā.
            var analysisTabWindow = new AnalysisTabWindow(
                rootVisualElement.Q("analysis-view"), () =>
                {
                    mainTabs.activeTab = rulesSettingsTab;
                }, fixChangeTemplate, propertyChangeTemplate);
            _analysisTabWindow = analysisTabWindow;
            analysisTabWindow.SetTargetPlatform(_analysisContextView.TargetLabel);
            _analysisController = new AnalysisWorkflowController(rootVisualElement, analysisTabWindow,
                ProjectAnalysisServices.CreateDefault(_fixState), _fixState,
                () => _ruleSettingsDraft.CreateSavedSnapshot(),
                () => { RefreshBuildTarget(); return _analysisContextView.CurrentTarget; },
                (title, message, action) => ConfirmChanges(title, message, action), _reportController);
        }

        private void OnFocus()
        {
            // Atgriežoties logā, atkārtoti nolasām aktīvo kvalitātes līmeni.
            _analysisContextView?.RefreshQualityLevel();
            RefreshBuildTarget();
        }

        private void OnInspectorUpdate() => RefreshBuildTarget();

        private void RefreshBuildTarget()
        {
            if (_analysisContextView == null) return;
            // Mērķa maiņa padara iepriekšējos rezultātus un labojuma plānu novecojušus.
            if (_analysisContextView.RefreshTarget(UnityBuildTargetReader.Read()))
                _analysisTabWindow?.MarkResultsOutdated();
            _analysisTabWindow?.SetTargetPlatform(_analysisContextView.TargetLabel);
        }
    }
}
