using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime.Injection;
using Mimimi.Animation;
using LibSM64;
using Il2CppInterop.Runtime.Attributes;

using Vector3NET = System.Numerics.Vector3;

using PirateBase;

namespace ShadowMario;

internal class MarioStateSyncer : ModSaveable
{
    public MiCharacter m_character;

    public Collider m_hitbox;
    public Collider m_hurtbox;

    public float m_hue = 0;

    private SM64Mario m_mario;
    private MarioInputProvider m_inputProvider;

    public SM64MarioAction m_lastAction;
    private SM64MarioFlag m_lastFlags;
    private short m_lastHealth;
    private Vector3 m_lastVelocity;
    private Vector3 m_lastPosition;
    private float m_lastFaceAngle;

    private MarioState m_state;

    private MarioCap m_cap;
    private float m_capDuration;
    private float m_capStartTime = -1;
    private MarioCap m_capOnDoor;

    private MiCharacter m_spinningCharacter;
    private float m_lastSpinDiff;
    private bool m_spinningCharHideable;

    private int m_warpFrame = -1;
    private int m_transferMarioDamageFrame = -1;
    private int m_portraitDelayFrames = 0;

    private Dictionary<System.IntPtr, float> m_knockedOutCharsDict = new();

    private Sprite m_spritePortrait;
    private Sprite m_spriteNormal;
    private MiAnimAction m_groundPoundAction;
    private MiAnimAction m_diveKillAction;

    private MiCharacter.MiCharacterEvent m_onCharDamaged;
    private MiCharacter.MiCharacterEvent m_onCharDeath;
    private MiCharacter.MiCharacterEvent m_onCharRevived;

    private const int c_totalPortraitDelayFrames = 16;

    public static readonly int marioColliderMask = (1 << (int)MiLayer.Walkable) | (1 << (int)MiLayer.Terrain) | (1 << (int)MiLayer.Occluder);

    public static readonly int marioLayer = (int)MiLayer.TransparentFX;
    public static readonly int objLayer = (int)MiLayer.NavMeshConnections;

    public const MiCharacterTarget.Modifier c_shootCharacterTargetModifier = (MiCharacterTarget.Modifier)(1 << 31);

    public enum MarioState { Normal, Door, Spinning }
    public enum MarioCap { Normal, Metal, Wing }

    [HideFromIl2Cpp]
    public SM64Mario mario => m_mario;
    [HideFromIl2Cpp]
    public MarioInputProvider inputProvider => m_inputProvider;

    private Transform m_cachedCharacterTransform;
    public Transform charTrans
    {
        get
        {
            if (m_cachedCharacterTransform == null && m_character != null)
            {
                m_cachedCharacterTransform = m_character.transform;
            }

            return m_cachedCharacterTransform;
        }
    }


    public MarioStateSyncer(System.IntPtr ptr) : base(ptr) { }

