using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using LibSM64;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal abstract class BlockBase : ModSaveable
{
    private Transform m_vis;

    public BlockBase(System.IntPtr ptr) : base(ptr) { }

    public BlockBase() : base(ClassInjector.DerivedConstructorPointer<BlockBase>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    protected static T spawn<T>(Vector3NET _position, string _name) where T : BlockBase
    {
        var go = new GameObject($"block_{_name}");
        go.SetActive(false);
        go.layer = (int)MiLayer.Perceptable;
        go.transform.SetParent(SaveLoadSceneManager.transGetRoot());
        go.transform.position = _position.ToIL2CPP();

        var collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.center = new Vector3(0, -0.2f, 0);
        collider.size = new Vector3(0.7f, 1.4f, 0.7f);

        var block = go.AddComponent<T>();
        block.createVis();

        go.AddComponent<MiModdingSpawnedObject>();

        go.SetActive(true);

        return block;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (m_vis != null)
            Destroy(m_vis.gameObject);
    }

    [HideFromIl2Cpp]
    protected override void serializeMod(ref ModSerializeHelper _helper)
    {
        base.serializeMod(ref _helper);

    }

    [HideFromIl2Cpp]
    protected override void deserializeMod(ref ModDeserializeHelper _helper)
    {
        base.deserializeMod(ref _helper);

    }

    public override void OnLoadingFinished()
    {
        base.OnLoadingFinished();

        createVis();
    }

    private void Update()
    {
        if (ModUpdate.shouldSkipUpdate)
            return;

        if (m_vis != null && trans.hasChanged)
        {
            m_vis.position = trans.position;
            m_vis.rotation = trans.rotation;
            trans.hasChanged = false;
        }
    }

    private void OnTriggerStay(Collider _col)
    {
        if (ModUpdate.shouldSkipUpdate || s_bOnDeserialize)
            return;

        var mario = _col.GetComponent<MarioStateSyncer>();
        if (mario != null)
        {
            var action = mario.mario.action;

            bool isGroundPound = MarioStateSyncer.actionIsGroundPound(action);
            bool isJumpingUp = (action & SM64MarioAction.FLAG_AIR) != 0 && mario.mario.velocity.Y > 0
                && mario.transform.position.y < trans.position.y;

            if (isGroundPound || isJumpingUp)
                Break(mario);

            if (isJumpingUp) // fake mario hitting his head
                mario.mario.setVelocity(new Vector3NET(mario.mario.velocity.X, 0, mario.mario.velocity.Z));
        }
    }

    [HideFromIl2Cpp]
    public void Break(MarioStateSyncer _mario)
    {
        SM64Context.PlaySound(SM64SoundBits.GeneralWallExplosion, trans.position.ToNET());
        onBreak(_mario);
        Destroy(this.gameObject);
    }

    [HideFromIl2Cpp]
    protected void createVis()
    {
        if (m_vis != null)
            return;

        m_vis = GameObject.Instantiate(getVisPrefab()).transform;
        m_vis.position = transform.position;

        GameEvents.RunNextFixedUpdate(() =>
        {
            if (m_vis != null)
                m_vis.gameObject.AddComponent<SM64DynamicTerrain>();
        }, 2);

        ShaderUtility.ReplaceObjectShaders(m_vis.gameObject, ShaderUtility.FindStandardHideVCShader());

        FakeShadow.Spawn(m_vis.transform, _isSquare: true);
    }

    [HideFromIl2Cpp]
    protected abstract GameObject getVisPrefab();
    [HideFromIl2Cpp]
    protected abstract void onBreak(MarioStateSyncer _mario);
}
