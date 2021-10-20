﻿using Kingmaker.Blueprints;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TabletopTweaks.NewComponents;
using TabletopTweaks.NewUI;

namespace TabletopTweaks.NewUnitParts {
    public class UnitPartPseudoActivatableAbilities : UnitPart {

        private static BlueprintBuffReference _nullBuffRef = BlueprintReferenceBase.CreateTyped<BlueprintBuffReference>(null);

        private Dictionary<BlueprintAbility, List<WeakReference<MechanicActionBarSlot>>> m_AbilitiesToMechanicSlots =
            new Dictionary<BlueprintAbility, List<WeakReference<MechanicActionBarSlot>>>();

        private Dictionary<BlueprintAbility, HashSet<BlueprintBuffReference>> m_AbilitiesToBuffs =
            new Dictionary<BlueprintAbility, HashSet<BlueprintBuffReference>>();

        private Dictionary<BlueprintBuffReference, HashSet<BlueprintAbility>> m_BuffsToAbilities =
            new Dictionary<BlueprintBuffReference, HashSet<BlueprintAbility>>();

        private HashSet<BlueprintBuffReference> m_ActiveWatchedBuffs = new HashSet<BlueprintBuffReference>();


        public void RegisterPseudoActivatableAbilitySlot(MechanicActionBarSlot mechanicSlot) {
            if (!(mechanicSlot is IPseudoActivatableMechanicsBarSlot abilitySlot))
                return;

            var abilityBlueprint = abilitySlot.PseudoActivatableAbility.Blueprint;
            if (m_AbilitiesToMechanicSlots.TryGetValue(abilityBlueprint, out var slotRefs)) {
                slotRefs.Add(new WeakReference<MechanicActionBarSlot>(mechanicSlot));
            }
            else {
                m_AbilitiesToMechanicSlots.Add(abilityBlueprint, new List<WeakReference<MechanicActionBarSlot>>() { new WeakReference<MechanicActionBarSlot>(mechanicSlot) });
            }

            if (m_AbilitiesToBuffs.ContainsKey(abilityBlueprint)) {
                UpdateStateForAbility(abilityBlueprint);
                return;
            }

            if (!abilitySlot.BuffToWatch.Equals(_nullBuffRef)) {
                m_AbilitiesToBuffs.Add(abilityBlueprint, new HashSet<BlueprintBuffReference> { abilitySlot.BuffToWatch });
                if (m_BuffsToAbilities.TryGetValue(abilitySlot.BuffToWatch, out var abilities)) {
                    abilities.Add(abilityBlueprint);
                } else {
                    m_BuffsToAbilities.Add(abilitySlot.BuffToWatch, new HashSet<BlueprintAbility> { abilityBlueprint });
                }
            } else {
                var abilityVariants = abilityBlueprint.GetComponent<AbilityVariants>();
                if (abilityVariants != null) {
                    HashSet<BlueprintBuffReference> variantBlueprintBuffsToWatch = new HashSet<BlueprintBuffReference>();
                    foreach (var variant in abilityVariants.Variants) {
                        var pseudoActivatableComponent = variant.GetComponent<PseudoActivatable>();
                        if (pseudoActivatableComponent != null && !pseudoActivatableComponent.BuffToWatch.Equals(_nullBuffRef)) {
                            variantBlueprintBuffsToWatch.Add(pseudoActivatableComponent.BuffToWatch);
                        }
                    }
                    if (variantBlueprintBuffsToWatch.Any()) {
                        m_AbilitiesToBuffs.Add(abilityBlueprint, variantBlueprintBuffsToWatch);
                        foreach(var buffRef in variantBlueprintBuffsToWatch) {
                            if (m_BuffsToAbilities.TryGetValue(buffRef, out var abilities)) {
                                abilities.Add(abilityBlueprint);
                            } else {
                                m_BuffsToAbilities.Add(buffRef, new HashSet<BlueprintAbility> { abilityBlueprint });
                            }
                        }
                    }
                }
            }
#if DEBUG
            Validate();
#endif
            UpdateStateForAbility(abilityBlueprint);
        }

