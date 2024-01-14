using BepInEx;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using MiIngameToolHandles;
using PirateBase;

using Vector3NET = System.Numerics.Vector3;

namespace ShadowMario;

internal class MarioModdingTab : MiModdingTab
{
    private SpawnMode m_spawnMode;
    private Vector2 m_scrollPosition;

    public MarioModdingTab(System.IntPtr ptr) : base(ptr) { }

    public MarioModdingTab() : base(ClassInjector.DerivedConstructorPointer<MarioModdingTab>())
    {
        ClassInjector.DerivedConstructorBody(this);
    }

    private enum SpawnMode { Coin, Star, BlockFly, BlockMetal, ObstacleIce, ObstacleLava };

    public static void Inject()
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<MarioModdingTab>())
            ClassInjector.RegisterTypeInIl2Cpp<MarioModdingTab>();

        var popup = MiModdingToolsRuntime.instance.miModdingPopup;
        if (popup == null)
            return;
        var tabs = new Il2CppSystem.Collections.Generic.List<MiModdingTab>(popup.m_arTabs.Cast<Il2CppSystem.Collections.Generic.IEnumerable<MiModdingTab>>());
        if (tabs.Exists((Il2CppSystem.Predicate<MiModdingTab>)(x => x.TryCast<MarioModdingTab>() != null)))
            return;

        tabs.Add(new MarioModdingTab());
        popup.m_arTabs = (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<MiModdingTab>)tabs.ToArray();
    }

    public override string strTabName => "Mario";

    public override void drawSectionGUI()
    {
        m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition);

        m_spawnMode = (SpawnMode)GUILayout.SelectionGrid((int)m_spawnMode, new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(
            new[] { "Coin", "Star", "Block Fly", "Block Metal", "Obstacle Ice", "Obstacle Lava" }), 2);

        GUILayout.Label("Mario can also interact with obstacles from the Spawn tab");

        GUILayout.Space(20);

        var handles = MiModdingToolsRuntime.instance.toolHandles;
        var target = handles?.m_targetTransform;

        if (GUILayout.Button("Snap Rotation Of Selected"))
        {
            if (target != null)
            {
                Vector3 rotation = target.localRotation.eulerAngles;
                target.localRotation = Quaternion.Euler(roundToNearest(rotation.x, 45), roundToNearest(rotation.y, 45), roundToNearest(rotation.z, 45));
            }
        }

        GUILayout.Space(20);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Coin Counter"))
        {
            if (MarioSceneSaveHandler.instance != null)
                MarioSceneSaveHandler.instance.ResetCoins();
        }

        if (GUILayout.Button("Reset Star Counter"))
        {
            if (MarioSceneSaveHandler.instance != null)
                MarioSceneSaveHandler.instance.ResetStars();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        if (GUILayout.Button("Regenerate Terrain"))
        {
            if (MarioSceneHandler.instance != null)
                MarioSceneHandler.instance.RegenerateTerrainAndUpdateStreaming();
        }

        if (target != null)
        {
            GUILayout.Space(20);
            GUILayout.Label("Scale Selected Object (Careful, this can break things!)");

            Vector3 scale = target.localScale;

            GUI.changed = false;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"X:{scale.x:0.00}", GUILayout.Width(40));
            scale.x = GUILayout.HorizontalSlider(scale.x, 0.1f, 20);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Y:{scale.y:0.00}", GUILayout.Width(40));
            scale.y = GUILayout.HorizontalSlider(scale.y, 0.1f, 20);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Z:{scale.z:0.00}", GUILayout.Width(40));
            scale.z = GUILayout.HorizontalSlider(scale.z, 0.1f, 20);
            GUILayout.EndHorizontal();
            if (GUI.changed)
            {
                GUI.changed = false;
                target.localScale = scale;
            }

            if (GUILayout.Button("Reset Scale"))
            {
                target.localScale = Vector3.one;
            }
        }


        /*GUILayout.Space(20);

        if (this.Button("Set Cam"))
        {
            var cam = MarioCam.CreateCam();
            Mimimi.Cam.MiCamHandler.instance.activateCam(cam.Cast<Mimimi.Cam.IMiCam>());
        }
        if (this.Button("Reset Cam"))
        {
            Mimimi.Cam.MiCamHandler.instance.activatePlayerCam();
        }*/

        GUILayout.EndScrollView();
    }

    public override void onUpdate(bool _isMouseOverGUIArea)
    {
        if (!_isMouseOverGUIArea && Input.GetMouseButtonDown(1))
        {
            var ray = Camera.current.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var rayHit, 200, MarioStateSyncer.marioColliderMask, QueryTriggerInteraction.Ignore))
            {
                Vector3NET pos = rayHit.point.ToNET();
                Plugin.PluginLog.LogInfo($"Hit: {pos}");

                MonoBehaviour spawnedComponent = null;
                switch (m_spawnMode)
                {
                    case SpawnMode.Coin:
                        spawnedComponent = Coin.SpawnStatic(pos, _addModdedComponent: true);
                        break;
                    case SpawnMode.Star:
                        spawnedComponent = Star.Spawn(pos, false);
                        break;
                    case SpawnMode.BlockFly:
                        spawnedComponent = BlockFly.Spawn(pos);
                        break;
                    case SpawnMode.BlockMetal:
                        spawnedComponent = BlockMetal.Spawn(pos);
                        break;
                    case SpawnMode.ObstacleIce:
                        spawnedComponent = MarioObstacle.Spawn(pos, MarioObstacle.ObstacleType.Ice, _addModdedComponent: true);
                        break;
                    case SpawnMode.ObstacleLava:
                        spawnedComponent = MarioObstacle.Spawn(pos, MarioObstacle.ObstacleType.Lava, _addModdedComponent: true);
                        break;
                }

                if (spawnedComponent != null)
                {
                    updateMarioObjectTransformHandle(spawnedComponent.gameObject);
                }
            }
        }
    }

    private static void updateMarioObjectTransformHandle(GameObject _go)
    {
        var handles = MiModdingToolsRuntime.instance.toolHandles;
        handles.resetAll();
        handles.enableHandle(MiIngameToolTransformHandles.Handle.RotY);
        handles.enableHandle(MiIngameToolTransformHandles.Handle.PosXZ);
        handles.enableHandle(MiIngameToolTransformHandles.Handle.PosX);
        handles.enableHandle(MiIngameToolTransformHandles.Handle.PosZ);
        handles.enableHandle(MiIngameToolTransformHandles.Handle.PosY);
        handles.enable(_go.transform);
    }

    protected static float roundToNearest(float _value, float _roundTo)
    {
        float sign = System.Math.Sign(_value);
        float absValue = System.Math.Abs(_value);
        float remainder = absValue % _roundTo;

        if (System.Math.Abs(remainder) > _roundTo / 2)
            return (absValue + (_roundTo - remainder)) * sign;
        else
            return (absValue - remainder) * sign;
    }
}
