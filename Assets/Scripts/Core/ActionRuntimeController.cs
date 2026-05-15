using System;
using System.Collections.Generic;
using CheckmateRPG.Core.Actions;
using CheckmateRPG.Core.Actions.Resolution;
using CheckmateRPG.Core.Actions.Resolvers;
using CheckmateRPG.Core.Effects;
using CheckmateRPG.Core.Effects.Processors;
using CheckmateRPG.Core.Events.ActionEvents;
using CheckmateRPG.Core.Prediction;
using CheckmateRPG.Core.Replay;
using CheckmateRPG.Core.Runtime;
using CheckmateRPG.Core.Runtime.Mutations;
using CheckmateRPG.Core.Runtime.Ownership;
using CheckmateRPG.Core.Runtime.Processors;
using CheckmateRPG.Core.Simulation;
using CheckmateRPG.Core.Simulation.Spatial;
using CheckmateRPG.Core.Simulation.Validation;
using CheckmateRPG.Data;
using CheckmateRPG.Grid;
using CheckmateRPG.Units;
using UnityEngine;

namespace CheckmateRPG.Core
{
    public sealed class ActionRuntimeController : MonoBehaviour
    {
        private const float DefaultActionSpeed = 1f;
        private const int DefaultRecoveryTicks = 1;
        private const int DefaultSimulationSeed = 1001;
        private static readonly ActionDefinition MoveActionDefinition =
            new(InterruptPriority.Soft, InterruptWindow.CastingInterruptible, true, true);
        private static readonly ActionDefinition AttackActionDefinition =
            new(InterruptPriority.Normal, InterruptWindow.CastingInterruptible, true, true);

        [SerializeField] private int _simulationSeed = DefaultSimulationSeed;
        [SerializeField] private bool _enableReplayRecording = true;
        [SerializeField] private bool _enableJournalRecording = true;
        [SerializeField] private bool _enableSimulationTimelineDebug;
        [SerializeField] private bool _enableRuntimeValidation = true;
        [SerializeField] private bool _haltSimulationOnCriticalValidation;
        [SerializeField] private int _snapshotInterval = 1;
        [SerializeField] private int _maxSnapshotCount = 32;

        public static ActionRuntimeController Instance { get; private set; }

        private readonly Dictionary<Guid, UnitBrain> _unitsById = new();
        private readonly EventBus _eventBus = new();
        private ActionScheduler _scheduler;
        private SimulationRuntime _simulationRuntime;
        private TickScheduler _tickScheduler;
        private EffectSystem _effectSystem;
        private ActionResolverRegistry _resolverRegistry;
        private RuntimeMutationProcessor _mutationProcessor;
        private MutationCommitService _mutationCommitService;
        private ReactionSystem _reactionSystem;
        private AbilityExecutionPipeline _abilityPipeline;
        private IActionCostPolicy _actionCostPolicy;
        private ActionCostReservation _actionCostReservation;
        private RuntimeBattleContext _battleContext;
        private ResolutionPhasePipeline _resolutionPipeline;
        private PositionReservationSystem _positionReservationSystem;
        private PositionReservationSnapshot _positionReservations = PositionReservationSnapshot.Empty;
        private readonly Dictionary<string, AbilityDefinition> _abilityDefinitions = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, Dictionary<string, AbilityRuntimeState>> _abilityStatesByActor = new();
        private ReplayRecorder _replayRecorder;
        private ActionJournal _actionJournal;
        private MutationJournal _mutationJournal;
        private EventTraceRecorder _eventTraceRecorder;
        private SimulationTimelineRecorder _timelineRecorder;
        private RuntimeValidationSystem _runtimeValidationSystem;
        private ValidationExecutionStage _validationExecutionStage;
        private SnapshotRecorder _snapshotRecorder;
        private ValidationResult _lastValidationResult = ValidationResult.Valid();
        private bool _validationHalted;
        private int _lastResolveMutationQueueCount;
        private int _lastCleanupMutationQueueCount;
        private string _lastResolveMutationQueueSummary = "none";
        private string _lastCleanupMutationQueueSummary = "none";
        private PredictionPipeline _predictionPipeline;
        private PredictionQueryService _predictionQueryService;
        private UIPredictionAdapter _uiPredictionAdapter;
        private AIPredictionAdapter _aiPredictionAdapter;
        private bool ShouldRecordReplay => _enableReplayRecording && _replayRecorder != null;
        private bool ShouldRecordJournal => _enableJournalRecording && _actionJournal != null && _mutationJournal != null;

        public ActionScheduler Scheduler => _scheduler;
        public IEventBus EventBus => _eventBus;
        public ReplayRecorder ReplayRecorder => _replayRecorder;
        public ActionJournal ActionJournal => _actionJournal;
        public MutationJournal MutationJournal => _mutationJournal;
        public SimulationTimelineRecorder TimelineRecorder => _timelineRecorder;
        public SnapshotRecorder SnapshotRecorder => _snapshotRecorder;
        public int LastQueuedMutationCount => _lastResolveMutationQueueCount + _lastCleanupMutationQueueCount;
        public string LastQueuedMutationSummary =>
            $"resolve={_lastResolveMutationQueueCount} ({_lastResolveMutationQueueSummary}), " +
            $"cleanup={_lastCleanupMutationQueueCount} ({_lastCleanupMutationQueueSummary})";

        /// <summary>
        /// Read-only view of the simulation runtime state. Safe to read from debug overlays.
        /// </summary>
        public IReadOnlySimulationRuntime SimulationRuntime => _simulationRuntime;

        /// <summary>
        /// Current position reservation snapshot. Updated each tick during spatial arbitration.
        /// Safe to read from debug overlays.
        /// </summary>
        public PositionReservationSnapshot PositionReservations => _positionReservations;

        /// <summary>
        /// Returns the prediction pipeline configured for this runtime.
        /// May be <c>null</c> before <c>Awake</c> has been called.
        /// </summary>
        public PredictionPipeline PredictionPipeline => _predictionPipeline;
        public IReadOnlyPredictionQueryAdapter UIPrediction => _uiPredictionAdapter;
        public AIPredictionAdapter AIPrediction => _aiPredictionAdapter;

