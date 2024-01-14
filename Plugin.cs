using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using HarmonyLib;
using UnityEngine;

using PirateBase;

namespace ShadowMario;

[BepInPlugin(c_pluginGUID, c_pluginName, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess(PirateBase.Plugin.c_processName)]
[BepInDependency(PirateBase.Plugin.c_pluginGUID, "1.x.x")]
public class Plugin : BasePlugin
{
    internal static ManualLogSource PluginLog { get; private set; }
    internal static MarioConfig PluginConfig { get; private set; }

    private const string c_pluginGUID = "com.crabnickolson.shadow_mario";
    private const string c_pluginName = "ShadowMario";

    public override void Load()
    {
        PluginLog = Log;
        PluginConfig = new MarioConfig(Config);

        if (!MarioResources.Init())
            return;

        registerTypes();

        Harmony.CreateAndPatchAll(typeof(MarioPatches));

        GameEvents.RunOnGameInit(onGameInit);
        GameEvents.startModEditing += MarioModdingTab.Inject;


        Log.LogInfo($"Plugin {c_pluginGUID} is loaded!");
    }

    public override bool Unload()
    {
        Log.LogInfo($"Plugin {c_pluginGUID} is unloaded!");

        return base.Unload();
    }

    private void registerTypes()
    {
        ClassInjector.RegisterTypeInIl2Cpp<LibSM64.SM64Context>();
        ClassInjector.RegisterTypeInIl2Cpp<LibSM64.SM64Mario>();
        ClassInjector.RegisterTypeInIl2Cpp<LibSM64.SM64StaticTerrain>();
        ClassInjector.RegisterTypeInIl2Cpp<LibSM64.SM64StreamedTerrain>();
        ClassInjector.RegisterTypeInIl2Cpp<LibSM64.SM64DynamicTerrain>();

        ClassInjector.RegisterTypeInIl2Cpp<MarioSceneHandler>();
        ClassInjector.RegisterTypeInIl2Cpp<MarioSceneSaveHandler>();
        ClassInjector.RegisterTypeInIl2Cpp<MarioStateSyncer>();
        ClassInjector.RegisterTypeInIl2Cpp<WaterController>();
        ClassInjector.RegisterTypeInIl2Cpp<MarioObstacle>();
        ClassInjector.RegisterTypeInIl2Cpp<MarioSurfaceProperties>();
        ClassInjector.RegisterTypeInIl2Cpp<MarioCam>();
        ClassInjector.RegisterTypeInIl2Cpp<FakeShadow>();
        ClassInjector.RegisterTypeInIl2Cpp<Coin>();
        ClassInjector.RegisterTypeInIl2Cpp<Star>();
        ClassInjector.RegisterTypeInIl2Cpp<BlockFly>();
        ClassInjector.RegisterTypeInIl2Cpp<BlockMetal>();

        ClassInjector.RegisterTypeInIl2Cpp<MarioScripting>();

        ModSaveManager.RegisterType<MarioSceneSaveHandler>();
        ModSaveManager.RegisterType<MarioStateSyncer>();
        ModSaveManager.RegisterType<MarioObstacle>();
        ModSaveManager.RegisterType<MarioSurfaceProperties>();
        ModSaveManager.RegisterType<MarioCam>();
        ModSaveManager.RegisterType<Coin>();
        ModSaveManager.RegisterType<Star>();
        ModSaveManager.RegisterType<BlockFly>();
        ModSaveManager.RegisterType<BlockMetal>();
    }

    private void onGameInit()
    {
        MarioSGAssets.Init();

        ModScripting.RegisterLibrary(new MarioScripting());
        AddComponent<MarioSpawnInputListener>();

        Log.LogInfo($"Plugin {c_pluginGUID} game init!");
    }
}
