using System;
using UnityEngine;
using System.Runtime.InteropServices;

using Vector2NET = System.Numerics.Vector2;
using Vector3NET = System.Numerics.Vector3;

namespace LibSM64
{
    internal static class Interop
    {
        public const float SCALE_FACTOR = 100.0f;

        public const int SM64_TEXTURE_WIDTH  = 64 * 11;
        public const int SM64_TEXTURE_HEIGHT = 64;
        public const int SM64_GEO_MAX_TRIANGLES = 1024;

        public const int SM64_MAX_HEALTH = 8;

        [StructLayout(LayoutKind.Sequential)]
        public struct SM64MarioInputs
        {
            public float camLookX, camLookZ;
            public float stickX, stickY;
            public byte buttonA, buttonB, buttonZ;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct SM64MarioState
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] position;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] velocity;
            public float faceAngle;
            public short health;
            public uint action;
            public uint flags;
            public uint particleFlags;
            public short invincTimer;

            public Vector3NET unityPosition {
                get { return position != null ? Utils.sm64ToUnityToPos(new Vector3NET(position[0], position[1], position[2])): Vector3NET.Zero; }
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        struct SM64MarioGeometryBuffers
        {
            public IntPtr position;
            public IntPtr normal;
            public IntPtr color;
            public IntPtr uv;
            public ushort numTrianglesUsed;
        };

        [StructLayout(LayoutKind.Sequential)]
        struct SM64ObjectTransform
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            float[] position;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            float[] eulerRotation;

            static public SM64ObjectTransform FromUnityWorld( Vector3 position, Quaternion rotation )
            {
                float[] vecToArr( Vector3 v )
                {
                    return new float[] { v.x, v.y, v.z };
                }

                float fmod( float a, float b )
                {
                    return a - b * Mathf.Floor( a / b );
                }
                
                float fixAngle( float a )
                {
                    return fmod( a + 180.0f, 360.0f ) - 180.0f;
                }

                var pos = SCALE_FACTOR * Vector3.Scale( position, new Vector3( -1, 1, 1 ));
                var rot = Vector3.Scale( rotation.eulerAngles, new Vector3( -1, 1, 1 ));

                rot.x = fixAngle( rot.x );
                rot.y = fixAngle( rot.y );
                rot.z = fixAngle( rot.z );

                return new SM64ObjectTransform {
                    position = vecToArr( pos ),
                    eulerRotation = vecToArr( rot )
                };
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        struct SM64SurfaceObject
        {
            public SM64ObjectTransform transform;
            public uint surfaceCount;
            public IntPtr surfaces;
        }

        [DllImport("sm64")]
        static extern void sm64_global_init( IntPtr rom, IntPtr outTexture, IntPtr debugPrintFunctionPtr );
        [DllImport("sm64")]
        static extern void sm64_global_terminate();

        [DllImport("sm64")]
        static extern void sm64_audio_init( IntPtr rom );
        [DllImport("sm64")]
        static extern uint sm64_audio_tick(uint numQueuedSamples, uint numDesiredSamples, IntPtr audio_buffer);

        [DllImport("sm64")]
        static extern void sm64_static_surfaces_load( SM64Surface[] surfaces, ulong numSurfaces );

        [DllImport("sm64")]
        static extern int sm64_mario_create( float marioX, float marioY, float marioZ );
        [DllImport("sm64")]
        static extern void sm64_mario_tick( int marioId, ref SM64MarioInputs inputs, ref SM64MarioState outState, ref SM64MarioGeometryBuffers outBuffers );
        [DllImport("sm64")]
        static extern void sm64_mario_delete( int marioId );

        [DllImport("sm64")]
        static extern void sm64_set_mario_action(int marioId, uint action);
        [DllImport("sm64")]
        static extern void sm64_set_mario_position(int marioId, float x, float y, float z);
        [DllImport("sm64")]
        static extern void sm64_set_mario_angle(int marioId, float x, float y, float z);
        [DllImport("sm64")]
        static extern void sm64_set_mario_velocity(int marioId, float x, float y, float z);
        [DllImport("sm64")]
        static extern void sm64_set_mario_forward_velocity(int marioId, float vel);
        [DllImport("sm64")]
        static extern void sm64_set_mario_invincibility(int marioId, short timer);
        [DllImport("sm64")]
        static extern void sm64_set_mario_water_level(int marioId, int level);
        [DllImport("sm64")]
        static extern void sm64_set_mario_gas_level(int marioId, int level);
        [DllImport("sm64")]
        static extern void sm64_set_mario_health(int marioId, ushort health);
        [DllImport("sm64")]
        static extern void sm64_mario_take_damage(int marioId, uint damage, uint subtype, float x, float y, float z);
        [DllImport("sm64")]
        static extern void sm64_mario_heal(int marioId, byte healCounter);
        [DllImport("sm64")]
        static extern void sm64_mario_kill(int marioId);
        [DllImport("sm64")]
        static extern void sm64_mario_interact_cap(int marioId, uint capFlag, ushort capTime, byte playMusic);

        [DllImport("sm64")]
        static extern uint sm64_surface_object_create( ref SM64SurfaceObject surfaceObject );
        [DllImport("sm64")]
        static extern void sm64_surface_object_move( uint objectId, ref SM64ObjectTransform transform );
        [DllImport("sm64")]
        static extern void sm64_surface_object_delete( uint objectId );

        [DllImport("sm64")]
        static extern void sm64_play_music(byte player, ushort seqArgs, ushort fadeTimer);
        [DllImport("sm64")]
        static extern void sm64_stop_background_music(ushort seqId);
        [DllImport("sm64")]
        static extern void sm64_play_sound(int soundBits, IntPtr pos);
        [DllImport("sm64")]
        static extern void sm64_play_sound_global(int soundBits);
        [DllImport("sm64")]
        static extern void sm64_set_sound_volume(float vol);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void DebugPrintFuncDelegate(string str);

        static public Texture2D marioTexture { get; private set; }
        static public bool isGlobalInit { get; private set; }

        //[AOT.MonoPInvokeCallback(typeof(DebugPrintFuncDelegate))]
        static void debugPrintCallback(string str)
        {
            ShadowMario.Plugin.PluginLog.LogInfo("libsm64: " + str);
        }

        public static void GlobalInit( byte[] rom )
        {
            var callbackDelegate = new DebugPrintFuncDelegate( debugPrintCallback );
            var romHandle = GCHandle.Alloc( rom, GCHandleType.Pinned );
            var textureData = new byte[ 4 * SM64_TEXTURE_WIDTH * SM64_TEXTURE_HEIGHT ];
            var textureDataHandle = GCHandle.Alloc( textureData, GCHandleType.Pinned );

            sm64_global_init( romHandle.AddrOfPinnedObject(), textureDataHandle.AddrOfPinnedObject(), Marshal.GetFunctionPointerForDelegate( callbackDelegate ));

            Color32[] cols = new Color32[ SM64_TEXTURE_WIDTH * SM64_TEXTURE_HEIGHT ];
            marioTexture = new Texture2D( SM64_TEXTURE_WIDTH, SM64_TEXTURE_HEIGHT );
            for( int ix = 0; ix < SM64_TEXTURE_WIDTH; ix++)
            for( int iy = 0; iy < SM64_TEXTURE_HEIGHT; iy++)
            {
                cols[ix + SM64_TEXTURE_WIDTH*iy] = new Color32(
                    textureData[4*(ix + SM64_TEXTURE_WIDTH*iy)+0],
                    textureData[4*(ix + SM64_TEXTURE_WIDTH*iy)+1],
                    textureData[4*(ix + SM64_TEXTURE_WIDTH*iy)+2],
                    textureData[4*(ix + SM64_TEXTURE_WIDTH*iy)+3]
                );
            }
            marioTexture.SetPixels32( cols );
            marioTexture.Apply();

            romHandle.Free();
            textureDataHandle.Free();

            isGlobalInit = true;
        }

        public static void GlobalTerminate()
        {
            sm64_global_terminate();
            marioTexture = null;
            isGlobalInit = false;
        }

        public static void AudioInit(byte[] rom)
        {
            var romHandle = GCHandle.Alloc(rom, GCHandleType.Pinned);
            sm64_audio_init(romHandle.AddrOfPinnedObject());
            romHandle.Free();
        }

        public static uint AudioTick(uint numQueuedSamples, uint numDesiredSamples, float[] audio_buffer)
        {
            var audioData = new short[544 * 2 * 2];
            var audioDataHandle = GCHandle.Alloc(audioData, GCHandleType.Pinned);

            uint numSamples = sm64_audio_tick(numQueuedSamples, numDesiredSamples, audioDataHandle.AddrOfPinnedObject());

            for (int i = 0; i < numSamples * 2 * 2; i++)
            {
                audio_buffer[i] = (float)audioData[i] / short.MaxValue;
            }

            audioDataHandle.Free();

            return numSamples;
        }

        public static void StaticSurfacesLoad( SM64Surface[] surfaces )
        {
            sm64_static_surfaces_load( surfaces, (ulong)surfaces.Length );
        }

        public static int MarioCreate( Vector3 marioPos )
        {
            return sm64_mario_create( (short)marioPos.x, (short)marioPos.y, (short)marioPos.z );
        }

        public static SM64MarioState MarioTick( int marioId, SM64MarioInputs inputs, Vector3NET[] positionBuffer, Vector3NET[] normalBuffer, Vector3NET[] colorBuffer, Vector2NET[] uvBuffer, out int numTriangles )
        {
            SM64MarioState outState = new SM64MarioState();

            var posHandle = GCHandle.Alloc( positionBuffer, GCHandleType.Pinned );
            var normHandle = GCHandle.Alloc( normalBuffer, GCHandleType.Pinned );
            var colorHandle = GCHandle.Alloc( colorBuffer, GCHandleType.Pinned );
            var uvHandle = GCHandle.Alloc( uvBuffer, GCHandleType.Pinned );

            SM64MarioGeometryBuffers buff = new SM64MarioGeometryBuffers
            {
                position = posHandle.AddrOfPinnedObject(),
                normal = normHandle.AddrOfPinnedObject(),
                color = colorHandle.AddrOfPinnedObject(),
                uv = uvHandle.AddrOfPinnedObject()
            };

            sm64_mario_tick( marioId, ref inputs, ref outState, ref buff );

            posHandle.Free();
            normHandle.Free();
            colorHandle.Free();
            uvHandle.Free();

            numTriangles = buff.numTrianglesUsed;

            return outState;
        }

        public static void MarioDelete( int marioId )
        {
            sm64_mario_delete( marioId );
        }

        public static void MarioSetAction(int marioId, uint action)
        {
            sm64_set_mario_action(marioId, action);
        }

        public static void MarioSetPosition(int marioId, float x, float y, float z)
        {
            sm64_set_mario_position(marioId, x, y, z);
        }

        public static void MarioSetAngle(int marioId, float x, float y, float z)
        {
            sm64_set_mario_angle(marioId, x, y, z);
        }

        public static void MarioSetVelocity(int marioId, float x, float y, float z)
        {
            sm64_set_mario_velocity(marioId, x, y, z);
        }

        public static void MarioSetForwardVelocity(int marioId, float vel)
        {
            sm64_set_mario_forward_velocity(marioId, vel);
        }

        public static void MarioSetInvincibility(int marioId, short timer)
        {
            sm64_set_mario_invincibility(marioId, timer);
        }

        public static void MarioSetWaterLevel(int marioId, int level)
        {
            sm64_set_mario_water_level(marioId, level);
        }

        public static void MarioSetGasLevel(int marioId, int level)
        {
            sm64_set_mario_gas_level(marioId, level);
        }

        public static void MarioSetHealth(int marioId, ushort health)
        {
            sm64_set_mario_health(marioId, health);
        }

        public static void MarioTakeDamage(int marioId, uint damage, uint subtype, float x, float y, float z)
        {
            sm64_mario_take_damage(marioId, damage, subtype, x, y, z);
        }

        public static void MarioHeal(int marioId, byte healCounter)
        {
            sm64_mario_heal(marioId, healCounter);
        }

        public static void MarioKill(int marioId)
        {
            sm64_mario_kill(marioId);
        }

        public static void MarioInteractCap(int marioId, uint capFlag, ushort capTime, bool playMusic)
        {
            sm64_mario_interact_cap(marioId, capFlag, capTime, playMusic ? (byte)1 : (byte)0);
        }

        public static uint SurfaceObjectCreate( Vector3 position, Quaternion rotation, SM64Surface[] surfaces )
        {
            var surfListHandle = GCHandle.Alloc( surfaces, GCHandleType.Pinned );
            var t = SM64ObjectTransform.FromUnityWorld( position, rotation );

            SM64SurfaceObject surfObj = new SM64SurfaceObject
            {
                transform = t,
                surfaceCount = (uint)surfaces.Length,
                surfaces = surfListHandle.AddrOfPinnedObject()
            };

            uint result = sm64_surface_object_create( ref surfObj );

            surfListHandle.Free();

            return result;
        }

        public static void SurfaceObjectMove( uint id, Vector3 position, Quaternion rotation )
        {
            var t = SM64ObjectTransform.FromUnityWorld( position, rotation );
            sm64_surface_object_move( id, ref t );
        }

        public static void SurfaceObjectDelete( uint id )
        {
            sm64_surface_object_delete( id );
        }

        public static void PlayMusic(byte player, ushort seqArgs, ushort fadeTimer)
        {
            sm64_play_music(player, seqArgs, fadeTimer);
        }

        public static void StopMusic(ushort seqId)
        {
            sm64_stop_background_music(seqId);
        }

        public static void PlaySound(int soundBits, Vector3NET pos)
        {
            var posData = new [] { pos.X, pos.Y, pos.Z };
            var posHandle = GCHandle.Alloc(posData, GCHandleType.Pinned);

            sm64_play_sound(soundBits, posHandle.AddrOfPinnedObject());

            posHandle.Free();
        }

        public static void PlaySoundGlobal(int soundBits)
        {
            sm64_play_sound_global(soundBits);
        }

        public static void SetSoundVolume(float vol)
        {
            sm64_set_sound_volume(vol);
        }
    }
}