        public static ActionRuntimeController EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("ActionRuntimeController");
            return go.AddComponent<ActionRuntimeController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SeededRandomProvider.SetGlobalSeed(_simulationSeed);
            _scheduler = new ActionScheduler(_eventBus);
            _simulationRuntime = new SimulationRuntime(_scheduler.CurrentTick);
            _resolverRegistry = new ActionResolverRegistry();
            _abilityPipeline = new AbilityExecutionPipeline();
            _actionCostPolicy = new DefaultActionCostPolicy();
            _actionCostReservation = new ActionCostReservation();
            _battleContext = new RuntimeBattleContext(this);
            _positionReservationSystem = new PositionReservationSystem(SpatialResolutionPolicy.PriorityWin);
            _predictionPipeline = new PredictionPipeline(
                isCellValid: cell => GridSystem.Instance != null && GridSystem.Instance.IsValidCell(cell),
                attackRangeLookup: unitId =>
                {
                    if (_unitsById.TryGetValue(unitId, out UnitBrain brain) && brain?.UnitData != null)
                        return Mathf.Max(1, brain.UnitData.AttackRange);
                    return 1;
                },
                criticalDamageMultiplier: 2,
                spatialPolicy: SpatialResolutionPolicy.PriorityWin);
            _predictionQueryService = new PredictionQueryService(
                tickProvider: () => _scheduler != null ? _scheduler.CurrentTick : 0,
                runtimeProvider: () => BuildSimulationRuntime(_scheduler != null ? _scheduler.CurrentTick : 0),
                predictionExecutor: (actions, source) =>
                {
                    if (_predictionPipeline == null || _simulationRuntime == null || actions == null || actions.Count == 0)
                        return PredictionResult.Empty;

                    return _predictionPipeline.Execute(
                        actions,
                        _simulationRuntime,
                        _scheduler != null ? _scheduler.CurrentTick : 0,
                        source);
                });
            _uiPredictionAdapter = new UIPredictionAdapter(_predictionQueryService);
            _aiPredictionAdapter = new AIPredictionAdapter(_predictionQueryService);
            _effectSystem = BuildEffectSystem();
            _mutationProcessor = new RuntimeMutationProcessor(
                id => _unitsById.TryGetValue(id, out UnitBrain u) ? u : null,
                _simulationRuntime,
                state => _effectSystem != null && _effectSystem.ApplyOrRefreshEffect(state),
                TryApplyAbilityActionCompleteMutation);
            _mutationCommitService = new MutationCommitService(_mutationProcessor);
            _reactionSystem = new ReactionSystem(
                _eventBus,
                _mutationCommitService,
                () => _simulationRuntime);
            _reactionSystem.Attach();
            _resolutionPipeline = new ResolutionPhasePipeline(
                resolveAction: (action, _) =>
                {
                    TryResolveAction(action, out ActionResolutionResult result);
                    return result;
                },
                onPreResolveAction: HandlePreResolveAction);
            _replayRecorder = new ReplayRecorder();
            _actionJournal = new ActionJournal();
            _mutationJournal = new MutationJournal();
            _eventTraceRecorder = new EventTraceRecorder(_replayRecorder, GetCurrentTickSafe);
            _runtimeValidationSystem = BuildRuntimeValidationSystem();
            _validationExecutionStage = new ValidationExecutionStage(_runtimeValidationSystem);
            _snapshotRecorder = new SnapshotRecorder(new SnapshotPolicy(_snapshotInterval, _maxSnapshotCount));
            if (_enableReplayRecording)
                _eventTraceRecorder.Attach(_eventBus);
            if (_enableSimulationTimelineDebug)
            {
                _timelineRecorder = new SimulationTimelineRecorder(_replayRecorder, GetCurrentTickSafe);
                _timelineRecorder.Attach(_eventBus);
            }
            _resolverRegistry.Register(new AbilityActionResolver(
                _abilityPipeline,
                abilityId => TryGetAbilityDefinition(abilityId, out AbilityDefinition definition) ? definition : null,
                (actorId, abilityId) => TryGetAbilityRuntimeState(actorId, abilityId, out AbilityRuntimeState state) ? state : null,
                ResolveUnit,
                () => _scheduler.CurrentTick,
                () => _unitsById));
            _tickScheduler = TickScheduler.EnsureExists();
            _tickScheduler.OnTick += HandleRuntimeTick;
            _eventBus.Subscribe<ActionQueuedEvent>(HandleActionQueued);
            _eventBus.Subscribe<ActionCompletedEvent>(HandleActionCompleted);
            _eventBus.Subscribe<ActionInterruptedEvent>(HandleActionInterrupted);
            _eventBus.Subscribe<ActionStateChangedEvent>(HandleActionStateChanged);
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            if (_tickScheduler != null)
                _tickScheduler.OnTick -= HandleRuntimeTick;
            _eventTraceRecorder?.Detach();
            _timelineRecorder?.Detach();
            _reactionSystem?.Detach();
            _eventBus.Unsubscribe<ActionQueuedEvent>(HandleActionQueued);
            _eventBus.Unsubscribe<ActionCompletedEvent>(HandleActionCompleted);
            _eventBus.Unsubscribe<ActionInterruptedEvent>(HandleActionInterrupted);
            _eventBus.Unsubscribe<ActionStateChangedEvent>(HandleActionStateChanged);
            Instance = null;
        }

        public bool ApplyEffectRuntime(EffectRuntimeState effectState)
        {
            if (effectState == null || _mutationCommitService == null)
                return false;

            var queue = new MutationQueue();
            queue.Enqueue(new ApplyEffectMutation(
                SeededRandomProvider.Shared.NextGuid(),
                effectState.EffectId,
                effectState.SourceId,
                effectState.TargetId,
                effectState.RemainingTick,
                effectState.TickInterval,
                effectState.NextTickIn,
                effectState.StackCount,
                effectState.Magnitude,
                effectState.StackPolicy,
                effectState.MaxStackCap,
                TimingPhase: effectState.TimingPhase, // timingPhase -> TimingPhase 로 수정
                ActionSpeedLevel: effectState.ActionSpeedLevel, // actionSpeedLevel -> ActionSpeedLevel 로 수정
                IsReaction: effectState.IsReaction, // isReaction -> IsReaction 로 수정
                Context: new MutationContext( // context -> Context 로 수정
                    _scheduler != null ? _scheduler.CurrentTick : 0,
                    Guid.Empty,
                    effectState.TargetId,
                    nameof(ApplyEffectMutation))));

            MutationCommitResult commitResult = _mutationCommitService.Commit(queue);
            RecordMutationCommitJournal(commitResult);
            EnqueueResolvedEvents(commitResult.StagedEvents);
            _eventBus.ProcessQueue();
            return commitResult.AppliedMutations.Count > 0;
        }

        public void RegisterUnit(UnitBrain unit)
        {
            if (unit == null || unit.ActorId == Guid.Empty)
                return;

            _unitsById[unit.ActorId] = unit;
            SyncRuntimeState(unit);
        }

        public void UnregisterUnit(UnitBrain unit)
        {
            if (unit == null || unit.ActorId == Guid.Empty)
                return;

            _unitsById.Remove(unit.ActorId);
            _abilityStatesByActor.Remove(unit.ActorId);
            _simulationRuntime?.UnregisterUnit(unit.ActorId);
        }

        public bool TryEnqueueMove(UnitBrain actor, Vector2Int destination)
        {
            if (!CanQueueAction(actor))
                return false;
            if (actor.Movement == null || !actor.Movement.CanReachCell(destination))
                return false;

            IActionCommand command = CreateMoveCommand(actor, destination);
            return TryReserveAndQueueAction(
                actor,
                command,
                null,
                null,
                $"MoveInput Actor={actor.ActorId:N} Destination=({destination.x},{destination.y})");
        }

        public bool TryEnqueueAttack(UnitBrain actor, GameObject target)
        {
            if (!CanQueueAction(actor))
                return false;
            if (target == null || actor.Combat == null || !actor.Combat.CanAttack)
                return false;
            if (!TryGetActorId(target, out Guid targetId))
                return false;

            IActionCommand command = CreateAttackCommand(actor, targetId);
            return TryReserveAndQueueAction(
                actor,
                command,
                null,
                null,
                $"AttackInput Actor={actor.ActorId:N} Target={targetId:N}");
        }

        public bool TryEnqueueAbility(UnitBrain actor, AbilityDefinition definition, IReadOnlyList<Guid> targetIds)
        {
            if (!CanQueueAction(actor) || definition == null)
                return false;

            RegisterAbilityDefinition(definition);
            AbilityRuntimeState runtimeState = GetOrCreateAbilityRuntimeState(actor.ActorId, definition.name);
            AbilityQueueRequest request = new(
                actor,
                definition,
                runtimeState,
                targetIds ?? Array.Empty<Guid>(),
                _scheduler.CurrentTick,
                action =>
                {
                    return TryReserveAndQueueAction(
                        actor,
                        action,
                        definition,
                        runtimeState,
                        $"AbilityInput Actor={actor.ActorId:N} Ability={definition.name} Targets={action.TargetIds.Count}");
                },
                _unitsById);

            return _abilityPipeline.TryQueueAbility(request, out _);
        }

