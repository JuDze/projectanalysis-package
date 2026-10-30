using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    // Vienā datnē glabā pašreizējās analīzes stāvokli, nevis visu analīžu darbību arhīvu.
    internal sealed class JsonFixSessionStore : IFixSessionStore
    {
        private readonly string _path;
        [Serializable] private sealed class Envelope { public string Payload; public string Hash; }
        [Serializable] private sealed class State
        {
            public int Version = 1;
            public string SessionId;
            public List<FixOperationRecord> Operations = new List<FixOperationRecord>();
        }
        public JsonFixSessionStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A fix state directory is required.", nameof(directory));
            _path = Path.Combine(Path.GetFullPath(directory), "Session.json");
        }
        public string Id => Read().SessionId;
        public void Start()
        {
            string backup = null;
            try { Read(); }
            catch
            {
                // Jauna analīze drīkst sākt tukšu stāvokli; bojāto datni saglabā atsevišķi.
                if (File.Exists(_path)) backup = _path + "." + Guid.NewGuid().ToString("N") + ".bak";
            }
            Write(new State { SessionId = Guid.NewGuid().ToString("N") }, backup);
        }
        public void Save(FixOperationRecord operation)
        {
            Validate(operation);
            var state = Read();
            if (string.IsNullOrEmpty(state.SessionId) && state.Operations.Count == 0) state.SessionId = operation.SessionId;
            // JsonUtility tukšu virkni un null serializē vienādi.
            if ((state.SessionId ?? "") != (operation.SessionId ?? ""))
                throw new InvalidOperationException("The fix session changed. Run analysis again before applying fixes.");
            int index = state.Operations.FindIndex(record => record.Id == operation.Id);
            if (index < 0) state.Operations.Add(operation);
            else state.Operations[index] = operation;
            // Saglabā vienu aktuālo kopiju katrai operācijai; starpposma versijas nekrājas.
            Write(state);
        }
        public IReadOnlyList<FixOperationRecord> LoadAll()
        {
            var state = Read();
            foreach (var record in state.Operations)
                if (record.State == FixOperationStatus.InProgress)
                {
                    // Pārtraukts mēģinājums nav pabeigts labojums un netiek automātiski atsākts.
                    record.State = FixOperationStatus.Interrupted;
                    record.Message = "Operation was interrupted. Inspect the asset before further changes.";
                    foreach (var asset in record.Assets.Where(a => a.State == FixAssetStatus.Applying))
                        asset.State = FixAssetStatus.UnknownAfterInterruption;
                }
            return state.Operations.OrderByDescending(record => record.StartedUtc, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }
        private State Read()
        {
            string json;
            try { json = File.ReadAllText(_path, Encoding.UTF8); }
            catch (FileNotFoundException) { return new State(); }
            catch (DirectoryNotFoundException) { return new State(); }
            var envelope = JsonUtility.FromJson<Envelope>(json);
            if (envelope == null || string.IsNullOrEmpty(envelope.Payload) ||
                Hash128.Compute(envelope.Payload).ToString() != envelope.Hash)
                throw new InvalidDataException("Fix state is damaged. Start a new analysis; the original file will be preserved.");
            var state = JsonUtility.FromJson<State>(envelope.Payload);
            if (state == null || state.Version != 1 || state.Operations == null ||
                state.Operations.Any(record => record == null || record.SessionId != state.SessionId) ||
                state.Operations.Select(record => record.Id).Distinct(StringComparer.Ordinal).Count() != state.Operations.Count)
                throw new InvalidDataException("Invalid fix session state. Start a new analysis.");
            foreach (var record in state.Operations) Validate(record);
            return state;
        }
        private void Write(State state, string backup = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string payload = JsonUtility.ToJson(state);
            var envelope = new Envelope { Payload = payload, Hash = Hash128.Compute(payload).ToString() };
            string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope, true));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                // Iepriekšējais stāvoklis paliek spēkā, līdz pilnīgi ierakstīta jaunā datne.
                if (File.Exists(_path)) File.Replace(temporary, _path, backup);
                else File.Move(temporary, _path);
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        private static void Validate(FixOperationRecord record)
        {
            if (record == null || (record.Version != 1 && record.Version != 2) || !Guid.TryParseExact(record.Id, "N", out _) ||
                string.IsNullOrEmpty(record.PlanId) || string.IsNullOrEmpty(record.StartedUtc) ||
                record.State == FixOperationStatus.Invalid || record.Assets == null || record.Assets.Count == 0 ||
                record.Assets.Any(a => a == null || a.State == FixAssetStatus.Invalid || (a.Category == "Models" ? a.ModelBefore == null : a.Category == "Audio" ? a.AudioBefore == null || a.AudioBefore.Default == null || a.AudioBefore.Effective == null : a.Before == null) || string.IsNullOrEmpty(a.Guid) || a.Changes == null ||
                    a.Changes.Any(change => change == null || string.IsNullOrWhiteSpace(change.Property)) ||
                    a.RuleIds == null || a.RevertsOperationIds == null) ||
                record.Assets.Select(a => a.Guid).Distinct(StringComparer.Ordinal).Count() != record.Assets.Count ||
                record.Recheck == null || record.Recheck.State == FixRecheckState.Invalid || record.Recheck.BeforeKeys == null || record.Recheck.AfterKeys == null || record.Recheck.Diagnostics == null)
                throw new InvalidDataException("Invalid fix operation. The existing state was preserved.");
        }
    }
}
