using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using PirateBase;

using Vector2NET = System.Numerics.Vector2;
using Vector3NET = System.Numerics.Vector3;

namespace LibSM64
{
    public class SM64Mario : MonoBehaviour
    {
        public Material material = null;
        public ISM64InputProvider inputProvider;
        public float hue;

        Vector3NET[][] positionBuffers;
        Vector3NET[][] normalBuffers;
        Vector3NET[] lerpPositionBuffer;
        Vector3NET[] lerpNormalBuffer;
        Vector3NET[] colorBuffer;
        Vector2NET[] uvBuffer;
        int numTriangles;
        int buffIndex;
        Interop.SM64MarioState[] states;

        Il2CppStructArray<Vector3> lerpPositionBufferIL2CPP;
        Il2CppStructArray<Vector3> lerpNormalBufferIL2CPP;
        Il2CppStructArray<Color> colorBufferIL2CPP;
        Il2CppStructArray<Vector2> uvBufferIL2CPP;

        GameObject marioRendererObject;
        Mesh marioMesh;
        int marioId;

        void OnEnable()
        {
            SM64Context.RegisterMario( this );

            SM64Context.surfaceStreaming.forceUpdateCullingState();

            Vector3 initPos = transform.position;
            Vector3 initMarioPos = new Vector3(-initPos.x, initPos.y, initPos.z) * Interop.SCALE_FACTOR;
            marioId = Interop.MarioCreate(initMarioPos);

            if( inputProvider == null )
                throw new System.Exception("Need to add an input provider component to Mario");

            marioRendererObject = new GameObject("MARIO");
            marioRendererObject.hideFlags |= HideFlags.HideInHierarchy;
            
            var renderer = marioRendererObject.AddComponent<MeshRenderer>();
            var meshFilter = marioRendererObject.AddComponent<MeshFilter>();

            states = new Interop.SM64MarioState[2] {
                new Interop.SM64MarioState(),
                new Interop.SM64MarioState()
            };

            states[0].position = new[] { initMarioPos.x, initMarioPos.y, initMarioPos.z };
            states[1].position = new[] { initMarioPos.x, initMarioPos.y, initMarioPos.z };

            renderer.material = new Material(material);
            renderer.sharedMaterial.SetTexture("_MainTex", Interop.marioTexture);
            renderer.sharedMaterial.SetFloat("_HueShift", hue);

            marioRendererObject.transform.localScale = new Vector3( -1, 1, 1 ) / Interop.SCALE_FACTOR;
            marioRendererObject.transform.localPosition = Vector3.zero;

            lerpPositionBuffer = new Vector3NET[3 * Interop.SM64_GEO_MAX_TRIANGLES];
            lerpNormalBuffer = new Vector3NET[3 * Interop.SM64_GEO_MAX_TRIANGLES];
            positionBuffers = new Vector3NET[][] { new Vector3NET[3 * Interop.SM64_GEO_MAX_TRIANGLES], new Vector3NET[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
            normalBuffers = new Vector3NET[][] { new Vector3NET[3 * Interop.SM64_GEO_MAX_TRIANGLES], new Vector3NET[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
            colorBuffer = new Vector3NET[3 * Interop.SM64_GEO_MAX_TRIANGLES];
            uvBuffer = new Vector2NET[3 * Interop.SM64_GEO_MAX_TRIANGLES];

            lerpPositionBufferIL2CPP = new Il2CppStructArray<Vector3>(3 * Interop.SM64_GEO_MAX_TRIANGLES);
            lerpNormalBufferIL2CPP = new Il2CppStructArray<Vector3>(3 * Interop.SM64_GEO_MAX_TRIANGLES);
            colorBufferIL2CPP = new Il2CppStructArray<Color>(3 * Interop.SM64_GEO_MAX_TRIANGLES);
            uvBufferIL2CPP = new Il2CppStructArray<Vector2>(3 * Interop.SM64_GEO_MAX_TRIANGLES);

            numTriangles = Interop.SM64_GEO_MAX_TRIANGLES;

            marioMesh = new Mesh();
            marioMesh.vertices = lerpPositionBuffer.ToIL2CPP();
            marioMesh.triangles = createTriangleArray(3 * Interop.SM64_GEO_MAX_TRIANGLES);
            meshFilter.sharedMesh = marioMesh;
        }

        void OnDisable()
        {
            if( marioRendererObject != null )
            {
                Destroy( marioRendererObject );
                marioRendererObject = null;
            }

            if( Interop.isGlobalInit )
            {
                SM64Context.UnregisterMario( this );
                Interop.MarioDelete( marioId );
            }
        }

        public Vector3NET position => states[buffIndex].unityPosition;
        public Vector3NET velocity => states[buffIndex].velocity != null ? new Vector3NET(states[buffIndex].velocity[0], states[buffIndex].velocity[1], states[buffIndex].velocity[2]) : Vector3NET.Zero;
        public float faceAngle => -states[buffIndex].faceAngle;
        public short health => states[buffIndex].health;
        public SM64MarioAction action => (SM64MarioAction)states[buffIndex].action;
        public SM64MarioFlag flags => (SM64MarioFlag)states[buffIndex].flags;
        public uint particleFlags => states[buffIndex].particleFlags;
        public short invincTimer => states[buffIndex].invincTimer;

        public GameObject rendererObject => marioRendererObject;

        public void contextFixedUpdate()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var inputs = new Interop.SM64MarioInputs();
            var look = inputProvider.GetCameraLookDirection();
            look.Y = 0;
            look = Vector3NET.Normalize(look);

            var joystick = inputProvider.GetJoystickAxes();

            inputs.camLookX = -look.X;
            inputs.camLookZ = look.Z;
            inputs.stickX = joystick.X;
            inputs.stickY = -joystick.Y;
            inputs.buttonA = inputProvider.GetButtonHeld( ISM64InputProvider.Button.Jump  ) ? (byte)1 : (byte)0;
            inputs.buttonB = inputProvider.GetButtonHeld( ISM64InputProvider.Button.Kick  ) ? (byte)1 : (byte)0;
            inputs.buttonZ = inputProvider.GetButtonHeld( ISM64InputProvider.Button.Stomp ) ? (byte)1 : (byte)0;

            states[buffIndex] = Interop.MarioTick(marioId, inputs, positionBuffers[buffIndex], normalBuffers[buffIndex], colorBuffer, uvBuffer, out int newNumTriangles);

            if (newNumTriangles != numTriangles)
            {
                ShadowMario.Plugin.PluginLog.LogInfo($"triangle count changed {numTriangles} -> {newNumTriangles}");

                numTriangles = newNumTriangles;
                marioMesh.triangles = createTriangleArray(3 * numTriangles);
            }

            colorBuffer.CopyColorToIL2CPP(colorBufferIL2CPP, 1f);
            uvBuffer.CopyToIL2CPP(uvBufferIL2CPP);
            marioMesh.colors = colorBufferIL2CPP;
            marioMesh.uv = uvBufferIL2CPP;

            buffIndex = 1 - buffIndex;

            sw.Stop();
            if (ShadowMario.Plugin.PluginConfig.debug.logPerformance.Value)
                ShadowMario.Plugin.PluginLog.LogInfo($"SM64Mario.contextFixedUpdate: {sw.Elapsed.TotalMilliseconds:0.00}ms");
        }

        public void contextUpdate()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            int j = 1 - buffIndex;

            for ( int i = 0; i < lerpPositionBuffer.Length; ++i )
            {
                lerpPositionBuffer[i] = Vector3NET.Lerp( positionBuffers[buffIndex][i], positionBuffers[j][i], t );
                lerpNormalBuffer[i] = Vector3NET.Lerp( normalBuffers[buffIndex][i], normalBuffers[j][i], t );
            }

            transform.position = Vector3NET.Lerp( states[buffIndex].unityPosition, states[j].unityPosition, t ).ToIL2CPP();

            lerpPositionBuffer.CopyToIL2CPP(lerpPositionBufferIL2CPP);
            lerpNormalBuffer.CopyToIL2CPP(lerpNormalBufferIL2CPP);

            marioMesh.vertices = lerpPositionBufferIL2CPP;
            marioMesh.normals = lerpNormalBufferIL2CPP;

            marioMesh.RecalculateBounds();
            marioMesh.RecalculateTangents();

            sw.Stop();
            if (ShadowMario.Plugin.PluginConfig.debug.logPerformance.Value)
                ShadowMario.Plugin.PluginLog.LogInfo($"SM64Mario.contextUpdate: {sw.Elapsed.TotalMilliseconds:0.00}ms");
        }

        private static Il2CppStructArray<int> createTriangleArray(int _triangleCount)
        {
            var array = new Il2CppStructArray<int>(_triangleCount);
            for (int i = 0; i < _triangleCount; i++)
                array[i] = i;
            return array;
        }

        public void setAction(SM64MarioAction action, bool _applyImmediately = false)
        {
            Interop.MarioSetAction(marioId, (uint)action);
            if (_applyImmediately)
            {
                states[0].action = (uint)action;
                states[1].action = (uint)action;
            }
        }

        public void setPosition(Vector3NET pos)
        {
            pos = Utils.unityToSM64Pos(pos);
            Interop.MarioSetPosition(marioId, pos.X, pos.Y, pos.Z);
        }

        public void setAngle(Vector3NET angle)
        {
            Interop.MarioSetAngle(marioId, angle.X, angle.Y, angle.Z);
        }

        public void setVelocity(Vector3NET vel)
        {
            Interop.MarioSetVelocity(marioId, vel.X, vel.Y, vel.Z);
        }

        public void setForwardVelocity(float vel)
        {
            Interop.MarioSetForwardVelocity(marioId, vel);
        }

        public void setInvincibility(short timer)
        {
            Interop.MarioSetInvincibility(marioId, timer);
        }

        public void setWaterLevel(float level)
        {
            Interop.MarioSetWaterLevel(marioId, (int)(level * Interop.SCALE_FACTOR));
        }

        public void setGasLevel(float level)
        {
            Interop.MarioSetGasLevel(marioId, (int)(level * Interop.SCALE_FACTOR));
        }

        public void setHealth(ushort health)
        {
            Interop.MarioSetHealth(marioId, health);
        }

        public void takeDamage(uint damage, uint subtype, Vector3NET pos)
        {
            pos = Utils.unityToSM64Pos(pos);
            Interop.MarioTakeDamage(marioId, damage, subtype, pos.X, pos.Y, pos.Z);
        }

        public void heal(byte healCounter)
        {
            Interop.MarioHeal(marioId, healCounter);
        }

        public void kill()
        {
            Interop.MarioKill(marioId);
        }

        public void interactCap(uint capFlag, ushort capTime, bool playMusic)
        {
            Interop.MarioInteractCap(marioId, capFlag, capTime, playMusic);
        }


        void OnDrawGizmos()
        {
            if( !Application.isPlaying )
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere( transform.position, 0.5f );
            }
        }
    }
}