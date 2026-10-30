using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Judze.ProjectAnalysis.Editor.Settings;
using static Judze.ProjectAnalysis.Editor.WebConnection.WebResponseJson;

namespace Judze.ProjectAnalysis.Editor.WebConnection
{
    internal interface IWebReportUploadClient
    {
        // Atbrīvojot darbību, pārtrauc pieprasījumu un vairs neizsauc pabeigšanas funkciju.
        IDisposable Upload(WebReportUploadRequest request, Action<WebReportUploadResult> completed);
    }

    // Nosūtīšanai izmanto gatavu JSON kopiju; nepalaiž analīzi un nemaina pārskata saturu.
    internal sealed class WebReportUploadRequest
    {
        public const int MaximumReportBytes = 10 * 1024 * 1024;
        public string ServerUrl { get; }
        public string Endpoint { get; }
        public string ProjectUuid { get; }
        public string ReportId { get; }
        public string AccessToken { get; }
        public byte[] ReportBytes { get; }

        private WebReportUploadRequest(string url, string uuid, string token, string reportId, string json)
        {
            ServerUrl = url;
            Endpoint = url + "/api/projects/" + uuid + "/reports";
            ProjectUuid = uuid;
            ReportId = reportId;
            AccessToken = token;
            ReportBytes = new UTF8Encoding(false).GetBytes(json);
        }

        public static bool TryCreate(string serverUrl, string projectUuid, string token,
            string reportId, string json, out WebReportUploadRequest request, out string error)
        {
            request = null;
            if (!WebConnectionUrl.TryNormalize(serverUrl, out string url, out error)) return false;
            if (url.Length == 0) { error = "Enter a Server URL in Web Connection."; return false; }
            if (!WebProjectUuidValidator.TryNormalize(projectUuid, out string uuid, out error)) return false;
            if (uuid.Length == 0) { error = "Enter the Web Project UUID in Web Connection."; return false; }
            string value = (token ?? "").Trim();
            if (value.Length == 0) { error = "Enter an Access token in Web Connection."; return false; }
            if (value.Any(c => c <= 32 || c >= 127))
            { error = "The Access token must not contain spaces, control characters or non-ASCII characters."; return false; }
            if (string.IsNullOrWhiteSpace(reportId) || reportId.Length > 64 || string.IsNullOrWhiteSpace(json))
            { error = "The selected report is unavailable. Select a valid report."; return false; }
            // Ierobežojums attiecas uz UTF-8 datnes baitiem
            if (Encoding.UTF8.GetByteCount(json) > MaximumReportBytes)
            { error = "The report exceeds the Web API limit of 10 MiB."; return false; }
            request = new WebReportUploadRequest(url, uuid, value, reportId, json);
            error = null;
            return true;
        }
    }

    internal enum WebReportUploadStatus
    {
        Uploaded, AlreadyExists, Unauthorized, Forbidden, PasswordChangeRequired, NotFound,
        TooLarge, InvalidReport, RateLimited, ServerError, NetworkError, Timeout, Redirected, InvalidResponse, Rejected
    }

    internal sealed class WebReportUploadResult
    {
        public WebReportUploadStatus Status { get; }
        public long HttpStatusCode { get; }
        public bool Succeeded => Status == WebReportUploadStatus.Uploaded || Status == WebReportUploadStatus.AlreadyExists;

        public WebReportUploadResult(WebReportUploadStatus status, long httpStatusCode = 0)
        { Status = status; HttpStatusCode = httpStatusCode; }

        // Neattēlo servera atbildes vai izņēmuma tekstu, kurā varētu būt slepena informācija.
        public string Message
        {
            get
            {
                switch (Status)
                {
                    case WebReportUploadStatus.Uploaded: return "Report uploaded successfully.";
                    case WebReportUploadStatus.AlreadyExists: return "This report already exists on Web. The existing report was kept.";
                    case WebReportUploadStatus.Unauthorized: return "Authentication failed (401). Check the token in Web Connection; it may be expired or revoked.";
                    case WebReportUploadStatus.Forbidden: return "Access denied (403). Check project access and the token's reports:submit permission.";
                    case WebReportUploadStatus.PasswordChangeRequired: return "Change your password in the web application before sending reports.";
                    case WebReportUploadStatus.NotFound: return "Project or upload endpoint not found (404). Check Server URL, Web Project UUID and project access.";
                    case WebReportUploadStatus.TooLarge: return "The server rejected the report size (413). The API limit is 10 MiB; hosting limits may be lower.";
                    case WebReportUploadStatus.InvalidReport: return "The server rejected the report JSON. The Web API must accept the current Unity export format.";
                    case WebReportUploadStatus.RateLimited: return "Too many uploads (429). Wait before sending again.";
                    case WebReportUploadStatus.ServerError: return "The server could not confirm report storage. Try sending the same report again later.";
                    case WebReportUploadStatus.NetworkError: return "Could not confirm delivery. Check the connection and retry the same report; Web will not create a duplicate.";
                    case WebReportUploadStatus.Timeout: return "The upload timed out. Web may already have saved it; retrying the same report will not create a duplicate.";
                    case WebReportUploadStatus.Redirected: return "The server redirected the upload. Check the final Server URL; the token and report were not forwarded.";
                    case WebReportUploadStatus.InvalidResponse: return "Web did not confirm storage of this report. Check the API response; retrying the same report will not create a duplicate.";
                    default: return "The server rejected the upload (HTTP " + HttpStatusCode + ").";
                }
            }
        }
    }

    internal static class WebReportUploadResponse
    {
        public static WebReportUploadResult Read(long statusCode, string contentType, string body, string reportId)
        {
            WebReportUploadResult Result(WebReportUploadStatus status) => new WebReportUploadResult(status, statusCode);
            if (statusCode == 401) return Result(WebReportUploadStatus.Unauthorized);
            if (statusCode == 403)
                return Result(ReadString(ReadObject(body), "code") == "PASSWORD_CHANGE_REQUIRED"
                    ? WebReportUploadStatus.PasswordChangeRequired : WebReportUploadStatus.Forbidden);
            if (statusCode == 404) return Result(WebReportUploadStatus.NotFound);
            if (statusCode == 413) return Result(WebReportUploadStatus.TooLarge);
            if (statusCode == 400 || statusCode == 422) return Result(WebReportUploadStatus.InvalidReport);
            if (statusCode == 408 || statusCode == 504) return Result(WebReportUploadStatus.Timeout);
            if (statusCode == 429) return Result(WebReportUploadStatus.RateLimited);
            if (statusCode >= 500) return Result(WebReportUploadStatus.ServerError);
            if (statusCode >= 300 && statusCode < 400) return Result(WebReportUploadStatus.Redirected);
            if (statusCode != 200 && statusCode != 201) return Result(WebReportUploadStatus.Rejected);
            if (!IsJson(contentType)) return Result(WebReportUploadStatus.InvalidResponse);

            var root = ReadObject(body);
            string duplicate = Field(root, "duplicate", "boolean")?.Value;
            // HTTP statuss viens pats nepierāda, ka serveris saglabājis tieši atlasīto pārskatu.
            if (!string.Equals(ReadString(root, "unityReportId"), reportId, StringComparison.Ordinal) ||
                !long.TryParse(Field(root, "id", "number")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0 ||
                (statusCode == 201 && duplicate != "false") || (statusCode == 200 && duplicate != "true"))
                return Result(WebReportUploadStatus.InvalidResponse);
            return Result(statusCode == 201 ? WebReportUploadStatus.Uploaded : WebReportUploadStatus.AlreadyExists);
        }
    }
}
