using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Vector3NET = System.Numerics.Vector3;

namespace LibSM64
{
    public static class Utils
    {
        public static SM64Surface CreateSurface(Vector3NET _p1, Vector3NET _p2, Vector3NET _p3, SM64SurfaceType _surfaceType, SM64TerrainType _terrainType)
        {
            return new SM64Surface
            {
                force = 0,
                type = (short)_surfaceType,
                terrain = (ushort)_terrainType,
                v0x = (short)(Interop.SCALE_FACTOR * -_p1.X),
                v0y = (short)(Interop.SCALE_FACTOR * _p1.Y),
                v0z = (short)(Interop.SCALE_FACTOR * _p1.Z),
                v1x = (short)(Interop.SCALE_FACTOR * -_p3.X),
                v1y = (short)(Interop.SCALE_FACTOR * _p3.Y),
                v1z = (short)(Interop.SCALE_FACTOR * _p3.Z),
                v2x = (short)(Interop.SCALE_FACTOR * -_p2.X),
                v2y = (short)(Interop.SCALE_FACTOR * _p2.Y),
                v2z = (short)(Interop.SCALE_FACTOR * _p2.Z)
            };
        }

        static public void transformAndGetSurfaces( List<SM64Surface> outSurfaces, Mesh mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<Vector3,Vector3> transformFunc )
        {
            var tris = mesh.GetTriangles(0);
            var vertices = mesh.vertices.Select(transformFunc).ToArray();

            for( int i = 0; i < tris.Length; i += 3 )
            {
                outSurfaces.Add(new SM64Surface {
                    force = 0,
                    type = (short)surfaceType,
                    terrain = (ushort)terrainType,
                    v0x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i  ]].x),
                    v0y = (short)(Interop.SCALE_FACTOR *  vertices[tris[i  ]].y),
                    v0z = (short)(Interop.SCALE_FACTOR *  vertices[tris[i  ]].z),
                    v1x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i+2]].x),
                    v1y = (short)(Interop.SCALE_FACTOR *  vertices[tris[i+2]].y),
                    v1z = (short)(Interop.SCALE_FACTOR *  vertices[tris[i+2]].z),
                    v2x = (short)(Interop.SCALE_FACTOR * -vertices[tris[i+1]].x),
                    v2y = (short)(Interop.SCALE_FACTOR *  vertices[tris[i+1]].y),
                    v2z = (short)(Interop.SCALE_FACTOR *  vertices[tris[i+1]].z)
                });
            }
        }

        static public SM64Surface[] GetSurfacesForMesh( Vector3 scale, Mesh mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType )
        {
            var surfaces = new List<SM64Surface>();
            transformAndGetSurfaces( surfaces, mesh, surfaceType, terrainType, x => Vector3.Scale( scale, x ));
            return surfaces.ToArray();
        }

        static public SM64Surface[] GetAllStaticSurfaces()
        {
            var surfaces = new List<SM64Surface>();

            foreach( var obj in GameObject.FindObjectsOfType<SM64StaticTerrain>())
            {
                if (obj.activeSurface)
                {
                    var mc = obj.GetComponent<MeshCollider>();
                    transformAndGetSurfaces(surfaces, mc.sharedMesh, obj.SurfaceType, obj.TerrainType, x => mc.transform.TransformPoint(x));
                }
            }

            return surfaces.ToArray();
        }

        static public Vector3NET unityToSM64Pos(Vector3NET _pos)
        {
            return new Vector3NET(-_pos.X, _pos.Y, _pos.Z) * Interop.SCALE_FACTOR;
        }

        static public Vector3NET sm64ToUnityToPos(Vector3NET _pos)
        {
            return new Vector3NET(-_pos.X, _pos.Y, _pos.Z) / Interop.SCALE_FACTOR;
        }
    }
}