using System;
using System.Reflection;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Analysis.Audio
{
    internal interface IAudioImportSizeReader
    {
        bool TryRead(AudioImporter importer, out long bytes, out string issue);
    }

    // Project Auditor 1.0.2 izmanto iekšējo compSize; atkarība ir izolēta un var nebūt pieejama.
    internal sealed class UnityAudioImportSizeReader : IAudioImportSizeReader
    {
        private static readonly PropertyInfo SizeProperty = typeof(AudioImporter).GetProperty("compSize",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public bool TryRead(AudioImporter importer, out long bytes, out string issue)
        {
            bytes = 0; issue = null;
            if (importer == null || SizeProperty == null)
            { issue = "Unity's internal imported audio size (compSize) is unavailable."; return false; }
            try
            {
                object value = SizeProperty.GetValue(importer);
                if (value is int intValue) bytes = intValue;
                else if (value is long longValue) bytes = longValue;
                else if (value is uint uintValue) bytes = uintValue;
                else { issue = "Unity returned an unsupported imported audio size value."; return false; }
                if (bytes <= 0)
                { issue = "Unity did not return a positive imported audio size."; return false; }
                return true;
            }
            catch (Exception exception)
            { issue = "Could not read imported audio size: " + exception.GetBaseException().Message; return false; }
        }
    }
}
