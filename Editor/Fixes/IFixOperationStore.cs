using System.Collections.Generic;

namespace Judze.ProjectAnalysis.Editor.Fixes
{
    internal interface IFixOperationStore
    {
        void Save(FixOperationRecord operation);
        IReadOnlyList<FixOperationRecord> LoadAll();
    }

    // Jauna analīze nosaka atcelšanas robežu; FixManager izmanto tikai operāciju glabāšanas līgumu.
    internal interface IFixSessionStore : IFixOperationStore
    {
        string Id { get; }
        void Start();
    }
}
