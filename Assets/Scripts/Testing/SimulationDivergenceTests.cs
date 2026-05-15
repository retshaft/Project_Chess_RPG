using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Replay;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    [TestFixture]
    public sealed class SimulationDivergenceTests
    {
        [Test]
        public void OriginalAndReplayRuntime_ShouldMatch_WhenReplayingSameActionJournal()
        {
            SimulationRuntime originalRuntime = new SimulationRuntime(currentTick: 0);
            SimulationRuntime replayRuntime = new SimulationRuntime(currentTick: 0);

            ActionJournal sourceJournal = BuildChainedActionJournal();
            ActionJournal replayJournal = CloneActionJournal(sourceJournal);

            ReplayRecorder originalReplayRecorder = new ReplayRecorder();
            MutationJournal originalMutationJournal = ExecuteOriginalAndExtractMutationJournal(
                originalRuntime,
                sourceJournal,
                originalReplayRecorder);

            MutationJournal replayMutationJournal = ExecuteReplayRuntime(replayRuntime, replayJournal);

            RuntimeSnapshot originalSnapshot = CaptureRuntimeSnapshot(originalRuntime);
            RuntimeSnapshot replaySnapshot = CaptureRuntimeSnapshot(replayRuntime);

            var verifier = new ReplayVerification();
            ReplayVerificationResult verificationResult = verifier.Verify(
                sourceJournal,
                originalMutationJournal,
                replayJournal,
                replayMutationJournal,
                originalSnapshot,
                replaySnapshot);

            if (!verificationResult.IsMatch)
            {
                var detector = new DivergenceDetector(verifier);
                IReadOnlyList<DivergenceEvent> divergences = detector.Detect(verificationResult);
                for (int i = 0; i < divergences.Count; i++)
                {
                    DivergenceEvent divergence = divergences[i];
                    Debug.LogError(
                        $"[SimulationDivergenceTests] Tick={divergence.Tick}, Reason={divergence.Reason}, Kind={divergence.Kind}, Message={divergence.Message}");
                }

                Assert.Fail(
                    $"Replay verification diverged at Tick={verificationResult.DivergenceTick}, Reason={verificationResult.DivergenceReason}.");
            }
        }

        private static ActionJournal BuildChainedActionJournal()
        {
            var journal = new ActionJournal();
            Guid actorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

            Guid action1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
            Guid action2 = Guid.Parse("10000000-0000-0000-0000-000000000002");
            Guid action3 = Guid.Parse("10000000-0000-0000-0000-000000000003");

            journal.RecordTick(1);
            journal.RecordActionRequest(1, action1, actorId, "Action=ChainStart;Stage=1", scheduledTick: 1);
            journal.RecordActionResult(1, action1, actorId, "Result=Applied;Stage=1");

            journal.RecordTick(2);
            journal.RecordActionRequest(2, action2, actorId, "Action=ChainLink;Stage=2", scheduledTick: 2);
            journal.RecordActionResult(2, action2, actorId, "Result=Applied;Stage=2");

            journal.RecordTick(3);
            journal.RecordActionRequest(3, action3, actorId, "Action=ChainBurst;Stage=3", scheduledTick: 3);
            journal.RecordActionResult(3, action3, actorId, "Result=Applied;Stage=3");

            return journal;
        }

        private static ActionJournal CloneActionJournal(ActionJournal source)
        {
            var cloned = new ActionJournal();
            IReadOnlyList<ActionJournalEntry> entries = source.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                ActionJournalEntry entry = entries[i];
                switch (entry.EntryType)
                {
                    case ActionJournalEntryType.TickAdvance:
                        cloned.RecordTick(entry.Tick);
                        break;
                    case ActionJournalEntryType.ActionRequest:
                        cloned.RecordActionRequest(entry.Tick, entry.ActionId, entry.ActorId, entry.Details, entry.ScheduledTick);
                        break;
                    case ActionJournalEntryType.Reservation:
                        cloned.RecordReservation(entry.Tick, entry.ActionId, entry.ActorId, entry.Details, entry.ReservationResult);
                        break;
                    case ActionJournalEntryType.ResolveOrder:
                        cloned.RecordResolveOrder(entry.Tick, entry.ActionId, entry.ActorId, entry.ResolveOrder, entry.Details);
                        break;
                    case ActionJournalEntryType.ActionResult:
                        cloned.RecordActionResult(entry.Tick, entry.ActionId, entry.ActorId, entry.Details);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return cloned;
        }

        private static MutationJournal ExecuteOriginalAndExtractMutationJournal(
            SimulationRuntime runtime,
            ActionJournal sourceJournal,
            ReplayRecorder replayRecorder)
        {
            _ = runtime;
            var chainStateByActor = new Dictionary<Guid, int>();

            var pipeline = new ReplaySimulationPipeline(sourceJournal);
            pipeline.Execute(entry =>
            {
                replayRecorder.RecordAction(entry.Tick, SerializeEntry(entry));
                return BuildDeterministicMutations(entry, chainStateByActor);
            });

            ActionJournal extractedJournal = BuildActionJournalFromReplayRecorder(replayRecorder);
            var extractedPipeline = new ReplaySimulationPipeline(extractedJournal);
            var extractedChainState = new Dictionary<Guid, int>();
            ReplaySimulationResult extractedResult = extractedPipeline.Execute(
                entry => BuildDeterministicMutations(entry, extractedChainState));
            return extractedResult.ReplayedMutations;
        }

        private static MutationJournal ExecuteReplayRuntime(SimulationRuntime runtime, ActionJournal actionJournal)
        {
            _ = runtime;
            var chainStateByActor = new Dictionary<Guid, int>();
            var pipeline = new ReplaySimulationPipeline(actionJournal);
            ReplaySimulationResult replayResult = pipeline.Execute(entry => BuildDeterministicMutations(entry, chainStateByActor));
            return replayResult.ReplayedMutations;
        }

        private static IReadOnlyList<IRuntimeMutation> BuildDeterministicMutations(
            ActionJournalEntry entry,
            Dictionary<Guid, int> chainStateByActor)
        {
            if (entry.EntryType == ActionJournalEntryType.TickAdvance)
                return Array.Empty<IRuntimeMutation>();

            if (!chainStateByActor.TryGetValue(entry.ActorId, out int chainDepth))
                chainDepth = 0;

            if (entry.EntryType == ActionJournalEntryType.ActionResult)
            {
                chainDepth++;
                chainStateByActor[entry.ActorId] = chainDepth;
            }

            var mutations = new List<IRuntimeMutation>();
            MutationContext context = new MutationContext(
                entry.Tick,
                entry.ActionId,
                entry.ActorId,
                entry.EntryType.ToString());

            mutations.Add(new ResourceMutation(
                CreateDeterministicGuid(entry, mutationIndex: 0, chainDepth),
                entry.ActorId,
                ResourceMutationType.ActionPoint,
                Delta: -1,
                Reason: $"Tick{entry.Tick}_ReserveAP",
                Context: context));

            if (entry.EntryType == ActionJournalEntryType.ActionResult)
            {
                mutations.Add(new DamageMutation(
                    CreateDeterministicGuid(entry, mutationIndex: 1, chainDepth),
                    entry.ActorId,
                    entry.ActorId,
                    Amount: 10 * chainDepth,
                    IsCritical: chainDepth >= 3,
                    Context: context));

                mutations.Add(new ApplyEffectMutation(
                    CreateDeterministicGuid(entry, mutationIndex: 2, chainDepth),
                    EffectId: "ChainReaction",
                    SourceId: entry.ActorId,
                    TargetId: entry.ActorId,
                    DurationTicks: 2 + chainDepth,
                    TickInterval: 1,
                    InitialTickIn: 1,
                    StackCount: chainDepth,
                    Magnitude: chainDepth,
                    Context: context));

                mutations.Add(new ReservationMutation(
                    CreateDeterministicGuid(entry, mutationIndex: 3, chainDepth),
                    entry.ActorId,
                    ReservationKey: $"Chain_{entry.ActionId:N}",
                    Operation: chainDepth >= 3
                        ? ReservationMutationOperation.Confirm
                        : ReservationMutationOperation.Reserve,
                    Tick: entry.Tick,
                    Scope: "ChainResolution",
                    Context: context));
            }

            return mutations;
        }

        private static ActionJournal BuildActionJournalFromReplayRecorder(ReplayRecorder replayRecorder)
        {
            var reconstructed = new ActionJournal();
            IReadOnlyList<SimulationFrame> frames = replayRecorder.GetFrames();
            for (int i = 0; i < frames.Count; i++)
            {
                SimulationFrame frame = frames[i];
                for (int j = 0; j < frame.Actions.Count; j++)
                {
                    ActionJournalEntry entry = DeserializeEntry(frame.Tick, frame.Actions[j]);
                    switch (entry.EntryType)
                    {
                        case ActionJournalEntryType.TickAdvance:
                            reconstructed.RecordTick(entry.Tick);
                            break;
                        case ActionJournalEntryType.ActionRequest:
                            reconstructed.RecordActionRequest(entry.Tick, entry.ActionId, entry.ActorId, entry.Details, entry.ScheduledTick);
                            break;
                        case ActionJournalEntryType.Reservation:
                            reconstructed.RecordReservation(entry.Tick, entry.ActionId, entry.ActorId, entry.Details, entry.ReservationResult);
                            break;
                        case ActionJournalEntryType.ResolveOrder:
                            reconstructed.RecordResolveOrder(entry.Tick, entry.ActionId, entry.ActorId, entry.ResolveOrder, entry.Details);
                            break;
                        case ActionJournalEntryType.ActionResult:
                            reconstructed.RecordActionResult(entry.Tick, entry.ActionId, entry.ActorId, entry.Details);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return reconstructed;
        }

        private static string SerializeEntry(ActionJournalEntry entry)
        {
            return string.Join("|",
                ((int)entry.EntryType).ToString(),
                entry.ActionId.ToString("N"),
                entry.ActorId.ToString("N"),
                entry.ScheduledTick.ToString(),
                entry.ReservationResult ?? string.Empty,
                entry.ResolveOrder.ToString(),
                entry.Details ?? string.Empty);
        }

        private static ActionJournalEntry DeserializeEntry(int tick, string serialized)
        {
            string[] parts = (serialized ?? string.Empty).Split('|');
            if (parts.Length < 7)
                throw new FormatException($"Invalid serialized journal entry: {serialized}");

            ActionJournalEntryType entryType = (ActionJournalEntryType)int.Parse(parts[0]);
            Guid actionId = ParseGuid(parts[1]);
            Guid actorId = ParseGuid(parts[2]);
            int scheduledTick = int.Parse(parts[3]);
            string reservationResult = parts[4];
            int resolveOrder = int.Parse(parts[5]);
            string details = parts[6];

            return new ActionJournalEntry(
                tick,
                sequence: 0,
                entryType,
                actionId,
                actorId,
                scheduledTick,
                reservationResult,
                resolveOrder,
                details);
        }

        private static Guid ParseGuid(string value)
        {
            return Guid.TryParse(value, out Guid parsed) ? parsed : Guid.Empty;
        }

        private static Guid CreateDeterministicGuid(ActionJournalEntry entry, int mutationIndex, int chainDepth)
        {
            string seed =
                $"{entry.Tick}|{entry.Sequence}|{(int)entry.EntryType}|{entry.ActionId:N}|{entry.ActorId:N}|{mutationIndex}|{chainDepth}";
            byte[] seedBytes = Encoding.UTF8.GetBytes(seed);
            byte[] hash;
            using (MD5 md5 = MD5.Create())
                hash = md5.ComputeHash(seedBytes);
            return new Guid(hash);
        }

        private static RuntimeSnapshot CaptureRuntimeSnapshot(SimulationRuntime runtime)
        {
            var unitStates = new Dictionary<Guid, SimulationUnitSnapshot>();
            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> pair in runtime.RuntimeStates)
                unitStates[pair.Key] = SimulationUnitSnapshot.From(pair.Key, pair.Value);

            var occupancy = new Dictionary<Vector2Int, Guid>();
            foreach (KeyValuePair<Vector2Int, Guid> pair in runtime.OccupiedPositions)
                occupancy[pair.Key] = pair.Value;

            var activeEffects = new Dictionary<string, SimulationEffectSnapshot>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyEffectRuntimeState> pair in runtime.ActiveEffects)
                activeEffects[pair.Key] = SimulationEffectSnapshot.From(pair.Key, pair.Value);

            return RuntimeSnapshot.Capture(
                runtime.CurrentTick,
                unitStates,
                occupancy,
                ap: 0f,
                activeEffects,
                reservations: Array.Empty<RuntimeReservationEntry>());
        }
    }
}
