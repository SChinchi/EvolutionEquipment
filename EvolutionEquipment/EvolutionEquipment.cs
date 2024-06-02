using BepInEx;
using BepInEx.Logging;
using RoR2;
using RoR2.CharacterAI;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace EvolutionEquipment
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class EvolutionEquipment : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Chinchi";
        public const string PluginName = "EvolutionEquipment";
        public const string PluginVersion = "1.0.3";

        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;
            Configuration.Init(Config);
            Hooks.Init();
            Language.collectLanguageRootFolders += AddLanguageFolder;
            RoR2Application.onLoad += PatchAI;
        }

        private void AddLanguageFolder(List<string> languageFolders)
        {
            var folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "languages");
            if (System.IO.Directory.Exists(folder))
            {
                languageFolders.Add(folder);
            }
        }

        private void PatchAI()
        {
            var blacklist = new HashSet<string>(Configuration.blacklistedMonsters.Value.Split(',').Select(x => x.Trim()));
            foreach (var ai in MasterCatalog.allAiMasters)
            {
                if (blacklist.Contains(ai.gameObject.name))
                {
                    continue;
                }
                if (ai.bodyPrefab == null || ai.bodyPrefab.GetComponent<EquipmentSlot>() == null)
                {
                    continue;
                }
                var skillDrivers = ai.GetComponents<AISkillDriver>();
                var activatesEquipment = skillDrivers.Any(x => x.shouldFireEquipment);
                if (!activatesEquipment)
                {
                    var hull = ai.bodyPrefab.GetComponent<CharacterBody>().hullClassification;
                    float health;
                    float distance;
                    int uses;
                    switch (hull)
                    {
                        case HullClassification.Human:
                            health = Configuration.humanHealthThreshold.Value;
                            distance = Configuration.humanDistanceThreshold.Value;
                            uses = Configuration.humanUsesLimit.Value;
                            break;
                        case HullClassification.Golem:
                            health = Configuration.golemHealthThreshold.Value;
                            distance = Configuration.golemDistanceThreshold.Value;
                            uses = Configuration.golemUsesLimit.Value;
                            break;
                        default:
                            health = Configuration.queenHealthThreshold.Value;
                            distance = Configuration.queenDistanceThreshold.Value;
                            uses = Configuration.queenUsesLimit.Value;
                            break;
                    }
                    var equipmentSkill = ai.gameObject.AddComponent<AISkillDriver>();
                    equipmentSkill.customName = "FireEquipment";
                    equipmentSkill.skillSlot = SkillSlot.None;
                    equipmentSkill.requireEquipmentReady = true;
                    equipmentSkill.maxUserHealthFraction = health;
                    equipmentSkill.maxDistance = distance;
                    equipmentSkill.selectionRequiresTargetLoS = true;
                    equipmentSkill.selectionRequiresAimTarget = true;
                    equipmentSkill.activationRequiresTargetLoS = true;
                    equipmentSkill.activationRequiresAimConfirmation = true;
                    equipmentSkill.shouldFireEquipment = true;
                    equipmentSkill.aimType = AISkillDriver.AimType.AtMoveTarget;
                    equipmentSkill.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
                    equipmentSkill.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
                    equipmentSkill.noRepeat = true;
                    equipmentSkill.maxTimesSelected = uses;
                    // Moving every original skill driver to the bottom
                    // so that "FireEquipment" has priority
                    foreach (var driver in skillDrivers)
                    {
                        // The AISkillDriver can be subclassed so we can't rely on generics
                        var copy = ai.gameObject.AddComponent(driver.GetType());
                        foreach (var field in driver.GetType().GetFields())
                        {
                            field.SetValue(copy, field.GetValue(driver));
                        }
                        DestroyImmediate(driver);
                    }
                }
            }
        }
    }
}