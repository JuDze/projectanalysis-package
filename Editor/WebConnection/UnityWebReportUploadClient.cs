using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.Networking;

namespace Judze.ProjectAnalysis.Editor.WebConnection
{
    // Pārsūta vienu JSON datni; glabāšanu datubāzē un bucket veic Web API.
    internal sealed class UnityWebReportUploadClient : IWebReportUploadClient
    {
        private readonly int _timeoutSeconds;

        public UnityWebReportUploadClient(int timeoutSeconds = 60)
        {
            if (timeoutSeconds < 1 || timeoutSeconds > 300) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            _timeoutSeconds = timeoutSeconds;
        }

        public IDisposable Upload(WebReportUploadRequest request, Action<WebReportUploadResult> completed)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            var operation = new UploadOperation(request.ReportId, completed, _timeoutSeconds);
            try { operation.Start(request); return operation; }
            catch
            {
                operation.Dispose();
                throw new InvalidOperationException("Could not start the report upload.");
            }
        }

        private sealed class UploadOperation : IDisposable
        {
            private readonly string _reportId;
            private readonly int _timeoutSeconds;
            private Action<WebReportUploadResult> _completed;
            private UnityWebRequest _request;
            private LimitedDownload _download;
            private double _deadline;
            private bool _disposed;

            public UploadOperation(string reportId, Action<WebReportUploadResult> completed, int timeoutSeconds)
            { _reportId = reportId; _completed = completed; _timeoutSeconds = timeoutSeconds; }

            public void Start(WebReportUploadRequest input)
            {
                var form = new List<IMultipartFormSection>
                {
                    new MultipartFormFileSection("report", input.ReportBytes, "ProjectAnalysis.json", "application/json")
                };
                // Unity izveido multipart robežu un Content-Type; tos nedrīkst aizstāt ar application/json.
                _request = UnityWebRequest.Post(input.Endpoint, form);
                var defaultDownload = _request.downloadHandler;
                _download = new LimitedDownload();
                _request.downloadHandler = _download;
                defaultDownload?.Dispose();
                _request.redirectLimit = 0;
                _request.timeout = _timeoutSeconds + 1;
                _request.SetRequestHeader("Accept", "application/json");
                _request.SetRequestHeader("Authorization", "Bearer " + input.AccessToken);
                _deadline = EditorApplication.timeSinceStartup + _timeoutSeconds;
                _request.SendWebRequest();
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Dispose;
                EditorApplication.quitting += Dispose;
            }

            private void Tick()
            {
                if (_disposed) return;
                WebReportUploadResult result;
                if (!_request.isDone)
                {
                    if (EditorApplication.timeSinceStartup < _deadline) return;
                    result = new WebReportUploadResult(WebReportUploadStatus.Timeout);
                }
                else if (_download.TooLarge || _request.result == UnityWebRequest.Result.DataProcessingError)
                    result = new WebReportUploadResult(WebReportUploadStatus.InvalidResponse, _request.responseCode);
                else if (_request.responseCode >= 300 && _request.responseCode < 400)
                    result = new WebReportUploadResult(WebReportUploadStatus.Redirected, _request.responseCode);
                else if (_request.result == UnityWebRequest.Result.ConnectionError || _request.responseCode == 0)
                    result = new WebReportUploadResult(WebReportUploadStatus.NetworkError);
                else
                    result = WebReportUploadResponse.Read(_request.responseCode, _request.GetResponseHeader("Content-Type"),
                        _download.Text, _reportId);
                var completed = _completed;
                Dispose();
                completed?.Invoke(result);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                EditorApplication.update -= Tick;
                AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
                EditorApplication.quitting -= Dispose;
                _completed = null;
                if (_request != null)
                {
                    if (!_request.isDone) _request.Abort();
                    _request.Dispose();
                    _request = null;
                }
                _download = null;
            }
        }

        // Servera kļūdas lapa nedrīkst izraisīt neierobežotu datu lejupielādi.
        private sealed class LimitedDownload : DownloadHandlerScript
        {
            private readonly byte[] _body = new byte[WebResponseJson.MaximumBytes];
            private int _length;
            public bool TooLarge { get; private set; }
            public string Text => Encoding.UTF8.GetString(_body, 0, _length);
            public LimitedDownload() : base(new byte[8192]) { }
            protected override void ReceiveContentLengthHeader(ulong contentLength)
            { if (contentLength > (ulong)_body.Length) TooLarge = true; }
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (TooLarge || dataLength > _body.Length - _length) { TooLarge = true; return false; }
                if (dataLength > 0) { Buffer.BlockCopy(data, 0, _body, _length, dataLength); _length += dataLength; }
                return true;
            }
        }
    }
}
