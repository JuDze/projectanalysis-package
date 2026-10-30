using System;
using System.Linq;
using Judze.ProjectAnalysis.Editor.Settings;
using static Judze.ProjectAnalysis.Editor.WebConnection.WebResponseJson;

namespace Judze.ProjectAnalysis.Editor.WebConnection
{
    internal interface IWebConnectionClient
    {
        // Atbrīvojot darbību, pārbaudi atceļ un vairs neizsauc pabeigšanas funkciju.
        IDisposable Check(WebConnectionRequest request, Action<WebConnectionResult> completed);
    }

    // Pieprasījums dzīvo tikai atmiņā; tokens nav daļa no saglabājamajiem iestatījumiem.
    internal sealed class WebConnectionRequest
    {
        public string Endpoint { get; }
        public string ProjectUuid { get; }
        public string AccessToken { get; }
        private WebConnectionRequest(string url, string uuid, string token)
        { Endpoint = url + "/api/projects/" + uuid + "/connection"; ProjectUuid = uuid; AccessToken = token; }

        public static bool TryCreate(string serverUrl, string projectUuid, string token,
            out WebConnectionRequest request, out string error)
        {
            request = null;
            if (!WebConnectionUrl.TryNormalize(serverUrl, out string url, out error)) return false;
            if (url.Length == 0) { error = "Enter a Server URL to test the connection."; return false; }
            if (!WebProjectUuidValidator.TryNormalize(projectUuid, out string uuid, out error)) return false;
            if (uuid.Length == 0) { error = "Enter the Web Project UUID to test the connection."; return false; }
            string value = (token ?? "").Trim();
            if (value.Length == 0) { error = "Enter an Access token to test the connection."; return false; }
            if (value.Any(c => c <= 32 || c >= 127))
            { error = "The Access token must not contain spaces, control characters or non-ASCII characters."; return false; }
            request = new WebConnectionRequest(url, uuid, value);
            error = null;
            return true;
        }
    }

    internal enum WebConnectionStatus
    { Connected, Unauthorized, Forbidden, PasswordChangeRequired, NotFound, RateLimited, ServerError, NetworkError, Timeout, Redirected, InvalidResponse, Rejected }

    internal sealed class WebConnectionResult
    {
        public WebConnectionStatus Status { get; }
        public long HttpStatusCode { get; }
        public bool Connected => Status == WebConnectionStatus.Connected;
        public WebConnectionResult(WebConnectionStatus status, long httpStatusCode = 0)
        { Status = status; HttpStatusCode = httpStatusCode; }

        // Lietotāja paziņojumos neatspoguļo servera atbildes saturu vai tokenu.
        public string Message
        {
            get
            {
                switch (Status)
                {
                    case WebConnectionStatus.Connected: return "Connection verified. The token can submit reports to this Web project.";
                    case WebConnectionStatus.Unauthorized: return "Authentication failed (401). Check the token; it may be invalid, expired or revoked.";
                    case WebConnectionStatus.Forbidden: return "Access denied (403). Check project access and the token's reports:submit permission.";
                    case WebConnectionStatus.PasswordChangeRequired: return "Change your password in the web application before using this token.";
                    case WebConnectionStatus.NotFound: return "Project or connection endpoint not found (404). Check Server URL, Web Project UUID and project access.";
                    case WebConnectionStatus.RateLimited: return "Too many connection checks (429). Wait before trying again.";
                    case WebConnectionStatus.ServerError: return "The server could not complete the check. Try again later.";
                    case WebConnectionStatus.NetworkError: return "Could not reach the server securely. Check the address, network and server certificate.";
                    case WebConnectionStatus.Timeout: return "The connection check timed out. Try again when the server is available.";
                    case WebConnectionStatus.Redirected: return "The server redirected the request. Check the final Server URL; the token was not forwarded.";
                    case WebConnectionStatus.InvalidResponse: return "The server response did not confirm access to the selected project. Check the URL and API version.";
                    default: return "The server rejected the connection check (HTTP " + HttpStatusCode + ").";
                }
            }
        }
    }

    internal static class WebConnectionResponse
    {
        public const int MaximumBytes = WebResponseJson.MaximumBytes;

        public static WebConnectionResult Read(long statusCode, string contentType, string body, string projectUuid)
        {
            WebConnectionResult Result(WebConnectionStatus status) => new WebConnectionResult(status, statusCode);
            if (statusCode == 401) return Result(WebConnectionStatus.Unauthorized);
            if (statusCode == 403)
            {
                var error = ReadObject(body);
                return Result(ReadString(error, "code") == "PASSWORD_CHANGE_REQUIRED"
                    ? WebConnectionStatus.PasswordChangeRequired : WebConnectionStatus.Forbidden);
            }
            if (statusCode == 404) return Result(WebConnectionStatus.NotFound);
            if (statusCode == 408 || statusCode == 504) return Result(WebConnectionStatus.Timeout);
            if (statusCode == 429) return Result(WebConnectionStatus.RateLimited);
            if (statusCode >= 500) return Result(WebConnectionStatus.ServerError);
            if (statusCode >= 300 && statusCode < 400) return Result(WebConnectionStatus.Redirected);
            if (statusCode != 200) return Result(WebConnectionStatus.Rejected);

            if (!IsJson(contentType))
                return Result(WebConnectionStatus.InvalidResponse);
            var root = ReadObject(body);
            var project = Field(root, "project", "object");
            var permissions = Field(root, "permissions", "object");
            // HTTP 200 nepietiek: jāsakrīt UUID un jābūt skaidri apstiprinātām tiesībām.
            if (Field(root, "connected", "boolean")?.Value != "true" ||
                !Guid.TryParseExact(ReadString(project, "uuid"), "D", out Guid returnedUuid) ||
                !Guid.TryParseExact(projectUuid, "D", out Guid expectedUuid) || returnedUuid != expectedUuid ||
                Field(permissions, "submitReports", "boolean")?.Value != "true")
                return Result(WebConnectionStatus.InvalidResponse);
            return Result(WebConnectionStatus.Connected);
        }
    }
}