        /// <summary>
        /// Runs a deterministic, isolated prediction for the given set of actions
        /// against a deep-clone of the current runtime.
        /// <para>
        /// The live runtime is <em>never</em> mutated.
        /// No events are broadcast and no state is persisted.
        /// </para>
        /// </summary>
        /// <param name="actions">
        /// Actions to simulate.  Pass <c>null</c> or an empty list to receive
        /// <see cref="PredictionResult.Empty"/>.
        /// </param>
        /// <returns>
        /// An immutable <see cref="PredictionResult"/> describing the predicted
        /// outcomes, or <see cref="PredictionResult.Empty"/> when the runtime or
        /// pipeline is not yet initialised.
        /// </returns>
        public PredictionResult Predict(IReadOnlyList<IActionCommand> actions)
        {
            if (_predictionPipeline == null || _simulationRuntime == null)
                return PredictionResult.Empty;

            if (actions == null || actions.Count == 0)
                return PredictionResult.Empty;

            return _predictionPipeline.Execute(
                actions,
                _simulationRuntime,
                _scheduler?.CurrentTick ?? 0,
                PredictionSource.ActionRuntimeController);
        }

        public IActionCommand BuildMovePredictionCommand(UnitBrain actor, Vector2Int destination)
        {
            if (!CanQueueAction(actor))
                return null;
            return CreateMoveCommand(actor, destination);
        }

        public IActionCommand BuildAttackPredictionCommand(UnitBrain actor, Guid targetId)
        {
            if (!CanQueueAction(actor) || targetId == Guid.Empty)
                return null;
            return CreateAttackCommand(actor, targetId);
        }

