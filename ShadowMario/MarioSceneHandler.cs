using System.Collections;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using Mimimi.SceneEvents;
using LibSM64;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

[Il2CppImplements(typeof(IMiSceneEventListener))]
internal class MarioSceneHandler : MonoBehaviour
{
    private TerrainGenerator m_terrainGen;

    private List<MarioStateSyncer> m_marios = new();

    [HideFromIl2Cpp]
    public TerrainGenerator terrainGen => m_terrainGen;

    [HideFromIl2Cpp]
    public IReadOnlyList<MarioStateSyncer> marios => m_marios;

    public MarioStateSyncer leaderMario
    {
        get
        {
            foreach (var mario in m_marios)
            {
                if (mario.m_character.controllerPlayer.bLeader)
                    return mario;
            }
            return null;
        }
    }

    private static MarioSceneHandler s_instance;
    public static MarioSceneHandler instance => s_instance;


    public static void Init()
    {
        if (s_instance != null)
            return;

        Plugin.PluginLog.LogInfo("Spawning Mario Scene Handler...");

        var handlerGo = new GameObject("sm64_scene_handler");
        //handlerGo.transform.SetParent(SaveLoadSceneManager.transGetRoot());
        handlerGo.AddComponent<MarioSceneHandler>();
    }

    private void Awake()
    {
        s_instance = this;
        createObjects();
        initPhysicsLayers();
        applyGlobalHacks();
        fixAudioSettings();

        GameEvents.beforeSave += onBeforeSave;
        GameEvents.afterSave += onAfterSave;
        GameEvents.afterLoad += onAfterLoad;

        registerSceneCallbacks();
    }

    private void OnDestroy()
    {
        s_instance = null;
        destroyObjects();

        GameEvents.beforeSave -= onBeforeSave;
        GameEvents.afterSave -= onAfterSave;
        GameEvents.afterLoad -= onAfterLoad;

        unregisterSceneCallbacks();
    }

    private void Update()
    {
        SM64Context.UpdateActive = MiGameTime.bInstanceExists && !MiGameTime.instance.pausedSelf && !ModUpdate.shouldSkipUpdate;

        if (ModUpdate.shouldSkipUpdate)
            return;

        //SM64Context.SetSoundVolume(m_volume * MiAudioMixer.s_fVolumeMaster);
        SM64Context.audio.m_volume = MiAudioMixer.s_fVolumeMaster * 0.85f;

        applyGlobalHacks();
    }

    private void LateUpdate()
    {
        if (ModUpdate.shouldSkipUpdate)
            return;

        // apply on late update again to override coroutines...
        applyGlobalHacks();
    }


    private void registerSceneCallbacks()
    {
        MiSceneEventHandler.instance.subscribeToEvent(MiSceneEventType.CharacterDeath, this.Cast<IMiSceneEventListener>());
        MiSceneEventHandler.instance.subscribeToEvent(MiSceneEventType.CharacterKnockout, this.Cast<IMiSceneEventListener>());
    }

    private void unregisterSceneCallbacks()
    {
        if (MiSceneEventHandler.bInstanceExists)
        {
            MiSceneEventHandler.instance.unsubscribeFromEvent(MiSceneEventType.CharacterDeath, this.Cast<IMiSceneEventListener>());
            MiSceneEventHandler.instance.unsubscribeFromEvent(MiSceneEventType.CharacterKnockout, this.Cast<IMiSceneEventListener>());
        }
    }

    private void onBeforeSave(SaveGameHolder _saveGameHolder)
    {
        foreach (var mario in m_marios)
            mario.OnBeforeSave();

        unregisterSceneCallbacks();
    }

    private void onAfterSave(SaveGameHolder _saveGameHolder, SaveProcessManager.SaveProcessResult _result)
    {
        foreach (var mario in m_marios)
            mario.OnAfterSave();

        registerSceneCallbacks();
        //_saveGameHolder.m_additionalResourceContainerNextWrite.enumerateResources
    }

    private void onAfterLoad(SaveGameHolder _saveGameHolder, SaveProcessManager.LoadProcessResult _result)
    {
        unregisterSceneCallbacks();
        registerSceneCallbacks();
    }

    private void createObjects()
    {
        if (m_terrainGen != null)
            return;

        RegenerateTerrain();
    }

    private void destroyObjects()
    {
        if (m_terrainGen != null)
        {
            m_terrainGen.Dispose();
            m_terrainGen = null;
        }
    }

    private void initPhysicsLayers()
    {
        // required for state syncer collider to notice collisions from characters and usables
        Physics.IgnoreLayerCollision(MarioStateSyncer.marioLayer, (int)MiLayer.Perceptable, false);
        Physics.IgnoreLayerCollision(MarioStateSyncer.marioLayer, (int)MiLayer.ClickableObject, false);

        // TODO remove this?
        //Physics.IgnoreLayerCollision((int)MiLayer.NavMeshNoSolidGround);
        Physics.IgnoreLayerCollision(MarioStateSyncer.objLayer, (int)MiLayer.Occluder, false);
        Physics.IgnoreLayerCollision(MarioStateSyncer.objLayer, (int)MiLayer.Walkable, false);
        Physics.IgnoreLayerCollision(MarioStateSyncer.objLayer, (int)MiLayer.Terrain, false);
    }

