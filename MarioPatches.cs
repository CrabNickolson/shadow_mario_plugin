using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HarmonyLib;
using UnityEngine;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class MarioPatches
{
    [HarmonyPatch(typeof(MiPlayerInput), nameof(MiPlayerInput.checkMovementType))]
    [HarmonyPrefix]
    public static bool patchCheckMovementType()
    {
        // prevent controller rumble when pressing crouch button
        var leaderMario = MarioSceneHandler.instance != null ? MarioSceneHandler.instance.leaderMario : null;
        if (leaderMario != null && MarioInputProvider.isMarioInputActive(leaderMario.m_character))
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(MiGameInput), nameof(MiGameInput.devBeam))]
    [HarmonyPrefix]
    public static bool patchDevBeam(MiCharacter _player, ref Vector3 _v3TargetPosition)
    {
        // allow mario to be teleported outside of navmesh
        var mario = MarioStateSyncer.GetCharMario(_player);
        if (mario != null && MiInputState.eInputState == MiInputState.InputState.Controller)
        {
            var cam = Mimimi.Cam.MiCamHandler.instance.activeCamera.TryCast<Mimimi.Cam.IMiPlayerControlledCam>();
            if (cam != null)
            {
                var input = cam.input?.m_viewconeInput?.moduleController;
                if (input != null)
                {
                    var ray = Camera.current.ViewportPointToRay(input.m_v2FreeCursorPosViewport);
                    if (Physics.Raycast(ray, out var rayHit, 200, MarioStateSyncer.marioColliderMask, QueryTriggerInteraction.Ignore))
                    {
                        _v3TargetPosition = rayHit.point;
                    }
                }
            }
        }

        return true;
    }

    [HarmonyPatch(typeof(MiUsableAmmoCrate), nameof(MiUsableAmmoCrate.onUse))]
    [HarmonyPrefix]
    public static bool patchPreOnUse(MiUsableAmmoCrate __instance, ref int __state)
    {
        __state = __instance.m_item.iCount;
        return true;
    }

    [HarmonyPatch(typeof(MiUsableAmmoCrate), nameof(MiUsableAmmoCrate.onUse))]
    [HarmonyPostfix]
    public static void patchPostOnUse(MiUsableAmmoCrate __instance, ref int __state)
    {
        // only spawn coin if character obtained ammo
        if (__instance.m_item.iCount < __state)
        {
            Vector3NET pos = __instance.transform.position.ToNET() + new Vector3NET(0, 0.5f, 0);
            Vector3NET force = new Vector3NET(0, 5, 0);
            Vector3NET spread = new Vector3NET(1.5f, 0, 1.5f);
            Coin.SpawnMultiple(pos, force, spread, 3, 10, _playSfx: true);
        }
    }

    [HarmonyPatch(typeof(MiFlying), nameof(MiFlying.startObject), typeof(Vector3), typeof(Il2CppStructArray<Vector3>), typeof(Il2CppStructArray<Vector3>))]
    [HarmonyPrefix]
    public static bool patchStartObject(MiFlying __instance, Vector3 _v3Target, ref Il2CppStructArray<Vector3> _arPositions)
    {
        if (_arPositions == null && __instance.m_target.m_eModifier == MarioStateSyncer.c_shootCharacterTargetModifier)
        {
            _arPositions = MarioStateSyncer.CalculateThrowTrajectory(__instance.m_transThis.position, _v3Target);
        }

        return true;
    }

    [HarmonyPatch(typeof(SkillCannonRecall), nameof(SkillCannonRecall.isTargetValidDynamic))]
    [HarmonyPrefix]
    public static bool patchIsTargetValidDynamic(MiCharacter _character)
    {
        // cannon recall breaks mario so lets disable it for now
        if (MarioStateSyncer.CharIsMario(_character))
        {
            return false;
        }

        return true;
    }
}
