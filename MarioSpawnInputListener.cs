using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Attributes;
using System.Collections;
using UnityEngine;
using LibSM64;

namespace ShadowMario;

internal class MarioSpawnInputListener : MonoBehaviour
{
    private float m_currentDuration = 0;

    private const float c_stickHoldDuration = 3;

    private void Update()
    {
        var rewired = PrimaryGameUser.rewiredPlayer;
        if (rewired == null)
            return;

        if (rewired.GetButton((int)MiInputActions.ContextActionToggle) && !rewired.GetButton((int)MiInputActions.HighlightAll))
        {
            m_currentDuration += Time.unscaledDeltaTime;
            if (m_currentDuration > c_stickHoldDuration)
            {
                m_currentDuration = 0;
                if (MarioStateSyncer.Spawn())
                    StartCoroutine(BepInEx.Unity.IL2CPP.Utils.Collections.CollectionExtensions.WrapToIl2Cpp(playHelloCoro()));
            }
        }
        else
        {
            m_currentDuration = 0;
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator playHelloCoro()
    {
        yield return new WaitForFixedUpdate();
        SM64Context.PlaySoundGlobal(SM64SoundBits.MarioHello);
    }
}
