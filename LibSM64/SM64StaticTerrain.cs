using System.Collections.Generic;
using UnityEngine;

namespace LibSM64
{
    public class SM64StaticTerrain : MonoBehaviour
    {
        SM64TerrainType terrainType = SM64TerrainType.Grass;
        SM64SurfaceType surfaceType = SM64SurfaceType.Default;

        private bool m_activeSurface = true;

        public SM64TerrainType TerrainType { get => terrainType; set => terrainType = value; }
        public SM64SurfaceType SurfaceType { get => surfaceType; set => surfaceType = value; }

        public bool activeSurface
        {
            get => m_activeSurface;
            set => m_activeSurface = value;
        }
    }
}