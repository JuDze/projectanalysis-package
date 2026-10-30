using System;
using System.IO;
using System.Text;

namespace Judze.ProjectAnalysis.Editor.Settings
{
    // Atbild par iestatījumu datnes lasīšanu un rakstīšanu.
    internal sealed class JsonRuleSettingsStorage : IRuleSettingsStorage
    {
        private readonly string _filePath;
        private bool _preserveOriginalOnSave;

        public JsonRuleSettingsStorage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("A settings file path is required.", nameof(filePath));

            _filePath = Path.GetFullPath(filePath);
        }

        public RuleSettings Load()
        {
            _preserveOriginalOnSave = false;
            try
            {
                string json = File.ReadAllText(_filePath, Encoding.UTF8);
                return RuleSettingsJson.Deserialize(json);
            }
            catch (FileNotFoundException)
            {
                // Datnes neesamība pirmajā palaišanas reizē nav kļūda.
                return new RuleSettings();
            }
            catch (DirectoryNotFoundException)
            {
                return new RuleSettings();
            }
            catch (Exception)
            {
                // Nākamās saglabāšanas laikā saglabā sākotnējās datnes kopiju.
                _preserveOriginalOnSave = true;
                throw;
            }
        }

        public void Save(RuleSettings settings)
        {
            string json = RuleSettingsJson.Serialize(settings);
            SettingsFileWriter.WriteAtomically(_filePath, json, _preserveOriginalOnSave);
            _preserveOriginalOnSave = false;
        }
    }

    // Savienojuma iestatījumi glabājas atsevišķi no analīzes noteikumiem un pārskatiem.
    internal sealed class JsonWebConnectionSettingsStorage : IWebConnectionSettingsStorage
    {
        private readonly string _path;
        private bool _preserveOriginal;
        public JsonWebConnectionSettingsStorage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A settings path is required.", nameof(path));
            _path = Path.GetFullPath(path);
        }

        public WebConnectionSettings Load()
        {
            _preserveOriginal = false;
            try { return WebConnectionSettingsJson.Deserialize(File.ReadAllText(_path, Encoding.UTF8)); }
            catch (FileNotFoundException) { return new WebConnectionSettings(); }
            catch (DirectoryNotFoundException) { return new WebConnectionSettings(); }
            catch { _preserveOriginal = true; throw; }
        }

        public void Save(WebConnectionSettings settings)
        {
            string json = WebConnectionSettingsJson.Serialize(settings);
            SettingsFileWriter.WriteAtomically(_path, json, _preserveOriginal);
            _preserveOriginal = false;
        }
    }

    // Kopīgā datnes rakstīšana nepārbauda iestatījumus un neapstrādā tokenus.
    internal static class SettingsFileWriter
    {
        public static void WriteAtomically(string path, string json, bool preserveOriginal)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Vispirms pilnībā ieraksta UTF-8 datus bez BOM pagaidu datnē.
                byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    string backup = preserveOriginal ? path + "." + Guid.NewGuid().ToString("N") + ".bak" : null;
                    File.Replace(temporary, path, backup);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                // Pagaidu datnes tīrīšana nedrīkst paslēpt sākotnējo rakstīšanas kļūdu.
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
