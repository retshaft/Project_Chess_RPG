namespace CheckmateRPG.Core.Actions.Resolvers
{
    public readonly record struct ActionResolutionResult(bool Succeeded)
    {
        public static ActionResolutionResult Success => new(true);
        public static ActionResolutionResult Failure => new(false);
    }
}
