using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using PirateBase;
using LibSM64;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class Star : ModSaveable
{
    private Transform m_vis;

    private StarState m_state;
    private MarioStateSyncer m_sequenceMario;

    private enum StarState { Idle, Spawning, Collecting, Collected }

    public Star(System.IntPtr ptr) : base(ptr) { }

    public Star() : base(ClassInjector.DerivedConstructorPointer<Star>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public static Star Spawn(Vector3NET _position, bool _playMusic)
    {
        var go = new GameObject("star");
        go.SetActive(false);
        go.layer = (int)MiLayer.Perceptable;
        go.transform.SetParent(SaveLoadSceneManager.transGetRoot());
        go.transform.position = _position.ToIL2CPP();

        var collider = go.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.5f;

        var star = go.AddComponent<Star>();
        star.createVis();

        go.AddComponent<MiModdingSpawnedObject>();

        go.SetActive(true);

        if (_playMusic)
            SM64Context.PlayMusic(1, SeqId.SEQ_EVENT_CUTSCENE_STAR_SPAWN, 10);

        return star;
    }

    public override void AwakeDelayed()
    {
        GameEvents.beforeSave += onBeforeSave;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (m_vis != null)
            Destroy(m_vis.gameObject);

        GameEvents.beforeSave -= onBeforeSave;
    }

    [HideFromIl2Cpp]
    protected override void serializeMod(ref ModSerializeHelper _helper)
    {
        base.serializeMod(ref _helper);

        _helper.Serialize(0, (int)m_state);
    }

    [HideFromIl2Cpp]
    protected override void deserializeMod(ref ModDeserializeHelper _helper)
    {
        base.deserializeMod(ref _helper);

        if (_helper.dictFields.TryGetValue(0, out var value0))
            m_state = (StarState)value0.Unbox<int>();
    }

    public override void OnLoadingFinished()
    {
        base.OnLoadingFinished();

        if (m_state != StarState.Collected)
            createVis();
    }

    [HideFromIl2Cpp]
    private void onBeforeSave(SaveGameHolder _saveGameHolder)
    {
        if (m_state == StarState.Collecting && m_sequenceMario != null)
        {
            // we can't really save coroutines right now, so just cancel it and set to the end state
            this.StopAllCoroutines();
            setStarGetEndState(m_sequenceMario);
        }
    }

    private void Update()
    {
        if (ModUpdate.shouldSkipUpdate)
            return;

        if (m_state != StarState.Collected && m_vis != null)
        {
            m_vis.Rotate(0, 100 * MiTime.deltaTime, 0, Space.Self);

            if (trans.hasChanged)
            {
                m_vis.position = trans.position;
                trans.hasChanged = false;
            }
        }
    }

    public override void MiOnTriggerEnter(Collider _col)
    {
        var mario = _col.GetComponent<MarioStateSyncer>();
        if (mario != null)
        {
            Collect(mario);
        }
    }

    [HideFromIl2Cpp]
    public void Collect(MarioStateSyncer _mario)
    {
        if (m_state != StarState.Idle)
            return;

        m_state = StarState.Collecting;
        if (m_vis != null)
            m_vis.gameObject.SetActive(false);

        if (MarioSceneSaveHandler.instance != null)
            StartCoroutine(BepInEx.Unity.IL2CPP.Utils.Collections.CollectionExtensions.WrapToIl2Cpp(marioStarSequenceCoro(_mario)));
    }

    [HideFromIl2Cpp]
    private void createVis()
    {
        if (m_vis != null)
            return;

        m_vis = GameObject.Instantiate(MarioResources.LoadStarPrefab()).transform;
        m_vis.position = trans.position;

        ShaderUtility.ReplaceObjectShaders(m_vis.gameObject, ShaderUtility.FindStandardHideVCShader());

        FakeShadow.Spawn(m_vis, 0.6f);
    }

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator marioStarSequenceCoro(MarioStateSyncer _mario)
    {
        m_sequenceMario = _mario;

        _mario.inputProvider.forceLookDirection = -Camera.main.transform.forward.ToNET(); // so mario faces camera

        MarioSceneSaveHandler.instance.AddStar();
        _mario.mario.setAction(SM64MarioAction.FALL_AFTER_STAR_GRAB);
        _mario.m_character.setInvisibleReason(true, Entity.InvisibleReason.Cutscene);

        SM64Context.PlaySoundGlobal(SM64SoundBits.GeneralShortStar);

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();


        while (_mario.mario.action == SM64MarioAction.FALL_AFTER_STAR_GRAB)
        {
            yield return null;
        }

        SM64Context.PlayMusic(1, SeqId.SEQ_EVENT_CUTSCENE_COLLECT_STAR, 10);

        yield return new WaitForSeconds(1.3f);

        SM64Context.PlaySoundGlobal(SM64SoundBits.MarioHereWeGo);

        yield return new WaitForSeconds(2.7f);

        setStarGetEndState(_mario);
    }

    [HideFromIl2Cpp]
    private void setStarGetEndState(MarioStateSyncer _mario)
    {
        _mario.inputProvider.forceLookDirection = null;

        if (_mario.mario.action == SM64MarioAction.STAR_DANCE_WATER)
            _mario.m_lastAction = SM64MarioAction.WATER_IDLE;
        else
            _mario.m_lastAction = SM64MarioAction.IDLE;
        _mario.mario.setAction(_mario.m_lastAction);
        _mario.m_character.setInvisibleReason(false, Entity.InvisibleReason.Cutscene);

        m_sequenceMario = null;
        m_state = StarState.Collected;
    }
}
