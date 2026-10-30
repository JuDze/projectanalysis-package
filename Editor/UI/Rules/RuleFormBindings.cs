using System;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Settings;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Katras noteikuma formas lauki tiek sasaistīti ar tās tipizētajiem iestatījumiem.
    internal sealed class RuleFormBindings
    {
        private readonly RuleSettings _settings;
        private readonly Action _onChanged;
        private readonly RuleValidationView _validationView;
        private Label _mod004ThresholdHint;
        private VisualElement _mod002Parameters;
        private VisualElement _aud005Parameters;
        private Label _aud001ThresholdHint;

        public RuleFormBindings(
            RuleSettings settings, Action onChanged, RuleValidationView validationView)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _validationView = validationView ?? throw new ArgumentNullException(nameof(validationView));
        }

        public void BindTex001(VisualElement root)
        {
            var settings = _settings.Tex001;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
            Bind(root, "TEX001", nameof(Tex001Settings.ResolutionThreshold), "tex001-threshold",
                settings.ResolutionThreshold, value => settings.ResolutionThreshold = value);
            Bind(root, "TEX001", nameof(Tex001Settings.ResolutionHighThreshold), "tex001-high-threshold",
                settings.ResolutionHighThreshold, value => settings.ResolutionHighThreshold = value);
        }

        public void BindTex002(VisualElement root)
        {
            var settings = _settings.Tex002;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
            Bind(root, "TEX002", nameof(Tex002Settings.CpuDataThresholdMiB), "tex002-threshold",
                settings.CpuDataThresholdMiB, value => settings.CpuDataThresholdMiB = value);
            Bind(root, "TEX002", nameof(Tex002Settings.CpuDataHighThresholdMiB), "tex002-high-threshold",
                settings.CpuDataHighThresholdMiB, value => settings.CpuDataHighThresholdMiB = value);
        }

        public void BindTex003(VisualElement root)
        {
            var settings = _settings.Tex003;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
        }

        public void BindTex004(VisualElement root)
        {
            var settings = _settings.Tex004;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
            Bind(root, "TEX004", nameof(Tex004Settings.MipDataThresholdMiB), "tex004-threshold",
                settings.MipDataThresholdMiB, value => settings.MipDataThresholdMiB = value);
            Bind(root, "TEX004", nameof(Tex004Settings.MipDataHighThresholdMiB), "tex004-high-threshold",
                settings.MipDataHighThresholdMiB, value => settings.MipDataHighThresholdMiB = value);
        }

        public void BindMod001(VisualElement root)
        {
            var settings = _settings.Mod001;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
            Bind(root, "MOD001", nameof(Mod001Settings.VertexThreshold), "mod001-threshold",
                settings.VertexThreshold, value => settings.VertexThreshold = value);
            Bind(root, "MOD001", nameof(Mod001Settings.VertexHighThreshold), "mod001-high-threshold",
                settings.VertexHighThreshold, value => settings.VertexHighThreshold = value);
        }

        public void BindMod002(VisualElement root)
        {
            var settings = _settings.Mod002;
            _mod002Parameters = RuleFieldBinding.Require<VisualElement>(root, "mod002-parameters");
            RuleFieldBinding.BindEnabled(root, settings.Enabled, value => settings.Enabled = value, () =>
            {
                RefreshMod004ThresholdHint();
                _onChanged();
            });
            Bind(root, "MOD002", nameof(Mod002Settings.TriangleThreshold), "mod002-threshold",
                settings.TriangleThreshold, value =>
                {
                    settings.TriangleThreshold = value;
                    RefreshMod004ThresholdHint();
                });
            Bind(root, "MOD002", nameof(Mod002Settings.TriangleHighThreshold), "mod002-high-threshold",
                settings.TriangleHighThreshold, value =>
                {
                    settings.TriangleHighThreshold = value;
                    RefreshMod004ThresholdHint();
                });
            RefreshMod004ThresholdHint();
        }

        public void BindMod003(VisualElement root)
        {
            var settings = _settings.Mod003;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
        }

        public void BindMod004(VisualElement root)
        {
            var settings = _settings.Mod004;
            RuleFieldBinding.BindEnabled(root, settings.Enabled, value => settings.Enabled = value, () =>
            {
                RefreshMod004ThresholdHint();
                _onChanged();
            });
            _mod004ThresholdHint = RuleFieldBinding.Require<Label>(root, "mod004-selection-threshold");
            RefreshMod004ThresholdHint();
        }

        private void RefreshMod004ThresholdHint()
        {
            // MOD002 lauka izmaiņas uzreiz atjauno arī MOD004 skaidrojumu.
            // Kopīgo lauku drīkst mainīt, kamēr to izmanto vismaz viens noteikums.
            _mod002Parameters?.SetEnabled(_settings.Mod002.Enabled || _settings.Mod004.Enabled);
            if (_mod004ThresholdHint == null) return;
            _mod004ThresholdHint.text = "Thresholds from MOD002.";
        }

        public void BindAud001(VisualElement root)
        {
            var settings = _settings.Aud001;
            _aud001ThresholdHint = RuleFieldBinding.Require<Label>(root, "aud001-threshold-hint");
            RuleFieldBinding.BindEnabled(root, settings.Enabled, value => settings.Enabled = value, () =>
            {
                RefreshAudioThresholdHint();
                _onChanged();
            });
            BindChoice(root, "aud001-optimization-goal", settings.Goal, value =>
            {
                settings.Goal = value;
                RefreshAudioThresholdHint();
            },
                ("Reduce RAM usage", AudioOptimizationGoal.ReduceRamUsage),
                ("Reduce playback CPU load", AudioOptimizationGoal.ReducePlaybackCpuLoad));
            // Paskaidrojumus atjauno pēc sākotnējās vērtības ievietošanas laukā.
            AudioRuleSettingsView.BindLoadType(root);
            RefreshAudioThresholdHint();
        }

        public void BindAud002(VisualElement root)
        {
            var settings = _settings.Aud002;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
            BindChoice(root, "aud002-target-format", settings.TargetFormat, value => settings.TargetFormat = value,
                ("Select a format", AudioCompressionChoice.NotSelected),
                ("ADPCM", AudioCompressionChoice.ADPCM),
                ("Vorbis", AudioCompressionChoice.Vorbis));
            Bind(root, "AUD002", nameof(Aud002Settings.VorbisQuality), "aud002-vorbis-quality",
                settings.VorbisQuality, value => settings.VorbisQuality = value);
            AudioRuleSettingsView.BindCompression(root);
        }

        public void BindAud003(VisualElement root)
        {
            var settings = _settings.Aud003;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
            Bind(root, "AUD003", nameof(Aud003Settings.SampleRateThresholdHz), "aud003-maximum-sample-rate",
                settings.SampleRateThresholdHz, value => settings.SampleRateThresholdHz = value);
            Bind(root, "AUD003", nameof(Aud003Settings.SampleRateHighThresholdHz), "aud003-high-threshold",
                settings.SampleRateHighThresholdHz, value => settings.SampleRateHighThresholdHz = value);
        }

        public void BindAud004(VisualElement root)
        {
            var settings = _settings.Aud004;
            BindEnabled(root, settings.Enabled, value => settings.Enabled = value);
        }

        public void BindAud005(VisualElement root)
        {
            var settings = _settings.Aud005;
            _aud005Parameters = RuleFieldBinding.Require<VisualElement>(root, "aud005-parameters");
            RuleFieldBinding.BindEnabled(root, settings.Enabled, value => settings.Enabled = value, () =>
            {
                RefreshAudioThresholdHint();
                _onChanged();
            });
            Bind(root, "AUD005", nameof(Aud005Settings.RuntimeSizeThresholdBytes), "aud005-threshold",
                settings.RuntimeSizeThresholdBytes, value =>
                {
                    settings.RuntimeSizeThresholdBytes = value;
                    RefreshAudioThresholdHint();
                });
            Bind(root, "AUD005", nameof(Aud005Settings.RuntimeSizeHighThresholdBytes), "aud005-high-threshold",
                settings.RuntimeSizeHighThresholdBytes, value =>
                {
                    settings.RuntimeSizeHighThresholdBytes = value;
                    RefreshAudioThresholdHint();
                });
            RefreshAudioThresholdHint();
        }

        private void RefreshAudioThresholdHint()
        {
            // Kopīgie lauki paliek pieejami, ja tos izmanto AUD001 RAM mērķis.
            bool ramGoal = _settings.Aud001.Goal == AudioOptimizationGoal.ReduceRamUsage;
            _aud005Parameters?.SetEnabled(_settings.Aud005.Enabled || _settings.Aud001.Enabled && ramGoal);
            if (_aud001ThresholdHint == null) return;
            _aud001ThresholdHint.text = ramGoal
                ? "RAM thresholds from AUD005."
                : "Playback CPU priority: ADPCM = Low; Vorbis = Medium. Size thresholds do not apply.";
        }

        private void Bind<T>(
            VisualElement root, string ruleId, string propertyName, string name,
            T initialValue, Action<T> writeValue)
        {
            var field = RuleFieldBinding.Require<BaseField<T>>(root, name);
            var message = RuleFieldBinding.Require<Label>(root, name + "-error");
            _validationView.Register(ruleId, propertyName, field, message);
            RuleFieldBinding.BindValue(field, initialValue, writeValue, _onChanged);
        }

        private void BindEnabled(VisualElement root, bool initialValue, Action<bool> writeValue)
        {
            RuleFieldBinding.BindEnabled(root, initialValue, writeValue, _onChanged);
        }

        private void BindChoice<T>(
            VisualElement root, string name, T initialValue, Action<T> writeValue,
            params (string Label, T Value)[] options) where T : struct, Enum
        {
            RuleFieldBinding.BindChoice(
                RuleFieldBinding.Require<DropdownField>(root, name), initialValue, writeValue, _onChanged, options);
        }
    }

    // Kopīgās darbības formas lauka sākotnējai aizpildīšanai un izmaiņu nolasīšanai.
    internal static class RuleFieldBinding
    {
        public static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            return root.Q<T>(name)
                ?? throw new InvalidOperationException($"Missing rule UI element '{name}'.");
        }

        public static void BindValue<T>(
            BaseField<T> field, T initialValue, Action<T> writeValue, Action onChanged)
        {
            // Formas aizpildīšana nav lietotāja veikta izmaiņa.
            field.SetValueWithoutNotify(initialValue);
            field.RegisterValueChangedCallback(evt =>
            {
                // Iekšējo lauku notikumus neapstrādā atkārtoti.
                if (!ReferenceEquals(evt.target, field))
                    return;

                writeValue(evt.newValue);
                onChanged();
            });
        }

        public static void BindEnabled(
            VisualElement root, bool initialValue, Action<bool> writeValue, Action onChanged)
        {
            var enabled = Require<Toggle>(root, "rule-enabled");
            var parameters = root.Q<VisualElement>(className: "rule-parameters")
                ?? throw new InvalidOperationException("Missing rule parameter group.");

            parameters.SetEnabled(initialValue);
            BindValue(enabled, initialValue, value =>
            {
                writeValue(value);
                // Izslēdzot noteikumu, tā parametru vērtības netiek atiestatītas.
                parameters.SetEnabled(value);
            }, onChanged);
        }

        public static void BindChoice<T>(
            DropdownField field, T initialValue, Action<T> writeValue, Action onChanged,
            params (string Label, T Value)[] options) where T : struct, Enum
        {
            // Saskarnes uzrakstus sasaista ar konkrētām enum vērtībām, nevis to kārtas numuriem.
            int initialIndex = Array.FindIndex(options,
                option => EqualityComparer<T>.Default.Equals(option.Value, initialValue));
            if (initialIndex < 0)
                throw new InvalidOperationException($"Unsupported value for '{field.name}'.");

            field.choices = new List<string>(Array.ConvertAll(options, option => option.Label));
            BindValue(field, options[initialIndex].Label, label =>
            {
                int index = Array.FindIndex(options, option => option.Label == label);
                if (index < 0)
                    throw new InvalidOperationException($"Unknown option for '{field.name}'.");

                writeValue(options[index].Value);
            }, onChanged);
        }
    }
}
