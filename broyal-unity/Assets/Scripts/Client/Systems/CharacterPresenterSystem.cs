﻿using System.Linq;
using Bootstrappers;
using RemoteConfig;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public struct CharacterPresenter : IComponentData
{
    public int Id;
    public int AttackTransId;
    public int DamageTransId;
}
public struct Link : IComponentData
{
    public Entity entity;
}
public struct StateComponent : ISystemStateComponentData
{
    public int State;
}

[DisableAutoCreation]
public class CharacterPresenterSystem : ComponentSystem
{
    private const float RotationSpeed = 10.0f;

    private EntityQuery _group;
    private MainConfig _config;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Damage = Animator.StringToHash("Damage");
    private static readonly int AttackTrigger = Animator.StringToHash("AttackTrigger");
    private static readonly int DamageTrigger = Animator.StringToHash("DamageTrigger");
    private EntityQuery _otherPlayers;
    private static readonly int Health = Animator.StringToHash("Health");
    private static readonly int Death = Animator.StringToHash("Death");
    private MaterialPropertyBlock _matBlock;
    private static readonly int Fill = Shader.PropertyToID("_Fill");
    private static readonly int Type = Animator.StringToHash("Type");
    private AppConfig _appConfig => BaseBootStrapper.Container.Resolve<AppConfig>();
    private Session _session => BaseBootStrapper.Container.Resolve<Session>();
    private FXData _fxData => BaseBootStrapper.Container.Resolve<FXData>();
    private UIController _uiController => BaseBootStrapper.Container.Resolve<UIController>();
    protected override void OnCreate()
    {
        base.OnCreate();
        
        _matBlock = new MaterialPropertyBlock();

        _config = ClientBootstrapper.Container.Resolve<MainConfig>();

        _group = GetEntityQuery(
            ComponentType.ReadWrite<CharacterPresenter>(),
            ComponentType.ReadOnly<Animator>(),
            ComponentType.ReadOnly<GameObject>(),
            ComponentType.ReadOnly<MeshRenderer>(),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<Attack>(),
            ComponentType.ReadOnly<Damage>(),
            ComponentType.ReadOnly<PlayerData>(),
            ComponentType.Exclude<DEAD>()
        );

        _otherPlayers = GetEntityQuery(
            ComponentType.ReadWrite<PlayerData>(),
            ComponentType.ReadOnly<Damage>(),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.Exclude<PlayerInput>(),
            ComponentType.Exclude<DEAD>()
        );
    }

    protected override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;
        var groupEntities = _group.ToEntityArray(Allocator.TempJob);
        var otherPlayer = _otherPlayers.ToEntityArray(Allocator.TempJob);

        foreach (var e in groupEntities)
        {
            var player = EntityManager.GetComponentData<PlayerData>(e);

            _fxData.SetDeadZoneRadius(player.damageRadius);
            
            var data = EntityManager.GetComponentData<CharacterPresenter>(e);
            var translation = EntityManager.GetComponentData<Translation>(e);

            var go = EntityManager.GetComponentObject<GameObject>(e);
            var animator = EntityManager.GetComponentObject<Animator>(e);
            var healthBarRenderer = EntityManager.GetComponentObject<MeshRenderer>(e);
            
            healthBarRenderer.GetPropertyBlock(_matBlock);
            _matBlock.SetFloat(Fill, player.health / (float)player.maxHealth);
            healthBarRenderer.SetPropertyBlock(_matBlock);


            var prevPosition = new float3(go.transform.position);

            var dist = math.distance(prevPosition, translation.Value);
            var direction = math.normalize(translation.Value - prevPosition);

            animator.SetFloat(Speed, dist > 0.1f ? dist : 0.0f);

            go.transform.position = translation.Value;
            
            _uiController.SetPlayerPosition(go.transform.position);
            
            if (dist > 0.1f)
            {
                go.transform.forward = direction;
                //go.transform.forward = Vector3.Lerp(go.transform.forward, direction, RotationSpeed * deltaTime);
            }

            var attack = EntityManager.GetComponentData<Attack>(e);
            var damage = EntityManager.GetComponentData<Damage>(e);

            if (attack.ProccesedId != 0 || attack.AttackType != 0)
            {
                if (data.AttackTransId != attack.Seed)
                {
                    Debug.LogWarning($"Client:Attack To => {attack.Target} => {attack.AttackType} => {data.AttackTransId} != {attack.Seed}");
                    animator.SetInteger(Type, player.primarySkillId);
                    animator.SetTrigger(AttackTrigger);
                    data.AttackTransId = attack.Seed;
                    EntityManager.SetComponentData(e, data);

                    _fxData.Start(player.primarySkillId, go, go.transform.position, new Vector3(attack.AttackDirection.x,0, attack.AttackDirection.y));
                }//else  Debug.LogWarning($"Client:Attack To => {e} => {data.AttackTransId}{attack.Seed}");
                
                var target = attack.Target;
                if (target != Entity.Null && player.primarySkillId < 2 )
                {
                    var lookdirection = EntityManager.GetComponentData<Translation>(target).Value - translation.Value;
                    //go.transform.forward = Vector3.Lerp(go.transform.forward, math.normalize(lookdirection), RotationSpeed * Time.DeltaTime);

                    go.transform.forward = math.normalize(lookdirection);
                }
            }

            if (damage.DamageType != 0 && data.DamageTransId != damage.Seed )
            {
                Debug.LogWarning($"Client:Damage To => {e} => {damage.DamageType}");
                animator.SetTrigger(DamageTrigger);
                data.DamageTransId = damage.Seed;
                EntityManager.SetComponentData(e, data);
            }

            animator.SetBool(Death, player.health <= 0.0f);
        }

        groupEntities.Dispose();
        otherPlayer.Dispose();
    }
}


// if (attack.HaveTransition())
// {
// if (math.abs(data.TransId - attack.Seed) > 0.01f)
// {
//     //Debug.Log($"Client:Attack => {e} => {attack.Type} => {attack.TransHash}");
//     animator.SetTrigger(AttackTrigger);
//     data.TransId = attack.Seed;
//     EntityManager.SetComponentData(e, data);
// }
//
// if (math.abs(1.0f - attack.Type.x) < 0.01f)
// {
//     var distance = 10.0f;
//     var entity = otherPlayer.FirstOrDefault(x =>
//         math.distancesq(translation.Value, EntityManager.GetComponentData<Translation>(x).Value) < distance * 2);
//
//     if (entity != Entity.Null)
//     {
//         var lookdirection = EntityManager.GetComponentData<Translation>(entity).Value - translation.Value;
//         go.transform.forward = Vector3.Lerp(go.transform.forward, math.normalize(lookdirection), RotationSpeed * Time.DeltaTime);
//     }
// }
// }