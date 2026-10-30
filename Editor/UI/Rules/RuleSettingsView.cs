using System;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Noteikumu formas attēlo kopīgā sarakstā ar izvēršamām resursu grupām
    internal sealed class RuleSettingsView
    {
        private const string FormsPath = "Packages/com.judze.projectanalysis/Editor/UI/Rules/Forms/";
        private readonly ScrollView _scroll;
        private readonly Label _status;
        private readonly RuleSettingsDraft _draft;
        private readonly RuleGroup[] _groups;
        private readonly RuleSettingsValidator _validator = new RuleSettingsValidator();
        private readonly RuleValidationView _validationView = new RuleValidationView();
        private readonly Button _saveButton;
        private readonly Button _cancelButton;
        private readonly Button _resetButton;
        private readonly Action<RuleSettings> _saveSettings;
        private RuleFormBindings _bindings;

        // Pēc kļūdainas ielādes arī nemainītās noklusējuma vērtības jāsaglabā
        private bool _requiresSave;
        private bool NeedsSave => _draft.HasChanges || _requiresSave;

        public RuleSettingsView(
            VisualElement root, RuleSettingsDraft draft,
            Action<RuleSettings> onSave, bool requiresSave = false)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _scroll = RuleFieldBinding.Require<ScrollView>(root, "rules-scroll");
            _status = RuleFieldBinding.Require<Label>(root, "rules-status");
            _saveButton = RuleFieldBinding.Require<Button>(root, "save-rules-button");
            _cancelButton = RuleFieldBinding.Require<Button>(root, "cancel-rules-button");
            _resetButton = RuleFieldBinding.Require<Button>(root, "reset-rules-button");
            _draft = draft ?? throw new ArgumentNullException(nameof(draft));
            _saveSettings = onSave ?? throw new ArgumentNullException(nameof(onSave));
            _requiresSave = requiresSave;
            _bindings = new RuleFormBindings(_draft.Values, RefreshValidationAndStatus, _validationView);

            _groups = new[]
            {
                new RuleGroup(RuleFieldBinding.Require<Foldout>(root, "rules-textures-group"),
                    "textures-rules-form", "TextureRulesView",
                    new RuleForm("TEX001", "tex001-form",
                        view => _bindings.BindTex001(view)),
                    new RuleForm("TEX002", "tex002-form",
                        view => _bindings.BindTex002(view)),
                    new RuleForm("TEX003", "tex003-form",
                        view => _bindings.BindTex003(view)),
                    new RuleForm("TEX004", "tex004-form",
                        view => _bindings.BindTex004(view))),
                new RuleGroup(RuleFieldBinding.Require<Foldout>(root, "rules-models-group"),
                    "models-rules-form", "ModelRulesView",
                    new RuleForm("MOD001", "mod001-form",
                        view => _bindings.BindMod001(view)),
                    new RuleForm("MOD002", "mod002-form",
                        view => _bindings.BindMod002(view)),
                    new RuleForm("MOD003", "mod003-form",
                        view => _bindings.BindMod003(view)),
                    new RuleForm("MOD004", "mod004-form",
                        view => _bindings.BindMod004(view))),
                new RuleGroup(RuleFieldBinding.Require<Foldout>(root, "rules-audio-group"),
                    "audio-rules-form", "AudioRulesView",
                    new RuleForm("AUD001", "aud001-form",
                        view => _bindings.BindAud001(view)),
                    new RuleForm("AUD002", "aud002-form",
                        view => _bindings.BindAud002(view)),
                    new RuleForm("AUD003", "aud003-form",
                        view => _bindings.BindAud003(view)),
                    new RuleForm("AUD004", "aud004-form",
                        view => _bindings.BindAud004(view)),
                    new RuleForm("AUD005", "aud005-form",
                        view => _bindings.BindAud005(view)))
            };

            // Katrai grupai ir viens UXML šablons; laukus piesaista attiecīgā noteikuma blokā.
            foreach (var group in _groups)
            {
                group.View = RuleFieldBinding.Require<TemplateContainer>(group.Foldout, group.ViewName);
                group.Template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FormsPath + group.TemplateName + ".uxml")
                    ?? throw new InvalidOperationException($"Missing rule group template '{group.TemplateName}'.");
                BindRuleForms(group);
            }

            _saveButton.clicked += SaveRules;
            _cancelButton.clicked += CancelChanges;
            _resetButton.clicked += ResetToDefaults;
            _saveButton.tooltip = "Save rule settings for this project on this computer.";
            _cancelButton.tooltip = "Discard all unsaved rule settings.";
            _resetButton.tooltip = "Restore default rule values.";
            RefreshValidationAndStatus();
        }

        public void MarkSettingsFileSaved()
        {
            // Veiksmīga saglabāšana pabeidz noteikumu datnes atjaunošanu
            _requiresSave = false;
            RefreshValidationAndStatus();
        }

        private void SaveRules()
        {
            var issues = _validator.Validate(_draft.Values);

            if (issues.Count > 0 || !NeedsSave)
            {
                RefreshValidationAndStatus();
                return;
            }

            var snapshot = _draft.CreateSnapshot();

            try
            {
                // Darbība atgriežas tikai pēc veiksmīgas saglabāšanas.
                _saveSettings(snapshot);
            }
            catch (Exception exception)
            {
                // Neveiksmīga saglabāšana nemaina melnraksta sākuma stāvokli.
                RefreshValidationAndStatus();

                _status.text = "Save failed. Unsaved changes.";

                Debug.LogException(exception);

                EditorUtility.DisplayDialog(
                    "Could not save rule settings",
                    "Your changes remain in the form.\n" + exception.Message,
                    "OK");

                return;
            }

            _draft.AcceptSavedSnapshot(snapshot);
            _requiresSave = false;

            RefreshValidationAndStatus();
        }

        private void CancelChanges()
        {
            if(!_draft.HasChanges)
            {
                return;
            }
            _draft.CancelChanges();
            RebuildRuleForms();
        }

        private void ResetToDefaults()
        {
            if(_draft.HasDefaultsValues)
            {
                return;
            }
            _draft.ResetToDefaults();
            RebuildRuleForms();
        }

        private void RebuildRuleForms()
        {
            var scrollOffset = _scroll.scrollOffset;
            _validationView.Clear();
            _bindings = new RuleFormBindings(_draft.Values, RefreshValidationAndStatus, _validationView);

            // Aizstāj grupas saturu un piesaista jauno melnrakstu; izvēršana un ritināšana saglabājas.
            foreach (var group in _groups)
            {
                group.View.Clear();
                group.Template.CloneTree(group.View);
                BindRuleForms(group);
            }

            RefreshValidationAndStatus();
            _scroll.scrollOffset = scrollOffset;
        }

        private static void BindRuleForms(RuleGroup group)
        {
            // Vienādi lauku nosaukumi dažādos noteikumos neietekmē citu formu piesaisti.
            foreach (var form in group.Forms)
            {
                form.View = RuleFieldBinding.Require<VisualElement>(group.View, form.ViewName);
                form.BindFields(form.View);
            }
        }

        private void RefreshValidationAndStatus()
        {
            var issues = _validator.Validate(_draft.Values);
            _validationView.Show(issues);

            foreach (var group in _groups)
            {
                int groupErrors = 0;
                foreach (var form in group.Forms)
                {
                    int count = 0;
                    foreach (var issue in issues)
                        if (issue.RuleId == form.Id) count++;
                    groupErrors += count;
                    form.View.EnableInClassList("rule-form--invalid", count > 0);
                }

                // Kļūdu skaits paliek redzams arī aizvērtā grupā.
                group.Foldout.text = groupErrors == 0 ? group.Title
                    : $"{group.Title} · {groupErrors} {(groupErrors == 1 ? "error" : "errors")}";
                group.Foldout.EnableInClassList("rule-category--invalid", groupErrors > 0);
            }

            string changes = _draft.HasChanges
                ? "Unsaved changes"
                : _requiresSave ? "Defaults loaded — not saved" : "No changes";
            _status.text = issues.Count == 0
                ? changes
                : $"{changes} · {issues.Count} {(issues.Count == 1 ? "error" : "errors")}";
            _saveButton.SetEnabled(NeedsSave && issues.Count == 0);
            _cancelButton.SetEnabled(_draft.HasChanges);
            _resetButton.SetEnabled(!_draft.HasDefaultsValues);
        }

        // Grupēšana attiecas tikai uz saskarni un nemaina noteikumu izpildi.
        private sealed class RuleGroup
        {
            public readonly Foldout Foldout;
            public readonly string Title;
            public readonly string ViewName;
            public readonly string TemplateName;
            public readonly RuleForm[] Forms;
            public TemplateContainer View;
            public VisualTreeAsset Template;

            public RuleGroup(Foldout foldout, string viewName, string templateName, params RuleForm[] forms)
            {
                Foldout = foldout;
                Title = foldout.text;
                ViewName = viewName;
                TemplateName = templateName;
                Forms = forms;
            }
        }

        private sealed class RuleForm
        {
            public readonly string Id;
            public readonly string ViewName;
            public readonly Action<VisualElement> BindFields;
            public VisualElement View;

            public RuleForm(string id, string viewName, Action<VisualElement> bindFields)
            {
                Id = id;
                ViewName = viewName;
                BindFields = bindFields;
            }
        }
    }
}
