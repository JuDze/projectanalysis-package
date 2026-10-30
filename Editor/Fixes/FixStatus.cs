using System;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Operācijas, resursa un pārbaudes stāvokļi ir atšķirīgi līgumi.
    internal enum FixOperationStatus
    {
        Invalid = 0,
        InProgress = 1,
        Completed = 2,
        PartiallyCompleted = 3,
        Failed = 4,
        Interrupted = 5,
    }

    internal enum FixAssetStatus
    {
        Invalid = 0,
        NotStarted = 1,
        Applying = 2,
        NotApplied = 3,
        Completed = 4,
        RestoredAfterFailure = 5,
        RecoveryRequired = 6,
        UnknownAfterInterruption = 7,
        Failed = 8,
    }

    internal enum FixRecheckState
    {
        Invalid = 0,
        NotRun = 1,
        Incomplete = 2,
        ResultsAvailable = 3,
        Failed = 4,
    }

    // Tikai šeit nosaka stāvokļu tekstu; saglabātais JSON un saskarnes uzraksti nemainās.
    internal static class FixStatusText
    {
        private static readonly string[] OperationNames = { null, "In progress", "Completed", "Partially completed", "Failed", "Interrupted" };
        public static string ToText(FixOperationStatus value) => Name(OperationNames, (int)value);
        public static FixOperationStatus ParseOperation(string value) => (FixOperationStatus)Math.Max(0, Array.IndexOf(OperationNames, value));

        private static readonly string[] AssetNames = { null, "Not started", "Applying", "Not applied", "Completed", "Restored after failure", "Recovery required", "Unknown after interruption", "Failed" };
        public static string ToText(FixAssetStatus value) => Name(AssetNames, (int)value);
        public static FixAssetStatus ParseAsset(string value) => (FixAssetStatus)Math.Max(0, Array.IndexOf(AssetNames, value));

        private static readonly string[] RecheckNames = { null, "Not run", "Incomplete", "Results available", "Failed" };
        public static string ToText(FixRecheckState value) => Name(RecheckNames, (int)value);
        public static FixRecheckState ParseRecheck(string value) => (FixRecheckState)Math.Max(0, Array.IndexOf(RecheckNames, value));

        public static string ToReportText(FixAssetStatus value) =>
            value == FixAssetStatus.Completed ? ToText(FixAssetStatus.Completed) :
            value == FixAssetStatus.NotApplied || value == FixAssetStatus.NotStarted
                ? ToText(FixAssetStatus.NotApplied) : ToText(FixAssetStatus.Failed);

        private static string Name(string[] names, int value)
        {
            if (value < 0 || value >= names.Length) throw new ArgumentOutOfRangeException(nameof(value));
            return names[value];
        }
    }
}
