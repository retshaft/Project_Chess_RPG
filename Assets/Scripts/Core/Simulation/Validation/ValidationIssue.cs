using System;

namespace CheckmateRPG.Core.Simulation.Validation
{
    public readonly record struct ValidationIssue(
        ValidationSeverity Severity,
        string Message,
        Guid? UnitId,
        int Tick);
}
