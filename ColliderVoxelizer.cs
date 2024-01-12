using System;
using System.Collections.Generic;
using UnityEngine;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class ColliderVoxelizer
{
    public struct VoxelizationResult
    {
        public float[,,] voxels;
        public int voxelCount;
        public ModBounds bounds;
    }

    public struct RemeshResult
    {
        public Vector3NET[] vertices;
        public int[] indices;
    }

    public static VoxelizationResult Voxelize(MeshCollider _collider, float _voxelSize, int _boundsExpansion = 1)
    {
        // TODO bounds expansion could be better
        var bounds = new ModBounds(_collider.bounds);
        bounds.extents = Vector3NET.Max(bounds.extents, new Vector3NET(_voxelSize, _voxelSize, _voxelSize));
        bounds.Expand(_voxelSize * _boundsExpansion * 2);

        var voxels = new float[
            Mathf.CeilToInt(bounds.size.X / _voxelSize),
            Mathf.CeilToInt(bounds.size.Y / _voxelSize),
            Mathf.CeilToInt(bounds.size.Z / _voxelSize)];

        int count = 0;

        var downHits = new List<int>();
        var upHits = new List<int>();

        for (int z = 0; z < voxels.GetLength(2); z++)
        {
            float zPos = voxelToWorld(z, bounds.min.Z, _voxelSize);
            for (int x = 0; x < voxels.GetLength(0); x++)
            {

                float xPos = voxelToWorld(x, bounds.min.X, _voxelSize);
                float yPos = bounds.max.Y;
                while (true)
                {
                    if (_collider.Raycast(new Ray(new Vector3(xPos, yPos, zPos), Vector3.down), out var hit, bounds.size.Y))
                    {
                        int y = worldToVoxel(hit.point.y, bounds.min.Y, _voxelSize);
                        if (downHits.Count == 0 || downHits[downHits.Count - 1] != y)
                            downHits.Add(y);
                        yPos = hit.point.y - 0.05f;
                    }
                    else
                    {
                        break;
                    }
                }

                yPos = bounds.min.Y;
                while (true)
                {
                    if (_collider.Raycast(new Ray(new Vector3(xPos, yPos, zPos), Vector3.up), out var hit, bounds.size.Y))
                    {
                        int y = worldToVoxel(hit.point.y, bounds.min.Y, _voxelSize);
                        if (upHits.Count == 0 || upHits[upHits.Count - 1] != y)
                            upHits.Add(y);
                        yPos = hit.point.y + 0.05f;
                    }
                    else
                    {
                        break;
                    }
                }

                count += fillColumn(voxels, x, z, downHits, upHits);

                downHits.Clear();
                upHits.Clear();
            }
        }

        return new VoxelizationResult { voxels = voxels, voxelCount = count, bounds = bounds };
    }

    public static RemeshResult Remesh(float[,,] _voxels, float _voxelSize, ModBounds _bounds, Transform _transform)
    {
        var marchingCubes = new MarchingCubesProject.MarchingCubes();
        var vertices = new List<Vector3NET>();
        var indices = new List<int>();
        marchingCubes.Generate(_voxels, vertices, indices);

        var scaledVertices = new Vector3NET[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
        {
            scaledVertices[i] = _transform.InverseTransformPoint(voxelToWorld(vertices[i], _bounds.min, _voxelSize).ToIL2CPP()).ToNET();
        }

        return new RemeshResult { vertices = scaledVertices, indices = indices.ToArray() };
    }

    public static RemeshResult Remesh(MeshCollider _collider, float _voxelSize, int _boundsExpansion = 1)
    {
        var result = Voxelize(_collider, _voxelSize, _boundsExpansion);
        return Remesh(result.voxels, _voxelSize, result.bounds, _collider.transform);
    }

    private static int fillColumn(float[,,] _voxels, int _x, int _z, List<int> _downHits, List<int> _upHits)
    {
        int count = 0;

        if (_downHits.Count == 0 && _upHits.Count == 0)
            return count;

        int downIndex = 0;
        int upIndex = _upHits.Count - 1;

        do
        {
            int downY = downIndex < _downHits.Count ? _downHits[downIndex] : _voxels.GetLength(1) - 1;
            int upY = upIndex >= 0 && _upHits.Count != 0 ? _upHits[upIndex] : 0;

            if (downY >= upY)
            {
                for (int y = upY; y <= downY; y++)
                {
                    _voxels[_x, y, _z] = 1f;
                    count++;
                }
                downIndex++;
            }
            else
            {
                upIndex--;
            }
        }
        while (downIndex < _downHits.Count && upIndex >= 0);

        return count;
    }

    private static float voxelToWorld(int _value, float _min, float _voxelSize)
    {
        return _min + _value * _voxelSize;
    }

    private static Vector3NET voxelToWorld(Vector3NET _value, Vector3NET _min, float _voxelSize)
    {
        return _min + _value * _voxelSize;
    }

    private static int worldToVoxel(float _value, float _min, float _voxelSize)
    {
        return (int)System.MathF.Floor((_value - _min) / _voxelSize);
    }
}
