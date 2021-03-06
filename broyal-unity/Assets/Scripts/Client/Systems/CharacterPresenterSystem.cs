﻿using Bootstrappers;
using RemoteConfig;
using Scripts.Common;
using Scripts.Scenes.Client;
using Scripts.Scenes.Lobby.States;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
    private const float RotationSpeed = 20.0f;
    private const float MoveSpeed = 16.0f;
    private const float RunAnimationSmoothFactor = 2.0f;
    
    private EntityQuery _group;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Damage = Animator.StringToHash("Damage");
    private static readonly int AttackTrigger = Animator.StringToHash("AttackTrigger");
    private static readonly int DamageTrigger = Animator.StringToHash("DamageTrigger");
    private EntityQuery _otherPlayers;
    private static readonly int Health = Animator.StringToHash("Health");
    private static readonly int Death = Animator.StringToHash("Death");
    
    private static readonly int Type = Animator.StringToHash("Type");

    private MainConfig _config;
    private FXData _fxData;
    private UIController _uiController;

    private AppConfig _appConfig;
    private IGameMinimap gameMinimap = null;
    private IGameTopPanel gameTopPanel = null;
    private int initForMinimapForPlayers = 0;
    private bool gameOverIsShowed = false;
    
    protected override void OnCreate()
    {
        base.OnCreate();
        _appConfig = ClientBootstrapper.Container.Resolve<AppConfig>();
        _config = ClientBootstrapper.Container.Resolve<MainConfig>();
        _fxData = ClientBootstrapper.Container.Resolve<FXData>();
        _uiController = ClientBootstrapper.Container.Resolve<UIController>();
        
        _group = GetEntityQuery(
            ComponentType.ReadWrite<CharacterPresenter>(),
            ComponentType.ReadOnly<GameObject>(),
            ComponentType.ReadOnly<CharactersBindData>(),
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

    private void InitCharactersForMinimap( NativeArray<Entity> entities )
    {
        foreach ( var e in entities )
        {
            var isMyPlayer = EntityManager.HasComponent<PlayerInput>( e );
            var bindData = EntityManager.GetComponentObject<CharactersBindData>( e );

            if ( bindData.gameObject.GetComponent<EntityMinimapViewHelper>() == null )
            {
                var minimapCharHelper = bindData.gameObject.AddComponent<EntityMinimapViewHelper>();
                minimapCharHelper.SetEntityType( isMyPlayer ? MinimapEntityType.LocalCharacter : MinimapEntityType.OtherCharacter );
            }
        }
    }

    protected override void OnUpdate()
    {
        var deltaTime = Time.DeltaTime;
        var groupEntities = _group.ToEntityArray(Allocator.TempJob);
        var otherPlayer = _otherPlayers.ToEntityArray(Allocator.TempJob);

        if ( initForMinimapForPlayers < groupEntities.Length )
        {
            InitCharactersForMinimap( groupEntities );
            initForMinimapForPlayers = groupEntities.Length;
        }

        if ( gameMinimap == null ) { gameMinimap = GameObject.FindObjectOfType<GameMinimap>(); }
        if ( gameTopPanel == null ) { gameTopPanel = GameObject.FindObjectOfType<GameTopPanel>(); }

        foreach (var e in groupEntities)
        {
            var player = EntityManager.GetComponentData<PlayerData>(e);
            var isMyPlayer = EntityManager.HasComponent<PlayerInput>(e);
            var data = EntityManager.GetComponentData<CharacterPresenter>(e);
            var translation = EntityManager.GetComponentData<Translation>(e);
            
            var attack = EntityManager.GetComponentData<Attack>(e);
            var damage = EntityManager.GetComponentData<Damage>(e);

            var go = EntityManager.GetComponentObject<GameObject>(e);
            var bindData = EntityManager.GetComponentObject<CharactersBindData>(e);
            
            float3 attackDirection = new float3(attack.AttackDirection.x,0,attack.AttackDirection.y);
            var isAutoAttack = math.lengthsq(attackDirection) < 0.01f;
            
            float3 prevPos = go.transform.position;
            var dist = math.distance(prevPos, translation.Value);

            go.transform.position = Vector3.Lerp(go.transform.position, translation.Value, deltaTime * MoveSpeed);
            //go.transform.position = translation.Value;

            bindData.UpdateHealthBar(player.health / (float)player.maxHealth);

            if (isMyPlayer)
            {
                gameMinimap?.SetDeadZoneRadius( player.damageRadius );
                gameTopPanel?.SetCountdownValue( player.damageRadius );

                _fxData.SetDeadZoneRadius(player.damageRadius);
                _uiController.SetPlayerGo(go);
                _uiController.SetPlayerPosition(translation.Value);
                _uiController.GameUI.SetGems(player.GetItems());
                
                var tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;

                var input = EntityManager.GetBuffer<PlayerInput>(e);
                if (input.GetDataAtTick(tick - 1, out var pinput))
                {
                    var direction = new Vector2(pinput.horizontal,pinput.vertical);
                    var prevSpeed = bindData.Animator.GetFloat(Speed);
                    var newSpeed = direction.sqrMagnitude > 0.01 ? 1 : 0;
                    bindData.Animator.SetFloat(Speed, math.lerp(prevSpeed, newSpeed,  deltaTime * RunAnimationSmoothFactor) );
                }
                
                if (player.health <= 0.0f && ! gameOverIsShowed )
                {
                    _uiController.GameUI.HideBottom();
                    _uiController.GameOver.Setup( new Scripts.Scenes.Client.UI.RewardData() );
                    _uiController.GameOver.Show( () => { InitState.ShowBattleResult = true; SceneManager.LoadScene( Constants.SCENE_LOBBY ); } );
                    gameOverIsShowed = true;
                }
            }
            else
            {
                var prevSpeed = bindData.Animator.GetFloat(Speed);
                var newSpeed = dist > 0.1 ? 1 : 0;
                bindData.Animator.SetFloat(Speed, math.lerp(prevSpeed, newSpeed,  deltaTime * RunAnimationSmoothFactor));
            }
            
            var target = attack.Target;
            
            if ((attack.ProccesedId != 0 || attack.AttackType != 0) && attack.NeedApplyDamage == false)
            {
                if (data.AttackTransId != attack.Seed)
                {
                    var skillInfo = _appConfig.GetSkillByAttackType(attack.AttackType);
                    
                    Debug.Log($" [CharacterPresenter] {skillInfo.Id} " +
                              $"To => {attack.Target} => {attack.AttackType} =>" +
                              $"Direction => {attack.AttackDirection} =>" +
                              $" {data.AttackTransId} != {attack.Seed}");
                    
                    bindData.Animator.SetInteger(Type, _appConfig.GetSkillIndex(skillInfo));
                    bindData.Animator.SetTrigger(AttackTrigger);
                    data.AttackTransId = attack.Seed;
                    EntityManager.SetComponentData(e, data);

                    var fxDirection = attackDirection;
                    Transform targetTransform = target == Entity.Null
                        ? default
                        : EntityManager.GetComponentObject<GameObject>(target)?.transform;
                    
                    if (isAutoAttack)
                    {
                        if(target == Entity.Null) fxDirection = go.transform.forward;
                        else
                        {
                            fxDirection = EntityManager.GetComponentData<Translation>(target).Value - translation.Value;
                        }
                    }
   
                   // Debug.LogWarning($"Client:Attack To => {e} => {isAutoAttack} {target} {fxDirection}");
                    _fxData.Start(skillInfo, go, bindData.Weapom.transform, fxDirection, targetTransform );
                }//else  Debug.LogWarning($"Client:Attack To => {e} => {data.AttackTransId}{attack.Seed}");

                if (isAutoAttack)
                {
                    if (target != Entity.Null)
                    {
                        var newDirection = EntityManager.GetComponentData<Translation>(target).Value - translation.Value;
                        go.transform.forward = Vector3.Lerp(go.transform.forward, math.normalize(newDirection), RotationSpeed * Time.DeltaTime);
                    }
                }
                else
                {
                    go.transform.forward = Vector3.Lerp(go.transform.forward, attackDirection, RotationSpeed * deltaTime);
                }
            }
            else
            {
                if (dist > 0.01f)
                {
                    var direction = math.normalize(translation.Value - prevPos);
                    go.transform.forward = Vector3.Lerp(go.transform.forward, direction, RotationSpeed * deltaTime);
                }
            }

            if (damage.DamageType != 0 && data.DamageTransId != damage.Seed )
            {
                Debug.LogWarning($"Client:Damage To => {e} => {damage.DamageType}");
                bindData.Animator.SetTrigger(DamageTrigger);
                data.DamageTransId = damage.Seed;
                EntityManager.SetComponentData(e, data);
            }
            
            bindData.Animator.SetBool(Death, player.health <= 0.0f);
        }

        groupEntities.Dispose();
        otherPlayer.Dispose();
    }
}