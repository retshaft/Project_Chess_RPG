using CheckmateRPG.Core.Actions;

namespace CheckmateRPG.Units
{
    public readonly struct ActionBid
    {
        public ActionBid(UnitBrain executor, IActionCommand command, float requiredAP, float score)
        {
            Executor = executor;
            Command = command;
            RequiredAP = requiredAP;
            Score = score;
        }

        public UnitBrain Executor { get; }
        public IActionCommand Command { get; }
        public float RequiredAP { get; }
        public float Score { get; }

        public bool IsValid => Executor != null && Command != null && RequiredAP > 0f;
    }
}
