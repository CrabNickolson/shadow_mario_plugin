using BepInEx;
using Il2CppSystem.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using Il2CppSystem;
using UnityEngine;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class BlockFly : BlockBase
{
    public BlockFly(System.IntPtr ptr) : base(ptr) { }

    public BlockFly() : base(ClassInjector.DerivedConstructorPointer<BlockMetal>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    public static BlockFly Spawn(Vector3NET _position)
    {
        return spawn<BlockFly>(_position, "fly");
    }

    [HideFromIl2Cpp]
    protected override GameObject getVisPrefab()
    {
        return MarioResources.LoadBlockFlyPrefab();
    }

    [HideFromIl2Cpp]
    protected override void onBreak(MarioStateSyncer _mario)
    {
        _mario.GiveCap(MarioStateSyncer.MarioCap.Wing, 20, true);
    }
}
