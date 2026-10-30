using System;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    internal static class WebConnectionUrl
    {
        public static bool TryNormalize(string value, out string normalized, out string error)
        {
            normalized = "";
            error = null;
            string text = (value ?? "").Trim();
            // Tukša adrese apzīmē vēl nekonfigurētu savienojumu un ļauj saglabāt atiestatīšanu.
            if (text.Length == 0) return true;
            foreach (char character in text)
                if (char.IsControl(character) || char.IsWhiteSpace(character) || character == '\\')
                { error = "Enter a server URL without spaces or control characters."; return false; }
            if (!Uri.TryCreate(text, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Host))
            { error = "Enter a complete server URL starting with https://."; return false; }
            if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            { error = "Use HTTPS. HTTP is supported only for a local server on this computer."; return false; }
            if (!string.IsNullOrEmpty(uri.UserInfo) || text.IndexOf('?') >= 0 || text.IndexOf('#') >= 0)
            { error = "Use a base URL without credentials, a query or a fragment. Enter the token separately."; return false; }
            normalized = uri.AbsoluteUri.TrimEnd('/');
            return true;
        }

        public static string Normalize(string value)
        {
            if (!TryNormalize(value, out string normalized, out string error))
                throw new ArgumentException(error, nameof(value));
            return normalized;
        }
    }

    // Pārbauda tikai UUID pierakstu; projekta esamību Web sistēmā šeit nepārbauda.
    internal static class WebProjectUuidValidator
    {
        public static bool TryNormalize(string value, out string normalized, out string error)
        {
            normalized = "";
            error = null;
            string text = (value ?? "").Trim();
            // Neaizpildītu formu drīkst saglabāt, kamēr Web projekts vēl nav izveidots.
            if (text.Length == 0) return true;
            if (!Guid.TryParseExact(text, "D", out Guid uuid) || uuid == Guid.Empty)
            {
                error = "Enter the Web project UUID in the format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.";
                return false;
            }
            normalized = uuid.ToString("D");
            return true;
        }

        public static string Normalize(string value)
        {
            if (!TryNormalize(value, out string normalized, out string error))
                throw new ArgumentException(error, nameof(value));
            return normalized;
        }
    }
}
