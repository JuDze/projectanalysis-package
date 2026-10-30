using System;
using System.Linq;
using System.Collections.Generic;
using Judze.ProjectAnalysis.Editor.Analysis.Execution;
using Judze.ProjectAnalysis.Editor.Reporting;
using Judze.ProjectAnalysis.Editor.Analysis.Scope;
using Judze.ProjectAnalysis.Editor.Fixes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Judze.ProjectAnalysis.Editor.UI
{
    // Pārskatu izveide, saglabāšana un dzēšana izmanto loga saglabāto stāvokli.
    internal sealed class ReportWorkflowController : IDisposable
    {
        private readonly VisualElement _root;
        private readonly ReportsTabView _view;
        private readonly List<string> _reportJsons;
        private readonly Func<string> _getBaseline;
        private readonly Action<string> _setBaseline;
        private readonly IFixSessionStore _fixState;
        private readonly IReportFileWriter _writer;
        private readonly Func<string, string, string, string, string> _selectPath;
        private readonly Func<string, string, string, bool> _confirm;
        private AnalysisTabWindow _analysis;

        public ReportWorkflowController(VisualElement root, ReportsTabView view, List<string> reportJsons,
            Func<string> getBaseline, Action<string> setBaseline, IFixSessionStore fixState,
            IReportFileWriter writer, Func<string, string, string, string, string> selectPath,
            Func<string, string, string, bool> confirm)
        {
            _root = root; _view = view; _reportJsons = reportJsons;
            _getBaseline = getBaseline; _setBaseline = setBaseline; _fixState = fixState;
            _writer = writer; _selectPath = selectPath; _confirm = confirm;
            _view.SetRows(ReadReportRows());
            _view.SaveRequested += SaveReport;
            _view.DeleteRequested += DeleteReport;
        }

        public void Dispose()
        {
            _view.SaveRequested -= SaveReport;
            _view.DeleteRequested -= DeleteReport;
        }

        public void AttachAnalysis(AnalysisTabWindow analysis)
        {
            _analysis = analysis;
            if (string.IsNullOrEmpty(_getBaseline())) return;
            try { _analysis.SetReportAvailable(AnalysisReportJson.ReadSnapshot(_getBaseline()).SessionId == _fixState.Id); }
            catch (Exception error) { _analysis.ShowFixMessage(error.Message); }
        }

        public void ClearBaseline() => _setBaseline(null);

        public void Capture(AnalysisRunResult run, AnalysisScopeSelection selection)
        {
            string scope = selection.Mode == AnalysisScopeMode.AllProjectAssets ? "Assets and Packages" : selection.Path;
            var snapshot = new AnalysisReportBuilder().Capture(run, _fixState.Id, Application.productName,
                Application.unityVersion, scope, DateTimeOffset.UtcNow);
            _setBaseline(AnalysisReportJson.WriteSnapshot(snapshot));
        }

        private void SaveReport(ReportPreviewRow report)
        {
            try
            {
                string fileName = "ProjectAnalysis-" + report.Created.ToString("yyyyMMdd-HHmmss") + "-" + report.Id + ".json";
                string path = _selectPath("Save Report", "", fileName, "json");
                if (string.IsNullOrEmpty(path)) return;
                _writer.Save(path, report.Json);
                _view.ShowMessage("Report saved: " + path);
            }
            catch (Exception error) { _view.ShowMessage("Could not save report: " + error.Message); }
        }

        private void DeleteReport(ReportPreviewRow report)
        {
            if (!_confirm("Delete Report", "Delete this report from the list?\n\n" + report.Title + "\n" + report.Id +
                "\n\nJSON files already saved on disk will be kept. This cannot be undone.", "Delete Report")) return;
            int index = _reportJsons.FindIndex(json =>
            {
                try { return AnalysisReportJson.Read(json).Id == report.Id; }
                catch { return false; }
            });
            if (index < 0) { _view.ShowMessage("The selected report is no longer available."); return; }
            _reportJsons.RemoveAt(index);
            _view.SetRows(ReadReportRows());
            _view.ShowMessage("Report deleted from the list.");
        }

        private List<ReportPreviewRow> ReadReportRows()
        {
            var rows = new List<ReportPreviewRow>();
            foreach (string json in _reportJsons)
            {
                try { rows.Add(ReportRowMapper.Create(json)); }
                catch (Exception error)
                {
                    // Bojāto momentuzņēmumu saglabā, bet nepārvērš par tukšu pārskatu.
                    _root.Q("reports-view").Insert(0, new HelpBox("Could not display a report: " + error.Message, HelpBoxMessageType.Warning));
                }
            }
            return rows;
        }

        public void CreateReport()
        {
            try
            {
                if (string.IsNullOrEmpty(_getBaseline()))
                    throw new InvalidOperationException("Run analysis before creating a report.");
                var snapshot = AnalysisReportJson.ReadSnapshot(_getBaseline());
                if (snapshot.SessionId != _fixState.Id)
                    throw new InvalidOperationException("The analysis session changed. Run analysis again.");
                var report = new AnalysisReportBuilder().Create(snapshot, _fixState.LoadAll(), DateTimeOffset.UtcNow);
                string json = AnalysisReportJson.Write(report);
                var row = ReportRowMapper.Create(json);
                _reportJsons.Add(json);
                _view.SetRows(ReadReportRows());
                _view.SelectReport(row.Id);
                _root.Q<TabView>("main-tabs").activeTab = _root.Q<Tab>("reports-tab");
            }
            catch (Exception error)
            {
                _analysis.ShowFixMessage("Could not create report: " + error.Message);
            }
        }
    }
}
