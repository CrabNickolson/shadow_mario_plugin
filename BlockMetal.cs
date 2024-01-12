using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class BlockMetal : BlockBase
{
    public BlockMetal(System.IntPtr ptr) : base(ptr) { }

    public BlockMetal() : base(ClassInjector.DerivedConstructorPointer<BlockMetal>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public static BlockMetal Spawn(Vector3NET _position)
    {
        return spawn<BlockMetal>(_position, "metal");
    }

    [HideFromIl2Cpp]
    protected override GameObject getVisPrefab()
    {
        return MarioResources.LoadBlockMetalPrefab();
    }

    [HideFromIl2Cpp]
    protected override void onBreak(MarioStateSyncer _mario)
    {
        _mario.GiveCap(MarioStateSyncer.MarioCap.Metal, 20, true);
    }
}
