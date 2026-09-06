using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using CheckmateRPG.Core;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Replay;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace CheckmateRPG.Tests
{
    [TestFixture]
    public sealed class SimulationDivergenceTests
    {
        [Test]
        public void StressTest_DeterministicSymmetry()
        {
            const int seed = 20260516;
            const int iterations = 1000;
            const int unitCount = 10;
            const int totalTicks = 100;

            byte[] baselineDigest = null;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                byte[] digest = RunHeadlessDeterministicBattle(seed, unitCount, totalTicks);
                if (baselineDigest == null)
                {
                    baselineDigest = digest;
                    continue;
                }

                Assert.AreEqual(
                    baselineDigest.Length,
                    digest.Length,
                    $"Digest length mismatch at iteration {iteration}.");
                // Byte-for-byte contract: keep Assert.AreEqual per index for explicit divergence location.
                for (int i = 0; i < baselineDigest.Length; i++)
                {
                    Assert.AreEqual(
                        baselineDigest[i],
                        digest[i],
                        $"Digest mismatch at iteration {iteration}, byte index {i}.");
                }
            }
        }

        [Test]
        public void OriginalAndReplayRuntime_ShouldMatch_WhenReplayingSameActionJournal()
        {
            SimulationRuntime originalRuntime = new SimulationRuntime(currentTick: 0);
            SimulationRuntime replayRuntime = new SimulationRuntime(currentTick: 0);

            ActionJournal sourceJournal = BuildChainedActionJournal();
            ActionJournal replayJournal = CloneActionJournal(sourceJournal);

            ReplayRecorder originalReplayRecorder = new ReplayRecorder();
            MutationJournal originalMutationJournal = ExecuteOriginalAndExtractMutationJournal(
                sourceJournal,
                originalReplayRecorder);

            MutationJournal replayMutationJournal = ExecuteReplayRuntime(replayJournal);

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
            ActionJournal sourceJournal,
            ReplayRecorder replayRecorder)
        {
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

        private static MutationJournal ExecuteReplayRuntime(ActionJournal actionJournal)
        {
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
            if (serialized == null)
                throw new ArgumentNullException(nameof(serialized));

            string[] parts = serialized.Split('|');
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
                Sequence: 0,
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
            using (SHA256 sha256 = SHA256.Create())
                hash = sha256.ComputeHash(seedBytes);

            var guidBytes = new byte[16];
            Buffer.BlockCopy(hash, 0, guidBytes, 0, guidBytes.Length);
            return new Guid(guidBytes);
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

        private static byte[] RunHeadlessDeterministicBattle(int seed, int unitCount, int totalTicks)
        {
            const int boardSize = 16;

            SimulationRuntime runtime = new SimulationRuntime(currentTick: 0);
            var random = new SeededRandomProvider(seed);
            List<Guid> orderedUnitIds = InitializeUnits(runtime, random, unitCount, boardSize);

            var schedulerObject = new GameObject("DeterministicSymmetryTickScheduler");
            TickScheduler scheduler = schedulerObject.AddComponent<TickScheduler>();
            try
            {
                for (int tick = 0; tick < totalTicks; tick++)
                {
                    scheduler.Advance(TickScheduler.DefaultTickDurationSeconds);
                    runtime.SetCurrentTick(scheduler.CurrentTick);
                    ExecuteBattleTick(runtime, random, orderedUnitIds, boardSize, scheduler.CurrentTick);
                }

                RuntimeSnapshot snapshot = CaptureRuntimeSnapshot(runtime);
                return SnapshotHash.ComputeDigest(snapshot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(schedulerObject);
            }
        }

        private static List<Guid> InitializeUnits(
            SimulationRuntime runtime,
            SeededRandomProvider random,
            int unitCount,
            int boardSize)
        {
            var occupied = new HashSet<Vector2Int>();
            var unitIds = new List<Guid>(unitCount);
            for (int i = 0; i < unitCount; i++)
            {
                Guid unitId = random.NextGuid();
                while (unitId == Guid.Empty || unitIds.Contains(unitId))
                    unitId = random.NextGuid();

                Vector2Int position = NextUniquePosition(random, boardSize, occupied);
                var state = new UnitRuntimeState();
                state.SeedBaseline(
                    unitId,
                    hp: 120,
                    currentSP: 20,
                    maxSP: 20,
                    position: position,
                    currentActionId: null,
                    recoveryUntilTick: 0,
                    statusFlags: UnitStatusFlags.None);
                runtime.RegisterUnit(state);
                unitIds.Add(unitId);
            }

            unitIds.Sort();
            return unitIds;
        }

        private static Vector2Int NextUniquePosition(
            SeededRandomProvider random,
            int boardSize,
            HashSet<Vector2Int> occupied)
        {
            // 2x board area gives a deterministic fast-path before falling back to full linear scan.
            const int randomRetryMultiplier = 2;
            int attempts = boardSize * boardSize * randomRetryMultiplier;
            for (int i = 0; i < attempts; i++)
            {
                var candidate = new Vector2Int(random.NextInt(0, boardSize), random.NextInt(0, boardSize));
                if (occupied.Add(candidate))
                    return candidate;
            }

            for (int x = 0; x < boardSize; x++)
            {
                for (int y = 0; y < boardSize; y++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (occupied.Add(candidate))
                        return candidate;
                }
            }

            throw new InvalidOperationException("Unable to allocate deterministic unit position.");
        }

        private static void ExecuteBattleTick(
            SimulationRuntime runtime,
            SeededRandomProvider random,
            IReadOnlyList<Guid> unitIds,
            int boardSize,
            int currentTick)
        {
            for (int i = 0; i < unitIds.Count; i++)
                TryMoveUnit(runtime, random, unitIds[i], boardSize);

            for (int i = 0; i < unitIds.Count; i++)
                ApplyAreaAttack(runtime, random, unitIds, unitIds[i], boardSize, currentTick);
        }

        private static void TryMoveUnit(
            SimulationRuntime runtime,
            SeededRandomProvider random,
            Guid unitId,
            int boardSize)
        {
            if (!runtime.TryGetUnit(unitId, out IReadOnlyUnitRuntimeState unit) ||
                (unit.StatusFlags & UnitStatusFlags.Dead) != 0)
            {
                return;
            }

            int dx = random.NextInt(-1, 2);
            int dy = random.NextInt(-1, 2);
            if (dx == 0 && dy == 0)
                return;

            Vector2Int destination = new Vector2Int(
                Mathf.Clamp(unit.Position.x + dx, 0, boardSize - 1),
                Mathf.Clamp(unit.Position.y + dy, 0, boardSize - 1));

            if (destination == unit.Position)
                return;

            if (runtime.OccupiedPositions.TryGetValue(destination, out Guid occupant) && occupant != unitId)
                return;

            runtime.SetUnitPosition(unitId, destination, OwnershipOwners.MovementMutationProcessor);
        }

        private static void ApplyAreaAttack(
            SimulationRuntime runtime,
            SeededRandomProvider random,
            IReadOnlyList<Guid> unitIds,
            Guid attackerId,
            int boardSize,
            int currentTick)
        {
            if (!runtime.TryGetUnit(attackerId, out IReadOnlyUnitRuntimeState attacker) ||
                (attacker.StatusFlags & UnitStatusFlags.Dead) != 0)
            {
                return;
            }

            var attackActionId = random.NextGuid();
            runtime.SetUnitActionState(attackerId, attackActionId, currentTick + 1, OwnershipOwners.ActionScheduler);

            Vector2Int center = new Vector2Int(random.NextInt(0, boardSize), random.NextInt(0, boardSize));
            int radius = random.NextInt(1, 3);
            int damage = random.NextInt(3, 10);

            for (int i = 0; i < unitIds.Count; i++)
            {
                Guid targetId = unitIds[i];
                if (!runtime.TryGetUnit(targetId, out IReadOnlyUnitRuntimeState target) ||
                    (target.StatusFlags & UnitStatusFlags.Dead) != 0)
                {
                    continue;
                }

                int manhattanDistance = Mathf.Abs(target.Position.x - center.x) + Mathf.Abs(target.Position.y - center.y);
                if (manhattanDistance > radius)
                    continue;

                int remainingHp = Mathf.Max(0, target.HP - damage);
                runtime.SetUnitHP(targetId, remainingHp, OwnershipOwners.DamageMutationProcessor);
                if (remainingHp == 0)
                    runtime.AddUnitStatusFlag(targetId, UnitStatusFlags.Dead);
            }
        }
    }
}
