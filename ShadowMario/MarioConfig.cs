using BepInEx;
using BepInEx.Configuration;

namespace ShadowMario;

internal class MarioConfig
{
    public DebugCategory debug { get; private set; }
    public GameplayCategory gameplay { get; private set; }
    public TerrainCategory terrain { get; private set; }

    public struct DebugCategory
    {
        private const string c_category = "Debug";

        public ConfigEntry<bool> logPerformance { get; private set; }
        public ConfigEntry<bool> displayGenerationMesh { get; private set; }
        public ConfigEntry<bool> displayStreamingMesh { get; private set; }
        public ConfigEntry<bool> displayHUD { get; private set; }

        public DebugCategory(ConfigFile _config)
        {
            logPerformance = _config.Bind(c_category,
                                          "LogPerformance",
                                          false);

            displayGenerationMesh = _config.Bind(c_category,
                                                 "DisplayGenerationMesh",
                                                 false);

            displayStreamingMesh = _config.Bind(c_category,
                                                "DisplayStreamingMesh",
                                                false);

            displayHUD = _config.Bind(c_category,
                                      "DisplayHUD",
                                      false);
        }
    }

    public struct GameplayCategory
    {
        private const string c_category = "Gameplay";

        public ConfigEntry<bool> alwaysDetectAsCrouched { get; private set; }
        public ConfigEntry<bool> onlyKillAfterKnockOut { get; private set; }
        public ConfigEntry<float> groundPoundNoiseRadius { get; private set; }
        public ConfigEntry<bool> investigateGroundPoundNoise { get; private set; }
        public ConfigEntry<float> attackNoiseRadius { get; private set; }
        public ConfigEntry<float> punchKnockback { get; private set; }
        public ConfigEntry<float> boingKnockback { get; private set; }
        public ConfigEntry<float> throwDistanceMultiplier { get; private set; }
        public ConfigEntry<float> throwDistanceMetalMario { get; private set; }

        public GameplayCategory(ConfigFile _config)
        {
            alwaysDetectAsCrouched = _config.Bind(c_category,
                                                  "AlwaysDetectAsCrouched",
                                                  true,
                                                  "If this is true, then Mario can stand in the striped area of a viewcone without being detected. " +
                                                  "If false, then Mario will be detected unless he is crawling.");

            onlyKillAfterKnockOut = _config.Bind(c_category,
                                                 "OnlyKillAfterKnockOut",
                                                 true,
                                                 "If this is true, then an attack on a concious npc will only knock them out. You can then kill them " +
                                                 "with a ground pound or sliding attack. If this is false, then all attacks will kill immediately.");

            groundPoundNoiseRadius = _config.Bind(c_category,
                                                  "GroundPoundNoiseRadius",
                                                   5f,
                                                   "If this is greater than 0, then ground pounding will create a noise that guards will notice.");

            investigateGroundPoundNoise = _config.Bind(c_category,
                                                       "InvestigateGroundPoundNoise",
                                                       true,
                                                       "If this is true, then guards will not only look in the direction of the ground pound noise, " +
                                                       "but will also walk towards it to investigate.");

            attackNoiseRadius = _config.Bind(c_category,
                                             "AttackNoiseRadius",
                                              4f,
                                              "If this is greater than 0, then any attack on a guard will create a noise that other guards will notice.");

            punchKnockback = _config.Bind(c_category,
                                         "PunchKnockback",
                                         50f,
                                         "This value sets how hard Mario will be knocked back after attacking a npc.");

            boingKnockback = _config.Bind(c_category,
                                         "BoingKnockback",
                                         60f,
                                         "This value sets how hard Mario will be flung into the sky after jumping on a npc.");

            throwDistanceMultiplier = _config.Bind(c_category,
                                                   "ThrowDistanceMultiplier",
                                                   40f,
                                                   "If greater than 0, Mario can pick up and throw incapacitated npcs.");

            throwDistanceMetalMario = _config.Bind(c_category,
                                                   "ThrowDistanceMetalMario",
                                                   10f,
                                                   "If greater than 0, then npcs will be thrown backwards when attacked by Metal Mario.");
        }
    }

    public struct TerrainCategory
    {
        private const string c_category = "Terrain";

        public ConfigEntry<float> minSlipperyAngle { get; private set; }
        public ConfigEntry<float> streamRadius { get; private set; }
        public ConfigEntry<float> chunkSize { get; private set; }
        public ConfigEntry<float> voxelSize { get; private set; }
        public ConfigEntry<float> hubVoxelSize { get; private set; }

        public TerrainCategory(ConfigFile _config)
        {
            minSlipperyAngle = _config.Bind(c_category,
                                            "MinSlipperyAngle",
                                            65f,
                                            "The angle at which surfaces become slippery for Mario.");

            streamRadius = _config.Bind(c_category,
                                        "StreamRadius",
                                         10f,
                                         "The distance at which world geometry is streamed into the Mario plugin. Lower values may improve performance, " +
                                         "but may also cause issues if Mario moves too fast.");

            chunkSize = _config.Bind(c_category,
                                     "ChunkSize",
                                     4f,
                                     "The size of the chunks that world geometry will be divided into. Changing this will impact performance.");

            voxelSize = _config.Bind(c_category,
                                     "VoxelSize",
                                     0.4f,
                                     "Some geometry cannot be directly converted into the format that the Mario plugin needs. It first needs to be voxelized. " +
                                     "This sets the size of those voxels. Lower values mean more accurate geometry, but also a higher performance impact.");

            hubVoxelSize = _config.Bind(c_category,
                                     "HubVoxelSize",
                                     0.25f,
                                     "Same as VoxelSize, but specifically for the HUB.");
        }
    }

    public MarioConfig(ConfigFile _config)
    {
        debug = new DebugCategory(_config);
        gameplay = new GameplayCategory(_config);
        terrain = new TerrainCategory(_config);
    }
}
