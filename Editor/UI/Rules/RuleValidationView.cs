using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Sasaista iestatījumu kļūdas ar jau izveidotajiem formas laukiem un ziņojumiem.
    internal sealed class RuleValidationView
    {
        private readonly Dictionary<(string RuleId, string PropertyName), FieldView> _fields = new();

        public void Register(string ruleId, string propertyName, VisualElement field, Label message)
        {
            _fields.Add((ruleId, propertyName), new FieldView(field, message));
        }

        public void Clear()
        {
            _fields.Clear();
        }
        public void Show(IReadOnlyList<SettingsValidationIssue> issues)
        {
            foreach (var view in _fields.Values)
            {
                view.Field.RemoveFromClassList("rule-field--invalid");
                view.Message.text = string.Empty;
                view.Message.AddToClassList("pa-hidden");
            }

            foreach (var issue in issues)
            {
                // Neatvērtas formas kļūdu joprojām uzskaita kopējais statuss
                if (!_fields.TryGetValue((issue.RuleId, issue.PropertyName), out var view)) continue;

                view.Field.AddToClassList("rule-field--invalid");
                view.Message.text = string.IsNullOrEmpty(view.Message.text)
                    ? issue.Message
                    : view.Message.text + "\n" + issue.Message;
                view.Message.RemoveFromClassList("pa-hidden");
            }
        }

        private sealed class FieldView
        {
            public readonly VisualElement Field;
            public readonly Label Message;

            public FieldView(VisualElement field, Label message)
            {
                Field = field;
                Message = message;
            }
        }
    }
}
