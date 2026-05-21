using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Simulation.Validation;

namespace CheckmateRPG.Core.Determinism
{
    /// <summary>
    /// Validates that no runtime randomness violations have been recorded in the
    /// <see cref="DeterministicAuditLog"/> since the last clear.
    /// <para>
    /// Rules enforced:
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       Any <see cref="DeterminismViolationKind.RuntimeRandomness"/> entry in the audit log
    ///       results in a <see cref="ValidationSeverity.Critical"/> issue, because runtime random
    ///       usage breaks replay fidelity.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Other violation kinds (unordered iteration, frame-timing, etc.) result in a
    ///       <see cref="ValidationSeverity.Warning"/> issue so they surface without blocking
    ///       the simulation.
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// Usage: register this validator with <see cref="CheckmateRPG.Core.Simulation.Validation.RuntimeValidationSystem"/>
    /// and call <c>Validate</c> at the end of each tick.
    /// </summary>
    public sealed class RandomUsageValidator : ISimulationValidator
    {
        private readonly DeterministicAuditLog _auditLog;

        /// <param name="auditLog">
        ///   The shared audit log that simulation code writes violations into.
        ///   Must not be <c>null</c>.
        /// </param>
        public RandomUsageValidator(DeterministicAuditLog auditLog)
        {
            _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        }

        /// <inheritdoc/>
        public ValidationResult Validate(IReadOnlySimulationRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));

            IReadOnlyList<DeterministicAuditEntry> entries = _auditLog.GetEntries();
            if (entries.Count == 0)
                return ValidationResult.Valid();

            List<ValidationIssue> issues = new List<ValidationIssue>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                DeterministicAuditEntry entry = entries[i];

                ValidationSeverity severity = entry.Kind == DeterminismViolationKind.RuntimeRandomness
                    ? ValidationSeverity.Critical
                    : ValidationSeverity.Warning;

                issues.Add(new ValidationIssue(
                    severity,
                    BuildMessage(entry),
                    UnitId: null,
                    runtime.CurrentTick));
            }

            return new ValidationResult(isValid: false, issues);
        }

        private static string BuildMessage(DeterministicAuditEntry entry)
        {
            return entry.Kind == DeterminismViolationKind.RuntimeRandomness
                ? $"[Determinism] FORBIDDEN runtime randomness detected at tick {entry.Tick}. " +
                  $"Site: {entry.Site ?? "<unknown>"}. " +
                  "Use SeededRandomProvider exclusively."
                : $"[Determinism] {entry.Kind} violation at tick {entry.Tick}. " +
                  $"Site: {entry.Site ?? "<unknown>"}. " +
                  $"Detail: {entry.Detail ?? string.Empty}";
        }
    }
}