    private void applyGlobalHacks()
    {
        // required for mario to be at correct speed
        Time.fixedDeltaTime = 1 / 29.5f;
        Time.maximumDeltaTime = 0.1f;
    }

    private void fixAudioSettings()
    {
        if (AudioSettings.outputSampleRate != SM64Audio.c_frequency)
        {
            Plugin.PluginLog.LogWarning($"Fixing audio frequency.");

            AudioConfiguration configuration = AudioSettings.GetConfiguration();
            configuration.sampleRate = SM64Audio.c_frequency;
            if (!AudioSettings.Reset(configuration))
            {
                Plugin.PluginLog.LogWarning($"Could not fix outputSampleRate! Audio will not work.");
            }
            MiAudioMixer.instance.applySettings();
        }
    }

    private void fireEvent(
        MiSceneEventType _eEventType,
        Entity _entityOrigin,
        Entity _entityTarget,
        MiSceneEventData _eventData)
    {
        Plugin.PluginLog.LogInfo($"{_eEventType} {_entityOrigin?.name} {_entityTarget?.name} {_eventData}");
        switch (_eEventType)
        {
            case MiSceneEventType.CharacterDeath:
                var targetChar = _entityTarget.Cast<MiCharacter>();
                if (targetChar.m_eCharacter == MiCharacterType.Player)
                    break;

                Vector3NET pos = _entityTarget.transform.position.ToNET() + new Vector3NET(0, 0.5f, 0);
                Vector3NET force = new Vector3NET(0, 5, 0);
                if (_entityOrigin != null)
                {
                    Vector3NET dir = Vector3NET.Normalize(MiMath.v3X0Z(_entityTarget.transform.position - _entityOrigin.transform.position).ToNET());
                    force += dir * 3;
                }
                Vector3NET spread = new Vector3NET(3f, 0, 3f);
                int count = 1;
                if (targetChar.m_eCharacter == MiCharacterType.EnyStatic || targetChar.m_eCharacter == MiCharacterType.EnyMonk || targetChar.m_eCharacter == MiCharacterType.EnySharpshooter)
                    count = 2;
                else if (targetChar.m_eCharacter == MiCharacterType.EnyFreezer)
                    count = 4;
                Coin.SpawnMultiple(pos, force, spread, count, 10);

                playAttackNoise(_entityOrigin, targetChar);

                break;
            case MiSceneEventType.CharacterKnockout:
                var targetKnockChar = _entityTarget.Cast<MiCharacter>();
                if (targetKnockChar.m_eCharacter == MiCharacterType.Player)
                    break;

                playAttackNoise(_entityOrigin, targetKnockChar);

                break;
        }
    }

    private void playAttackNoise(Entity _entityOrigin, MiCharacter _targetChar)
    {
        if (_entityOrigin != null && Plugin.PluginConfig.gameplay.attackNoiseRadius.Value > 0)
        {
            var originChar = _entityOrigin.TryCast<MiCharacter>();
            if (originChar != null && MarioStateSyncer.CharIsMario(originChar))
            {
                var noiseSettings = ModCharacterUtility.CreateNoiseSettings(originChar);
                noiseSettings.emitDuration = new BalancedFloat(0.5f);
                noiseSettings.emitHeight = new BalancedFloat(4);
                noiseSettings.emitRadius = new BalancedFloat(Plugin.PluginConfig.gameplay.attackNoiseRadius.Value);
                noiseSettings.SetNoiseType(NoiseDetection.NoiseType.SuspiciousNoInvestigate);

                _targetChar.noise.emit(MiCharacterImpulseHandler.ImpulseOptionsEmitNoise.Create(_targetChar, Skill.SkillType.None, noiseSettings));
            }
        }
    }

    public void RegisterMario(MarioStateSyncer _mario)
    {
        m_marios.Add(_mario);
    }

    public void UnregisterMario(MarioStateSyncer _mario)
    {
        m_marios.Remove(_mario);
    }

    public void RegenerateTerrain()
    {
        if (m_terrainGen != null)
        {
            m_terrainGen.Dispose();
        }

        float voxelSize = Mimimi.HUB.HUBLogic.bInstanceExists ? Plugin.PluginConfig.terrain.hubVoxelSize.Value : Plugin.PluginConfig.terrain.voxelSize.Value;

        m_terrainGen = new TerrainGenerator(
            Plugin.PluginConfig.terrain.chunkSize.Value,
            voxelSize,
            Plugin.PluginConfig.terrain.minSlipperyAngle.Value,
            Plugin.PluginConfig.debug.displayGenerationMesh.Value);
        m_terrainGen.generateMeshes();
    }

    public void RegenerateTerrainAndUpdateStreaming()
    {
        RegenerateTerrain();
        if (SM64Context.surfaceStreaming != null)
        {
            GameEvents.RunNextUpdate(() =>
            {
                // Need to wait a frame so old terrains are actually gone.
                SM64Context.surfaceStreaming.updateTerrains();
                SM64Context.surfaceStreaming.forceUpdateCullingState();
            });
        }
    }
}
