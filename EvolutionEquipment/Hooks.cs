using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace EvolutionEquipment
{
    internal static class Hooks
    {
        private static Sprite backgroundSprite;
        private static readonly List<EquipmentIndex> availableEquipment = new();
        private static readonly List<EquipmentIndex> availableLunarEquipment = new();
        private static readonly List<EquipmentIndex> availableEquipmentMoon = new();
        private static readonly List<EquipmentIndex> availableLunarEquipmentMoon = new();
        private static readonly Dictionary<EnemyInfoPanel, Tuple<GameObject, UIElementAllocator<RawImage>>> enemyInfoPanels = new();
        private static readonly Xoroshiro128Plus rng = new(0UL);

        internal static void Init()
        {
            Run.onRunStartGlobal += FilterRunEquipment;
            On.RoR2.UI.EnemyInfoPanel.Awake += CreateEquipmentPanel;
            IL.RoR2.UI.EnemyInfoPanel.SetDisplayDataForViewer += UpdateEquipmentPanel;
            On.RoR2.UI.EnemyInfoPanel.OnDestroy += RemoveInfoPanel;
            On.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.EnsureMonsterItemCountMatchesStageCount += RollStageEquipment;
            On.RoR2.InfiniteTowerRun.AdvanceWave += RollWaveEquipment;
            IL.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.OnServerCardSpawnedGlobal += GiveMonsterEquipmentClassic;
            IL.RoR2.InfiniteTowerWaveController.OnCombatSquadMemberDiscovered += GiveMonsterEquipmentSimulacrum;
            Addressables.LoadAssetAsync<Sprite>("RoR2/Base/UI/texUICutOffCorner.png").Completed += (operation) =>
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    backgroundSprite = operation.Result;
                }
            };
        }

        private static void FilterRunEquipment(Run run)
        {
            var blacklist = new HashSet<string>(Configuration.blacklistedEquipment.Value.Split(',').Select(x => x.Trim()));
            InitAvailableEquipment(ToEquipmentIndex(run.availableEquipmentDropList), availableEquipment, blacklist);
            InitAvailableEquipment(ToEquipmentIndex(run.availableLunarEquipmentDropList), availableLunarEquipment, blacklist);
            var blacklistMoon = new HashSet<string>(Configuration.blacklistedEquipmentMoon.Value.Split(',').Select(x => x.Trim()));
            InitAvailableEquipment(availableEquipment, availableEquipmentMoon, blacklistMoon);
            InitAvailableEquipment(availableLunarEquipment, availableLunarEquipmentMoon, blacklistMoon);

            List<EquipmentIndex> ToEquipmentIndex(List<PickupIndex> droplist)
            {
                return droplist.Select(x => PickupCatalog.GetPickupDef(x).equipmentIndex).ToList();
            }

            void InitAvailableEquipment(List<EquipmentIndex> original, List<EquipmentIndex> filtered, HashSet<string> blacklist)
            {
                filtered.Clear();
                foreach (var index in original)
                {
                    var equip = EquipmentCatalog.GetEquipmentDef(index);
                    if (equip != null && !blacklist.Contains(equip.name))
                    {
                        filtered.Add(index);
                    }
                }
            }
        }

        private static void CreateEquipmentPanel(On.RoR2.UI.EnemyInfoPanel.orig_Awake orig, EnemyInfoPanel self)
        {
            orig(self);
            var panels = self.transform.GetChild(0);
            var equipmentContainer = new GameObject("EquipmentContainer");
            equipmentContainer.SetActive(false);
            var layout = equipmentContainer.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            var transform = layout.rectTransform;
            transform.SetParent(panels, false);
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
            transform.localEulerAngles = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.pivot = new Vector2(0.5f, 0.5f);
            transform.anchoredPosition3D = new Vector3(73f, -99f, 0f);
            transform.offsetMin = new Vector2(0f, -106f);
            transform.offsetMax = new Vector2(146f, -93f);

            var equipmentLabel = new GameObject("EquipmentLabel");
            var text = equipmentLabel.AddComponent<HGTextMeshProUGUI>();
            text.fontSize = 12f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            equipmentLabel.SetActive(false);
            var language = equipmentLabel.AddComponent<LanguageTextMeshController>();
            language.token = "HUD_MONSTER_EQUIPMENT_LABEL";
            equipmentLabel.SetActive(true);
            transform = text.rectTransform;
            transform.SetParent(layout.rectTransform, false);
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
            transform.localEulerAngles = Vector3.zero;
            transform.localScale = Vector3.one;

            var equipmentDisplay = new GameObject("EquipmentDisplay");
            var image = equipmentDisplay.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.5411f);
            image.sprite = backgroundSprite;
            image.type = Image.Type.Sliced;
            var grid = equipmentDisplay.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(32f, 32f);
            grid.spacing = new Vector2(2f, 2f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(2, 2, 2, 2);
            transform = grid.rectTransform;
            transform.SetParent(layout.rectTransform, false);
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, 0f);
            transform.localEulerAngles = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.pivot = new Vector2(0.5f, 1f);
            transform.anchoredPosition3D = new Vector3(73f, -12f, 0f);
            transform.offsetMin = new Vector2(0f, -84f);
            transform.offsetMax = new Vector2(146f, -12f);
            enemyInfoPanels[self] = Tuple.Create(equipmentContainer, new UIElementAllocator<RawImage>(transform, self.iconPrefab, true, false));
        }

        private static void UpdateEquipmentPanel(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(0)
            ))
            {
                EvolutionEquipment.Logger.LogError("Failed to patch EnemyInfoPanel.SetDisplayDataForViewer #1");
                return;
            }
            c.EmitDelegate<Func<bool, bool>>(shouldDisplay =>
            {
                var inventory = GetEnemyInventory();
                return shouldDisplay || (inventory && inventory.currentEquipmentIndex != EquipmentIndex.None);
            });
            if (!c.TryGotoNext(
                MoveType.After,
                x => x.MatchCallvirt<EnemyInfoPanel>("TrySetItems")
            ))
            {
                EvolutionEquipment.Logger.LogError("Failed to patch EnemyInfoPanel.SetDisplayDataForViewer #2");
                return;
            }
            c.Emit(OpCodes.Ldloc_1);
            c.EmitDelegate<Action<EnemyInfoPanel>>(panel =>
            {
                if (!enemyInfoPanels.TryGetValue(panel, out var enemyInfoPanel))
                {
                    EvolutionEquipment.Logger.LogWarning("Equipment icon allocator not found: " + panel.hud.targetMaster);
                    return;
                }
                var iconAllocator = enemyInfoPanel.Item2;
                var inventory = GetEnemyInventory();
                var equipIndex = inventory != null ? inventory.currentEquipmentIndex : EquipmentIndex.None;
                if (equipIndex != EquipmentIndex.None)
                {
                    var equip = EquipmentCatalog.GetEquipmentDef(equipIndex);
                    iconAllocator.AllocateElements(1);
                    iconAllocator.elements[0].texture = equip.pickupIconTexture;
                    var tooltip = iconAllocator.elements[0].gameObject.AddComponent<TooltipProvider>();
                    tooltip.titleColor = ColorCatalog.GetColor(equip.colorIndex);
                    tooltip.titleToken = equip.nameToken;
                    tooltip.bodyToken = equip.descriptionToken;
                    enemyInfoPanel.Item1.SetActive(true);
                }
                else
                {
                    iconAllocator.AllocateElements(0);
                    enemyInfoPanel.Item1.SetActive(false);
                }
            });

            static Inventory GetEnemyInventory()
            {
                if (Run.instance is InfiniteTowerRun && Configuration.isSimulacrumEnabled.Value)
                {
                    return (Run.instance as InfiniteTowerRun).enemyInventory;
                }
                else if (RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.MonsterTeamGainsItems))
                {
                    return RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.monsterTeamInventory;
                }
                return null;
            }
        }

        private static void RemoveInfoPanel(On.RoR2.UI.EnemyInfoPanel.orig_OnDestroy orig, EnemyInfoPanel self)
        {
            orig(self);
            enemyInfoPanels.Remove(self);
        }

        private static void RollStageEquipment(On.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.orig_EnsureMonsterItemCountMatchesStageCount orig)
        {
            orig();
            if (Run.instance && Run.instance is not InfiniteTowerRun)
            {
                var count = Run.instance.stageClearCount - Configuration.minStagesCleared.Value;
                var equip = EquipmentIndex.None;
                if (count >= 0)
                {
                    var blocks = count / Configuration.stepStagesCleared.Value;
                    equip = RollEquipment(blocks);
                }
                var inventory = RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.monsterTeamInventory;
                if (inventory.currentEquipmentIndex != equip)
                {
                    inventory.SetEquipmentIndex(equip);
                }
            }
        }

        private static void RollWaveEquipment(On.RoR2.InfiniteTowerRun.orig_AdvanceWave orig, InfiniteTowerRun self)
        {
            orig(self);
            if (Configuration.isSimulacrumEnabled.Value)
            {
                var run = Run.instance as InfiniteTowerRun;
                var count = run._waveIndex - 1 - Configuration.minWavesCleared.Value;
                var equip = count >= 0 ? RollEquipment(count / Configuration.stepWavesCleared.Value) : EquipmentIndex.None;
                var inventory = run.enemyInventory;
                if (inventory.currentEquipmentIndex != equip)
                {
                    inventory.SetEquipmentIndex(equip);
                    var pickupDef = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(equip));
                    if (pickupDef != null)
                    {
                        Chat.SendBroadcastChat(new Chat.PlayerPickupChatMessage
                        {
                            baseToken = "INFINITETOWER_ADD_ITEM",
                            pickupToken = pickupDef.nameToken,
                            pickupColor = pickupDef.baseColor
                        });
                    }
                }
            }
        }

        private static EquipmentIndex RollEquipment(int seedOffset)
        {
            rng.ResetSeed(Run.instance.seed + (ulong)seedOffset);
            var scene = SceneCatalog.currentSceneDef.cachedName;
            var isMoon = scene == "moon2" || scene == "moon";
            var droplist = !isMoon ? availableEquipment : availableEquipmentMoon;
            var equip = droplist.Count > 0 ? rng.NextElementUniform(droplist) : EquipmentIndex.None;
            if (Configuration.rerollLunar.Value)
            {
                var count = Util.GetItemCountGlobal(DLC1Content.Items.RandomlyLunar.itemIndex, false, false);
                if (count > 0 && rng.nextNormalizedFloat < RandomlyLunarUtils.replacePercentagePerStack * count)
                {
                    droplist = !isMoon ? availableLunarEquipment : availableLunarEquipmentMoon;
                    equip = droplist.Count > 0 ? rng.NextElementUniform(droplist) : EquipmentIndex.None;
                }
            }
            return equip;
        }

        private static void GiveMonsterEquipmentClassic(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(
                MoveType.After,
                x => x.MatchCallvirt<Inventory>("AddItemsFrom")
            ))
            {
                EvolutionEquipment.Logger.LogError("Failed to patch RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager");
                return;
            }
            c.Emit(OpCodes.Ldloc_0);
            c.EmitDelegate<Action<CharacterMaster>>(master =>
            {
                GiveMasterEquipment(master, RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.monsterTeamInventory);
            });
        }

        private static void GiveMonsterEquipmentSimulacrum(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.TryGotoNext(
                MoveType.After,
                x => x.MatchCallvirt<Inventory>("AddItemsFrom")
            ))
            {
                EvolutionEquipment.Logger.LogError("Failed to patch RoR2.InfiniteTowerWaveController.OnCombatSquadMemberDiscovered");
                return;
            }
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<Action<InfiniteTowerWaveController, CharacterMaster>>((waveController, master) =>
            {
                GiveMasterEquipment(master, waveController.enemyInventory);
            });
        }

        private static void GiveMasterEquipment(CharacterMaster master, Inventory enemyInventory)
        {
            var equip = enemyInventory.currentEquipmentIndex;
            if (master.inventory.currentEquipmentIndex == EquipmentIndex.None
                && equip != EquipmentIndex.None
                && master.GetComponent<ScavengerItemGranter>() == null
                && master.inventory.GetItemCount(DLC1Content.Items.GummyCloneIdentifier) == 0
            )
            {
                var pickupsOnStart = master.GetComponent<GivePickupsOnStart>();
                if (pickupsOnStart == null
                    || pickupsOnStart.equipmentDef == null
                    || string.IsNullOrEmpty(pickupsOnStart.equipmentString)
                )
                {
                    master.inventory.SetEquipmentIndex(equip);
                }
            }
        }
    }
}