using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Unity sesijas vērtība saglabājas pēc skriptu pārlādes, bet pazūd pēc Unity aizvēršanas.
    internal sealed class SessionStateAccessTokenStore : IAccessTokenStore
    {
        private readonly string _prefix;

        public SessionStateAccessTokenStore(string projectDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
                throw new ArgumentException("A project directory is required.", nameof(projectDirectory));
            _prefix = "Judze.ProjectAnalysis.AccessToken." + Hash(Path.GetFullPath(projectDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) + ".";
        }

        public string Load(string serverUrl)
        {
            string url = WebConnectionUrl.Normalize(serverUrl);
            return url.Length == 0 ? "" : SessionState.GetString(_prefix + Hash(url), "");
        }

        public void Save(string serverUrl, string token)
        {
            string url = WebConnectionUrl.Normalize(serverUrl);
            if (url.Length == 0)
            {
                if (!string.IsNullOrEmpty(token))
                    throw new ArgumentException("A server URL is required for the token.");
                return;
            }

            // Projekta un servera atslēga nepieļauj cita savienojuma tokena izmantošanu.
            string key = _prefix + Hash(url);
            if (string.IsNullOrEmpty(token)) SessionState.EraseString(key);
            else SessionState.SetString(key, token);
        }

        private static string Hash(string value)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", "");
        }
    }
}
