using System;

namespace CheckmateRPG.Core.Determinism
{
    /// <summary>
    /// Documents the collection-access rules that must be followed throughout the simulation
    /// to guarantee deterministic ordering across all runs.
    /// <para>
    /// FORBIDDEN patterns — any violation may silently introduce ordering drift:
    /// <list type="bullet">
    ///   <item>
    ///     <term>Unordered Dictionary iteration</term>
    ///     <description>
    ///       Never iterate <c>Dictionary&lt;,&gt;</c> or <c>HashSet&lt;&gt;</c> directly.
    ///       Enumerate only after sorting through
    ///       <see cref="DeterministicOrderUtility"/> or a <see cref="StableSortPolicy{T}"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>HashSet iteration</term>
    ///     <description>
    ///       <c>HashSet&lt;&gt;</c> iteration order is undefined. Convert to a sorted array
    ///       before processing.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Unity FindObjects ordering</term>
    ///     <description>
    ///       <c>Object.FindObjects*</c> / <c>FindObjectsOfType</c> return scene objects in
    ///       scene-hierarchy order, which is not stable across reloads or platforms. Sort the
    ///       result by a stable key (e.g. unit StableId) before use.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>LINQ unordered query</term>
    ///     <description>
    ///       LINQ operators such as <c>Where</c>, <c>Select</c>, and <c>GroupBy</c> preserve
    ///       input order for sequence sources (<c>IList</c>) but are undefined for hash-backed
    ///       sources. Always call <c>OrderBy</c> / <c>ThenBy</c> before consuming any LINQ
    ///       result that derives from a <c>Dictionary</c> or <c>HashSet</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Frame-timing dependency</term>
    ///     <description>
    ///       Never use <c>Time.deltaTime</c>, <c>Time.time</c>, <c>Time.frameCount</c>, or
    ///       frame-rate-relative values to drive simulation logic. Use discrete tick counts
    ///       only (<see cref="CheckmateRPG.Core.ActionTimelineFormula.TickMilliseconds"/>).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Runtime randomness</term>
    ///     <description>
    ///       <c>UnityEngine.Random</c> and <c>System.Random</c> are forbidden in simulation
    ///       code. Use <see cref="CheckmateRPG.Core.SeededRandomProvider"/> exclusively.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    /// <remarks>
    /// This class has no runtime functionality. Its sole purpose is to serve as the canonical
    /// reference for determinism rules; use <see cref="DeterministicAuditLog"/> to record
    /// violations detected at runtime.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct,
        AllowMultiple = false, Inherited = false)]
    public sealed class DeterministicCollectionRuleAttribute : Attribute
    {
        /// <summary>
        /// Marks the annotated member as a site that has been reviewed for deterministic
        /// collection access and confirmed to be compliant.
        /// </summary>
        public DeterministicCollectionRuleAttribute() { }
    }

    /// <summary>
    /// Categorises the kind of non-determinism that a given violation represents.
    /// Used by <see cref="DeterministicAuditLog"/> when recording entries.
    /// </summary>
    public enum DeterminismViolationKind
    {
        /// <summary>Iteration over an unordered <c>Dictionary</c> or similar hash collection.</summary>
        UnorderedDictionaryIteration,

        /// <summary>Iteration over a <c>HashSet</c> without prior sorting.</summary>
        UnorderedHashSetIteration,

        /// <summary>Use of <c>Object.FindObjects*</c> without subsequent stable sort.</summary>
        UnityFindObjectsOrdering,

        /// <summary>LINQ query applied to a hash-backed source without an <c>OrderBy</c>.</summary>
        LinqUnorderedQuery,

        /// <summary>Logic that depends on <c>Time.deltaTime</c> or frame timing.</summary>
        FrameTimingDependency,

        /// <summary>Use of <c>UnityEngine.Random</c> or <c>System.Random</c>.</summary>
        RuntimeRandomness,

        /// <summary>Any other ordering drift that does not fit the above categories.</summary>
        UnstableOrdering
    }
}
