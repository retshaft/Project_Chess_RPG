using System;

namespace CheckmateRPG.Core.Replay
{
    /// <summary>
    /// Journal replay dedicated resolve flow.
    /// Replays an <see cref="ActionJournal"/> deterministically via an isolated executor,
    /// and produces a <see cref="MutationJournal"/> and an optional final <see cref="RuntimeSnapshot"/>.
    /// <para>
    /// Restrictions enforced by this pipeline:
    /// <list type="bullet">
    ///   <item>No live-runtime overwrite: the executor delegate must target an isolated clone, not the live runtime.</item>
    ///   <item>No prediction leakage: prediction calls are prohibited while a replay is active.</item>
    ///   <item>No frame-dependent state: all inputs are tick-based; Unity time APIs must not be used.</item>
    ///   <item>No Unity execution-order dependency: this is a pure C# class with no MonoBehaviour coupling.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class ReplaySimulationPipeline
    {
        private readonly ActionJournal _actionJournal;
        private bool _isReplaying;

        public ReplaySimulationPipeline(ActionJournal actionJournal)
        {
            _actionJournal = actionJournal ?? throw new ArgumentNullException(nameof(actionJournal));
        }

        /// <summary>
        /// <c>true</c> while a replay is in progress.
        /// Use this flag to guard against prediction calls or live-runtime access during replay.
        /// </summary>
        public bool IsReplaying => _isReplaying;

        /// <summary>
        /// Executes the journal replay using the provided <paramref name="executor"/>.
        /// </summary>
        /// <param name="executor">
        /// A delegate that processes each <see cref="ActionJournalEntry"/> and returns the mutations
        /// produced by that entry.  The delegate must operate on an isolated runtime clone and must
        /// never write to the live simulation runtime.
        /// </param>
        /// <param name="initialSnapshot">
        /// Optional snapshot of the runtime state at the beginning of the replay window.
        /// Stored in <see cref="ReplaySimulationResult.InitialSnapshot"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ReplaySimulationResult"/> containing the replayed <see cref="MutationJournal"/>
        /// and the optional initial snapshot.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a replay is already in progress on this instance.
        /// </exception>
        public ReplaySimulationResult Execute(
            ReplayEntryExecutor executor,
            RuntimeSnapshot initialSnapshot = null)
        {
            if (executor == null)
                throw new ArgumentNullException(nameof(executor));

            if (_isReplaying)
                throw new InvalidOperationException(
                    "Replay is already in progress. Nested or concurrent replays are not allowed.");

            _isReplaying = true;
            try
            {
                var replayRuntime = new ReplayRuntime(_actionJournal);
                MutationJournal replayMutationJournal = replayRuntime.Execute(executor);
                return new ReplaySimulationResult(replayMutationJournal, initialSnapshot);
            }
            finally
            {
                _isReplaying = false;
            }
        }
    }

    /// <summary>
    /// Result produced by <see cref="ReplaySimulationPipeline.Execute"/>.
    /// </summary>
    public sealed class ReplaySimulationResult
    {
        public ReplaySimulationResult(MutationJournal replayedMutations, RuntimeSnapshot initialSnapshot)
        {
            ReplayedMutations = replayedMutations ?? throw new ArgumentNullException(nameof(replayedMutations));
            InitialSnapshot = initialSnapshot;
        }

        /// <summary>The mutations produced during replay, in journal order.</summary>
        public MutationJournal ReplayedMutations { get; }

        /// <summary>
        /// Optional snapshot of the runtime state at the start of the replay window.
        /// <c>null</c> when no initial snapshot was provided to <see cref="ReplaySimulationPipeline.Execute"/>.
        /// </summary>
        public RuntimeSnapshot InitialSnapshot { get; }
    }
}
