using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class Coin : ModSaveable
{
    private float m_totalLifetime;
    private float m_currentLifetime;
    private Vector3NET m_velocity;

    private Transform m_vis;

    private const float c_radius = 0.2f;
    private const float c_damping = 0.1f;
    private const float c_collisionDamping = 0.5f;
    private const float c_timeMultiplier = 1.2f;
    private const float c_minSpeedSqrImpactSfx = 2f * 2f;
    private const float c_rayOffset = 0.05f;

    public Coin(System.IntPtr ptr) : base(ptr) { }

    public Coin() : base(ClassInjector.DerivedConstructorPointer<Coin>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public static Coin SpawnStatic(Vector3NET _position, bool _addModdedComponent = false)
    {
        var go = new GameObject("coin");
        go.SetActive(false);
        go.layer = (int)MiLayer.Perceptable;
        go.transform.SetParent(SaveLoadSceneManager.transGetRoot());
        go.transform.position = _position.ToIL2CPP();

        var collider = go.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.4f;

        var coin = go.AddComponent<Coin>();
        coin.createVis();
        coin.m_totalLifetime = -1;

        if (_addModdedComponent)
            go.AddComponent<MiModdingSpawnedObject>();

        go.SetActive(true);

        return coin;
    }

    public static Coin Spawn(Vector3NET _position, Vector3NET _velocity, float _lifetime)
    {
        var coin = SpawnStatic(_position);
        coin.m_totalLifetime = _lifetime;
        coin.m_velocity = _velocity;
        return coin;
    }

    public static void SpawnMultiple(Vector3NET _position, Vector3NET _velocity, Vector3NET _spread, int _count, float _lifetime, bool _playSfx = false)
    {
        for (int i = 0; i < _count; i++)
        {
            Vector3NET force = _velocity + new Vector3NET(
                UnityEngine.Random.Range(-_spread.X, _spread.X),
                UnityEngine.Random.Range(-_spread.Y, _spread.Y),
                UnityEngine.Random.Range(-_spread.Z, _spread.Z));
            Spawn(_position, force, _lifetime);
        }

        if (_playSfx)
            LibSM64.SM64Context.PlaySound(LibSM64.SM64SoundBits.GeneralCoinSpurt, _position);
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

        _helper.Serialize(0, m_totalLifetime);
        _helper.Serialize(1, m_currentLifetime);
        _helper.Serialize(2, m_velocity.ToIL2CPP());
    }

    [HideFromIl2Cpp]
    protected override void deserializeMod(ref ModDeserializeHelper _helper)
    {
        base.deserializeMod(ref _helper);

        _helper.Deserialize(0, ref m_totalLifetime);
        _helper.Deserialize(1, ref m_currentLifetime);
        _helper.Deserialize(2, ref m_velocity);
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

        if (m_vis != null)
        {
            m_vis.Rotate(0, 200 * MiTime.deltaTime, 0, Space.Self);

            if (trans.hasChanged)
            {
                m_vis.position = trans.position;
                trans.hasChanged = false;
            }
        }

        if (m_totalLifetime >= 0)
        {
            doPhysics();

            m_currentLifetime += MiTime.deltaTime;
            float remainingLife = m_totalLifetime - m_currentLifetime;

            if (remainingLife <= 0)
            {
                Destroy(this.gameObject);
            }
            else if (remainingLife < 3 && m_vis != null)
            {
                m_vis.gameObject.SetActive(Mathf.FloorToInt(MiGameTime.timeSaved * 30) % 2 == 0);
            }
        }
    }

    [HideFromIl2Cpp]
    private void doPhysics()
    {
        // crappy custom physics because i dont want to deal with saving physics materials

        Vector3NET pos = transform.position.ToNET();
        float deltaTime = MiTime.deltaTime;
        float speedSqr = m_velocity.LengthSquared();

        m_velocity *= 1 - (c_damping * deltaTime);
        m_velocity.Y -= 9.81f * deltaTime * c_timeMultiplier;

        doPhysicsStep(ref pos, ref m_velocity, deltaTime, out bool didCollide);

        if (didCollide && speedSqr > c_minSpeedSqrImpactSfx)
            LibSM64.SM64Context.PlaySound(LibSM64.SM64SoundBits.GeneralCoinDrop, pos);

        transform.position = pos.ToIL2CPP();
    }

    [HideFromIl2Cpp]
    private static void doPhysicsStep(ref Vector3NET _pos, ref Vector3NET _velocity, float _deltaTime, out bool _didCollide)
    {
        Vector3NET oldPos = _pos;
        Vector3NET newPos = oldPos + _velocity * _deltaTime * c_timeMultiplier;
        Vector3NET dir = Vector3NET.Normalize(newPos - oldPos);
        Vector3 rayOrigin = (oldPos - dir * c_rayOffset).ToIL2CPP();
        Vector3 rayDir = Vector3NET.Normalize(_velocity).ToIL2CPP();
        float dist = Vector3NET.Distance(oldPos, newPos) + c_rayOffset;

        if (Physics.SphereCast(rayOrigin, c_radius, rayDir, out var hit, dist, MarioStateSyncer.marioColliderMask))
        {
            Vector3NET reflected = Vector3NET.Reflect(dir, hit.normal.ToNET());
            _velocity = reflected * _velocity.Length() * (1 - c_collisionDamping);

            newPos = (hit.point + hit.normal * c_radius).ToNET();
            _didCollide = true;
        }
        else
        {
            _didCollide = false;
        }

        _pos = newPos;
    }

    public override void MiOnTriggerEnter(Collider _col)
    {
        if (m_totalLifetime >= 0 && m_currentLifetime < 0.5f)
            return;

        var mario = _col.GetComponent<MarioStateSyncer>();
        if (mario != null)
        {
            Collect();
        }
        else
        {
            var entityRef = _col.GetComponent<EntityReference>();
            if (entityRef == null)
                return;

            if (entityRef.get(out MiCharacter character) && !character.m_bSpawnLater && character.m_eCharacter.bIsPlayer() && !MarioStateSyncer.CharIsMario(character))
            {
                Collect();
            }
        }
    }

    [HideFromIl2Cpp]
    public void Collect()
    {
        if (MarioSceneSaveHandler.instance != null)
        {
            MarioSceneSaveHandler.instance.AddCoins(1, transform.position.ToNET());
            LibSM64.SM64Context.PlaySoundGlobal(LibSM64.SM64SoundBits.GeneralCoin);
        }

        Destroy(this.gameObject);
    }

    [HideFromIl2Cpp]
    private void createVis()
    {
        if (m_vis != null)
            return;

        m_vis = GameObject.Instantiate(MarioResources.LoadCoinPrefab()).transform;
        m_vis.position = transform.position;

        ShaderUtility.ReplaceObjectShaders(m_vis.gameObject, ShaderUtility.FindStandardHideVCShader());

        FakeShadow.Spawn(m_vis.transform, 0.3f);
    }
}
