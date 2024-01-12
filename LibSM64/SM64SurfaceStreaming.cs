using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using PirateBase;
using ShadowMario;

using Vector3NET = System.Numerics.Vector3;

namespace LibSM64
{
    public class SM64SurfaceStreaming
    {
        private float m_streamRadius;
        private bool m_displayDebugMesh;

        private List<SM64StreamedTerrain> m_streamedTerrains;
        private List<SM64Surface> m_staticSurfaces;
        private int m_surfaceCount;

        private Camera.CameraCallback m_onPostRenderCallback;

        public int surfaceCount => m_surfaceCount;

        internal SM64SurfaceStreaming(float _streamRadius, bool _displayDebugMesh = false)
        {
            m_streamRadius = _streamRadius;
            m_displayDebugMesh = _displayDebugMesh;

            updateTerrains();

            if (m_displayDebugMesh)
            {
                m_onPostRenderCallback = (Camera.CameraCallback)onPostRender;
                Camera.onPostRender += m_onPostRenderCallback;
            }
        }

        internal void Dispose()
        {
            if (m_onPostRenderCallback != null)
            {
                Camera.onPostRender -= m_onPostRenderCallback;
                m_onPostRenderCallback = null;
            }
        }

        internal void Update()
        {
            forceUpdateCullingState();
        }

        private void onPostRender(Camera _camera)
        {
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(1 /*GL.LINES*/);

            GL.Color(Color.blue);

            Vector3NET offset = new Vector3NET(0, 0.05f, 0);

            foreach (var terrain in m_streamedTerrains)
            {
                if (terrain.activeSurface)
                {
                    for (int i = 0; i < terrain.m_surfaces.Length; i++)
                    {
                        if (terrain.m_surfaceCullingStates[i])
                        {
                            var surface = terrain.m_surfaces[i];

                            var terrainType = (SM64TerrainType)surface.terrain;
                            if (terrainType == SM64TerrainType.Grass)
                                GL.Color(Color.green);
                            else if (terrainType == SM64TerrainType.Sand)
                                GL.Color(Color.yellow);
                            else
                                GL.Color(Color.blue);

                            Vector3 p1 = (Utils.sm64ToUnityToPos(new Vector3NET(surface.v0x, surface.v0y, surface.v0z)) + offset).ToIL2CPP();
                            Vector3 p2 = (Utils.sm64ToUnityToPos(new Vector3NET(surface.v1x, surface.v1y, surface.v1z)) + offset).ToIL2CPP();
                            Vector3 p3 = (Utils.sm64ToUnityToPos(new Vector3NET(surface.v2x, surface.v2y, surface.v2z)) + offset).ToIL2CPP();

                            GL.Vertex(p1);
                            GL.Vertex(p2);

                            GL.Vertex(p1);
                            GL.Vertex(p3);

                            GL.Vertex(p2);
                            GL.Vertex(p3);
                        }
                    }
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        public void updateTerrains()
        {
            m_streamedTerrains = new List<SM64StreamedTerrain>(GameObject.FindObjectsOfType<SM64StreamedTerrain>());
            m_staticSurfaces = new List<SM64Surface>(Utils.GetAllStaticSurfaces());
        }

        private bool updateCullingStates()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            float boundsSizeSqr = m_streamRadius * m_streamRadius * 0.25f;

            bool anyChange = false;
            bool doReset = true;
            foreach (var mario in SM64Context.marios)
            {
                Vector3NET marioPos = mario.transform.position.ToNET();
                var marioBounds = new ModBounds(marioPos, new Vector3NET(m_streamRadius));

                System.Threading.Tasks.Parallel.ForEach(m_streamedTerrains, terrain =>
                {
                    ModThreadUtility.AttachThread();

                    bool overlap = terrain.isInBounds(marioBounds);
                    bool overlapChanged = overlap != terrain.activeSurface;
                    bool surfacesChanged = false;
                    if (overlap && terrain.useSurfaceCulling)
                        surfacesChanged = terrain.updateCulledSurfaces(marioPos, boundsSizeSqr, doReset || (overlapChanged && overlap));
                    if (overlapChanged || surfacesChanged)
                        anyChange = true;

                    if (doReset)
                        terrain.activeSurface = overlap;
                    else if (overlap)
                        terrain.activeSurface = true;
                });

                doReset = false;
            }

            sw.Stop();
            //if (ShadowMario.Plugin.PluginConfig.debug.logPerformance.Value)
            //    ShadowMario.Plugin.PluginLog.LogInfo($"SM64SurfaceCulling.updateCullingStates: {sw.Elapsed.TotalMilliseconds:0.00}ms");
            return anyChange;
        }

        public void refreshSurfaces()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var surfaces = createSurfaceArray();
            m_surfaceCount = surfaces.Length;
            SM64Context.RefreshStaticTerrain(surfaces);
            sw.Stop();
            //if (ShadowMario.Plugin.PluginConfig.debug.logPerformance.Value)
            //    ShadowMario.Plugin.PluginLog.LogInfo($"SM64SurfaceCulling.refreshSurfaces: {sw.Elapsed.TotalMilliseconds:0.00}ms");
        }

        private SM64Surface[] createSurfaceArray()
        {
            var surfaces = new List<SM64Surface>(m_staticSurfaces);

            foreach (var obj in m_streamedTerrains)
            {
                if (obj.activeSurface)
                {
                    if (obj.useSurfaceCulling)
                        addCulledSurfaces(obj, surfaces);
                    else
                        surfaces.AddRange(obj.m_surfaces);
                }
            }

            return surfaces.ToArray();
        }

        private static void addCulledSurfaces(SM64StreamedTerrain _terrain, List<SM64Surface> _surfaces)
        {
            for (int i = 0; i < _terrain.m_surfaces.Length; i++)
            {
                if (_terrain.m_surfaceCullingStates[i])
                    _surfaces.Add(_terrain.m_surfaces[i]);
            }
        }

        public void forceUpdateCullingState()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool anyChanges = updateCullingStates();
            if (anyChanges)
                refreshSurfaces();

            sw.Stop();
            //if (ShadowMario.Plugin.PluginConfig.debug.logPerformance.Value)
             //   ShadowMario.Plugin.PluginLog.LogInfo($"SM64SurfaceCulling.forceUpdateCullingState {sw.Elapsed.TotalMilliseconds:0.00}ms");
        }
    }
}
