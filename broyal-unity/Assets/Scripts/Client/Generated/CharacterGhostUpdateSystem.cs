using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Networking.Transport.Utilities;
using Unity.NetCode;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(GhostUpdateSystemGroup))]
public class CharacterGhostUpdateSystem : JobComponentSystem
{
    [BurstCompile]
    struct UpdateInterpolatedJob : IJobChunk
    {
        [ReadOnly] public NativeHashMap<int, GhostEntity> GhostMap;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [NativeDisableContainerSafetyRestriction] public NativeArray<uint> minMaxSnapshotTick;
#pragma warning disable 649
        [NativeSetThreadIndex]
        public int ThreadIndex;
#pragma warning restore 649
#endif
        [ReadOnly] public ArchetypeChunkBufferType<CharacterSnapshotData> ghostSnapshotDataType;
        [ReadOnly] public ArchetypeChunkEntityType ghostEntityType;
        public ArchetypeChunkComponentType<Attack> ghostAttackType;
        public ArchetypeChunkComponentType<Damage> ghostDamageType;
        public ArchetypeChunkComponentType<MovableCharacterComponent> ghostMovableCharacterComponentType;
        public ArchetypeChunkComponentType<PlayerData> ghostPlayerDataType;
        public ArchetypeChunkComponentType<PrefabCreator> ghostPrefabCreatorType;
        public ArchetypeChunkComponentType<Translation> ghostTranslationType;

