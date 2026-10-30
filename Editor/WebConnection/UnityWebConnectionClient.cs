using System;
using System.Text;
using UnityEditor;
using UnityEngine.Networking;

namespace Judze.ProjectAnalysis.Editor.WebConnection
{
    // Veic tikai GET savienojuma pārbaudi; pārskatu nosūtīšana ir atsevišķa darbība.
    internal sealed class UnityWebConnectionClient : IWebConnectionClient
    {
        private readonly int _timeoutSeconds;
        public UnityWebConnectionClient(int timeoutSeconds = 20)
        {
            if (timeoutSeconds < 1 || timeoutSeconds > 120) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            _timeoutSeconds = timeoutSeconds;
        }

        public IDisposable Check(WebConnectionRequest request, Action<WebConnectionResult> completed)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            var operation = new CheckOperation(request.ProjectUuid, completed, _timeoutSeconds);
            try { operation.Start(request); return operation; }
            catch
            {
                operation.Dispose();
                throw new InvalidOperationException("Could not start the connection check.");
            }
        }

        private sealed class CheckOperation : IDisposable
        {
            private readonly string _projectUuid;
            private readonly int _timeoutSeconds;
            private Action<WebConnectionResult> _completed;
            private UnityWebRequest _request;
            private LimitedDownload _download;
            private double _deadline;
            private bool _disposed;

            public CheckOperation(string projectUuid, Action<WebConnectionResult> completed, int timeoutSeconds)
            { _projectUuid = projectUuid; _completed = completed; _timeoutSeconds = timeoutSeconds; }

            public void Start(WebConnectionRequest input)
            {
                _request = new UnityWebRequest(input.Endpoint, UnityWebRequest.kHttpVerbGET);
                _download = new LimitedDownload();
                _request.downloadHandler = _download;
                _request.redirectLimit = 0;
                // Editor pulkstenis ļauj skaidri atšķirt mūsu laika ierobežojumu no tīkla kļūdas.
                _request.timeout = _timeoutSeconds + 1;
                _request.SetRequestHeader("Accept", "application/json");
                _request.SetRequestHeader("Authorization", "Bearer " + input.AccessToken);
                _request.SetRequestHeader("Cache-Control", "no-cache");
                _deadline = EditorApplication.timeSinceStartup + _timeoutSeconds;
                _request.SendWebRequest();
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Dispose;
                EditorApplication.quitting += Dispose;
            }

            private void Tick()
            {
                if (_disposed) return;
                WebConnectionResult result;
                if (!_request.isDone)
                {
                    if (EditorApplication.timeSinceStartup < _deadline) return;
                    result = new WebConnectionResult(WebConnectionStatus.Timeout);
                }
                else if (_download.TooLarge || _request.result == UnityWebRequest.Result.DataProcessingError)
                    result = new WebConnectionResult(WebConnectionStatus.InvalidResponse, _request.responseCode);
                else if (_request.responseCode >= 300 && _request.responseCode < 400)
                    result = new WebConnectionResult(WebConnectionStatus.Redirected, _request.responseCode);
                else if (_request.result == UnityWebRequest.Result.ConnectionError || _request.responseCode == 0)
                    result = new WebConnectionResult(WebConnectionStatus.NetworkError);
                else
                    result = WebConnectionResponse.Read(_request.responseCode, _request.GetResponseHeader("Content-Type"),
                        _download.Text, _projectUuid);
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

        // Neliela atbilde nedrīkst pārvērsties par neierobežotu datu lejupielādi.
        private sealed class LimitedDownload : DownloadHandlerScript
        {
            private readonly byte[] _body = new byte[WebConnectionResponse.MaximumBytes];
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
