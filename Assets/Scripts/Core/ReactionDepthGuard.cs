using UnityEngine;

namespace CheckmateRPG.Core
{
    public sealed class ReactionDepthGuard
    {
        public int MaxReactionDepth { get; }

        public ReactionDepthGuard(int maxReactionDepth)
        {
            MaxReactionDepth = Mathf.Max(1, maxReactionDepth);
        }

        public bool IsDepthAllowed(int reactionDepth, string triggerName)
        {
            if (reactionDepth <= MaxReactionDepth)
                return true;

            Debug.LogWarning(
                $"[ReactionSystem] Reaction skipped for {triggerName}: ReactionDepth({reactionDepth}) exceeds {MaxReactionDepth}.");
            return false;
        }
    }
}
