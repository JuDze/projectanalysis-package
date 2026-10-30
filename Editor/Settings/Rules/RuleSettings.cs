using System;
using System.Collections.Generic;
using System.Linq;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Kopīgais līgums prasa tikai ieslēgšanas stāvokli, nevis robežvērtības vai labojumus.
    internal interface IRuleSettings { bool Enabled { get; } }

    // Lietotāja noteikumu iestatījumi;
    [Serializable]
    internal sealed class RuleSettings
    {
        public Tex001Settings Tex001 = new Tex001Settings();
        public Tex002Settings Tex002 = new Tex002Settings();
        public Tex003Settings Tex003 = new Tex003Settings();
        public Tex004Settings Tex004 = new Tex004Settings();

        public Mod001Settings Mod001 = new Mod001Settings();
        public Mod002Settings Mod002 = new Mod002Settings();
        public Mod003Settings Mod003 = new Mod003Settings();
        public Mod004Settings Mod004 = new Mod004Settings();

        public Aud001Settings Aud001 = new Aud001Settings();
        public Aud002Settings Aud002 = new Aud002Settings();
        public Aud003Settings Aud003 = new Aud003Settings();
        public Aud004Settings Aud004 = new Aud004Settings();
        public Aud005Settings Aud005 = new Aud005Settings();

        // Grupas uzskaita vienuviet; īpašības nemaina saglabātā JSON laukus.
        public IEnumerable<IRuleSettings> TextureRules
        {
            get
            {
                yield return Tex001;
                yield return Tex002;
                yield return Tex003;
                yield return Tex004;
            }
        }
        public bool HasEnabledTextureRules => TextureRules.Any(rule => rule.Enabled);

        public IEnumerable<IRuleSettings> ModelRules
        {
            get
            {
                yield return Mod001;
                yield return Mod002;
                yield return Mod003;
                yield return Mod004;
            }
        }
        public bool HasEnabledModelRules => ModelRules.Any(rule => rule.Enabled);

        public IEnumerable<IRuleSettings> AudioRules
        {
            get
            {
                yield return Aud001;
                yield return Aud002;
                yield return Aud003;
                yield return Aud004;
                yield return Aud005;
            }
        }
        public bool HasEnabledAudioRules => AudioRules.Any(rule => rule.Enabled);

        public IEnumerable<IRuleSettings> All => TextureRules.Concat(ModelRules).Concat(AudioRules);
        public bool HasEnabledRules => All.Any(rule => rule.Enabled);

    }
}
