using BepInEx.Configuration;
using System;

namespace EvolutionEquipment
{
    internal class Configuration
    {
        internal static ConfigEntry<bool>
            isSimulacrumEnabled,
            rerollLunar;

        internal static ConfigEntry<float>
            humanDistanceThreshold,
            humanHealthThreshold,
            golemDistanceThreshold,
            golemHealthThreshold,
            queenDistanceThreshold,
            queenHealthThreshold;

        internal static ConfigEntry<int>
            minStagesCleared,
            stepStagesCleared,
            minWavesCleared,
            stepWavesCleared,
            humanUsesLimit,
            golemUsesLimit,
            queenUsesLimit;

        internal static ConfigEntry<string>
            blacklistedEquipment,
            blacklistedEquipmentMoon,
            blacklistedMonsters;

        internal static void Init(ConfigFile config)
        {
            humanDistanceThreshold = config.Bind("AI.Basic", "humanDistanceThreshold", 50f, "The maximum distance for basic monsters to activate their equipment");
            humanHealthThreshold = config.Bind("AI.Basic", "humanHealthThreshold", .75f, "The maximum normalised health for basic monsters to activate their equipment");
            humanUsesLimit = config.Bind("AI.Basic", "humanUsesLimit", -1, "The number of times a basic monster can activate their equipment. -1 is for unlimited");
            golemDistanceThreshold = config.Bind("AI.Miniboss", "golemDistanceThreshold", 65f, "The maximum distance for minibosses to activate their equipment");
            golemHealthThreshold = config.Bind("AI.Miniboss", "golemHealthThreshold", .5f, "The maximum normalised health for minibosses to activate their equipment");
            golemUsesLimit = config.Bind("AI.Miniboss", "golemUsesLimit", -1, "The number of times a miniboss can activate their equipment. -1 is for unlimited");
            queenDistanceThreshold = config.Bind("AI.Champion", "queenDistanceThreshold", 80f, "The maximum distance for champions to activate their equipment");
            queenHealthThreshold = config.Bind("AI.Champion", "queenHealthThreshold", .25f, "The maximum normalised health for champions to activate their equipment");
            queenUsesLimit = config.Bind("AI.Champion", "queenUsesLimit", -1, "The number of times a champion can activate their equipment. -1 is for unlimited");

            blacklistedEquipment = config.Bind("Blacklist", "blacklistedEquipment",
                "BFG,Blackhole,CommandMissile,DroneBackup,FireBallDash,GoldGat,Lightning,Saw", "Blacklisted equipment. Use 'list_equip' from DebugToolkit for internal names");
            blacklistedEquipmentMoon = config.Bind("Blacklist", "blacklistedEquipmentMoon", "Fruit,PassiveHealing", "Additional blacklisted equipment for Commencement. Intended to avoid any softlocks with Mithrix.");
            blacklistedMonsters = config.Bind("Blacklist", "blacklistedMonsters", "", "Monsters whose AI is not modified to use equipment. Use 'list_ai' from DebugToolkit for internal names");

            minStagesCleared = config.Bind("Evolution", "minStagesCleared", 0, "The minimum stages cleared required before selecting an equipment");
            stepStagesCleared = config.Bind("Evolution", "stepStagesCleared", 1, "The equipment will be rerolled every this many stages after 'minStagesCleared'");

            rerollLunar = config.Bind("General", "rerollLunar", true, "Reroll for a Lunar Equipment if the player holds Eulogy of Zero.");

            isSimulacrumEnabled = config.Bind("Simulacrum", "isSimulacrumEnabled", true, "Enable equipment giving for Simulacrum. If enabled, it overrides any Evolution settings for the game mode");
            minWavesCleared = config.Bind("Simulacrum", "minWavesCleared", 4, "The minimum number of Simulacrum waves required before selecting an equipment");
            stepWavesCleared = config.Bind("Simulacrum", "stepWavesCleared", 5, "The equipment will be rerolled every this many Simulacrum waves after 'minWavesCleared'");

            minStagesCleared.Value = Math.Max(minStagesCleared.Value, 0);
            stepStagesCleared.Value = Math.Max(stepStagesCleared.Value, 1);
            minWavesCleared.Value = Math.Max(minWavesCleared.Value, 0);
            stepWavesCleared.Value = Math.Max(stepWavesCleared.Value, 1);
        }
    }
}