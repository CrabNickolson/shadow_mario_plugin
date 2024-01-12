using BepInEx;
using BepInEx.Unity.IL2CPP;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ShadowMario;

internal static class MarioResources
{
    public static byte[] romFile { get; private set; }
    public static AssetBundle assetBundle { get; private set; }

    private const string c_romFileName = "baserom.us.z64";
    private const string c_bundleFileName = "sm64data.bundle";
    private const string c_materialName = "defaultmario.mat";

    private const string c_shadowName = "shadow.prefab";
    private const string c_shadowSquareName = "shadow_square.prefab";

    private const string c_coinName = "coin_vis.prefab";
    private const string c_starName = "star_vis.prefab";
    private const string c_obstacleColliderName = "obstacle_collider.prefab";
    private const string c_obstacleIceName = "obstacle_ice.prefab";
    private const string c_obstacleLavaName = "obstacle_lava.prefab";
    private const string c_blockFlyName = "block_vis_fly.prefab";
    private const string c_blockMetalName = "block_vis_metal.prefab";

    public static bool Init()
    {
        string romPath = findPluginFile(c_romFileName);
        if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath))
        {
            Plugin.PluginLog.LogError($"Rom file {c_romFileName} does not exist! You need to manually copy this file to the plugin folder.");
            return false;
        }
        romFile = File.ReadAllBytes(romPath);

        string bundlePath = findPluginFile(c_bundleFileName);
        assetBundle = AssetBundle.LoadFromFile(bundlePath);
        if (assetBundle == null)
        {
            Plugin.PluginLog.LogError($"Failed to load bundle file {c_bundleFileName}!");
            return false;
        }

        foreach (var name in assetBundle.GetAllAssetNames())
        {
            Plugin.PluginLog.LogInfo(name);
        }

        return true;
    }

    private static string findPluginFile(string _fileName)
    {
        return Directory.GetFiles(Paths.PluginPath, _fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static void throwIfAssetDoesNotExist(string _name)
    {
        if (!assetBundle.Contains(_name))
            throw new System.InvalidOperationException($"Mario Asset bundle does not contain asset {_name}!");
    }

    private static T loadAsset<T>(string _name) where T : UnityEngine.Object
    {
        throwIfAssetDoesNotExist(_name);
        return assetBundle.LoadAsset(_name).TryCast<T>();
    }

    public static Material LoadMarioMaterial() => loadAsset<Material>(c_materialName);

    public static GameObject LoadShadowPrefab(bool _isSquare) => loadAsset<GameObject>(_isSquare ? c_shadowSquareName : c_shadowName);

    public static GameObject LoadCoinPrefab() => loadAsset<GameObject>(c_coinName);
    public static GameObject LoadStarPrefab() => loadAsset<GameObject>(c_starName);
    public static GameObject LoadObstacleColliderPrefab() => loadAsset<GameObject>(c_obstacleColliderName);
    public static GameObject LoadObstacleIcePrefab() => loadAsset<GameObject>(c_obstacleIceName);
    public static GameObject LoadObstacleLavaPrefab() => loadAsset<GameObject>(c_obstacleLavaName);
    public static GameObject LoadBlockFlyPrefab() => loadAsset<GameObject>(c_blockFlyName);
    public static GameObject LoadBlockMetalPrefab() => loadAsset<GameObject>(c_blockMetalName);
}
