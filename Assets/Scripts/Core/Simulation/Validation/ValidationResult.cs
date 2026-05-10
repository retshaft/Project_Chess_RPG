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
            HighestSeverity = ResolveHighestSeverity(Issues);
            CriticalCount = CountIssues(Issues, ValidationSeverity.Critical);
        }

        public bool IsValid { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public ValidationSeverity HighestSeverity { get; }
        public int CriticalCount { get; }
        public bool HasCriticalIssues => CriticalCount > 0;

        public static ValidationResult Valid() => new(true, Array.Empty<ValidationIssue>());

        public static ValidationResult Merge(ValidationResult left, ValidationResult right)
        {
            if (left == null)
                return right ?? Valid();
            if (right == null)
                return left;
            if (left.Issues.Count == 0 && right.Issues.Count == 0)
                return new ValidationResult(left.IsValid && right.IsValid, Array.Empty<ValidationIssue>());

            var issues = new ValidationIssue[left.Issues.Count + right.Issues.Count];
            for (int i = 0; i < left.Issues.Count; i++)
                issues[i] = left.Issues[i];
            for (int i = 0; i < right.Issues.Count; i++)
                issues[left.Issues.Count + i] = right.Issues[i];

            return new ValidationResult(left.IsValid && right.IsValid, issues);
        }

        private static ValidationSeverity ResolveHighestSeverity(IReadOnlyList<ValidationIssue> issues)
        {
            ValidationSeverity highest = ValidationSeverity.Info;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity > highest)
                    highest = issues[i].Severity;
            }

            return highest;
        }

        private static int CountIssues(IReadOnlyList<ValidationIssue> issues, ValidationSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity)
                    count++;
            }

            return count;
        }
    }
}