        public void BuffActivated(BlueprintBuff buff) {
            Main.LogDebug($"UnitPartPseudoActivatableAbilities.BuffActivated: \"{buff.NameSafe()}\"");
            var buffRef = buff.ToReference<BlueprintBuffReference>();
            m_ActiveWatchedBuffs.Add(buffRef);
            UpdateAbilitiesForBuff(buffRef);
        }

        public void BuffDeactivated(BlueprintBuff buff) {
            Main.LogDebug($"UnitPartPseudoActivatableAbilities.BuffDeactivated: \"{buff.NameSafe()}\"");
            var buffRef = buff.ToReference<BlueprintBuffReference>();
            m_ActiveWatchedBuffs.Remove(buffRef);
            UpdateAbilitiesForBuff(buffRef);
        }

        private void UpdateStateForAbility(BlueprintAbility abilityBlueprint) {
            if (!m_AbilitiesToBuffs.TryGetValue(abilityBlueprint, out var watchedBuffs) || !m_AbilitiesToMechanicSlots.TryGetValue(abilityBlueprint, out var slotRefs))
                return;

            var shouldBeActive = watchedBuffs.Any(buff => m_ActiveWatchedBuffs.Contains(buff));
            Main.LogDebug($"UnitPartPseudoActivatableAbilities.UpdateStateForAbility: will update slot refs for ability {abilityBlueprint.NameSafe()} to {shouldBeActive}");
            UpdateSlotRefs(slotRefs, shouldBeActive);
        }

        private void UpdateAbilitiesForBuff(BlueprintBuffReference buff) {
            if (!m_BuffsToAbilities.TryGetValue(buff, out var abilities))
                return;

            Dictionary<BlueprintAbility, bool> abilitiesToggleStatus = new Dictionary<BlueprintAbility, bool>();
            foreach(var abilityBlueprint in abilities) {
                if (m_AbilitiesToBuffs.TryGetValue(abilityBlueprint, out var watchedBuffs)) {
                    abilitiesToggleStatus.Add(abilityBlueprint, watchedBuffs.Any(buff => m_ActiveWatchedBuffs.Contains(buff)));
                }
            }
            foreach(var abilityToggleStatus in abilitiesToggleStatus) {
                if (m_AbilitiesToMechanicSlots.TryGetValue(abilityToggleStatus.Key, out var slotRefs)) {
                    UpdateSlotRefs(slotRefs, abilityToggleStatus.Value);
                }
            }
        }

        private void UpdateSlotRefs(List<WeakReference<MechanicActionBarSlot>> slotRefs, bool shouldBeActive) {
            List<WeakReference<MechanicActionBarSlot>> slotRefsToRemove = new List<WeakReference<MechanicActionBarSlot>>();
            foreach (var slotRef in slotRefs) {
                if (slotRef.TryGetTarget(out var slot)) {
                    if (slot is IPseudoActivatableMechanicsBarSlot pseudoActivatableSlot) {
                        pseudoActivatableSlot.ShouldBeActive = shouldBeActive;
                    }
                } else {
                    slotRefsToRemove.Add(slotRef);
                }
            }
            if (slotRefsToRemove.Any()) {
                Main.LogDebug($"UnitPartPseudoActivatableAbilities.UpdateSlotRefs: Removing {slotRefsToRemove.Count} dead slot references.");
            }
            foreach (var slotRef in slotRefsToRemove) {
                slotRefs.Remove(slotRef);
            }
        }

        private void Validate() {
            Main.LogDebug("UnitPartPseudoActivatableAbilities.Validate");
            var abilitiesInBuffsDictionary = m_BuffsToAbilities.Values.SelectMany(x => x).ToHashSet();
            foreach(var abilityBlueprint in abilitiesInBuffsDictionary) {
                if (!m_AbilitiesToBuffs.ContainsKey(abilityBlueprint)) {
                    Main.LogDebug($"UnitPartPseudoActivatableAbilities.Validate: ability \"{abilityBlueprint.NameSafe()}\" is in values of Buff dictionary, but is not a key in Abilities dictionary");
                }
            }
        }
    }
}
