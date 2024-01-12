using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class MarioObstacle : ModSaveable
{
    private ObstacleType m_type;
    private bool m_spawnVis;

    private Transform m_vis;

    public enum ObstacleType { Ice, Lava }

    public MarioObstacle(System.IntPtr ptr) : base(ptr) { }

    public MarioObstacle() : base(ClassInjector.DerivedConstructorPointer<MarioObstacle>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public static MarioObstacle Spawn(Vector3NET _position, ObstacleType _type, bool _spawnVis = true, bool _disableSurfaceCulling = false, bool _addModdedComponent = false)
    {
        var go = GameObject.Instantiate(MarioResources.LoadObstacleColliderPrefab());
        go.SetActive(false);
        go.layer = (int)MiLayer.Occluder;
        go.transform.SetParent(SaveLoadSceneManager.transGetRoot());
        go.transform.position = _position.ToIL2CPP();

        var surfaceProperties = go.AddComponent<MarioSurfaceProperties>();
        surfaceProperties.m_disableSurfaceCulling = _disableSurfaceCulling;
        switch (_type)
        {
            default:
            case ObstacleType.Ice:
                surfaceProperties.m_surfaceType = LibSM64.SM64SurfaceType.Ice;
                surfaceProperties.m_terrainType = LibSM64.SM64TerrainType.Snow;
                break;
            case ObstacleType.Lava:
                surfaceProperties.m_surfaceType = LibSM64.SM64SurfaceType.Burning;
                surfaceProperties.m_terrainType = LibSM64.SM64TerrainType.Stone;
                break;
        }

        var obstacle = go.AddComponent<MarioObstacle>();
        obstacle.m_type = _type;
        obstacle.m_spawnVis = _spawnVis;
        obstacle.createVis();

        if (_addModdedComponent)
            go.AddComponent<MiModdingSpawnedObject>();

        go.SetActive(true);

        return obstacle;
    }

    [HideFromIl2Cpp]
    protected override void serializeMod(ref ModSerializeHelper _helper)
    {
        base.serializeMod(ref _helper);

        _helper.Serialize(0, (int)m_type);
        _helper.Serialize(1, m_spawnVis);
    }

    [HideFromIl2Cpp]
    protected override void deserializeMod(ref ModDeserializeHelper _helper)
    {
        base.deserializeMod(ref _helper);

        if (_helper.dictFields.TryGetValue(0, out var value0))
            m_type = (ObstacleType)value0.Unbox<int>();
        _helper.Deserialize(1, ref m_spawnVis);
    }

    public override void OnLoadingFinished()
    {
        base.OnLoadingFinished();

        createVis();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (m_vis != null)
            Destroy(m_vis.gameObject);
    }

    private void Update()
    {
        if (ModUpdate.shouldSkipUpdate)
            return;

        if (m_vis != null && trans.hasChanged)
        {
            m_vis.position = trans.position;
            m_vis.rotation = trans.rotation;
            m_vis.localScale = trans.localScale;
            trans.hasChanged = false;
        }
    }

    [HideFromIl2Cpp]
    private void createVis()
    {
        if (m_vis != null || !m_spawnVis)
            return;

        GameObject prefab;
        switch (m_type)
        {
            default:
            case ObstacleType.Ice:
                prefab = MarioResources.LoadObstacleIcePrefab();
                break;
            case ObstacleType.Lava:
                prefab = MarioResources.LoadObstacleLavaPrefab();
                break;
        }

        m_vis = GameObject.Instantiate(prefab).transform;
        m_vis.position = transform.position;
        m_vis.rotation = transform.rotation;
        m_vis.localScale = transform.localScale;

        ShaderUtility.ReplaceObjectShaders(m_vis.gameObject, ShaderUtility.FindStandardShowVCShader());
    }
}
