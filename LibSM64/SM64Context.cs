using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

using Vector3NET = System.Numerics.Vector3;

namespace LibSM64
{
    public class SM64Context : MonoBehaviour
    {
        static SM64Context s_instance = null;

        List<SM64Mario> _marios = new List<SM64Mario>();
        static List<SM64DynamicTerrain> _surfaceObjects = new List<SM64DynamicTerrain>();

        private SM64Audio m_audio;
        private SM64SurfaceStreaming m_surfaceStreaming;

        private bool m_updateActive = true;

        public static IReadOnlyList<SM64Mario> marios => s_instance._marios;
        public static SM64Audio audio => s_instance.m_audio;
        public static SM64SurfaceStreaming surfaceStreaming => s_instance.m_surfaceStreaming;

        void Awake()
        {
            Interop.GlobalInit(ShadowMario.MarioResources.romFile);
            RefreshStaticTerrain();
            m_surfaceStreaming = new SM64SurfaceStreaming(ShadowMario.Plugin.PluginConfig.terrain.streamRadius.Value, _displayDebugMesh: ShadowMario.Plugin.PluginConfig.debug.displayStreamingMesh.Value);
            m_audio = SM64Audio.Create(this.gameObject);
        }

        void Update()
        {
            if (!m_updateActive)
                return;

            foreach ( var o in _surfaceObjects )
                o.contextUpdate();

            foreach( var o in _marios )
                o.contextUpdate();
        }

        void FixedUpdate()
        {
            if (!m_updateActive)
                return;

            m_surfaceStreaming.Update();

            foreach( var o in _surfaceObjects )
                o.contextFixedUpdate();

            foreach( var o in _marios )
                o.contextFixedUpdate();
        }

        void OnAudioFilterRead(Il2CppStructArray<float> _data, int _channels)
        {
            if (m_audio != null)
                m_audio.OnAudioFilterRead(_data, _channels);
        }

        void OnDestroy()
        {
            if (m_audio != null)
            {
                m_audio.Dispose();
                m_audio = null;
            }

            if (m_surfaceStreaming != null)
            {
                m_surfaceStreaming.Dispose();
                m_surfaceStreaming = null;
            }

            Interop.GlobalTerminate();

            s_instance = null;
        }

        static void ensureInstanceExists()
        {
            if( s_instance == null )
            {
                var contextGo = new GameObject( "SM64_CONTEXT" );
                contextGo.hideFlags |= HideFlags.HideInHierarchy;
                s_instance = contextGo.AddComponent<SM64Context>();
            }
        }

        public static bool UpdateActive
        {
            get => s_instance.m_updateActive;
            set => s_instance.m_updateActive = value;
        }

        internal static void AudioInit()
        {
            Interop.AudioInit(ShadowMario.MarioResources.romFile);
        }

        static public uint AudioTick(uint numQueuedSamples, uint numDesiredSamples, float[] audioBuffer)
        {
            return Interop.AudioTick(numQueuedSamples, numDesiredSamples, audioBuffer);
        }

        static public void RefreshStaticTerrain()
        {
            Interop.StaticSurfacesLoad( Utils.GetAllStaticSurfaces());
        }

        static internal void RefreshStaticTerrain(SM64Surface[] _surfaces)
        {
            Interop.StaticSurfacesLoad(_surfaces);
        }

        static public void RegisterMario( SM64Mario mario )
        {
            ensureInstanceExists();

            if( !s_instance._marios.Contains( mario ))
                s_instance._marios.Add( mario );
        }

        static public void UnregisterMario( SM64Mario mario )
        {
            if( s_instance != null && s_instance._marios.Contains( mario ))
                s_instance._marios.Remove( mario );
        }

        static public void RegisterSurfaceObject( SM64DynamicTerrain surfaceObject )
        {
            if(!_surfaceObjects.Contains(surfaceObject))
                _surfaceObjects.Add(surfaceObject);
        }

        static public void UnregisterSurfaceObject( SM64DynamicTerrain surfaceObject )
        {
            _surfaceObjects.Remove(surfaceObject);
        }

        public static void PlayMusic(byte player, SeqId seqArgs, ushort fadeTimer)
        {
            Interop.PlayMusic(player, (ushort)seqArgs, fadeTimer);
        }

        public static void StopMusic(SeqId seqId)
        {
            Interop.StopMusic((ushort)seqId);
        }

        public static void PlaySound(int soundBits, Vector3NET pos)
        {
            if (s_instance == null)
                return;

            pos = Utils.unityToSM64Pos(pos);
            Interop.PlaySound(soundBits, pos);
        }

        public static void PlaySoundGlobal(int soundBits)
        {
            if (s_instance == null)
                return;

            Interop.PlaySoundGlobal(soundBits);
        }

        public static void SetSoundVolume(float vol)
        {
            Interop.SetSoundVolume(vol);
        }
    }
}