        public void RegisterAbilityDefinition(AbilityDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.name))
                return;
            _abilityDefinitions[definition.name] = definition;
        }

        public void UnregisterAbilityDefinition(AbilityDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.name))
                return;
            _abilityDefinitions.Remove(definition.name);
        }

        public void SyncRuntimeState(UnitBrain unit)
        {
            if (unit == null || unit.MutableRuntimeState == null || _simulationRuntime == null)
                return;

            if (!_simulationRuntime.TryGetMutableUnit(unit.ActorId, out UnitRuntimeState state))
            {
                UnitRuntimeState baseline = unit.MutableRuntimeState;
                int baselineHp = unit.Health != null ? Mathf.RoundToInt(unit.Health.CurrentHealth) : baseline.HP;
                int baselineSp = unit.StatusEffects != null ? Mathf.RoundToInt(unit.StatusEffects.CurrentSp) : baseline.SP;
                Vector2Int baselinePosition = unit.Movement != null ? unit.Movement.GridPosition : baseline.Position;
                UnitStatusFlags baselineFlags = UnitStatusFlags.None;
                if (unit.IsDead)
                    baselineFlags |= UnitStatusFlags.Dead;
                if (unit.Movement != null && unit.StatusEffects != null && !unit.StatusEffects.CanMove)
                    baselineFlags |= UnitStatusFlags.MoveLocked;
                if (unit.Combat != null && unit.StatusEffects != null && !unit.StatusEffects.CanAttack)
                    baselineFlags |= UnitStatusFlags.AttackLocked;

                baseline.SeedBaseline(
                    unit.ActorId,
                    baselineHp,
                    baselineSp,
                    baselinePosition,
                    baseline.CurrentActionId,
                    baseline.RecoveryUntilTick,
                    baselineFlags);
                _simulationRuntime.RegisterUnit(baseline);
            }

            state = _simulationRuntime.GetMutableUnit(unit.ActorId);
            int hp = unit.Health != null ? Mathf.RoundToInt(unit.Health.CurrentHealth) : state.HP;
            int sp = unit.StatusEffects != null ? Mathf.RoundToInt(unit.StatusEffects.CurrentSp) : state.SP;
            Vector2Int position = unit.Movement != null ? unit.Movement.GridPosition : state.Position;
            UnitStatusFlags flags = UnitStatusFlags.None;
            if (unit.IsDead)
                flags |= UnitStatusFlags.Dead;
            if (unit.Movement != null && unit.StatusEffects != null && !unit.StatusEffects.CanMove)
                flags |= UnitStatusFlags.MoveLocked;
            if (unit.Combat != null && unit.StatusEffects != null && !unit.StatusEffects.CanAttack)
                flags |= UnitStatusFlags.AttackLocked;

            _simulationRuntime.SetUnitDerivedState(unit.ActorId, sp, flags);
            _simulationRuntime.SetUnitHP(unit.ActorId, hp, OwnershipOwners.DamageMutationProcessor);
            _simulationRuntime.SetUnitPosition(unit.ActorId, position, OwnershipOwners.MovementMutationProcessor);
        }

        private bool CanQueueAction(UnitBrain actor)
        {
            if (actor == null || actor.IsDead || _simulationRuntime == null)
                return false;

            SyncRuntimeState(actor);
            return _simulationRuntime.TryGetMutableUnit(actor.ActorId, out _);
        }

        private IActionCommand CreateMoveCommand(UnitBrain actor, Vector2Int destination)
        {
            int startTick = _scheduler.CurrentTick + 1;
            return new MoveActionCommand(
                actor.ActorId,
                actor.Movement != null
                    ? actor.Movement.GridPosition
                    : (_simulationRuntime != null && _simulationRuntime.TryGetMutableUnit(actor.ActorId, out UnitRuntimeState state)
                        ? state.Position
                        : default),
                destination,
                startTick,
                ToSpeedTier(actor.UnitData != null ? actor.UnitData.ActionSpeed : DefaultActionSpeed),
                DefaultRecoveryTicks,
                definition: MoveActionDefinition);
        }

        private IActionCommand CreateAttackCommand(UnitBrain actor, Guid targetId)
        {
            int startTick = _scheduler.CurrentTick + 1;
            int damage = actor.UnitData != null ? Mathf.RoundToInt(actor.UnitData.AttackDamage) : 0;
            return new AttackActionCommand(
                actor.ActorId,
                targetId,
                damage,
                isCritical: false,
                startTick,
                ToSpeedTier(actor.UnitData != null ? actor.UnitData.ActionSpeed : DefaultActionSpeed),
                DefaultRecoveryTicks,
                definition: AttackActionDefinition);
        }

        private void BindQueuedAction(UnitBrain actor, IActionCommand command)
        {
            if (_simulationRuntime != null)
            {
                _simulationRuntime.SetUnitActionState(actor.ActorId, command.ActionId, command.RecoveryEndTick, OwnershipOwners.ActionScheduler);
                _simulationRuntime.RegisterAction(command);
            }
            string actionTrace = BuildActionTrace(command);
            if (ShouldRecordReplay)
                _replayRecorder.RecordAction(_scheduler.CurrentTick, actionTrace);
            _timelineRecorder?.RecordAction(actionTrace);
        }

        private void HandleRuntimeTick(int schedulerTick)
        {
            _ = schedulerTick;
            if (_validationHalted)
                return;

            _scheduler.AdvanceTick();
            if (ShouldRecordReplay)
                _replayRecorder.EnsureFrame(_scheduler.CurrentTick);
            if (ShouldRecordJournal)
                _actionJournal.RecordTick(_scheduler.CurrentTick);
            _timelineRecorder?.RecordTick(_scheduler.CurrentTick);
            ResetMutationQueueDebugSnapshot();

            IReadOnlyList<IActionCommand> ready = _scheduler.DrainResolveQueue();
            RecordResolveOrderJournal(ready);
            SyncAllRuntimeStates();
            _positionReservations = _positionReservationSystem.Build(ready, _simulationRuntime, _scheduler.CurrentTick);
            RecordReservationJournal(ready, _positionReservations);
            ActionResolutionContext resolutionContext =
                _resolutionPipeline.Execute(_scheduler.CurrentTick, ready, _battleContext);
            resolutionContext.CurrentPhase = ResolutionPhase.MutationCommit;
            MutationApplyInput mutationApplyInput = BuildMutationApplyInput(resolutionContext);
            IReadOnlyList<IRuntimeMutation> queuedMutations = mutationApplyInput.CommitQueue.CreateSnapshot();
            UpdateResolveMutationQueueDebugSnapshot(queuedMutations);
            _timelineRecorder?.RecordMutations(queuedMutations, MutationCommitPhase.QueueMutation.ToString());
            MutationCommitResult commitResult = _mutationCommitService.Commit(mutationApplyInput.CommitQueue);
            RecordMutationCommitJournal(commitResult);
            _timelineRecorder?.RecordMutations(commitResult.AppliedMutations, MutationCommitPhase.RuntimeApply.ToString());
            ExecuteDeathCheckStage();
            CleanupStageResult cleanupResult = ExecuteCleanupStage();

            if (_enableRuntimeValidation)
            {
                IReadOnlySimulationRuntime runtime = BuildSimulationRuntime(_scheduler.CurrentTick);
                _lastValidationResult = _validationExecutionStage.Execute(runtime);
                if (HandleCriticalValidation(_lastValidationResult))
                {
                    RecordSimulationSnapshot();
                    RecordReplaySnapshot();
                    return;
                }
            }

            var stagedEvents = new List<IGameEvent>();
            AppendEvents(stagedEvents, _positionReservations.ConflictEvents);
            AppendEvents(stagedEvents, mutationApplyInput.ActionEvents);
            AppendEvents(stagedEvents, commitResult.StagedEvents);
            AppendEvents(stagedEvents, cleanupResult.StagedEvents);
            EnqueueResolvedEvents(stagedEvents);
            _eventBus.ProcessQueue();
            RecordSimulationSnapshot();
            RecordReplaySnapshot();
        }

        private void RecordInput(string input)
        {
            if (ShouldRecordReplay)
                _replayRecorder.RecordInput(_scheduler.CurrentTick, input);
        }

        private EffectSystem BuildEffectSystem()
        {
            var effectSystem = new EffectSystem(_eventBus, ResolveUnit, () => _simulationRuntime);
            effectSystem.RegisterProcessor(new DotEffectProcessor(new Dictionary<string, float>
            {
                [StatusEffectType.Burn.ToString()] = 0.02f,
                [StatusEffectType.Ignite.ToString()] = 0.03f,
                [StatusEffectType.Poison.ToString()] = 0.02f
            }));
            effectSystem.RegisterProcessor(new HotEffectProcessor(new Dictionary<string, float>()));
            return effectSystem;
        }

        private RuntimeValidationSystem BuildRuntimeValidationSystem()
        {
            var validationSystem = new RuntimeValidationSystem();
            validationSystem.Register(new NegativeHpValidator());
            validationSystem.Register(new DuplicatePositionValidator());
            validationSystem.Register(new InvalidRecoveryValidator());
            validationSystem.Register(new DeadUnitActionValidator());
            validationSystem.Register(new DuplicateActionIdValidator());
            return validationSystem;
        }

        private UnitBrain ResolveUnit(Guid unitId)
        {
            return _unitsById.TryGetValue(unitId, out UnitBrain unit) ? unit : null;
        }

        private void SyncAllRuntimeStates()
        {
            foreach (KeyValuePair<Guid, UnitBrain> entry in _unitsById)
            {
                if (entry.Value != null)
                    SyncRuntimeState(entry.Value);
            }
        }

        private void SyncActiveActionsRuntime()
        {
            if (_simulationRuntime == null || _scheduler == null)
                return;

            _simulationRuntime.ReplaceActions(_scheduler.GetActiveActions());
        }

        private MutationApplyInput BuildMutationApplyInput(ActionResolutionContext resolutionContext)
        {
            if (resolutionContext == null ||
                (resolutionContext.PendingMutationQueue.Count == 0 && resolutionContext.PendingEvents.Count == 0))
                return MutationApplyInput.Empty;

            var mutationQueue = new MutationQueue();
            mutationQueue.EnqueueRange(resolutionContext.PendingMutationQueue);

            return new MutationApplyInput(
                resolutionContext.PendingEvents,
                mutationQueue);
        }

        private void ExecuteDeathCheckStage()
        {
            var deadUnitIds = new HashSet<Guid>();
            var deathMutationQueue = new MutationQueue();
            foreach (KeyValuePair<Guid, UnitBrain> entry in _unitsById)
            {
                if (entry.Value == null || !entry.Value.IsDead)
                    continue;

                deadUnitIds.Add(entry.Key);
                int currentTick = _scheduler.CurrentTick;
                deathMutationQueue.Enqueue(new DeathMutation(
                    SeededRandomProvider.Shared.NextGuid(),
                    entry.Key,
                    Guid.Empty,
                    currentTick,
                    new MutationContext(
                        currentTick,
                        Guid.Empty,
                        entry.Key,
                        nameof(DeathMutation))));
            }

            if (deathMutationQueue.Count > 0 && _mutationCommitService != null)
            {
                MutationCommitResult deathCommitResult = _mutationCommitService.Commit(deathMutationQueue);
                RecordMutationCommitJournal(deathCommitResult);
            }

            _scheduler.TerminateActionsForActors(deadUnitIds);
            SyncActiveActionsRuntime();
            SyncAllRuntimeStates();
        }

        private CleanupStageResult ExecuteCleanupStage()
        {
            if (_effectSystem == null || _mutationCommitService == null || _scheduler == null)
            {
                UpdateAbilityCooldownState(_scheduler != null ? _scheduler.CurrentTick : 0);
                return CleanupStageResult.Empty;
            }

            IReadOnlyList<QueuedMutation> queuedEffectMutations = _effectSystem.AdvanceTick(_scheduler.CurrentTick);
            if (queuedEffectMutations.Count == 0)
            {
                UpdateAbilityCooldownState(_scheduler.CurrentTick);
                return CleanupStageResult.Empty;
            }

            var queue = new MutationQueue();
            queue.EnqueueRange(queuedEffectMutations);
            IReadOnlyList<IRuntimeMutation> queuedSnapshot = queue.CreateSnapshot();
            UpdateCleanupMutationQueueDebugSnapshot(queuedSnapshot);
            _timelineRecorder?.RecordMutations(queuedSnapshot, MutationCommitPhase.QueueMutation.ToString());
            MutationCommitResult commitResult = _mutationCommitService.Commit(queue);
            RecordMutationCommitJournal(commitResult);
            _timelineRecorder?.RecordMutations(commitResult.AppliedMutations, MutationCommitPhase.RuntimeApply.ToString());
            UpdateAbilityCooldownState(_scheduler.CurrentTick);
            return new CleanupStageResult(commitResult.StagedEvents);
        }

        private void ResetMutationQueueDebugSnapshot()
        {
            _lastResolveMutationQueueCount = 0;
            _lastCleanupMutationQueueCount = 0;
            _lastResolveMutationQueueSummary = "none";
            _lastCleanupMutationQueueSummary = "none";
        }

        private void UpdateResolveMutationQueueDebugSnapshot(IReadOnlyList<IRuntimeMutation> queuedMutations)
        {
            _lastResolveMutationQueueCount = queuedMutations?.Count ?? 0;
            _lastResolveMutationQueueSummary = BuildMutationSummary(queuedMutations);
        }

        private void UpdateCleanupMutationQueueDebugSnapshot(IReadOnlyList<IRuntimeMutation> queuedMutations)
        {
            _lastCleanupMutationQueueCount = queuedMutations?.Count ?? 0;
            _lastCleanupMutationQueueSummary = BuildMutationSummary(queuedMutations);
        }

        private static string BuildMutationSummary(IReadOnlyList<IRuntimeMutation> queuedMutations)
        {
            if (queuedMutations == null || queuedMutations.Count == 0)
                return "none";

            var countsByType = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < queuedMutations.Count; i++)
            {
                IRuntimeMutation mutation = queuedMutations[i];
                if (mutation == null)
                    continue;

                string typeName = mutation.GetType().Name;
                if (countsByType.TryGetValue(typeName, out int current))
                    countsByType[typeName] = current + 1;
                else
                    countsByType[typeName] = 1;
            }

            if (countsByType.Count == 0)
                return "none";

            var summaryParts = new List<string>(countsByType.Count);
            foreach (KeyValuePair<string, int> pair in countsByType)
                summaryParts.Add($"{pair.Key}x{pair.Value}");

            return string.Join(", ", summaryParts);
        }

        private bool TryResolveAction(IActionCommand action, out ActionResolutionResult result)
        {
            result = ActionResolutionResult.Failed();
            if (action == null)
                return false;
            if (!_unitsById.TryGetValue(action.ActorId, out UnitBrain actor) || actor == null)
                return false;
            if (!_resolverRegistry.TryResolve(action, _battleContext, out result))
                return false;

            return result.Success;
        }

        private void HandlePreResolveAction(IActionCommand action, ActionResolutionContext context)
        {
            if (action == null || context == null)
                return;
            if (action.State == ActionState.Cancelled || action.State == ActionState.Interrupted || context.IsCancelled(action.ActionId))
                return;

            if (!_battleContext.TryGetUnit(action.ActorId, out BattleUnitSnapshot actor) ||
                (actor.StatusFlags & UnitStatusFlags.Dead) != 0)
            {
                CancelResolvingAction(action, ActionCancellationReason.Interrupted, context);
                return;
            }

            switch (action)
            {
                case MoveActionCommand move:
                    ValidateMoveActionPreResolve(move, context);
                    break;
                case AttackActionCommand attack:
                    ValidateAttackActionPreResolve(attack, context);
                    break;
            }

            if (context.IsCancelled(action.ActionId))
                return;

            if (!_actionCostReservation.Commit(action.ActionId, _scheduler.CurrentTick))
                CancelResolvingAction(action, ActionCancellationReason.ReservationLost, context);
        }

        private void ValidateMoveActionPreResolve(MoveActionCommand move, ActionResolutionContext context)
        {
            if (move == null)
                return;

            if (!_positionReservations.HasWinningReservation(move.ActionId))
            {
                CancelResolvingAction(move, ActionCancellationReason.ReservationLost, context);
                return;
            }

            if (!_battleContext.IsCellValid(move.To))
            {
                CancelResolvingAction(move, ActionCancellationReason.TargetInvalid, context);
                return;
            }
        }

        private void ValidateAttackActionPreResolve(AttackActionCommand attack, ActionResolutionContext context)
        {
            if (attack == null)
                return;

            if (!_battleContext.TryGetUnit(attack.TargetId, out BattleUnitSnapshot target) ||
                (target.StatusFlags & UnitStatusFlags.Dead) != 0)
            {
                CancelResolvingAction(attack, ActionCancellationReason.TargetInvalid, context);
                return;
            }

            if (!_battleContext.IsCellValid(target.Position))
            {
                CancelResolvingAction(attack, ActionCancellationReason.TargetInvalid, context);
                return;
            }

            if (!_battleContext.IsTargetInAttackRange(attack.ActorId, attack.TargetId))
            {
                CancelResolvingAction(attack, ActionCancellationReason.OutOfRange, context);
            }
        }

        private void CancelResolvingAction(
            IActionCommand action,
            ActionCancellationReason reason,
            ActionResolutionContext context)
        {
            if (action == null || context == null)
                return;
            if (context.IsCancelled(action.ActionId))
                return;

            _scheduler.CancelAction(action.ActionId);
            _actionCostReservation.Rollback(action.ActionId);
            context.MarkCancelled(action.ActionId, reason);
            context.AddEvent(new ActionCancelledEvent(
                new ActionCancelledPayload(action.ActionId, action.ActorId, reason, _scheduler.CurrentTick),
                action.ActorId.ToString("N"),
                action.ActionId.ToString("N")));

            if (_simulationRuntime != null &&
                _simulationRuntime.TryGetMutableUnit(action.ActorId, out UnitRuntimeState state))
            {
                Guid? updatedActionId = state.CurrentActionId;
                if (updatedActionId == action.ActionId)
                    updatedActionId = null;
                _simulationRuntime.SetUnitActionState(
                    action.ActorId,
                    updatedActionId,
                    _scheduler.CurrentTick,
                    OwnershipOwners.ActionScheduler);
            }

            _simulationRuntime?.UnregisterAction(action.ActionId);
            if (_unitsById.TryGetValue(action.ActorId, out UnitBrain actor) && actor != null)
                SyncRuntimeState(actor);
        }

        private IReadOnlySimulationRuntime BuildSimulationRuntime(int tick)
        {
            if (_simulationRuntime == null)
                return new SimulationRuntime(tick);

            SyncAllRuntimeStates();
            SyncActiveActionsRuntime();
            _simulationRuntime.SetCurrentTick(tick);
            return _simulationRuntime.CreateSnapshot(tick);
        }

        private bool HandleCriticalValidation(ValidationResult validationResult)
        {
            if (validationResult == null || !validationResult.HasCriticalIssues)
                return false;

            for (int i = 0; i < validationResult.Issues.Count; i++)
            {
                ValidationIssue issue = validationResult.Issues[i];
                if (issue.Severity != ValidationSeverity.Critical)
                    continue;

                string trace = BuildValidationTrace(issue);
                if (ShouldRecordReplay)
                    _replayRecorder.RecordEvent(_scheduler.CurrentTick, trace);
                Debug.LogError(trace);
            }

            if (!_haltSimulationOnCriticalValidation)
                return false;

            _validationHalted = true;
            if (_tickScheduler != null)
                _tickScheduler.enabled = false;
            string haltTrace = $"[Validation][Critical] Simulation halted at tick {_scheduler.CurrentTick}.";
            if (ShouldRecordReplay)
                _replayRecorder.RecordEvent(_scheduler.CurrentTick, haltTrace);
            Debug.LogError(haltTrace);
            return true;
        }

        private void RecordReplaySnapshot()
        {
            if (!ShouldRecordReplay)
                return;

            SyncAllRuntimeStates();
            _replayRecorder.RecordSnapshot(BuildFrameSnapshot(_scheduler.CurrentTick));
        }

        private void RecordSimulationSnapshot()
        {
            if (_snapshotRecorder == null || !_snapshotRecorder.ShouldCapture(_scheduler.CurrentTick))
                return;

            IReadOnlySimulationRuntime runtime = BuildSimulationRuntime(_scheduler.CurrentTick);
            _snapshotRecorder.TryRecord(new SimulationSnapshot(
                _scheduler.CurrentTick,
                runtime.RuntimeStates,
                runtime.ActiveActions,
                runtime.ActiveEffects));
        }

        private static void AppendEvents(List<IGameEvent> target, IReadOnlyList<IGameEvent> source)
        {
            AppendNonNullItems(target, source);
        }

        private static void AppendNonNullItems<T>(List<T> target, IReadOnlyList<T> source) where T : class
        {
            if (target == null || source == null || source.Count == 0)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    target.Add(source[i]);
            }
        }

        private static string BuildValidationTrace(ValidationIssue issue)
        {
            string unitSegment = issue.UnitId.HasValue ? issue.UnitId.Value.ToString("N") : "none";
            return $"[Validation][{issue.Severity}] Tick={issue.Tick} Unit={unitSegment} Message={issue.Message}";
        }

        private int GetCurrentTickSafe()
        {
            return _scheduler != null ? _scheduler.CurrentTick : 0;
        }

        private void EnqueueResolvedEvents(IReadOnlyList<IGameEvent> events)
        {
            if (events == null || events.Count == 0)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] == null)
                    continue;
                _eventBus.Publish(events[i]);
            }
        }

        private void HandleActionCompleted(ActionCompletedEvent actionCompletedEvent)
        {
            ActionLifecyclePayload payload = actionCompletedEvent.Payload;
            if (_simulationRuntime == null || !_simulationRuntime.TryGetMutableUnit(payload.ActorId, out UnitRuntimeState state))
                return;

            _simulationRuntime.SetUnitActionState(
                payload.ActorId,
                state.CurrentActionId == payload.ActionId ? null : state.CurrentActionId,
                Mathf.Max(state.RecoveryUntilTick, payload.RecoveryEndTick),
                OwnershipOwners.ActionScheduler);
            _simulationRuntime.UnregisterAction(payload.ActionId);
            if (_unitsById.TryGetValue(payload.ActorId, out UnitBrain actor) && actor != null)
                SyncRuntimeState(actor);
        }

        private void HandleActionQueued(ActionQueuedEvent actionQueuedEvent)
        {
            ActionLifecyclePayload payload = actionQueuedEvent.Payload;
            if (_scheduler == null || !_unitsById.TryGetValue(payload.ActorId, out UnitBrain actor) || actor == null)
                return;

            IReadOnlyCollection<IActionCommand> activeActions = _scheduler.GetActiveActions();
            foreach (IActionCommand action in activeActions)
            {
                if (action != null && action.ActionId == payload.ActionId)
                {
                    BindQueuedAction(actor, action);
                    break;
                }
            }
        }

        private void HandleActionStateChanged(ActionStateChangedEvent actionStateChangedEvent)
        {
            ActionLifecyclePayload payload = actionStateChangedEvent.Payload;
            switch (payload.CurrentState)
            {
                case ActionState.Cancelled:
                case ActionState.Interrupted:
                    _actionCostReservation.Rollback(payload.ActionId);
                    break;
            }
        }

        private void HandleActionInterrupted(ActionInterruptedEvent actionInterruptedEvent)
        {
            ActionInterruptedPayload payload = actionInterruptedEvent.Payload;
            if (_simulationRuntime == null || !_simulationRuntime.TryGetMutableUnit(payload.ActorId, out UnitRuntimeState state))
                return;

            _simulationRuntime.SetUnitActionState(
                payload.ActorId,
                state.CurrentActionId == payload.InterruptedAction ? null : state.CurrentActionId,
                payload.Tick,
                OwnershipOwners.ActionScheduler);
            _simulationRuntime.UnregisterAction(payload.InterruptedAction);
            if (_unitsById.TryGetValue(payload.ActorId, out UnitBrain actor) && actor != null)
                SyncRuntimeState(actor);
            _eventBus.Publish(new ActionCancelledEvent(
                new ActionCancelledPayload(payload.InterruptedAction, payload.ActorId, ActionCancellationReason.Interrupted, payload.Tick),
                payload.ActorId.ToString("N"),
                payload.InterruptedAction.ToString("N")));
        }

        private bool TryReserveAndQueueAction(
            UnitBrain actor,
            IActionCommand action,
            AbilityDefinition abilityDefinition,
            AbilityRuntimeState abilityRuntimeState,
            string inputTrace)
        {
            if (actor == null || action == null)
                return false;

            ActionCostBreakdown cost = _actionCostPolicy.Evaluate(
                action,
                new ActionCostContext(actor, abilityDefinition, abilityRuntimeState));
            if (!_actionCostReservation.CanAfford(actor.ActorId, cost, GetCurrentSp))
            {
                RecordActionRequestJournal(action, BuildActionRequestDetail(
                    outcome: "Rejected",
                    reason: "InsufficientCost",
                    inputTrace: inputTrace));
                return false;
            }

            ActionCostReservationHooks hooks = BuildReservationHooks(abilityDefinition, abilityRuntimeState);
            if (!_actionCostReservation.ReserveCost(action.ActionId, actor.ActorId, cost, hooks))
            {
                RecordActionRequestJournal(action, BuildActionRequestDetail(
                    outcome: "Rejected",
                    reason: "ReservationFailed",
                    inputTrace: inputTrace));
                return false;
            }

            try
            {
                ActionAdmissionResult admissionResult = _scheduler.ScheduleAction(action);
                RecordActionRequestJournal(
                    action,
                    BuildActionRequestDetail(
                        outcome: "Admission",
                        reason: admissionResult.Reason.ToString(),
                        inputTrace: inputTrace,
                        status: admissionResult.Status.ToString(),
                        currentLock: admissionResult.CurrentLock.ToString()));
                if (admissionResult.Status == ActionAdmissionStatus.Rejected)
                {
                    _actionCostReservation.Rollback(action.ActionId);
                    return false;
                }

                RecordInput(inputTrace);
                return true;
            }
            catch (Exception ex)
            {
                _actionCostReservation.Rollback(action.ActionId);
                RecordActionRequestJournal(action, BuildActionRequestDetail(
                    outcome: "Rejected",
                    reason: "Exception",
                    inputTrace: inputTrace,
                    message: ex.Message));
                Debug.LogWarning($"[ActionRuntimeController] Failed to queue action with reservation: {ex.Message}");
                return false;
            }
        }

        private static ActionCostReservationHooks BuildReservationHooks(
            AbilityDefinition definition,
            AbilityRuntimeState runtimeState)
        {
            if (definition == null || runtimeState == null)
                return ActionCostReservationHooks.Empty;

            return new ActionCostReservationHooks(
                // Ability cooldown/action-state reservation must not overlap with an already pending ability action.
                CanReserve: () => !runtimeState.PendingActionId.HasValue,
                Commit: (currentTick, actionId) =>
                {
                    runtimeState.SetIdentity(definition.name, runtimeState.Charges);
                    runtimeState.CommitQueuedAction(
                        actionId,
                        currentTick,
                        definition.Cooldown,
                        OwnershipOwners.ActionScheduler,
                        OwnershipOwners.TickScheduler);
                },
                // Queue-time ability state is untouched; only reserved AP/SP is rolled back.
                Rollback: _ => { });
        }

        private int GetCurrentSp(Guid actorId)
        {
            if (_simulationRuntime != null &&
                _simulationRuntime.TryGetMutableUnit(actorId, out UnitRuntimeState state))
            {
                return state.SP;
            }

            return 0;
        }

        private void RecordActionRequestJournal(IActionCommand action, string details)
        {
            if (!ShouldRecordJournal || action == null || _scheduler == null)
                return;

            _actionJournal.RecordActionRequest(
                _scheduler.CurrentTick,
                action.ActionId,
                action.ActorId,
                details ?? string.Empty);
        }

        private static string BuildActionRequestDetail(
            string outcome,
            string reason,
            string inputTrace,
            string status = null,
            string currentLock = null,
            string message = null)
        {
            string normalizedInput = inputTrace ?? string.Empty;
            string normalizedOutcome = outcome ?? string.Empty;
            string normalizedReason = reason ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(currentLock))
            {
                return $"Outcome={normalizedOutcome}|Status={status}|Reason={normalizedReason}|Lock={currentLock}|{normalizedInput}";
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                return $"Outcome={normalizedOutcome}|Reason={normalizedReason}|Message={message}|{normalizedInput}";
            }

            return $"Outcome={normalizedOutcome}|Reason={normalizedReason}|{normalizedInput}";
        }

        private void RecordResolveOrderJournal(IReadOnlyList<IActionCommand> readyActions)
        {
            if (!ShouldRecordJournal || readyActions == null || readyActions.Count == 0 || _scheduler == null)
                return;

            var sorted = new List<IActionCommand>(readyActions.Count);
            for (int i = 0; i < readyActions.Count; i++)
            {
                IActionCommand action = readyActions[i];
                if (action != null)
                    sorted.Add(action);
            }

            sorted.Sort(ResolutionOrderComparer.Default);
            for (int i = 0; i < sorted.Count; i++)
            {
                IActionCommand action = sorted[i];
                _actionJournal.RecordResolveOrder(
                    _scheduler.CurrentTick,
                    action.ActionId,
                    action.ActorId,
                    i,
                    $"Speed={action.SpeedTier}|ResolveTick={action.ResolveTick}");
            }
        }

        private void RecordReservationJournal(IReadOnlyList<IActionCommand> readyActions, PositionReservationSnapshot reservations)
        {
            if (!ShouldRecordJournal || _scheduler == null || reservations == null)
                return;

            foreach (KeyValuePair<Guid, ReservedPosition> pair in reservations.WinningReservationsByAction)
            {
                Guid actionId = pair.Key;
                ReservedPosition reserved = pair.Value;
                Guid actorId = ResolveActorId(actionId, readyActions);
                _actionJournal.RecordReservation(
                    _scheduler.CurrentTick,
                    actionId,
                    actorId,
                    $"Status=Granted|Pos=({reserved.Position.x},{reserved.Position.y})|Speed={reserved.ActionSpeedLevel}|ReservationTick={reserved.ReservationTick}");
            }

            foreach (Guid actionId in reservations.ReservationLostActions)
            {
                Guid actorId = ResolveActorId(actionId, readyActions);
                _actionJournal.RecordReservation(
                    _scheduler.CurrentTick,
                    actionId,
                    actorId,
                    "Status=Lost");
            }
        }

        private Guid ResolveActorId(Guid actionId, IReadOnlyList<IActionCommand> readyActions)
        {
            if (actionId == Guid.Empty)
                return Guid.Empty;

            if (readyActions != null)
            {
                for (int i = 0; i < readyActions.Count; i++)
                {
                    IActionCommand action = readyActions[i];
                    if (action != null && action.ActionId == actionId)
                        return action.ActorId;
                }
            }

            if (_simulationRuntime != null && _simulationRuntime.ActiveActions.TryGetValue(actionId, out IReadOnlyActionState activeAction))
                return activeAction.ActorId;

            return Guid.Empty;
        }

        private void RecordMutationCommitJournal(MutationCommitResult commitResult)
        {
            if (!ShouldRecordJournal || _scheduler == null)
                return;
            if (commitResult.AppliedMutations == null || commitResult.AppliedMutations.Count == 0)
                return;

            _mutationJournal.RecordCommit(_scheduler.CurrentTick, commitResult.AppliedMutations);
        }

        private static ActionSpeedTier ToSpeedTier(float actionSpeed)
        {
            if (actionSpeed >= 1.6f)
                return ActionSpeedTier.VeryFast;
            if (actionSpeed >= 1.25f)
                return ActionSpeedTier.Fast;
            if (actionSpeed <= 0.6f)
                return ActionSpeedTier.VerySlow;
            if (actionSpeed <= 0.85f)
                return ActionSpeedTier.Slow;
            return ActionSpeedTier.Normal;
        }

        private readonly record struct MutationApplyInput(
            IReadOnlyList<IGameEvent> ActionEvents,
            MutationQueue CommitQueue)
        {
            public static MutationApplyInput Empty =>
                new(Array.Empty<IGameEvent>(), new MutationQueue());
        }

        private readonly record struct CleanupStageResult(IReadOnlyList<IGameEvent> StagedEvents)
        {
            public static CleanupStageResult Empty => new(Array.Empty<IGameEvent>());
        }

        private static bool TryGetActorId(GameObject target, out Guid actorId)
        {
            actorId = Guid.Empty;
            if (target == null || !target.TryGetComponent(out UnitBrain brain))
                return false;

            actorId = brain.ActorId;
            return actorId != Guid.Empty;
        }

        private static string BuildActionTrace(IActionCommand action)
        {
            string detail = action switch
            {
                MoveActionCommand move => $"From=({move.From.x},{move.From.y}) To=({move.To.x},{move.To.y})",
                AttackActionCommand attack => $"Target={attack.TargetId:N} Damage={attack.Damage} Critical={attack.IsCritical}",
                AbilityActionCommand ability => $"Ability={ability.AbilityId} Targets={ability.TargetIds.Count}",
                _ => string.Empty
            };

            return
                $"{action.GetType().Name}|Action={action.ActionId:N}|Actor={action.ActorId:N}|State={action.State}" +
                $"|Queued={action.QueuedTick}|Start={action.StartTick}|Resolve={action.ResolveTick}|Recovery={action.RecoveryEndTick}|{detail}";
        }

        private FrameSnapshot BuildFrameSnapshot(int tick)
        {
            IReadOnlySimulationRuntime runtime = BuildSimulationRuntime(tick);
            var snapshot = new FrameSnapshot { Tick = tick };
            var units = new List<UnitFrameSnapshot>(runtime.RuntimeStates.Count);
            foreach (KeyValuePair<Guid, IReadOnlyUnitRuntimeState> entry in runtime.RuntimeStates)
            {
                IReadOnlyUnitRuntimeState state = entry.Value;
                if (state == null)
                    continue;

                units.Add(new UnitFrameSnapshot
                {
                    UnitId = state.UnitId.ToString("N"),
                    HP = state.HP,
                    SP = state.SP,
                    PosX = state.Position.x,
                    PosY = state.Position.y,
                    StatusFlags = state.StatusFlags,
                    CurrentActionId = state.CurrentActionId.HasValue ? state.CurrentActionId.Value.ToString("N") : string.Empty,
                    RecoveryUntilTick = state.RecoveryUntilTick
                });
            }

            units.Sort((left, right) => string.CompareOrdinal(left.UnitId, right.UnitId));
            snapshot.Units = units;

            var actions = new List<ActionFrameSnapshot>(runtime.ActiveActions.Count);
            foreach (IReadOnlyActionState action in runtime.ActiveActions.Values)
            {
                string data = action switch
                {
                    SimulationActionSnapshot moveSnapshot when moveSnapshot.From.HasValue && moveSnapshot.To.HasValue
                        => $"from={moveSnapshot.From.Value.x},{moveSnapshot.From.Value.y};to={moveSnapshot.To.Value.x},{moveSnapshot.To.Value.y}",
                    SimulationActionSnapshot attackSnapshot when attackSnapshot.TargetId.HasValue
                        => $"target={attackSnapshot.TargetId.Value:N};damage={attackSnapshot.Damage.GetValueOrDefault()};critical={attackSnapshot.IsCritical.GetValueOrDefault()}",
                    SimulationActionSnapshot abilitySnapshot when !string.IsNullOrWhiteSpace(abilitySnapshot.AbilityId)
                        => $"ability={abilitySnapshot.AbilityId};targets={abilitySnapshot.TargetIds.Count}",
                    _ => string.Empty
                };

                actions.Add(new ActionFrameSnapshot
                {
                    ActionId = action.ActionId.ToString("N"),
                    ActorId = action.ActorId.ToString("N"),
                    ActionType = action is SimulationActionSnapshot actionSnapshot
                        ? actionSnapshot.ActionType
                        : action.GetType().Name,
                    State = action.State.ToString(),
                    QueuedTick = action.QueuedTick,
                    StartTick = action.StartTick,
                    ResolveTick = action.ResolveTick,
                    RecoveryEndTick = action.RecoveryEndTick,
                    Data = data
                });
            }

            actions.Sort((left, right) => string.CompareOrdinal(left.ActionId, right.ActionId));
            snapshot.ActiveActions = actions;

            var effects = new List<EffectFrameSnapshot>(runtime.ActiveEffects.Count);
            foreach (KeyValuePair<string, IReadOnlyEffectRuntimeState> pair in runtime.ActiveEffects)
            {
                IReadOnlyEffectRuntimeState effect = pair.Value;
                if (effect == null)
                    continue;

                effects.Add(new EffectFrameSnapshot
                {
                    EffectId = effect.EffectId,
                    SourceId = effect.SourceId.ToString("N"),
                    TargetId = effect.TargetId.ToString("N"),
                    RemainingTick = effect.RemainingTick,
                    StackCount = effect.StackCount,
                    TickInterval = effect.TickInterval,
                    NextTickIn = effect.NextTickIn,
                    Magnitude = effect.Magnitude,
                    TimingPhase = effect.TimingPhase,
                    ActionSpeedLevel = effect.ActionSpeedLevel,
                    IsReaction = effect.IsReaction
                });
            }

            effects.Sort((left, right) =>
            {
                int targetCompare = string.CompareOrdinal(left.TargetId, right.TargetId);
                if (targetCompare != 0)
                    return targetCompare;

                int effectCompare = string.CompareOrdinal(left.EffectId, right.EffectId);
                if (effectCompare != 0)
                    return effectCompare;

                return string.CompareOrdinal(left.SourceId, right.SourceId);
            });

            snapshot.ActiveEffects = effects;
            return snapshot;
        }

        private bool TryGetAbilityDefinition(string abilityId, out AbilityDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(abilityId))
                return false;
            return _abilityDefinitions.TryGetValue(abilityId, out definition);
        }

        private AbilityRuntimeState GetOrCreateAbilityRuntimeState(Guid actorId, string abilityId)
        {
            if (!_abilityStatesByActor.TryGetValue(actorId, out Dictionary<string, AbilityRuntimeState> byAbility))
            {
                byAbility = new Dictionary<string, AbilityRuntimeState>(StringComparer.Ordinal);
                _abilityStatesByActor[actorId] = byAbility;
            }

            if (!byAbility.TryGetValue(abilityId, out AbilityRuntimeState state) || state == null)
            {
                state = new AbilityRuntimeState(abilityId, 1);
                byAbility[abilityId] = state;
            }

            return state;
        }

        private bool TryGetAbilityRuntimeState(Guid actorId, string abilityId, out AbilityRuntimeState state)
        {
            state = null;
            if (!_abilityStatesByActor.TryGetValue(actorId, out Dictionary<string, AbilityRuntimeState> byAbility))
                return false;

            if (!string.IsNullOrWhiteSpace(abilityId) && byAbility.TryGetValue(abilityId, out state))
                return state != null;

            foreach (KeyValuePair<string, AbilityRuntimeState> kv in byAbility)
            {
                if (kv.Value != null && kv.Value.PendingActionId.HasValue)
                {
                    state = kv.Value;
                    return true;
                }
            }

            return false;
        }

        private void UpdateAbilityCooldownState(int currentTick)
        {
            foreach (KeyValuePair<Guid, Dictionary<string, AbilityRuntimeState>> actorEntry in _abilityStatesByActor)
            {
                Dictionary<string, AbilityRuntimeState> abilityStates = actorEntry.Value;
                if (abilityStates == null)
                    continue;

                foreach (KeyValuePair<string, AbilityRuntimeState> stateEntry in abilityStates)
                {
                    AbilityRuntimeState state = stateEntry.Value;
                    if (state == null)
                        continue;

                    state.UpdateCooldown(currentTick, OwnershipOwners.TickScheduler);
                }
            }
        }

        private bool TryApplyAbilityActionCompleteMutation(AbilityActionCompleteMutation mutation)
        {
            if (mutation.TargetId == Guid.Empty || mutation.ActionId == Guid.Empty)
                return false;
            if (!_abilityStatesByActor.TryGetValue(mutation.TargetId, out Dictionary<string, AbilityRuntimeState> byAbility))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(mutation.AbilityId))
            {
                if (!byAbility.TryGetValue(mutation.AbilityId, out AbilityRuntimeState keyedState))
                    return false;
                if (keyedState == null)
                    return false;

                keyedState.CompleteQueuedAction(
                    mutation.ActionId,
                    mutation.Tick,
                    OwnershipOwners.ActionScheduler,
                    OwnershipOwners.TickScheduler);
                return true;
            }

            foreach (KeyValuePair<string, AbilityRuntimeState> entry in byAbility)
            {
                AbilityRuntimeState state = entry.Value;
                if (state == null || state.PendingActionId != mutation.ActionId)
                    continue;

                state.CompleteQueuedAction(
                    mutation.ActionId,
                    mutation.Tick,
                    OwnershipOwners.ActionScheduler,
                    OwnershipOwners.TickScheduler);
                return true;
            }

            return false;
        }

        private sealed class RuntimeBattleContext : IBattleContext
        {
            private readonly ActionRuntimeController _controller;

            public RuntimeBattleContext(ActionRuntimeController controller)
            {
                _controller = controller;
            }

            public int CriticalDamageMultiplier => 2;

            public bool TryGetUnit(Guid unitId, out BattleUnitSnapshot unit)
            {
                if (_controller._simulationRuntime != null &&
                    _controller._simulationRuntime.TryGetUnit(unitId, out IReadOnlyUnitRuntimeState state))
                {
                    unit = new BattleUnitSnapshot(
                        state.UnitId,
                        state.Position,
                        state.HP,
                        state.StatusFlags);
                    return true;
                }

                unit = default;
                return false;
            }

            public bool IsCellValid(Vector2Int cell)
            {
                return GridSystem.Instance != null && GridSystem.Instance.IsValidCell(cell);
            }

            public bool IsCellOccupied(Vector2Int cell, Guid ignoredUnitId = default)
            {
                if (_controller._simulationRuntime == null)
                    return false;
                if (!_controller._simulationRuntime.IsOccupied(cell))
                    return false;

                IReadOnlyList<IReadOnlyUnitRuntimeState> occupants = _controller._simulationRuntime.GetUnitsAtPosition(cell);
                if (occupants.Count == 0)
                    return false;

                if (ignoredUnitId != Guid.Empty)
                {
                    for (int i = 0; i < occupants.Count; i++)
                    {
                        if (occupants[i] != null && occupants[i].UnitId != ignoredUnitId)
                            return true;
                    }

                    return false;
                }

                return true;
            }

            public bool IsTargetInAttackRange(Guid attackerId, Guid targetId)
            {
                if (!TryGetUnit(attackerId, out BattleUnitSnapshot attacker))
                    return false;
                if (!TryGetUnit(targetId, out BattleUnitSnapshot target))
                    return false;
                if (!_controller._unitsById.TryGetValue(attackerId, out UnitBrain attackerBrain) || attackerBrain == null)
                    return false;

                int range = 1;
                if (attackerBrain.UnitData != null)
                    range = Mathf.Max(1, attackerBrain.UnitData.AttackRange);

                int distance = Mathf.Max(
                    Mathf.Abs(attacker.Position.x - target.Position.x),
                    Mathf.Abs(attacker.Position.y - target.Position.y));

                return distance <= range;
            }
        }

    }
}
