namespace CheckmateRPG.Core.Actions.Resolvers
{
    /// <summary>
    /// Immutable resolve outcome describing whether action resolution succeeded.
    /// </summary>
    /// <param name="Succeeded">True when resolution produced a valid outcome.</param>
    public readonly record struct ActionResolutionResult(bool Succeeded)
    {
        public static ActionResolutionResult Success => new(true);
        public static ActionResolutionResult Failure => new(false);
    }
}
