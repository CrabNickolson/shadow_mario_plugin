using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using PirateBase;

namespace ShadowMario;

internal static class MarioSGAssets
{
    private static GameObject s_cnnKeyCannonShootFlying;
    private static string s_cnnKeyCannonShootFlyingKey = "Assets/Prefabs/character/player/cnn/skills/cnn_KeyCannonShoot_flying.prefab";

    private static GameObject s_vfxPlyJumpIntoWater;
    private static string s_vfxPlyJumpIntoWaterKey = "Assets/Prefabs/graphics/vfx/container/chars/player/vfx_ply_jump_into_water_00.prefab";

    public static void Init()
    {
        // need to load everything on boot, because a save might need it.
        // TODO need to actually check if we need to do this, but this is safer for now.

        get(ref s_cnnKeyCannonShootFlying, s_cnnKeyCannonShootFlyingKey);
        {
            // TODO also need to load dependencies manually, which is not ideal...
            ModModularContainer.LoadComponent<MiFlyingObjectWarrior>(s_cnnKeyCannonShootFlyingKey);
            ModModularContainer.LoadComponent<ParticleSystem>("Assets/Prefabs/graphics/vfx/particle_systems/chars/cannon_gal/ps_cnn_KeyCannonShoot_fly_trail_00.prefab");
            ModModularContainer.Load<Material>("Assets/graphics/vfx/trails/cnn_magicTrail_shoot_flying_00.mat");
        }

        get(ref s_vfxPlyJumpIntoWater, s_vfxPlyJumpIntoWaterKey);
    }

    private static T get<T>(ref T _field, string _path) where T : Object
    {
        if (_field == null)
            _field = ModModularContainer.Load<T>(_path);
        return _field;
    }

    public static GameObject cnnKeyCannonShootFlying => get(ref s_cnnKeyCannonShootFlying, s_cnnKeyCannonShootFlyingKey);
    public static GameObject vfxPlyJumpIntoWater => get(ref s_vfxPlyJumpIntoWater, s_vfxPlyJumpIntoWaterKey);
}
