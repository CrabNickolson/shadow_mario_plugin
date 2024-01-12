using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using ShadowMario;

using Vector3NET = System.Numerics.Vector3;

namespace LibSM64
{
    public class SM64StreamedTerrain : MonoBehaviour
    {
        SM64TerrainType terrainType = SM64TerrainType.Grass;
        SM64SurfaceType surfaceType = SM64SurfaceType.Default;
        private bool m_useSurfaceCulling = false;
        private bool m_ignoreYCull = false;

        private ModBounds m_bounds;
        internal SM64Surface[] m_surfaces;
        private bool m_activeSurface = true;
        internal bool[] m_surfaceCullingStates;

        public SM64TerrainType TerrainType { get => terrainType; set => terrainType = value; }
        public SM64SurfaceType SurfaceType { get => surfaceType; set => surfaceType = value; }

        public bool useSurfaceCulling
        {
            get => m_useSurfaceCulling;
            set => m_useSurfaceCulling = value;
        }
        public bool ignoreYCull
        {
            get => m_ignoreYCull;
            set => m_ignoreYCull = value;
        }
        public bool activeSurface
        {
            get => m_activeSurface;
            set => m_activeSurface = value;
        }

        internal ModBounds bounds => m_bounds;

        private void Awake()
        {
            var mc = GetComponent<MeshCollider>();
            if (mc != null)
            {
                var surfaceList = new List<SM64Surface>();
                Utils.transformAndGetSurfaces(surfaceList, mc.sharedMesh, SurfaceType, TerrainType, x => mc.transform.TransformPoint(x));
                SetData(surfaceList.ToArray(), new ModBounds(mc.bounds));
            }
        }

        [HideFromIl2Cpp]
        public void SetData(SM64Surface[] _surfaces, ModBounds _bounds)
        {
            m_surfaces = _surfaces;
            m_surfaceCullingStates = new bool[_surfaces.Length];
            m_bounds = _bounds;
        }

        [HideFromIl2Cpp]
        internal bool isInBounds(ModBounds _bounds)
        {
            var terrainBounds = bounds;
            if (ignoreYCull)
                terrainBounds.extents += new Vector3NET(0, 20000, 0);
            return terrainBounds.Intersects(_bounds);
        }

        [HideFromIl2Cpp]
        internal bool updateCulledSurfaces(Vector3NET _pos, float _radiusSqr, bool _reset)
        {
            if (_reset)
            {
                for (int i = 0; i < m_surfaceCullingStates.Length; i++)
                    m_surfaceCullingStates[i] = false;
            }

            if (ignoreYCull)
            {
                _pos.Y = 0;
            }

            bool anyChange = false;

            for (int i = 0; i < m_surfaces.Length; i++)
            {
                Vector3NET p1 = Utils.sm64ToUnityToPos(new Vector3NET(m_surfaces[i].v0x, m_surfaces[i].v0y, m_surfaces[i].v0z));
                Vector3NET p2 = Utils.sm64ToUnityToPos(new Vector3NET(m_surfaces[i].v1x, m_surfaces[i].v1y, m_surfaces[i].v1z));
                Vector3NET p3 = Utils.sm64ToUnityToPos(new Vector3NET(m_surfaces[i].v2x, m_surfaces[i].v2y, m_surfaces[i].v2z));

                if (ignoreYCull)
                {
                    p1.Y = 0;
                    p2.Y = 0;
                    p3.Y = 0;
                }

                bool overlap = (getClosestPoint(p1, p2, _pos) - _pos).LengthSquared() < _radiusSqr
                    || (getClosestPoint(p2, p3, _pos) - _pos).LengthSquared() < _radiusSqr
                    || (getClosestPoint(p3, p1, _pos) - _pos).LengthSquared() < _radiusSqr;



                if (m_surfaceCullingStates[i] != overlap)
                    anyChange = true;
                m_surfaceCullingStates[i] |= overlap;
            }

            return anyChange;
        }

        private static Vector3NET getClosestPoint(Vector3NET _a, Vector3NET _b, Vector3NET _p)
        {
            Vector3NET AB = _b - _a;
            return (_a + AB * getClosestPointPercentage(_a, _b, _p));
        }

        private static float getClosestPointPercentage(Vector3NET _a, Vector3NET _b, Vector3NET _p)
        {
            Vector3NET ap = _p - _a;
            Vector3NET ab = _b - _a;
            float ab2 = ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z;
            float ap_ab = ap.X * ab.X + ap.Y * ab.Y + ap.Z * ab.Z;
            float t = ap_ab / ab2;

            if (t < 0.0f)
                t = 0.0f;
            else if (t > 1.0f)
                t = 1.0f;

            return t;
        }
    }
}
