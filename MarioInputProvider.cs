using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PirateBase;
using LibSM64;

using Vector2NET = System.Numerics.Vector2;
using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class MarioInputProvider : ISM64InputProvider
{
    private MiCharacter m_character;
    private bool m_isLocked;

    public bool isLocked
    {
        get => m_isLocked;
        set => m_isLocked = value;
    }

    public Vector3NET? forceLookDirection { get; set; }

    public MarioInputProvider(MiCharacter _character)
    {
        m_character = _character;
    }

    public bool GetButtonHeld(ISM64InputProvider.Button button)
    {
        if (!isInputActive())
            return false;

        var rewired = PrimaryGameUser.rewiredPlayer;

        switch (button)
        {
            case ISM64InputProvider.Button.Jump:
                return rewired.GetButton((int)MiInputActions.ContextAction);
            case ISM64InputProvider.Button.Kick:
                return rewired.GetButton((int)MiInputActions.DoSkill);
            case ISM64InputProvider.Button.Stomp:
                return rewired.GetButton((int)MiInputActions.ActivateSkill) || rewired.GetButton((int)MiInputActions.SkillWheel);
            default:
                return false;
        }
    }

    public Vector3NET GetCameraLookDirection()
    {
        if (forceLookDirection.HasValue)
            return forceLookDirection.Value;
        else if (Camera.main != null)
        {
            Vector3NET lookDir = Camera.main.transform.forward.ToNET();
            if (lookDir == -Vector3NET.UnitY)
                return Camera.main.transform.up.ToNET();
            else
                return lookDir;
        }
        else
            return Vector3NET.UnitZ;
    }

    public Vector2NET GetJoystickAxes()
    {
        Vector2NET result = Vector2NET.Zero;

        if (isInputActive())
        {
            var helper = MiCoreServices.GlobalManager.instance?.gameUserService?.primaryGameUser?.inputService?.inputHelper;
            if (helper != null)
            {
                result = helper.getAxisInput((int)MiInputActions.MoveHorizontal, (int)MiInputActions.MoveVertical).ToNET();
            }
        }

        if (result.Length() > 1)
            result = Vector2NET.Normalize(result);

        return result;
    }

    private bool isInputActive()
    {
        return !isLocked && isMarioInputActive(m_character);
    }

    public static bool isMarioInputActive(MiCharacter _character)
    {
        return _character != null && _character.bControllerPlayer(out var controllerPlayer)
            && controllerPlayer.bLeader
            && MiInputState.eInputState == MiInputState.InputState.Controller
            && !MiCutsceneHandler.bCutsceneRunning;
    }
}