        public uint targetTick;
        public float targetTickFraction;
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var deserializerState = new GhostDeserializerState
            {
                GhostMap = GhostMap
            };
            var ghostEntityArray = chunk.GetNativeArray(ghostEntityType);
            var ghostSnapshotDataArray = chunk.GetBufferAccessor(ghostSnapshotDataType);
            var ghostAttackArray = chunk.GetNativeArray(ghostAttackType);
            var ghostDamageArray = chunk.GetNativeArray(ghostDamageType);
            var ghostMovableCharacterComponentArray = chunk.GetNativeArray(ghostMovableCharacterComponentType);
            var ghostPlayerDataArray = chunk.GetNativeArray(ghostPlayerDataType);
            var ghostPrefabCreatorArray = chunk.GetNativeArray(ghostPrefabCreatorType);
            var ghostTranslationArray = chunk.GetNativeArray(ghostTranslationType);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var minMaxOffset = ThreadIndex * (JobsUtility.CacheLineSize/4);
#endif
            for (int entityIndex = 0; entityIndex < ghostEntityArray.Length; ++entityIndex)
            {
                var snapshot = ghostSnapshotDataArray[entityIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var latestTick = snapshot.GetLatestTick();
                if (latestTick != 0)
                {
                    if (minMaxSnapshotTick[minMaxOffset] == 0 || SequenceHelpers.IsNewer(minMaxSnapshotTick[minMaxOffset], latestTick))
                        minMaxSnapshotTick[minMaxOffset] = latestTick;
                    if (minMaxSnapshotTick[minMaxOffset + 1] == 0 || SequenceHelpers.IsNewer(latestTick, minMaxSnapshotTick[minMaxOffset + 1]))
                        minMaxSnapshotTick[minMaxOffset + 1] = latestTick;
                }
#endif
                // If there is no data found don't apply anything (would be default state), required for prespawned ghosts
                CharacterSnapshotData snapshotData;
                if (!snapshot.GetDataAtTick(targetTick, targetTickFraction, out snapshotData))
                    return;

                var ghostAttack = ghostAttackArray[entityIndex];
                var ghostDamage = ghostDamageArray[entityIndex];
                var ghostMovableCharacterComponent = ghostMovableCharacterComponentArray[entityIndex];
                var ghostPlayerData = ghostPlayerDataArray[entityIndex];
                var ghostPrefabCreator = ghostPrefabCreatorArray[entityIndex];
                var ghostTranslation = ghostTranslationArray[entityIndex];
                ghostAttack.AttackType = snapshotData.GetAttackAttackType(deserializerState);
                ghostAttack.Seed = snapshotData.GetAttackSeed(deserializerState);
                ghostAttack.AttackDirection = snapshotData.GetAttackAttackDirection(deserializerState);
                ghostAttack.Target = snapshotData.GetAttackTarget(deserializerState);
                ghostAttack.NeedApplyDamage = snapshotData.GetAttackNeedApplyDamage(deserializerState);
                ghostDamage.DamageType = snapshotData.GetDamageDamageType(deserializerState);
                ghostMovableCharacterComponent.PlayerId = snapshotData.GetMovableCharacterComponentPlayerId(deserializerState);
                ghostPlayerData.health = snapshotData.GetPlayerDatahealth(deserializerState);
                ghostPlayerData.primarySkillId = snapshotData.GetPlayerDataprimarySkillId(deserializerState);
                ghostPlayerData.maxHealth = snapshotData.GetPlayerDatamaxHealth(deserializerState);
                ghostPlayerData.power = snapshotData.GetPlayerDatapower(deserializerState);
                ghostPlayerData.magic = snapshotData.GetPlayerDatamagic(deserializerState);
                ghostPlayerData.damageRadius = snapshotData.GetPlayerDatadamageRadius(deserializerState);
                ghostPlayerData.inventory = snapshotData.GetPlayerDatainventory(deserializerState);
                ghostPlayerData.attackSkillId = snapshotData.GetPlayerDataattackSkillId(deserializerState);
                ghostPlayerData.defenceSkillId = snapshotData.GetPlayerDatadefenceSkillId(deserializerState);
                ghostPlayerData.utilsSkillId = snapshotData.GetPlayerDatautilsSkillId(deserializerState);
                ghostPlayerData.speedMod = snapshotData.GetPlayerDataspeedMod(deserializerState);
                ghostPlayerData.stun = snapshotData.GetPlayerDatastun(deserializerState);
                ghostPrefabCreator.NameId = snapshotData.GetPrefabCreatorNameId(deserializerState);
                ghostPrefabCreator.SkinId = snapshotData.GetPrefabCreatorSkinId(deserializerState);
                ghostPrefabCreator.SkinSetting = snapshotData.GetPrefabCreatorSkinSetting(deserializerState);
                ghostTranslation.Value = snapshotData.GetTranslationValue(deserializerState);
                ghostAttackArray[entityIndex] = ghostAttack;
                ghostDamageArray[entityIndex] = ghostDamage;
                ghostMovableCharacterComponentArray[entityIndex] = ghostMovableCharacterComponent;
                ghostPlayerDataArray[entityIndex] = ghostPlayerData;
                ghostPrefabCreatorArray[entityIndex] = ghostPrefabCreator;
                ghostTranslationArray[entityIndex] = ghostTranslation;
            }
        }
    }
    [BurstCompile]
    struct UpdatePredictedJob : IJobChunk
    {
        [ReadOnly] public NativeHashMap<int, GhostEntity> GhostMap;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [NativeDisableContainerSafetyRestriction] public NativeArray<uint> minMaxSnapshotTick;
#endif
#pragma warning disable 649
        [NativeSetThreadIndex]
        public int ThreadIndex;
#pragma warning restore 649
        [NativeDisableParallelForRestriction] public NativeArray<uint> minPredictedTick;
        [ReadOnly] public ArchetypeChunkBufferType<CharacterSnapshotData> ghostSnapshotDataType;
        [ReadOnly] public ArchetypeChunkEntityType ghostEntityType;
        public ArchetypeChunkComponentType<PredictedGhostComponent> predictedGhostComponentType;
        public uint targetTick;
        public uint lastPredictedTick;
        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var deserializerState = new GhostDeserializerState
            {
                GhostMap = GhostMap
            };
            var ghostEntityArray = chunk.GetNativeArray(ghostEntityType);
            var ghostSnapshotDataArray = chunk.GetBufferAccessor(ghostSnapshotDataType);
            var predictedGhostComponentArray = chunk.GetNativeArray(predictedGhostComponentType);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var minMaxOffset = ThreadIndex * (JobsUtility.CacheLineSize/4);
#endif
            for (int entityIndex = 0; entityIndex < ghostEntityArray.Length; ++entityIndex)
            {
                var snapshot = ghostSnapshotDataArray[entityIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var latestTick = snapshot.GetLatestTick();
                if (latestTick != 0)
                {
                    if (minMaxSnapshotTick[minMaxOffset] == 0 || SequenceHelpers.IsNewer(minMaxSnapshotTick[minMaxOffset], latestTick))
                        minMaxSnapshotTick[minMaxOffset] = latestTick;
                    if (minMaxSnapshotTick[minMaxOffset + 1] == 0 || SequenceHelpers.IsNewer(latestTick, minMaxSnapshotTick[minMaxOffset + 1]))
                        minMaxSnapshotTick[minMaxOffset + 1] = latestTick;
                }
#endif
                CharacterSnapshotData snapshotData;
                snapshot.GetDataAtTick(targetTick, out snapshotData);

                var predictedData = predictedGhostComponentArray[entityIndex];
                var lastPredictedTickInst = lastPredictedTick;
                if (lastPredictedTickInst == 0 || predictedData.AppliedTick != snapshotData.Tick)
                    lastPredictedTickInst = snapshotData.Tick;
                else if (!SequenceHelpers.IsNewer(lastPredictedTickInst, snapshotData.Tick))
                    lastPredictedTickInst = snapshotData.Tick;
                if (minPredictedTick[ThreadIndex] == 0 || SequenceHelpers.IsNewer(minPredictedTick[ThreadIndex], lastPredictedTickInst))
                    minPredictedTick[ThreadIndex] = lastPredictedTickInst;
                predictedGhostComponentArray[entityIndex] = new PredictedGhostComponent{AppliedTick = snapshotData.Tick, PredictionStartTick = lastPredictedTickInst};
                if (lastPredictedTickInst != snapshotData.Tick)
                    continue;

            }
        }
    }
    private ClientSimulationSystemGroup m_ClientSimulationSystemGroup;
    private GhostPredictionSystemGroup m_GhostPredictionSystemGroup;
    private EntityQuery m_interpolatedQuery;
    private EntityQuery m_predictedQuery;
    private GhostUpdateSystemGroup m_GhostUpdateSystemGroup;
    private uint m_LastPredictedTick;
    protected override void OnCreate()
    {
        m_GhostUpdateSystemGroup = World.GetOrCreateSystem<GhostUpdateSystemGroup>();
        m_ClientSimulationSystemGroup = World.GetOrCreateSystem<ClientSimulationSystemGroup>();
        m_GhostPredictionSystemGroup = World.GetOrCreateSystem<GhostPredictionSystemGroup>();
        m_interpolatedQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new []{
                ComponentType.ReadWrite<CharacterSnapshotData>(),
                ComponentType.ReadOnly<GhostComponent>(),
                ComponentType.ReadWrite<Attack>(),
                ComponentType.ReadWrite<Damage>(),
                ComponentType.ReadWrite<MovableCharacterComponent>(),
                ComponentType.ReadWrite<PlayerData>(),
                ComponentType.ReadWrite<PrefabCreator>(),
                ComponentType.ReadWrite<Translation>(),
            },
            None = new []{ComponentType.ReadWrite<PredictedGhostComponent>()}
        });
        m_predictedQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new []{
                ComponentType.ReadOnly<CharacterSnapshotData>(),
                ComponentType.ReadOnly<GhostComponent>(),
                ComponentType.ReadOnly<PredictedGhostComponent>(),
            }
        });
        RequireForUpdate(GetEntityQuery(ComponentType.ReadWrite<CharacterSnapshotData>(),
            ComponentType.ReadOnly<GhostComponent>()));
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var ghostEntityMap = m_GhostUpdateSystemGroup.GhostEntityMap;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var ghostMinMaxSnapshotTick = m_GhostUpdateSystemGroup.GhostSnapshotTickMinMax;
#endif
        if (!m_predictedQuery.IsEmptyIgnoreFilter)
        {
            var updatePredictedJob = new UpdatePredictedJob
            {
                GhostMap = ghostEntityMap,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                minMaxSnapshotTick = ghostMinMaxSnapshotTick,
#endif
                minPredictedTick = m_GhostPredictionSystemGroup.OldestPredictedTick,
                ghostSnapshotDataType = GetArchetypeChunkBufferType<CharacterSnapshotData>(true),
                ghostEntityType = GetArchetypeChunkEntityType(),
                predictedGhostComponentType = GetArchetypeChunkComponentType<PredictedGhostComponent>(),

                targetTick = m_ClientSimulationSystemGroup.ServerTick,
                lastPredictedTick = m_LastPredictedTick
            };
            m_LastPredictedTick = m_ClientSimulationSystemGroup.ServerTick;
            if (m_ClientSimulationSystemGroup.ServerTickFraction < 1)
                m_LastPredictedTick = 0;
            inputDeps = updatePredictedJob.Schedule(m_predictedQuery, JobHandle.CombineDependencies(inputDeps, m_GhostUpdateSystemGroup.LastGhostMapWriter));
            m_GhostPredictionSystemGroup.AddPredictedTickWriter(inputDeps);
        }
        if (!m_interpolatedQuery.IsEmptyIgnoreFilter)
        {
            var updateInterpolatedJob = new UpdateInterpolatedJob
            {
                GhostMap = ghostEntityMap,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                minMaxSnapshotTick = ghostMinMaxSnapshotTick,
#endif
                ghostSnapshotDataType = GetArchetypeChunkBufferType<CharacterSnapshotData>(true),
                ghostEntityType = GetArchetypeChunkEntityType(),
                ghostAttackType = GetArchetypeChunkComponentType<Attack>(),
                ghostDamageType = GetArchetypeChunkComponentType<Damage>(),
                ghostMovableCharacterComponentType = GetArchetypeChunkComponentType<MovableCharacterComponent>(),
                ghostPlayerDataType = GetArchetypeChunkComponentType<PlayerData>(),
                ghostPrefabCreatorType = GetArchetypeChunkComponentType<PrefabCreator>(),
                ghostTranslationType = GetArchetypeChunkComponentType<Translation>(),
                targetTick = m_ClientSimulationSystemGroup.InterpolationTick,
                targetTickFraction = m_ClientSimulationSystemGroup.InterpolationTickFraction
            };
            inputDeps = updateInterpolatedJob.Schedule(m_interpolatedQuery, JobHandle.CombineDependencies(inputDeps, m_GhostUpdateSystemGroup.LastGhostMapWriter));
        }
        return inputDeps;
    }
}
public partial class CharacterGhostSpawnSystem : DefaultGhostSpawnSystem<CharacterSnapshotData>
{
}
