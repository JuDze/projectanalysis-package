using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Labojumu operāciju dati nesatur dzīvas Unity objektu atsauces vai izpildāmas komandas.
    [Serializable]
    internal sealed class FixOperationRecord
    {
        public int Version = 1;
        public string SessionId;
        public string Action = "Apply fixes";
        public string Id;
        public string PlanId;
        public string StartedUtc;
        public string FinishedUtc;
        // Status ir privāts serializācijas lauks; izpildes loģika izmanto tipizētu State.
        [SerializeField] private string Status;
        public FixOperationStatus State
        {
            get => FixStatusText.ParseOperation(Status);
            set => Status = FixStatusText.ToText(value);
        }
        public string Message;
        public string Target;
        public string SettingsJson;
        public List<string> ScopePaths = new List<string>();
        public List<FixAssetRecord> Assets = new List<FixAssetRecord>();
        public FixRecheckRecord Recheck = new FixRecheckRecord();
    }

    [Serializable]
    internal sealed class FixAssetRecord
    {
        public string Guid;
        public string Path;
        public List<string> RuleIds = new List<string>();
        public string Category;
        public List<string> RevertsOperationIds = new List<string>();
        public AudioImporterSnapshot AudioBefore;
        public AudioImporterSnapshot AudioAfter;
        public List<string> AudioChecks = new List<string>();
        public ModelImporterSnapshot ModelBefore;
        public ModelImporterSnapshot ModelAfter;
        public List<string> ModelChecks = new List<string>();
        public TextureImporterSnapshot Before;
        public TextureImporterSnapshot After;
        // Status ir privāts serializācijas lauks; izpildes loģika izmanto tipizētu State.
        [SerializeField] private string Status = FixStatusText.ToText(FixAssetStatus.NotStarted);
        public FixAssetStatus State
        {
            get => FixStatusText.ParseAsset(Status);
            set => Status = FixStatusText.ToText(value);
        }
        public string Message;
        public List<FixValueChange> Changes = new List<FixValueChange>();
    }

    [Serializable]
    internal sealed class FixValueChange
    {
        public string Property;
        public string Before;
        public string Intended;
        public string Actual;
    }

    [Serializable]
    internal sealed class FixRecheckRecord
    {
        // Status ir privāts serializācijas lauks; izpildes loģika izmanto tipizētu State.
        [SerializeField] private string Status = FixStatusText.ToText(FixRecheckState.NotRun);
        public FixRecheckState State
        {
            get => FixStatusText.ParseRecheck(Status);
            set => Status = FixStatusText.ToText(value);
        }
        public string TimeUtc;
        public string Message;
        public bool HasCounts;
        public int BeforeCount, AfterCount, Resolved, Remaining, New;
        public List<string> BeforeKeys = new List<string>();
        public List<string> AfterKeys = new List<string>();
        public List<string> Diagnostics = new List<string>();
    }

    // Rezultātu pieejamība nenozīmē, ka izdevies salīdzināt visu analīzes tvērumu.
    internal static class FixRecheckStatus
    {
        public static FixRecheckState ForOperation(FixOperationRecord operation)
        {
            var recheck = operation.Recheck;
            // Pabeigts labojums un notikusi pārbaude dod arī daļējus rezultātus.
            // Neveikta vai ar kļūdu pārtraukta pārbaude saglabā savu statusu.
            if (recheck.State == FixRecheckState.Incomplete && !string.IsNullOrEmpty(recheck.TimeUtc) &&
                operation.Assets.Any(asset => asset.State == FixAssetStatus.Completed))
                return FixRecheckState.ResultsAvailable;

            return recheck.State;
        }
    }
}
