using System;
using Judze.ProjectAnalysis.Editor.Settings;
using Judze.ProjectAnalysis.Editor.WebConnection;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Savieno pārskata atlasi ar augšupielādi; pati forma neveic HTTP pieprasījumus.
    internal sealed class ReportUploadController : IDisposable
    {
        private readonly ReportsTabView _view;
        private readonly WebConnectionSettingsDraft _settings;
        private readonly IWebReportUploadClient _client;
        private IDisposable _operation;
        private int _generation;
        private bool _uploading, _disposed;

        public ReportUploadController(ReportsTabView view, WebConnectionSettingsDraft settings, IWebReportUploadClient client)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _view.SendRequested += Send;
            _view.CancelUploadRequested += Cancel;
        }

        private void Send(ReportPreviewRow report)
        {
            if (_disposed || _uploading || report == null) return;
            if (!WebReportUploadRequest.TryCreate(_settings.ServerUrl, _settings.WebProjectUuid,
                _settings.AccessToken, report.Id, report.Json, out var request, out string error))
            { _view.ShowMessage(error); return; }

            // Pieprasījums fiksē pašreizējās formas vērtības; vēlākas izmaiņas tā saturu nemaina.
            int generation = ++_generation;
            _uploading = true;
            _view.SetUploading(true);
            _view.ShowMessage("Uploading report " + request.ReportId + " to " + request.ServerUrl + "…");
            try
            {
                var operation = _client.Upload(request, result =>
                {
                    if (_disposed || generation != _generation || !_uploading) return;
                    Stop();
                    _view.ShowMessage("Report " + request.ReportId + ": " +
                        (result ?? new WebReportUploadResult(WebReportUploadStatus.InvalidResponse)).Message);
                });
                // Arī tūlītēja atbilde nedrīkst atstāt nepabeigtu darbības atsauci.
                if (_uploading && generation == _generation && !_disposed) _operation = operation;
                else operation?.Dispose();
            }
            catch
            {
                Stop();
                _view.ShowMessage("Could not start or confirm the upload. Check Web Connection and retry the same report.");
            }
        }

        private void Cancel()
        {
            if (_disposed || !_uploading) return;
            Stop();
            _view.ShowMessage("Upload cancelled. Web may already have saved the report; retrying the same report will not create a duplicate.");
        }

        private void Stop()
        {
            // Pēc atcelšanas vai loga pārbūves novēlotas atbildes ignorē.
            _generation++;
            _uploading = false;
            _operation?.Dispose();
            _operation = null;
            _view.SetUploading(false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _view.SendRequested -= Send;
            _view.CancelUploadRequested -= Cancel;
            Stop();
        }
    }
}
