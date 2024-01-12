using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using PirateBase;
using LibSM64;

namespace ShadowMario;

internal class MarioSurfaceProperties : ModSaveable
{
    public SM64TerrainType m_terrainType;
    public SM64SurfaceType m_surfaceType;
    public bool m_disableSurfaceCulling;

    public MarioSurfaceProperties(System.IntPtr ptr) : base(ptr) { }

    public MarioSurfaceProperties() : base(ClassInjector.DerivedConstructorPointer<MarioSurfaceProperties>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    [HideFromIl2Cpp]
    protected override void serializeMod(ref ModSerializeHelper _helper)
    {
        base.serializeMod(ref _helper);

        _helper.Serialize(0, (int)m_terrainType);
        _helper.Serialize(1, (int)m_surfaceType);
        _helper.Serialize(2, m_disableSurfaceCulling);
    }

    [HideFromIl2Cpp]
    protected override void deserializeMod(ref ModDeserializeHelper _helper)
    {
        base.deserializeMod(ref _helper);

        if (_helper.dictFields.TryGetValue(0, out var value0))
            m_terrainType = (SM64TerrainType)value0.Unbox<int>();
        if (_helper.dictFields.TryGetValue(1, out var value1))
            m_surfaceType = (SM64SurfaceType)value1.Unbox<int>();
        _helper.Deserialize(2, ref m_disableSurfaceCulling);
    }
}
