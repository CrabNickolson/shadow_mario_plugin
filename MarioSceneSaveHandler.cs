using System.Collections;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using PirateBase;
using LibSM64;

using Vector2NET = System.Numerics.Vector2;
using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class MarioSceneSaveHandler : ModSaveable
{
    private int m_collectedCoins;
    private int m_collectedStars;
    private int m_coinStarAmount = 50;

    private int m_hueIndex;
    private static readonly float[] s_hues = new[] { 0f, 0.35f, 0.115f, 0.7f };

    private bool m_gotCoinStar;
    private int m_musicID = -1;

    private GUIStyle m_guiStyle;

    private static MarioSceneSaveHandler s_instance;
    public static MarioSceneSaveHandler instance => s_instance;

    public static void Init()
    {
        if (s_instance != null)
            return;

        Plugin.PluginLog.LogInfo("Spawning Mario Scene Save Handler...");

        var handlerGo = new GameObject("sm64_scene_save_handler");
        handlerGo.transform.SetParent(SaveLoadSceneManager.transGetRoot(), false);
        handlerGo.AddComponent<MarioSceneSaveHandler>();
    }

    public MarioSceneSaveHandler(System.IntPtr ptr) : base(ptr) { }

    public MarioSceneSaveHandler() : base(ClassInjector.DerivedConstructorPointer<MarioSceneSaveHandler>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    [HideFromIl2Cpp]
    protected override void serializeMod(ref ModSerializeHelper _helper)
    {
        base.serializeMod(ref _helper);

        _helper.Serialize(0, m_collectedCoins);
        _helper.Serialize(1, m_collectedStars);
        _helper.Serialize(2, m_coinStarAmount);
        _helper.Serialize(3, m_hueIndex);
        _helper.Serialize(4, m_gotCoinStar);
        _helper.Serialize(5, m_musicID);
    }

    [HideFromIl2Cpp]
    protected override void deserializeMod(ref ModDeserializeHelper _helper)
    {
        base.deserializeMod(ref _helper);

        _helper.Deserialize(0, ref m_collectedCoins);
        _helper.Deserialize(1, ref m_collectedStars);
        _helper.Deserialize(2, ref m_coinStarAmount);
        _helper.Deserialize(3, ref m_hueIndex);
        _helper.Deserialize(4, ref m_gotCoinStar);
        _helper.Deserialize(5, ref m_musicID);
    }

    public override void AwakeDelayed()
    {
        s_instance = this;
    }

    public override void OnLoadingFinished()
    {
        base.OnLoadingFinished();

        if (m_musicID != (int)SeqId.SEQ_EVENT_METAL_CAP)
            SM64Context.StopMusic(SeqId.SEQ_EVENT_METAL_CAP);
        if (m_musicID != (int)SeqId.SEQ_EVENT_POWERUP)
            SM64Context.StopMusic(SeqId.SEQ_EVENT_POWERUP);

        if (m_musicID >= 0)
        {
            PlayMusic((SeqId)m_musicID);
        }
        else
        {
            StopMusic();
        }
    }

    public override void OnDestroy()
    {
        s_instance = null;
    }

    public void AddCoins(int _amount, Vector3NET _pos)
    {
        m_collectedCoins += Mathf.Max(_amount, 0);

        if (!m_gotCoinStar && m_collectedCoins >= m_coinStarAmount)
        {
            m_gotCoinStar = true;
            Star.Spawn(_pos + new Vector3NET(0, 3, 0), true);
        }
    }

    [HideFromIl2Cpp]
    public void ResetCoins()
    {
        m_collectedCoins = 0;
    }

    [HideFromIl2Cpp]
    public void AddStar()
    {
        m_collectedStars++;
    }

    [HideFromIl2Cpp]
    public void ResetStars()
    {
        m_collectedStars = 0;
    }

    [HideFromIl2Cpp]
    public void SetCoinStarAmount(int _amount)
    {
        m_coinStarAmount = _amount;
    }

    [HideFromIl2Cpp]
    public float GetMarioHue()
    {
        float hue = s_hues[m_hueIndex];
        m_hueIndex = (m_hueIndex + 1) % s_hues.Length;
        return hue;
    }

    [HideFromIl2Cpp]
    public void PlayMusic(SeqId _musicID)
    {
        m_musicID = (int)_musicID;
        SM64Context.PlayMusic(0, _musicID, 100);
    }

    [HideFromIl2Cpp]
    public void StopMusic()
    {
        if (m_musicID >= 0)
        {
            SM64Context.StopMusic((SeqId)m_musicID);
            m_musicID = -1;
        }
    }

    private void OnGUI()
    {
        if (MarioSceneHandler.instance == null)
            return;

        if (MiInputState.eInputState != MiInputState.InputState.Controller
            || MiCutsceneHandler.bCutsceneRunning)
            return;

        if (m_guiStyle == null)
        {
            var op = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Font>("Assets/graphics/font/Vollkorn-Regular.ttf");
            op.WaitForCompletion();
            var font = op.Result;

            m_guiStyle = new GUIStyle(GUI.skin.label);
            m_guiStyle.fontSize = 30;
            m_guiStyle.font = font;
        }

        var marioHandler = MarioSceneHandler.instance.leaderMario;

        if (!Mimimi.HUB.HUBLogic.bInstanceExists)
        {
            Vector2NET hudPosition = marioHandler != null ? new Vector2NET(80, 150) : new Vector2NET(80, 400);
            GUI.color = new Color(0, 0, 0, 0.7f);
            drawMarioLabelGUI(hudPosition + new Vector2NET(2, 2));
            GUI.color = Color.white;
            drawMarioLabelGUI(hudPosition);
        }

        if (Plugin.PluginConfig.debug.displayHUD.Value && marioHandler != null)
        {
            var mario = marioHandler.mario;

            GUILayout.BeginArea(new Rect(10, 10, 800, 80), GUI.skin.box);
            GUILayout.Label($"surfaces:{SM64Context.surfaceStreaming?.surfaceCount ?? 0}\n" +
                $"mayro h:{mario.health} f:{mario.faceAngle * Mathf.Rad2Deg:0.000} v:{mario.velocity:0.000}\n" +
                $"a:{mario.action}\n" +
                $"f:{mario.flags}");
            GUILayout.EndArea();
        }
    }

    private void drawMarioLabelGUI(Vector2NET _pos)
    {
        GUILayout.BeginArea(new Rect(_pos.X, _pos.Y, 300, 300));
        GUILayout.Label($"Stars: {m_collectedStars}", m_guiStyle);
        GUILayout.Label($"Coins: {m_collectedCoins}", m_guiStyle);
        GUILayout.EndArea();
    }
}
