using System.IO;
using System.Text;
using System;

namespace Judze.ProjectAnalysis.Editor.Reporting
{
    // Raksta jau pārbaudītu eksporta JSON, nemainot tā saturu un neveicot atkārtotu serializāciju.
    internal sealed class JsonReportFileWriter : IReportFileWriter
    {
        public void Save(string path, string exportJson)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A report file path is required.", nameof(path));
            if (string.IsNullOrWhiteSpace(exportJson)) throw new ArgumentException("Prepared report JSON is required.", nameof(exportJson));
            string destination = Path.GetFullPath(path);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Esošo datni aizstāj tikai pēc pilnīgas jaunā satura ierakstīšanas.
                byte[] bytes = new UTF8Encoding(false).GetBytes(exportJson);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                // Tīrīšanas kļūda nedrīkst paslēpt sākotnējo saglabāšanas kļūdu.
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    internal interface IReportFileWriter
    {
        // Izsaucējs pirms saglabāšanas pārbauda pārskatu un sagatavo eksportējamo JSON.
        void Save(string path, string exportJson);
    }
}
