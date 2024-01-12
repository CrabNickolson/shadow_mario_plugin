using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LibSM64;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class TerrainGenerator
{
    public float m_chunkSize = 4;
    public float m_voxelSize = 0.4f;
    public float m_minSlipperyAngle = 65;

    public bool m_createDebugVis = false;

    private GameObject m_parent;

    public TerrainGenerator(float _chunkSize, float _voxelSize, float _minSlipperyAngle, bool _createDebugVis)
    {
        m_chunkSize = _chunkSize;
        m_voxelSize = _voxelSize;
        m_minSlipperyAngle = _minSlipperyAngle;
        m_createDebugVis = _createDebugVis;
    }

    private class MeshSplitTask
    {
        public MeshCollider collider;
        public float chunkSize;

        public ModBounds inputMeshBounds;
        public Vector3NET[] inputVertices;
        public int[] inputTriangles;

        public Vector3NET[][] outputVertices;
        public int[][] outputTriangles;

        public MeshSplitTask(MeshCollider _collider, float _chunkSize, ModBounds _inputMeshBounds, Vector3NET[] _inputVertices, int[] _inputTriangles)
        {
            collider = _collider;
            chunkSize = _chunkSize;
            inputMeshBounds = _inputMeshBounds;
            inputVertices = _inputVertices;
            inputTriangles = _inputTriangles;
        }

        public void Execute()
        {
            Vector3NET size = inputMeshBounds.size;
            if (size.X < chunkSize && size.Y < chunkSize && size.Z < chunkSize)
            {
                outputVertices = new Vector3NET[1][];
                outputTriangles = new int[1][];

                outputVertices[0] = inputVertices;
                outputTriangles[0] = inputTriangles;

                return;
            }

            var chunks = new Dictionary<Vector3Int, (List<Vector3NET>, List<int>)>();

            for (int i = 0; i < inputTriangles.Length; i += 3)
            {
                int t1 = inputTriangles[i];
                int t2 = inputTriangles[i+1];
                int t3 = inputTriangles[i+2];

                Vector3NET v1 = inputVertices[t1];
                Vector3NET v2 = inputVertices[t2];
                Vector3NET v3 = inputVertices[t3];

                
                Vector3Int chunkID = new Vector3Int(Mathf.FloorToInt(v1.X / chunkSize), Mathf.FloorToInt(v1.Y / chunkSize), Mathf.FloorToInt(v1.Z / chunkSize));
                if (!chunks.TryGetValue(chunkID, out var value))
                {
                    value = (new List<Vector3NET>(), new List<int>());
                    chunks.Add(chunkID, value);
                }

                int triIndex = value.Item1.Count;
                value.Item1.Add(v1);
                value.Item1.Add(v2);
                value.Item1.Add(v3);

                value.Item2.Add(triIndex);
                value.Item2.Add(triIndex+1);
                value.Item2.Add(triIndex+2);
            }

            outputVertices = new Vector3NET[chunks.Keys.Count][];
            outputTriangles = new int[chunks.Keys.Count][];

            int index = 0;
            foreach (var kvp in chunks)
            {
                outputVertices[index] = kvp.Value.Item1.ToArray();
                outputTriangles[index] = kvp.Value.Item2.ToArray();
                index++;
            }
        }
    }

    public void Dispose()
    {
        if (m_parent != null)
            GameObject.Destroy(m_parent);
    }

    public void generateMeshes()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        m_parent = new GameObject("mario_terrain_gen");

        var colliders = MiFindUtility.findComponentsInScenes<MeshCollider>(_bIncludeInactive: false);
        var meshSplitTasks = new List<MeshSplitTask>();

        ModBounds? terrainBounds = null;

        foreach (var collider in colliders)
        {
            if (!collider.enabled || collider.sharedMesh == null)
                continue;
            if (collider.gameObject.layer != (int)MiLayer.Walkable && collider.gameObject.layer != (int)MiLayer.Occluder)
                continue;
            if (collider.GetComponent<SM64StaticTerrain>() != null || collider.GetComponent<SM64DynamicTerrain>() != null
                || collider.GetComponent<SM64StreamedTerrain>() != null)
                continue;

            var mesh = collider.sharedMesh;

            if (colliderIsTerrain(collider))
            {
                if (terrainBounds.HasValue)
                {
                    var b = terrainBounds.Value;
                    b.Encapsulate(new ModBounds(collider.bounds));
                    terrainBounds = b;
                }
                else
                    terrainBounds = new ModBounds(collider.bounds);
            }

            ModBounds bounds;
            Vector3NET[] vertices;
            int[] triangles;
            if (mesh.isReadable)
            {
                bounds = new ModBounds(mesh.bounds);
                vertices = mesh.vertices.ToNET();
                triangles = mesh.triangles;
            }
            else
            {
                var meshCopy = mesh.CreateReadableCopy(_validate: true);
                if (meshCopy != null)
                {
                    bounds = new ModBounds(meshCopy.bounds);
                    vertices = meshCopy.vertices.ToNET();
                    triangles = meshCopy.triangles;
                    Object.Destroy(meshCopy);
                }
                else
                {
                    Plugin.PluginLog.LogWarning($"Failed to create readable mesh copy of {collider.transform.strGetPath()}. " +
                        $"Creating voxelized mesh instead.");
                    var result = ColliderVoxelizer.Remesh(collider, m_voxelSize);
                    bounds = new ModBounds(mesh.bounds);
                    vertices = result.vertices;
                    triangles = result.indices;
                }
            }

            meshSplitTasks.Add(new MeshSplitTask(collider, m_chunkSize, bounds, vertices, triangles));
        }

        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        System.Threading.Tasks.Parallel.ForEach(meshSplitTasks, task => { ModThreadUtility.AttachThread(); task.Execute(); });
        sw1.Stop();
        Plugin.PluginLog.LogInfo($"{nameof(TerrainGenerator)} split generation took {sw1.Elapsed.TotalMilliseconds:0.00}ms.");
        foreach (var task in meshSplitTasks)
        {
            for (int i = 0; i < task.outputVertices.Length; i++)
            {
                createTerrainObject(task.collider, task.outputVertices[i], task.outputTriangles[i], m_createDebugVis);
            }
        }

        createFallbackFloor(terrainBounds);

        sw.Stop();
        Plugin.PluginLog.LogInfo($"{nameof(TerrainGenerator)} generation took {sw.Elapsed.TotalMilliseconds:0.00}ms.");
    }

    private void createFallbackFloor(ModBounds? _terrainBounds)
    {
        if (_terrainBounds.HasValue)
        {
            const float size = 50;
            var bounds = _terrainBounds.Value;

            var boundsDown = bounds;
            boundsDown.min = bounds.min - new Vector3NET(0, 0, size);
            boundsDown.max = new Vector3NET(boundsDown.max.X, boundsDown.max.Y, bounds.min.Z);
            createQuadTerrain(boundsDown);

            var boundsUp = bounds;
            boundsUp.max = bounds.max + new Vector3NET(0, 0, size);
            boundsUp.min = new Vector3NET(boundsUp.min.X, boundsUp.min.Y, bounds.max.Z);
            createQuadTerrain(boundsUp);

            var boundsLeft = bounds;
            boundsLeft.min = bounds.min - new Vector3NET(size, 0, size);
            boundsLeft.max = new Vector3NET(bounds.min.X, boundsLeft.max.Y, boundsUp.max.Z);
            createQuadTerrain(boundsLeft);

            var boundsRight = bounds;
            boundsRight.max = bounds.max + new Vector3NET(size, 0, size);
            boundsRight.min = new Vector3NET(bounds.max.X, boundsLeft.min.Y, boundsDown.min.Z);
            createQuadTerrain(boundsRight);
        }
        else
        {
            var levelBounds = new ModBounds(
                new Vector3NET(0, LevelInformation.instance.levelInformationLayer.m_bounds.min.y, 0),
                new Vector3NET(256, 0, 256));
            createQuadTerrain(levelBounds);
        }
    }

    private SM64StaticTerrain createQuadTerrain(ModBounds _bounds)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.transform.SetParent(m_parent.transform, false);
        go.transform.position = new Vector3(_bounds.center.X, _bounds.min.Y, _bounds.center.Z);
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        go.transform.localScale = new Vector3(_bounds.size.X, _bounds.size.Z, 1f);
        go.GetComponent<MeshRenderer>().enabled = false;
        return go.AddComponent<SM64StaticTerrain>();
    }

    private GameObject createTerrainObject(Collider _collider, Vector3NET[] _vertices, int[] _triangles, bool _createDebugVis)
    {
        if (_vertices.Length < 3 || _triangles.Length == 0)
            return null;

        var go = new GameObject("sm64_terrain_" + _collider.gameObject.name);
        go.SetActive(false);
        var transform = go.transform;
        transform.SetParent(m_parent.transform, false);
        transform.position = _collider.transform.position;
        transform.rotation = _collider.transform.rotation;
        transform.localScale = _collider.transform.lossyScale;

        if (_createDebugVis)
        {
            var goVis = new GameObject("sm64_terrain_debug_" + _collider.gameObject.name);
            goVis.transform.SetParent(m_parent.transform, false);
            goVis.transform.position = _collider.transform.position;
            goVis.transform.rotation = _collider.transform.rotation;
            goVis.transform.localScale = _collider.transform.lossyScale;
            //goVis.transform.Translate(0, 5, 0, Space.World);

            var mesh = createMesh(_vertices.ToIL2CPP(), _triangles);
            mesh.RecalculateNormals();
            var meshFilter = goVis.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var renderer = goVis.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(ShaderUtility.FindStandardHideVCShader());
            renderer.sharedMaterial.color = new Color(0.5f, 0.5f, 0.5f);
        }

        SM64TerrainType? terrainTypeOverride = null;
        SM64SurfaceType? surfaceTypeOverride = null;
        bool useSurfaceCulling = true;

        var surfaceProperties = _collider.GetComponent<MarioSurfaceProperties>();
        if (surfaceProperties != null)
        {
            terrainTypeOverride = surfaceProperties.m_terrainType;
            surfaceTypeOverride = surfaceProperties.m_surfaceType;
            useSurfaceCulling = !surfaceProperties.m_disableSurfaceCulling;
        }

        bool isTerrain = colliderIsTerrain(_collider);

        var terrain = go.AddComponent<SM64StreamedTerrain>();
        terrain.useSurfaceCulling = useSurfaceCulling;
        terrain.ignoreYCull = isTerrain;

        var bounds = new ModBounds(transform.TransformPoint(_vertices[0].ToIL2CPP()).ToNET(), Vector3NET.Zero);
        var surfaces = new SM64Surface[_triangles.Length / 3];

        for (int i = 0; i < _vertices.Length; i++)
        {
            _vertices[i] = transform.TransformPoint(_vertices[i].ToIL2CPP()).ToNET();
            bounds.Encapsulate(_vertices[i]);
        }

        for (int i = 0; i < _triangles.Length; i += 3)
        {
            Vector3NET p1 = _vertices[_triangles[i]];
            Vector3NET p2 = _vertices[_triangles[i+1]];
            Vector3NET p3 = _vertices[_triangles[i+2]];

            Vector3NET normal = Vector3NET.Normalize(Vector3NET.Cross(p2 - p1, p3 - p1));
            float angle = System.MathF.Acos(System.Math.Clamp(Vector3NET.Dot(normal, Vector3NET.UnitY), -1, 1)) * Mathf.Rad2Deg;
            Vector3 midPoint = Vector3NET.Lerp(p1, Vector3NET.Lerp(p2, p3, 0.5f), 0.5f).ToIL2CPP();

            SM64SurfaceType surfaceType = SM64SurfaceType.Default;
            if (surfaceTypeOverride.HasValue)
                surfaceType = surfaceTypeOverride.Value;
            else if (angle > 15 && angle < m_minSlipperyAngle)
                surfaceType = SM64SurfaceType.NotSlippery;

            SM64TerrainType terrainType = SM64TerrainType.Stone;
            if (terrainTypeOverride.HasValue)
                terrainType = terrainTypeOverride.Value;
            else if (LevelInformation.instance.bGetFlag(out int flags, midPoint))
            {
                var infoFlag = (LevelInformationLayer.LevelInfoFlag)flags;
                if ((infoFlag & (LevelInformationLayer.LevelInfoFlag.Sand | LevelInformationLayer.LevelInfoFlag.Mud | LevelInformationLayer.LevelInfoFlag.ShallowWater)) != 0)
                    terrainType = SM64TerrainType.Sand;
                else if ((infoFlag & LevelInformationLayer.LevelInfoFlag.Leaves) != 0)
                    terrainType = SM64TerrainType.Grass;
            }

            surfaces[i/3] = Utils.CreateSurface(p1, p2, p3, surfaceType, terrainType);
        }

        terrain.SetData(surfaces, bounds);

        go.SetActive(true);
        return go;
    }

    private static bool colliderIsTerrain(Collider _collider)
    {
        return _collider.gameObject.name.Contains("chunk_", System.StringComparison.OrdinalIgnoreCase);
    }

    private static Mesh createMesh(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> _vertices, int[] _triangles)
    {
        var newMesh = new Mesh();
        newMesh.vertices = _vertices;
        newMesh.triangles = _triangles;
        newMesh.RecalculateBounds();
        return newMesh;
    }
}
