using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Determinism
{
    /// <summary>
    /// An immutable record of a single determinism violation captured at runtime.
    /// </summary>
    public sealed class DeterministicAuditEntry
    {
        /// <param name="kind">Category of the violation.</param>
        /// <param name="tick">Simulation tick at which the violation was detected.</param>
        /// <param name="site">
        ///   Human-readable call-site description, e.g. the class and method name.
        ///   May be <c>null</c> if unavailable.
        /// </param>
        /// <param name="detail">
        ///   Optional additional context (e.g. the offending collection type or Random API call).
        /// </param>
        public DeterministicAuditEntry(
            DeterminismViolationKind kind,
            int tick,
            string site,
            string detail = null)
        {
            Kind = kind;
            Tick = tick;
            Site = site;
            Detail = detail;
            RecordedAtUtc = DateTime.UtcNow;
        }

        /// <summary>Category of the violation.</summary>
        public DeterminismViolationKind Kind { get; }

        /// <summary>Simulation tick at which the violation was observed.</summary>
        public int Tick { get; }

        /// <summary>Human-readable description of the call site, if available.</summary>
        public string Site { get; }

        /// <summary>Optional supplementary detail about the violation.</summary>
        public string Detail { get; }

        /// <summary>Wall-clock UTC timestamp when the entry was created.</summary>
        public DateTime RecordedAtUtc { get; }

        /// <inheritdoc/>
        public override string ToString() =>
            $"[{Kind}] tick={Tick} site={Site ?? "<unknown>"}" +
            (Detail != null ? $" detail={Detail}" : string.Empty);
    }

    /// <summary>
    /// Thread-safe audit log that records determinism violations detected at runtime.
    /// <para>
    /// Simulation systems call one of the <c>Record*</c> helpers to register a violation.
    /// <see cref="RandomUsageValidator"/> reads this log during tick validation and elevates
    /// any <see cref="DeterminismViolationKind.RuntimeRandomness"/> entries to
    /// <see cref="CheckmateRPG.Core.Simulation.Validation.ValidationSeverity.Critical"/>.
    /// </para>
    /// <para>
    /// Call <see cref="Clear"/> at the start of each tick to prevent unbounded growth and to
    /// ensure that each tick's validation only sees violations from that tick.
    /// </para>
    /// </summary>
    public sealed class DeterministicAuditLog
    {
        private readonly object _lock = new object();
        private readonly List<DeterministicAuditEntry> _entries = new List<DeterministicAuditEntry>();

        // -----------------------------------------------------------------------
        // Recording helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Records an unordered <c>Dictionary</c> or <c>HashSet</c> iteration violation.
        /// </summary>
        /// <param name="tick">Current simulation tick.</param>
        /// <param name="site">Name of the calling class/method.</param>
        /// <param name="collectionType">Optional collection type description.</param>
        public void RecordUnorderedIteration(int tick, string site, string collectionType = null)
        {
            DeterminismViolationKind kind = collectionType != null &&
                                            collectionType.IndexOf("HashSet", StringComparison.OrdinalIgnoreCase) >= 0
                ? DeterminismViolationKind.UnorderedHashSetIteration
                : DeterminismViolationKind.UnorderedDictionaryIteration;

            Add(new DeterministicAuditEntry(kind, tick, site, collectionType));
        }

        /// <summary>
        /// Records an unstable ordering violation (e.g. Unity FindObjects used without sort,
        /// LINQ applied to a hash-backed source without OrderBy).
        /// </summary>
        /// <param name="tick">Current simulation tick.</param>
        /// <param name="site">Name of the calling class/method.</param>
        /// <param name="kind">Specific kind; defaults to <see cref="DeterminismViolationKind.UnstableOrdering"/>.</param>
        /// <param name="detail">Optional additional context.</param>
        public void RecordUnstableOrdering(
            int tick,
            string site,
            DeterminismViolationKind kind = DeterminismViolationKind.UnstableOrdering,
            string detail = null)
        {
            Add(new DeterministicAuditEntry(kind, tick, site, detail));
        }

        /// <summary>
        /// Records a runtime-randomness violation: <c>UnityEngine.Random</c> or
        /// <c>System.Random</c> was used instead of <see cref="SeededRandomProvider"/>.
        /// </summary>
        /// <param name="tick">Current simulation tick.</param>
        /// <param name="site">Name of the calling class/method.</param>
        /// <param name="apiUsed">
        ///   Textual description of the forbidden API that was called
        ///   (e.g. "UnityEngine.Random.Range" or "System.Random.Next").
        /// </param>
        public void RecordRuntimeRandomness(int tick, string site, string apiUsed = null)
        {
            Add(new DeterministicAuditEntry(
                DeterminismViolationKind.RuntimeRandomness,
                tick,
                site,
                apiUsed));
        }

        /// <summary>
        /// Records a frame-timing dependency violation (use of <c>Time.deltaTime</c> etc.).
        /// </summary>
        /// <param name="tick">Current simulation tick.</param>
        /// <param name="site">Name of the calling class/method.</param>
        /// <param name="detail">Optional description of the offending API.</param>
        public void RecordFrameTimingDependency(int tick, string site, string detail = null)
        {
            Add(new DeterministicAuditEntry(
                DeterminismViolationKind.FrameTimingDependency,
                tick,
                site,
                detail));
        }

        /// <summary>
        /// Appends an arbitrary pre-constructed entry.
        /// </summary>
        public void Record(DeterministicAuditEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            Add(entry);
        }

        // -----------------------------------------------------------------------
        // Queries
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns a snapshot of all entries currently in the log.
        /// The returned list is a copy; mutations to it do not affect the log.
        /// </summary>
        public IReadOnlyList<DeterministicAuditEntry> GetEntries()
        {
            lock (_lock)
            {
                return _entries.Count == 0
                    ? Array.Empty<DeterministicAuditEntry>()
                    : _entries.ToArray();
            }
        }

        /// <summary>
        /// Returns all entries whose <see cref="DeterministicAuditEntry.Kind"/> matches
        /// <paramref name="kind"/>.
        /// </summary>
        public IReadOnlyList<DeterministicAuditEntry> GetEntriesByKind(DeterminismViolationKind kind)
        {
            lock (_lock)
            {
                List<DeterministicAuditEntry> result = null;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Kind == kind)
                    {
                        result ??= new List<DeterministicAuditEntry>();
                        result.Add(_entries[i]);
                    }
                }

                return result ?? (IReadOnlyList<DeterministicAuditEntry>)Array.Empty<DeterministicAuditEntry>();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if any entry of the given <paramref name="kind"/> exists.
        /// </summary>
        public bool HasViolation(DeterminismViolationKind kind)
        {
            lock (_lock)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Kind == kind)
                        return true;
                }

                return false;
            }
        }

        /// <summary>Returns the total number of entries in the log.</summary>
        public int Count
        {
            get
            {
                lock (_lock)
                    return _entries.Count;
            }
        }

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        /// <summary>
        /// Removes all entries from the log.
        /// Call at the start of each tick so that <see cref="RandomUsageValidator"/>
        /// only surfaces violations from the current tick.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
                _entries.Clear();
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private void Add(DeterministicAuditEntry entry)
        {
            lock (_lock)
                _entries.Add(entry);
        }
    }
}
