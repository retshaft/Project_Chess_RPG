using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public sealed class ValidationResult
    {
        public ValidationResult(bool isValid, IReadOnlyList<ValidationIssue> issues)
        {
            IsValid = isValid;
            Issues = issues ?? Array.Empty<ValidationIssue>();
        }

        public bool IsValid { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }

        public static ValidationResult Valid() => new(true, Array.Empty<ValidationIssue>());
    }
}
