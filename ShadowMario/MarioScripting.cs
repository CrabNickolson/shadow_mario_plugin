using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using System.Linq;
using UnityEngine;
using PirateBase;
using LibSM64;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class MarioScripting : Il2CppSystem.Object
{
    public MarioScripting(System.IntPtr ptr) : base(ptr) { }

    public MarioScripting() : base(ClassInjector.DerivedConstructorPointer<MarioScripting>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    [ModScriptMethod("sm64-get-selected-mario")]
    public MiCharacter GetSelectedMario()
    {
        return MarioSceneHandler.instance.leaderMario?.m_character;
    }

    [ModScriptMethod("sm64-give-normal-cap")]
    public void GiveNormalCap(MiCharacter _character)
    {
        giveCap(_character, 100, false, MarioStateSyncer.MarioCap.Normal);
    }

    [ModScriptMethod("sm64-give-metal-cap")]
    public void GiveMetalCap(MiCharacter _character, float _duration, bool _playMusic)
    {
        giveCap(_character, _duration, _playMusic, MarioStateSyncer.MarioCap.Metal);
    }

    [ModScriptMethod("sm64-give-fly-cap")]
    public void GiveFlyCap(MiCharacter _character, float _duration, bool _playMusic)
    {
        giveCap(_character, _duration, _playMusic, MarioStateSyncer.MarioCap.Wing);
    }

    private void giveCap(MiCharacter _character, float _duration, bool _playMusic, MarioStateSyncer.MarioCap _cap)
    {
        if (_character == null)
            return;
        var mario = MarioStateSyncer.GetCharMario(_character);
        if (mario == null)
            return;

        mario.GiveCap(_cap, _duration, _playMusic);
    }

    [ModScriptMethod("sm64-spawn-star")]
    public void SpawnStar(Vector3 _position, bool _playMusic)
    {
        Star.Spawn(_position.ToNET(), _playMusic);
    }

    [ModScriptMethod("sm64-reset-star-counter")]
    public void ResetStarCounter()
    {
        if (MarioSceneSaveHandler.instance != null)
            MarioSceneSaveHandler.instance.ResetStars();
    }

    [ModScriptMethod("sm64-spawn-coin-static")]
    public void SpawnCoinStatic(Vector3 _position)
    {
        Coin.SpawnStatic(_position.ToNET(), _addModdedComponent: true);
    }

    [ModScriptMethod("sm64-spawn-coin")]
    public void SpawnCoin(Vector3 _position, Vector3 _velocity, float _lifetime)
    {
        Coin.Spawn(_position.ToNET(), _velocity.ToNET(), _lifetime);
    }

    [ModScriptMethod("sm64-spawn-coin-multiple")]
    public void SpawnCoinMultiple(Vector3 _position, Vector3 _velocity, Vector3 _spread, int _count, float _lifetime)
    {
        Coin.SpawnMultiple(_position.ToNET(), _velocity.ToNET(), _spread.ToNET(), _count, _lifetime);
    }

    [ModScriptMethod("sm64-reset-coin-counter")]
    public void ResetCoinCounter()
    {
        if (MarioSceneSaveHandler.instance != null)
            MarioSceneSaveHandler.instance.ResetCoins();
    }

    [ModScriptMethod("sm64-coin-star-amount")]
    public void SetCoinStarAmount(int _amount)
    {
        if (MarioSceneSaveHandler.instance != null)
            MarioSceneSaveHandler.instance.SetCoinStarAmount(_amount);
    }

    [ModScriptMethod("sm64-list-music")]
    public string ListMusic()
    {
        return string.Join("\n", System.Enum.GetValues<SeqId>());
    }

    [ModScriptMethod("sm64-play-music")]
    public void PlayMusic(string _musicName)
    {
        if (System.Enum.TryParse<SeqId>(_musicName, out var seqID) && MarioSceneSaveHandler.instance != null)
            MarioSceneSaveHandler.instance.PlayMusic(seqID);
    }

    [ModScriptMethod("sm64-stop-music")]
    public void StopMusic()
    {
        if (MarioSceneSaveHandler.instance != null)
            MarioSceneSaveHandler.instance.StopMusic();
    }

    [ModScriptMethod("sm64-list-sounds")]
    public string ListSounds()
    {
        return string.Join("\n", typeof(SM64SoundBits).GetProperties().Select(x => x.Name));
    }

    [ModScriptMethod("sm64-play-sound-global")]
    public void PlaySoundGlobal(string _soundName)
    {
        var props = typeof(SM64SoundBits).GetProperties();
        foreach (var prop in props)
        {
            if (prop.Name == _soundName)
            {
                var bits = (int)prop.GetValue(null);
                SM64Context.PlaySoundGlobal(bits);
                return;
            }
        }
    }

    [ModScriptMethod("sm64-play-sound")]
    public void PlaySound(string _soundName, Vector3 _position)
    {
        var props = typeof(SM64SoundBits).GetProperties();
        foreach (var prop in props)
        {
            if (prop.Name == _soundName)
            {
                var bits = (int)prop.GetValue(null);
                SM64Context.PlaySound(bits, _position.ToNET());
                return;
            }
        }
    }

    [ModScriptMethod("sm64-ocean-is-lava")]
    public void OceanIsLava()
    {
        var goOcean = WaterController.FindOcean();
        if (goOcean != null && MarioSceneHandler.instance != null)
        {
            var obstacle = MarioObstacle.Spawn(new Vector3NET(0, goOcean.transform.position.y - 0.5f, 0), MarioObstacle.ObstacleType.Lava, _disableSurfaceCulling: true);
            obstacle.trans.localScale = new Vector3(256, 1, 256);

            MarioSceneHandler.instance.RegenerateTerrainAndUpdateStreaming();
        }
    }
}