    public MarioStateSyncer() : base(ClassInjector.DerivedConstructorPointer<MarioStateSyncer>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public override void AwakeDelayed()
    {
        init();
    }

    public override void OnDestroy()
    {
        if (MarioSceneHandler.instance != null)
            MarioSceneHandler.instance.UnregisterMario(this);
        unregisterCallbacks();
        if (m_mario != null)
            Object.Destroy(m_mario.gameObject);
    }

    [HideFromIl2Cpp]
    protected override void serializeMod(ref ModSerializeHelper _helper)
    {
        base.serializeMod(ref _helper);

        _helper.SerializeUnityObject(0, m_character);
        _helper.SerializeUnityObject(1, m_hitbox);
        _helper.SerializeUnityObject(2, m_hurtbox);

        _helper.Serialize(3, (uint)m_lastAction);
        _helper.Serialize(4, (uint)m_lastFlags);
        _helper.Serialize(5, m_lastVelocity);
        _helper.Serialize(6, m_hue);

        _helper.Serialize(7, (int)m_state);
        _helper.Serialize(8, m_lastPosition);

        _helper.Serialize(9, (int)m_cap);
        _helper.Serialize(10, m_capDuration);
        _helper.Serialize(11, m_capStartTime);

        _helper.SerializeUnityObject(12, m_spinningCharacter);
        _helper.Serialize(13, m_spinningCharHideable);

        _helper.Serialize(14, (int)m_capOnDoor);
    }

    [HideFromIl2Cpp]
    protected override void deserializeMod(ref ModDeserializeHelper _helper)
    {
        base.deserializeMod(ref _helper);

        _helper.DeserializeUnityObject(0, ref m_character);
        _helper.DeserializeUnityObject(1, ref m_hitbox);
        _helper.DeserializeUnityObject(2, ref m_hurtbox);

        if (_helper.dictFields.TryGetValue(3, out var value3))
            m_lastAction = (SM64MarioAction)value3.Unbox<uint>();
        if (_helper.dictFields.TryGetValue(4, out var value4))
            m_lastFlags = (SM64MarioFlag)value4.Unbox<uint>();
        _helper.Deserialize(5, ref m_lastVelocity);
        _helper.Deserialize(6, ref m_hue);

        if (_helper.dictFields.TryGetValue(7, out var value7))
            m_state = (MarioState)value7.Unbox<int>();
        _helper.Deserialize(8, ref m_lastPosition);

        if (_helper.dictFields.TryGetValue(9, out var value9))
            m_cap = (MarioCap)value9.Unbox<int>();
        _helper.Deserialize(10, ref m_capDuration);
        _helper.Deserialize(11, ref m_capStartTime);

        _helper.DeserializeUnityObject(12, ref m_spinningCharacter);
        _helper.Deserialize(13, ref m_spinningCharHideable);

        if (_helper.dictFields.TryGetValue(14, out var value14))
            m_capOnDoor = (MarioCap)value14.Unbox<int>();
    }

    public override void OnLoadingFinished()
    {
        base.OnLoadingFinished();

        clearTargetCharSpinning();

        // delaying this so that MarioSurfaceProperties has time to deserialize
        GameEvents.RunNextFixedUpdate(init);

        // delaying this so that mario is already properly initialized when we restore action/velocity
        // TODO i don't think this actually works
        var lastAction = actionCanBeRestored(m_lastAction) ? m_lastAction : SM64MarioAction.IDLE;
        var lastVelocity = m_lastVelocity;
        GameEvents.RunNextFixedUpdate(() => restoreFromLoad(lastAction, lastVelocity), _updateCount: 3);
    }

    private void Update()
    {
        if (ModUpdate.shouldSkipUpdate)
            return;

        if (m_character == null || m_mario == null)
            return;

        if (m_state == MarioState.Normal || m_state == MarioState.Spinning)
        {
            if (m_state == MarioState.Spinning)
            {
                handleSpin();
            }

            handleInput();
            applyTeleportToMario();
            applyToCharacter();
            applyToMario();

        }
        else if (m_state == MarioState.Door)
        {
            handleExitDoor();
        }
    }

    private void FixedUpdate()
    {
        if (ModUpdate.shouldSkipUpdate)
            return;

        if (m_character == null || m_mario == null)
            return;

        // at start, wait for a few frames, take a screenshot of mario, then unlock input
        if (m_spritePortrait == null)
        {
            if (m_portraitDelayFrames < c_totalPortraitDelayFrames)
            {
                m_portraitDelayFrames++;
            }
            else
            {
                m_spritePortrait = PortraitGenerator.Generate(new Vector2Int(512, 512), m_mario);
                if (m_spritePortrait != null)
                {
                    m_spriteNormal = m_spritePortrait;
                    m_inputProvider.isLocked = false;
                }
            }
        }
    }

    [HideFromIl2Cpp]
    public void OnBeforeSave()
    {
        unregisterCallbacks();
    }

    [HideFromIl2Cpp]
    public void OnAfterSave()
    {
        registerCallbacks();
    }


    [HideFromIl2Cpp]
    private void handleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            m_character.processes.bStart(new MiCharacterProcessOptionsRevive(null, m_character, Skill.SkillType.None));
            if (Input.GetKey(KeyCode.LeftShift))
            {
                m_mario.enabled = false;
                m_mario.enabled = true;
            }
            else
            {
                m_mario.setHealth(2176);
                m_mario.setAction(SM64MarioAction.IDLE);
            }
        }
    }

    public static bool Spawn()
    {
        if (MiGameInput.bInstanceExists)
        {
            foreach (var character in MiGameInput.instance.liPlayableCharacter)
            {
                if (character.controllerPlayer.bLeader)
                    return Spawn(character);
            }
        }

        return false;
    }

    public static bool Spawn(MiCharacter _character)
    {
        if (CharIsMario(_character))
            return false;

        Plugin.PluginLog.LogInfo("Spawning Mario...");

        MarioSceneSaveHandler.Init();

        var marioSyncerGo = new GameObject("mario_state_syncer");
        marioSyncerGo.SetActive(false);
        marioSyncerGo.layer = marioLayer;
        marioSyncerGo.transform.SetParent(_character.transform, false);

        var marioSyncer = marioSyncerGo.AddComponent<MarioStateSyncer>();
        marioSyncer.m_character = _character;
        marioSyncer.m_hue = MarioSceneSaveHandler.instance.GetMarioHue();

        var rigidBody = marioSyncerGo.AddComponent<Rigidbody>();
        rigidBody.isKinematic = true;

        var hitbox = marioSyncerGo.AddComponent<CapsuleCollider>();
        hitbox.isTrigger = true;
        hitbox.center = new Vector3(0, 1.5f / 2, 0.2f);
        hitbox.radius = 0.7f;
        hitbox.height = 1.5f;
        hitbox.direction = 1;
        marioSyncer.m_hitbox = hitbox;

        var hurtbox = marioSyncerGo.AddComponent<CapsuleCollider>();
        hurtbox.isTrigger = true;
        hurtbox.center = new Vector3(0, 1.5f / 2, 0);
        hurtbox.radius = 0.15f;
        hurtbox.height = 1.5f;
        hurtbox.direction = 1;
        marioSyncer.m_hurtbox = hurtbox;

        var entityRef = marioSyncerGo.AddComponent<EntityReference>(); // so other game code doesn't complain on collisions
        entityRef.m_arEntities = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Entity>(new[] { _character });

        marioSyncerGo.SetActive(true);

        return true;
    }

    public static bool CharIsMario(MiCharacter _character)
    {
        return GetCharMario(_character) != null;
    }

    public static MarioStateSyncer GetCharMario(MiCharacter _character)
    {
        return _character.GetComponentInChildren<MarioStateSyncer>();
    }

    [HideFromIl2Cpp]
    private void init()
    {
        if (m_character == null || m_mario != null)
            return;

        MarioSceneHandler.Init();
        MarioSceneSaveHandler.Init();
        MarioSceneHandler.instance.RegisterMario(this);

        var marioGo = new GameObject($"mario_{m_character.m_characterData.m_eCharacterName}");
        marioGo.SetActive(false);

        m_mario = marioGo.AddComponent<SM64Mario>();
        m_mario.material = MarioResources.LoadMarioMaterial();
        if (m_lastPosition != Vector3.zero)
            m_mario.transform.position = m_lastPosition + new Vector3(0, 0.1f, 0);
        else
            m_mario.transform.position = charTrans.position + new Vector3(0, 2, 0);
        m_mario.transform.rotation = charTrans.rotation;

        registerCallbacks();

        m_inputProvider = new MarioInputProvider(m_character);
        m_inputProvider.isLocked = true;
        m_mario.inputProvider = m_inputProvider;
        m_mario.hue = m_hue;

        var waterController = marioGo.AddComponent<WaterController>();
        waterController.m_mario = m_mario;
        waterController.m_setToOceanHeight = true;

        m_groundPoundAction = MiAnimActionList.Get("KeyJumpKill_health");
        m_diveKillAction = MiAnimActionList.Get("KeyHealth_die_by_explosion");

        FakeShadow.Spawn(marioGo.transform, _scaleByDistance: true);

        marioGo.SetActive(true);

        if (m_state == MarioState.Door)
            m_mario.enabled = false;
    }

    [HideFromIl2Cpp]
    private void restoreFromLoad(SM64MarioAction _action, Vector3 _velocity)
    {
        if (m_character == null || m_mario == null)
            return;

        m_mario.setAction(_action);
        m_mario.setVelocity(_velocity.ToNET());

        if (m_character.health.bDead())
        {
            m_mario.kill();
        }
        else
        {
            restoreCap(m_cap);
        }
    }

    private void restoreCap(MarioCap _cap, bool _forceActivate = false)
    {
        float remainingCapTime = m_capDuration - (MiGameTime.timeSaved - m_capStartTime);
        if (remainingCapTime <= 0 && _forceActivate)
            remainingCapTime = 1f;

        if (_cap != MarioCap.Normal && remainingCapTime > 0)
            GiveCap(_cap, remainingCapTime, true);
    }

    [HideFromIl2Cpp]
    private void registerCallbacks()
    {
        if (m_character != null)
        {
            m_onCharDamaged = (MiCharacter.MiCharacterEvent)onCharDamaged;
            m_character.m_evOnDamage.AddIfNotContained(m_onCharDamaged);
            m_onCharDeath = (MiCharacter.MiCharacterEvent)onCharDeath;
            m_character.m_evOnDeathInstant.AddIfNotContained(m_onCharDeath);
            m_onCharRevived = (MiCharacter.MiCharacterEvent)onCharRevived;
            m_character.m_evOnRevive.AddIfNotContained(m_onCharRevived);
        }
    }

    [HideFromIl2Cpp]
    private void unregisterCallbacks()
    {
        if (m_character != null)
        {
            m_character.m_evOnDamage.Remove(m_onCharDamaged);
            m_onCharDamaged = null;
            m_character.m_evOnDeathInstant.Remove(m_onCharDeath);
            m_onCharDeath = null;
            m_character.m_evOnRevive.Remove(m_onCharRevived);
            m_onCharRevived = null;
        }
    }

    [HideFromIl2Cpp]
    private void applyToCharacter()
    {
        var currentAction = m_mario.action;
        var currentFlags = m_mario.flags;
        short currentHealth = m_mario.health;
        Vector3 currentPosition = m_mario.transform.position;

        setPreventSkills(true);
        m_character.eSpeakerID = new MiEnumValue<SpeakerIDEnum>(0);
        m_character.vis.setRenderersEnabled(false, MiCharacterVis.RendererDisableReason.Command);
        m_character.navigation.setNavMovementActive(false, MiCharNavigation.NavMeshMovementDisableReason.Command);
        m_character.animations.setAnimatorEnabled(false, MiCharacterAnimation.AnimatorDisableReason.Ragdoll);
        m_character.controllerPlayer.setControllerPlayerModifier(true, MiCharacterControllerPlayer.ControllerPlayerModifierFlags.PreventJumpController);
        // cant use ControllerPlayerModifierFlags.PreventCrouching, because we want character to be crouching for detection

        m_character.health.setImmortalReason(flagHasMetalCap(currentFlags), MiCharacterHealth.ImmortalReason.Remnant);

        m_character.setInvisibleReason((currentAction & SM64MarioAction.FLAG_SWIMMING) != 0, Entity.InvisibleReason.InWater);

        var movementStyle = MiCharacterMovementType.MovementStyle.Crouch;
        if ((currentAction & SM64MarioAction.FLAG_AIR) != 0)
            movementStyle = MiCharacterMovementType.MovementStyle.Walk;
        else if (!Plugin.PluginConfig.gameplay.alwaysDetectAsCrouched.Value && !actionIsCrawling(currentAction))
            movementStyle = MiCharacterMovementType.MovementStyle.Walk;
        m_character.movementType.setMovementType(movementStyle, MiCharacterMovementType.MovementModifier.None,
            MiCharacterMovementType.SetMovementTypeOptions.Create(_bBlendIn: false, _bSendEvents: false, _bSkipSound: true));

        if (m_character.inventory.tryGetItem(MiCharacterInventory.ItemType.GunAmmo, out var itemInfo))
        {
            // so we can open weapon crates forever
            itemInfo.m_iMax = new DifficultyValueEx<BalancedInt>(new BalancedInt(itemInfo.iCount + 1));
        }

        if ((currentAction & SM64MarioAction.FLAG_SWIMMING) != 0 && (m_lastAction & SM64MarioAction.FLAG_SWIMMING) == 0)
        {
            MiPoolEventsIParamContainer.instance.handleObjectThreadSave(
                MarioSGAssets.vfxPlyJumpIntoWater, transform, m_character.vfxUtility.Cast<MiAnimEventObj.IParamContainer>());
        }

        if (currentHealth != 0 && (m_lastHealth - currentHealth) > 30 && !m_character.health.bDead())
        {
            m_transferMarioDamageFrame = MiGameTime.frameCountSaved;
            var damageOptions = ModCharacterUtility.CreateDamageOptions(1, m_character, Skill.SkillType.Kill,
                _fDurationVisible: 1, _v3InDirection: Vector3.right); // setting nullables to avoid random exceptions
            m_character.impulseHandler.damage(damageOptions);
        }

        charTrans.position = currentPosition;
        m_character.setPosition(currentPosition);
        Vector3 forwardDir = Quaternion.Euler(0, m_mario.faceAngle * Mathf.Rad2Deg, 0) * Vector3.left;
        m_character.setForward(forwardDir);
        charTrans.forward = forwardDir;

        if (actionIsAttacking(currentAction))
        {
            if (m_hitbox != null)
                m_hitbox.enabled = true;
            if (m_hurtbox != null)
                m_hurtbox.enabled = false;
        }
        else
        {
            if (m_hitbox != null)
                m_hitbox.enabled = false;
            if (m_hurtbox != null)
                m_hurtbox.enabled = true;
        }

        if (currentAction == SM64MarioAction.GROUND_POUND_LAND && m_lastAction != SM64MarioAction.GROUND_POUND_LAND
            && Plugin.PluginConfig.gameplay.groundPoundNoiseRadius.Value > 0)
        {
            var noiseSettings = ModCharacterUtility.CreateNoiseSettings(m_character);
            noiseSettings.emitDuration = new BalancedFloat(0.5f);
            noiseSettings.emitHeight = new BalancedFloat(4);
            noiseSettings.emitRadius = new BalancedFloat(Plugin.PluginConfig.gameplay.groundPoundNoiseRadius.Value);
            if (Plugin.PluginConfig.gameplay.investigateGroundPoundNoise.Value)
                noiseSettings.SetNoiseType(NoiseDetection.NoiseType.Suspicious);
            else
                noiseSettings.SetNoiseType(NoiseDetection.NoiseType.SuspiciousNoInvestigate);

            m_character.noise.emit(MiCharacterImpulseHandler.ImpulseOptionsEmitNoise.Create(m_character, Skill.SkillType.None, noiseSettings));
        }

        MiLocalization.s_dicLocalizedTextInEnglish.TryAdd(64, "Mario");
        m_character.uiData.m_lstrFullName = new MiLocaString(64);
        m_character.uiData.lstrName = new MiLocaString(64);
        m_character.uiData.m_strModdedName = "Mario";

        overrideSpriteHolder(m_spritePortrait, m_character.uiData.spriteSet.m_portrait);
        overrideSpriteHolder(m_spriteNormal, m_character.uiData.spriteSet.m_normal);

        m_lastAction = currentAction;
        m_lastFlags = currentFlags;
        m_lastHealth = currentHealth;
        m_lastVelocity = m_mario.velocity.ToIL2CPP();
        m_lastPosition = currentPosition;
        m_lastFaceAngle = m_mario.faceAngle;
    }

    [HideFromIl2Cpp]
    private void overrideSpriteHolder(Sprite _sprite, MiSpriteHolderBase _spriteHolder)
    {
        if (_sprite == null)
            return;

        var spriteHolder = _spriteHolder.TryCast<MiSpriteHolder>();
        var spriteHolderContainer = _spriteHolder.TryCast<MiSpriteHolderContainer>();
        if (spriteHolder != null)
        {
            spriteHolder.m_sprite = _sprite;
            var spriteHolderWithBig = _spriteHolder.TryCast<MiSpriteHolderWithBigVersion>();
            if (spriteHolderWithBig != null)
                spriteHolderWithBig.m_spriteBig = _sprite;
        }
        else if (spriteHolderContainer != null)
        {
            foreach (var sp in spriteHolderContainer.m_arMiSpriteHolders)
                overrideSpriteHolder(_sprite, sp);
        }
    }

    [HideFromIl2Cpp]
    private void applyTeleportToMario()
    {
        if (m_character.navigation.lastWarpFrame >= m_warpFrame)
        {
            m_warpFrame = m_character.navigation.lastWarpFrame + 1;
            m_mario.setPosition(charTrans.position.ToNET());
            m_mario.setAngle(new Vector3NET(0, charTrans.rotation.eulerAngles.y * Mathf.Deg2Rad, 0));
            m_mario.setAction(SM64MarioAction.IDLE);
        }
    }

    [HideFromIl2Cpp]
    private void applyToMario()
    {
        if (m_character.health.bAlive())
        {
            m_mario.setHealth(2176);
        }

        if (m_character.health.hasCondition(MiCharacterHealth.HealthCondition.Frozen)
            && m_mario.action != SM64MarioAction.SHOCKED)
        {
            m_mario.setAction(SM64MarioAction.SHOCKED);
        }

        if ((m_mario.action == SM64MarioAction.START_SLEEPING || m_mario.action == SM64MarioAction.SLEEPING)
            && MarioSceneHandler.instance.leaderMario != this)
        {
            // prevent non-leader mario from sleeping, because his snoring gets annoying
            m_mario.setAction(SM64MarioAction.IDLE);
        }
    }

    private void onCharDamaged(MiCharacter _char)
    {
        if (m_transferMarioDamageFrame == MiGameTime.frameCountSaved)
            return; // don't apply damage that we ourselves just transfered from mario

        Vector3 pos = charTrans.position - m_character.vfxUtility.v3InDirection;
        m_mario.takeDamage(1, 0, pos.ToNET());
    }

    private void onCharDeath(MiCharacter _char)
    {
        //Vector3 pos = charTrans.position - m_character.vfxUtility.v3InDirection;
        m_mario.kill();
    }

    private void onCharRevived(MiCharacter _char)
    {
        m_mario.setHealth(2176);
        m_mario.setAction(SM64MarioAction.WAKING_UP);
        unregisterCallbacks();
        registerCallbacks();
    }

    public override void MiOnTriggerEnter(Collider _col)
    {
        var entityRef = _col.GetComponent<EntityReference>();
        if (entityRef == null)
            return;

        if (m_character.health.bDead())
            return;

        if (m_state != MarioState.Normal)
            return;

        var action = m_mario.action;
        var flags = m_mario.flags;
        bool isAirBonk = actionIsAirBonk(action, m_mario);
        bool isAttacking = actionIsAttacking(action);

        if (entityRef.get(out MiCharacter character) && character != m_character && !character.m_bSpawnLater && character.m_eCharacter != MiCharacterType.CivilianRemnant)
        {
            if (!character.health.bIncapacitated())
            {
                if (isAttacking || isAirBonk)
                {
                    if (flagIsPoweredUp(flags))
                    {
                        float throwDistance = Plugin.PluginConfig.gameplay.throwDistanceMetalMario.Value;
                        bool doThrow = throwDistance > 0 && flagHasMetalCap(flags) && !isAirBonk;
                        damageCharacter(character, action, false, false, _skipAnimation: doThrow);
                        if (doThrow)
                        {
                            Vector3 dir = (character.transform.position - charTrans.position).normalized;
                            Vector3 shootTargetPos = character.transform.position + dir * throwDistance;
                            tryThrowCharacter(character, shootTargetPos);
                        }

                    }
                    else
                        knockoutCharacter(character, action);

                    doAttackReaction(action, flags);
                }
                else
                {
                    if (character.m_eCharacter.bIsEnemy() && character.m_eCharacter != MiCharacterType.EnyCustodes)
                    {
                        getBumpedByCharacter(character);
                    }
                }
            }
            else if (character.health.bKnockedOut())
            {
                if (m_knockedOutCharsDict.TryGetValue(character.Pointer, out float knockoutTime) && (MiGameTime.timeSaved - knockoutTime) < 0.3f)
                    return; // prevent groundpound from knocking out and then killing characters immediately

                if (actionIsGroundPound(action) || actionIsSlide(action))
                {
                    damageCharacter(character, action, false, true);
                }
                else if (actionIsPunching(action))
                {
                    tryStartSpinningCharacter(character);
                }
            }
            else if (character.health.bDeadFinal() && !character.processes.bIsExecuting(MiCharacterProcess.Type.Revive) && character.m_eCharacter.bIsPlayer())
            {
                if (action == SM64MarioAction.GROUND_POUND || action == SM64MarioAction.GROUND_POUND_LAND)
                    character.processes.bStart(ModCharacterUtility.CreateReviveOptions(m_character, character, Skill.SkillType.None));
            }
            else if (actionIsPunching(action))
            {
                tryStartSpinningCharacter(character);
            }
        }
        else if (entityRef.get(out MiUsableDoor door))
        {
            if (isAttacking)
            {
                tryEnterDoor(door);
            }
        }
        else if (entityRef.get(out MiUsable usable))
        {
            if (isAttacking && usable.bUsableByChar(m_character) && (!usable.bOnce || !usable.bUsed))
            {
                usable.use(m_character);
            }
        }
    }

    [HideFromIl2Cpp]
    private void getBumpedByCharacter(MiCharacter _charTarget)
    {
        m_mario.takeDamage(0, 0, _charTarget.position.ToNET());
        if (!_charTarget.health.bDead() && !_charTarget.health.bKnockedOut() && _charTarget.controllerAI.bIsIdle())
        {
            // force npc to notice mario
            var impulse = new DetectionImpulse();
            impulse.m_eType = DetectionHandler.DetectionType.Proximity;
            impulse.m_char = m_character;
            impulse.m_entity = m_character;
            _charTarget.controllerAI.m_detectionHandler.runOverrideDetection(impulse, DetectionHandler.DetectionOverrideMode.FakeDetection);
        }
    }

    [HideFromIl2Cpp]
    private void damageCharacter(MiCharacter _charTarget, SM64MarioAction _action, bool _turnTarget, bool _playSfx, bool _skipAnimation = false)
    {
        Vector3 dir = (_charTarget.transform.position - charTrans.position).normalized;

        MiAnimAction actionFall = null;
        float durationVisible = 3;
        if (actionIsSlide(_action))
        {
            actionFall = m_diveKillAction;
            durationVisible = 1.5f;
        }
        else if (actionIsGroundPound(_action) || actionIsAirBonk(_action, m_mario))
        {
            actionFall = m_groundPoundAction;
            durationVisible = 1f;
        }
        else if (_turnTarget)
        {
            if (!CharIsMario(_charTarget) && _charTarget.health.bWouldTurnToCorpse(m_character, 1, Skill.SkillType.Kill, false))
            {
                var bodyState = _charTarget.animations.getBodyState();
                if (bodyState == BodyState.stand || bodyState == BodyState.crouch)
                    _charTarget.transform.rotation = Quaternion.LookRotation(MiMath.v3X0Z(-dir));
            }
        }

        if (flagHasMetalCap(m_mario.flags))
            _charTarget.health.setShieldActive(false);

        var killOptions = ModCharacterUtility.CreateDamageOptions(1, m_character, Skill.SkillType.Kill, _fDurationVisible: durationVisible,
            _v3InDirection: dir, _actionFall: actionFall, _bSkipAnimation: _skipAnimation);

        _charTarget.impulseHandler.damage(killOptions);

        if (_playSfx && !actionIsGroundPound(_action))
        {
            SM64Context.PlaySoundGlobal(SM64SoundBits.ActionHit1);
        }
    }

    [HideFromIl2Cpp]
    private void knockoutCharacter(MiCharacter _charTarget, SM64MarioAction _action)
    {
        bool isSlide = actionIsSlide(_action);
        Vector3 dir = (_charTarget.transform.position - charTrans.position).normalized;

        MiAnimAction actionFall = null;
        float durationVisible = 3;
        if (isSlide)
        {
            actionFall = m_diveKillAction;
            durationVisible = 1.5f;
        }
        else if (actionIsGroundPound(_action) || actionIsAirBonk(_action, m_mario))
        {
            actionFall = m_groundPoundAction;
            durationVisible = 1f;
        }
        else
        {
            if (!CharIsMario(_charTarget) && _charTarget.health.bWouldTurnToCorpse(m_character, 1, Skill.SkillType.Kill, true))
            {
                var bodyState = _charTarget.animations.getBodyState();
                if (bodyState == BodyState.stand || bodyState == BodyState.crouch)
                    _charTarget.transform.rotation = Quaternion.LookRotation(MiMath.v3X0Z(-dir));
            }
        }

        if (flagHasMetalCap(m_mario.flags))
            _charTarget.health.setShieldActive(false);

        if (Plugin.PluginConfig.gameplay.onlyKillAfterKnockOut.Value && _charTarget.m_eCharacter != MiCharacterType.Player)
        {
            var knockoutOptions = ModCharacterUtility.CreateKnockoutOptions(m_character, m_character, Skill.SkillType.Knockout,
                _fDurationVisible: durationVisible, _actionFall: actionFall);
            _charTarget.impulseHandler.knockout(knockoutOptions);
            if (!m_knockedOutCharsDict.TryAdd(_charTarget.Pointer, MiGameTime.timeSaved))
                m_knockedOutCharsDict[_charTarget.Pointer] = MiGameTime.timeSaved;
        }
        else
        {
            var killOptions = ModCharacterUtility.CreateDamageOptions(1, m_character, Skill.SkillType.Kill, _fDurationVisible: durationVisible,
                _v3InDirection: dir, _actionFall: actionFall);
            _charTarget.impulseHandler.damage(killOptions);
        }
    }

    [HideFromIl2Cpp]
    private void doAttackReaction(SM64MarioAction _action, SM64MarioFlag _flags)
    {
        if (actionIsMetalJump(_action, _flags))
        {
            SM64Context.PlaySoundGlobal(SM64SoundBits.ActionHit1);
        }
        else if (!actionIsAttacking(_action) || _action == SM64MarioAction.TWIRLING)
        {
            m_mario.setVelocity(new Vector3NET(0, Plugin.PluginConfig.gameplay.boingKnockback.Value, 0));
            m_mario.setAction(SM64MarioAction.TWIRLING);
            SM64Context.PlaySoundGlobal(SM64SoundBits.MarioTwirlBounce);
        }
        else if (actionHasKnockback(_action))
        {
            m_mario.setForwardVelocity(-Plugin.PluginConfig.gameplay.punchKnockback.Value);
            SM64Context.PlaySoundGlobal(SM64SoundBits.ActionHit1);
        }
        else if (actionIsSlide(_action))
        {
            SM64Context.PlaySoundGlobal(SM64SoundBits.ActionHit1);
        }
    }

    [HideFromIl2Cpp]
    private bool tryThrowCharacter(MiCharacter _charTarget, Vector3 _targetPos)
    {
        if (_charTarget.m_eCharacter == MiCharacterType.Player)
            return false;
        if (!_charTarget.health.bDead() && !_charTarget.health.bKnockedOut())
            return false;

        int areaFlag = _charTarget.navigation.areaMask;
        if (_charTarget.bHideable)
            areaFlag |= MiAreaFlags.c_flagWater;

        var location = m_character.navigation.m_query.MapLocation(_targetPos, new Vector3(0.1f, 50, 0.1f), 0, areaFlag);
        if (m_character.navigation.m_query.IsValid(location))
            _targetPos = location.position;

        if (UnityEngine.AI.NavMesh.SamplePosition(_targetPos, out var hit, 50, areaFlag))
        {
            if (_charTarget.health.bKnockedOut())
            {
                if (hit.mask == MiAreaFlags.c_flagWater)
                {
                    var killOptions = ModCharacterUtility.CreateDamageOptions(99, m_character, Skill.SkillType.Kill, _bSkipAnimation: true);
                    _charTarget.impulseHandler.damage(killOptions);
                }
                else
                {
                    var knockoutOptions = ModCharacterUtility.CreateKnockoutOptions(m_character, m_character, Skill.SkillType.Knockout, _bSkipAnimation: true);
                    _charTarget.impulseHandler.knockout(knockoutOptions);
                }
            }

            var flyingPrefab = MarioSGAssets.cnnKeyCannonShootFlying.GetComponent<MiFlyingObject>();

            var target = new MiCharacterTarget(hit.position);
            target.m_v3AlternativeTarget = _charTarget.transform.position;
            target.m_eModifier = c_shootCharacterTargetModifier;
            _charTarget.m_carryable.notifyThrow(m_character, target);

            var flyingInstance = MiPoolDynamicHandler.instance.instantiateRoot<MiFlyingObject>(flyingPrefab, _charTarget.transform.position, Quaternion.identity);
            flyingInstance.init(_charTarget, _catcher: null);
            flyingInstance.bTrySyncObjectPosition = true;
            var flyOptions = new MiFlying.FlyOptions();
            flyOptions.bInstantHit = false;
            flyOptions.onHit = null; // need to set this because il2cpp constructor for some reason doesn't null this

            // normally we would have to use a MiCharacterTargetPlayer to get a proper trajectory.
            // however, mario doesn't really have player skills he could use for this,
            // so instead we just patch the MiFlying.startObject intead to insert the trajectory positions.

            flyingInstance.startObject(target, flyOptions);

            bool shotInWater = JumpHelper.bDisposingWater(hit.position);

            var options = ModCharacterUtility.CreateThrowOptions(
                _charOrigin: m_character,
                _charTarget: _charTarget,
                _flying: flyingInstance,
                _bCanKnockoutCharacter: true,
                _bReapplyTiedUpAnimation: false,
                _actionFly: MiAnimActionList.Get("KeyCannonShoot_health_fly"),
                _actionHitGround: shotInWater
                    ? MiAnimActionList.Get("KeyThrowBody_health_land_water")
                    : MiAnimActionList.Get("KeyCannonShoot_health_land"));
            _charTarget.processes.bStart(options);
            return true;
        }

        return false;
    }

    public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> CalculateThrowTrajectory(Vector3 _from, Vector3 _to)
    {
        var positions = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(32);

        for (int i = 1; i < 6; i++)
        {
            bool valid = TrajectoryBallistic.calculate(
                _v3From: _from,
                _v3To: _to,
                _fRangeLimit: -1,
                _arNonAllocValid: positions,
                _arNonAllocInvalid: null,
                _fCurveHeightMultiplier: i,
                _bValidate: true);

            if (valid)
            {
                return positions;
            }
        }

        return positions;
    }

    [HideFromIl2Cpp]
    private bool tryEnterDoor(MiUsableDoor _door)
    {
        if (!_door.m_bWalkthroughDoor && _door.bCharCanGoThrough(m_character))
        {
            // TODO do we handle walkthrough Doors?

            m_state = MarioState.Door;
            setPreventSkills(false);
            m_mario.enabled = false;

            m_character.navigation.bWarp(_door.exitPointOutside.position);

            var skillCommand = new SkillCommand(
                m_character.controller.getSkill(Skill.SkillType.UseDoorEnter),
                new MiCharacterTarget(_door, _door.exitPointOutside));
            skillCommand.addModifier(SkillCommand.ExecutionModifier.SkipAnimation);

            m_character.controller.skillCommand = skillCommand;
            m_capOnDoor = flagIsPoweredUp(m_mario.flags) ? m_cap : MarioCap.Normal;

            return true;
        }

        return false;
    }

    [HideFromIl2Cpp]
    private void handleExitDoor()
    {
        if (!m_character.parallelStateHandler.bStateActive(ParallelState.ParallelStateType.Door)
            && !m_character.controller.bSkillActive(Skill.SkillType.UseDoorEnter)
            && !m_character.controller.bSkillActive(Skill.SkillType.UseDoorExit))
        {
            exitDoor();
        }
    }

    [HideFromIl2Cpp]
    private void exitDoor()
    {
        m_state = MarioState.Normal;
        setPreventSkills(true);

        m_mario.transform.position = charTrans.position + new Vector3(0, 2, 0);
        m_mario.transform.rotation = charTrans.rotation;
        m_mario.enabled = true;

        MarioCap capOnDoor = m_capOnDoor;
        GameEvents.RunNextFixedUpdate(() => restoreCap(capOnDoor, _forceActivate: true), 2);
        m_capOnDoor = MarioCap.Normal;
    }

    [HideFromIl2Cpp]
    private bool tryStartSpinningCharacter(MiCharacter _charTarget)
    {
        if (Plugin.PluginConfig.gameplay.throwDistanceMultiplier.Value <= 0)
            return false;
        if (_charTarget.m_eCharacter == MiCharacterType.Player)
            return false;
        if (!_charTarget.health.bKnockedOut() && !_charTarget.health.bDead())
            return false;

        if (AlarmSystem.instance.bAnySquadsAlarmed())
        {
            var nearbyEnemies = MiCharacterInRangeUtility.instance.findNPCsInRange(
                _charTarget.transform.position, 3, 2, MiCharacterTypeExtensions.c_iIsEnemy);
            for (int i = 0, count = nearbyEnemies.Count; i < count; i++)
            {
                var npc = nearbyEnemies[i];
                if (npc != null && npc.bControllerAI(out var controllerAI) && controllerAI.m_ai.bIsAlarmed())
                    return false; // don't start spinning if there are alarmed guards nearby
            }
            nearbyEnemies.Dispose();
        }

        _charTarget.animations.playAction(MiAnimActionList.Get(MiAnimActionName.KeyHealth_dead));

        m_spinningCharHideable = _charTarget.m_bHideable;
        _charTarget.m_bHideable = false;
        _charTarget.vis.destroyRagdoll();
        _charTarget.visOffset.setDisabled(MiCharacterVisOffset.VisOffsetDisableReason.Command);
        _charTarget.visOffset.endAlignRotationWithGround(MiCharacterVisOffset.AlignWithGroundRotationDisableReason.Carried);
        _charTarget.navigation.setNavMovementActive(false, MiCharNavigation.NavMeshMovementDisableReason.Command);

        m_state = MarioState.Spinning;
        m_mario.setAction(SM64MarioAction.PICKING_UP_BOWSER, _applyImmediately: true);
        m_spinningCharacter = _charTarget;
        m_lastSpinDiff = 0;
        return true;
    }

    [HideFromIl2Cpp]
    private void handleSpin()
    {
        if (m_spinningCharacter == null || (!m_spinningCharacter.health.bKnockedOut() && !m_spinningCharacter.health.bDead()))
        {
            m_state = MarioState.Normal;
            m_mario.setAction(SM64MarioAction.IDLE);

            if (m_spinningCharacter != null)
                getBumpedByCharacter(m_spinningCharacter);
            clearTargetCharSpinning();
        }
        else if (m_mario.action != SM64MarioAction.PICKING_UP_BOWSER && m_mario.action != SM64MarioAction.HOLDING_BOWSER)
        {
            m_state = MarioState.Normal;

            m_spinningCharacter.m_bHideable = m_spinningCharHideable;
            m_spinningCharacter.visOffset.setEnabled(MiCharacterVisOffset.VisOffsetDisableReason.Command);
            m_spinningCharacter.visOffset.beginAlignRotationWithGround(MiCharacterVisOffset.AlignWithGroundRotationDisableReason.Carried);
            m_spinningCharacter.navigation.setNavMovementActive(true, MiCharNavigation.NavMeshMovementDisableReason.Command);

            Vector3 dir = (m_spinningCharacter.transform.position - charTrans.position).normalized;
            float distance = m_lastSpinDiff * Plugin.PluginConfig.gameplay.throwDistanceMultiplier.Value;
            Vector3 shootTargetPos = m_spinningCharacter.transform.position + dir * distance;
            tryThrowCharacter(m_spinningCharacter, shootTargetPos);

            m_spinningCharacter = null;
        }
        else
        {
            Vector3 marioDir = Quaternion.Euler(0, m_mario.faceAngle * Mathf.Rad2Deg, 0) * Vector3.forward;
            m_spinningCharacter.transform.position = (charTrans.position + marioDir * 1) + new Vector3(0, 0.5f, 0);
            Vector3 lookDir = MiMath.v3X0Z(m_spinningCharacter.transform.position - charTrans.position).normalized;
            m_spinningCharacter.transform.rotation = Quaternion.LookRotation(lookDir);

            // hacky way to get rotation speed...
            float lastFaceAngleDef = m_lastFaceAngle * Mathf.Rad2Deg;
            float faceAngleDef = m_mario.faceAngle * Mathf.Rad2Deg;
            float spinDiff = System.Math.Abs(lastFaceAngleDef - faceAngleDef);
            if (spinDiff > 180)
                spinDiff -= 180;
            if (spinDiff > 0)
                m_lastSpinDiff = spinDiff * Mathf.Deg2Rad; 
        }
    }

    private void clearTargetCharSpinning()
    {
        if (m_spinningCharacter != null)
        {
            m_spinningCharacter.m_bHideable = m_spinningCharHideable;
            m_spinningCharacter.visOffset.setEnabled(MiCharacterVisOffset.VisOffsetDisableReason.Command);
            m_spinningCharacter.visOffset.beginAlignRotationWithGround(MiCharacterVisOffset.AlignWithGroundRotationDisableReason.Carried);
            m_spinningCharacter.navigation.setNavMovementActive(true, MiCharNavigation.NavMeshMovementDisableReason.Command);
            m_spinningCharacter.navigation.bWarpOntoNavMesh();
        }
        m_spinningCharacter = null;
    }

    [HideFromIl2Cpp]
    private void setPreventSkills(bool _value)
    {
        m_character.m_skillHandler.setSkillsBlocked(_value);

        var lSkills = m_character.controller.lSkills;
        if (lSkills != null)
        {
            foreach (var skill in lSkills)
                if (skill.bPreventActivation != _value)
                    skill.preventActivation(_value);
        }
    }

    [HideFromIl2Cpp]
    public void GiveCap(MarioCap _cap, float _duration, bool _playMusic)
    {
        if (m_mario != null)
        {
            m_mario.interactCap(capToID(_cap), (ushort)System.Math.Round(_duration * 50), _playMusic);
            m_cap = _cap;
            m_capDuration = _duration;
            m_capStartTime = MiGameTime.timeSaved;
        }
    }

    private static uint capToID(MarioCap _cap)
    {
        switch (_cap)
        {
            default:
            case MarioCap.Normal:
                return 1 << 0;
            case MarioCap.Metal:
                return 1 << 2;
            case MarioCap.Wing:
                return 1 << 3;
        }
    }

    public static bool actionIsAttacking(SM64MarioAction _action)
    {
        return (_action & SM64MarioAction.FLAG_ATTACKING) != 0 && _action != SM64MarioAction.CROUCH_SLIDE;
    }

    public static bool actionHasKnockback(SM64MarioAction _action)
    {
        return _action == SM64MarioAction.PUNCHING || _action == SM64MarioAction.MOVE_PUNCHING || _action == SM64MarioAction.JUMP_KICK;
    }

    public static bool actionIsSlide(SM64MarioAction _action)
    {
        return (_action & (SM64MarioAction.FLAG_DIVING | SM64MarioAction.FLAG_BUTT_OR_STOMACH_SLIDE)) != 0 || _action == SM64MarioAction.SLIDE_KICK;
    }

    public static bool actionIsGroundPound(SM64MarioAction _action)
    {
        return _action == SM64MarioAction.GROUND_POUND || _action == SM64MarioAction.GROUND_POUND_LAND;
    }

    public static bool actionIsPunching(SM64MarioAction _action)
    {
        return _action == SM64MarioAction.PUNCHING || _action == SM64MarioAction.MOVE_PUNCHING;
    }

    public static bool actionIsCrawling(SM64MarioAction _action)
    {
        return _action == SM64MarioAction.CRAWLING || _action == SM64MarioAction.START_CRAWLING || _action == SM64MarioAction.STOP_CRAWLING
            || _action == SM64MarioAction.CROUCHING || _action == SM64MarioAction.START_CROUCHING || _action == SM64MarioAction.STOP_CROUCHING
            || actionIsSlide(_action);
    }

    public static bool actionIsAirBonk(SM64MarioAction _action, SM64Mario _mario)
    {
        return (_action & SM64MarioAction.FLAG_AIR) != 0 && _mario.velocity.Y < 0;
    }

    public static bool actionIsMetalJump(SM64MarioAction _action, SM64MarioFlag _flags)
    {
        return (_action & SM64MarioAction.FLAG_AIR) != 0 && flagHasMetalCap(_flags);
    }

    public static bool actionCanBeRestored(SM64MarioAction _action)
    {
        if (_action == SM64MarioAction.SLEEPING || _action == SM64MarioAction.START_SLEEPING
            || _action == SM64MarioAction.PICKING_UP_BOWSER || _action == SM64MarioAction.HOLDING_BOWSER || _action == SM64MarioAction.RELEASING_BOWSER)
            return false;

        return true;
    }

    public static bool flagIsPoweredUp(SM64MarioFlag _flags)
    {
        return flagHasMetalCap(_flags) || (_flags & SM64MarioFlag.MARIO_WING_CAP) != 0;
    }

    public static bool flagHasMetalCap(SM64MarioFlag _flags)
    {
        return (_flags & SM64MarioFlag.MARIO_METAL_CAP) != 0;
    }
}
