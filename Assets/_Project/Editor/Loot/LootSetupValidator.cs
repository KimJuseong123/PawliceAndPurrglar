using System.Collections.Generic;
using System.Linq;
using PawsAndLoot.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Reports what is wrong with the loot setup before anybody plays it.
    ///
    /// Every fault here is one that produces no error at run time. A table with no
    /// drawable entry opens an empty cupboard, a missing icon draws an empty square,
    /// a container with no table looks exactly like a container that was unlucky.
    /// The point of the tool is that none of those are distinguishable from bad luck
    /// in a play session, and all of them are obvious in a list.
    ///
    /// Throws rather than logs when something is broken, so that a batch run fails
    /// its exit code instead of printing a warning nobody reads.
    /// </summary>
    public static class LootSetupValidator
    {
        [MenuItem("Pawlice and Purrglar/Loot/Validate Loot Setup")]
        public static void Validate()
        {
            var faults = new List<string>();
            var notes = new List<string>();

            LootTable[] tables = AssetDatabase
                .FindAssets($"t:{nameof(LootTable)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LootTable>)
                .Where(table => table != null)
                .OrderBy(table => table.name)
                .ToArray();

            if (tables.Length == 0)
            {
                faults.Add(
                    "No loot tables exist. Run 'Create Default Loot Tables'.");
            }

            foreach (LootTable table in tables)
            {
                if (table.Entries.Count == 0)
                {
                    faults.Add($"'{table.name}' has no entries.");
                    continue;
                }

                // Zero total weight is the quiet one. The table looks populated in
                // the inspector and can never give anything out.
                if (table.TotalWeight <= 0)
                {
                    faults.Add(
                        $"'{table.name}' has {table.Entries.Count} entries and a "
                        + "total weight of zero, so it can never draw.");
                }

                foreach (LootTableEntry entry in table.Entries)
                {
                    if (entry.Weight <= 0)
                    {
                        notes.Add(
                            $"'{table.name}': {entry.Kind} is weighted "
                            + $"{entry.Weight} and will never be drawn.");
                    }

                    if (!ThrowableCatalog.CanUseInQuickSlot(entry.Kind))
                    {
                        faults.Add(
                            $"'{table.name}': {entry.Kind} cannot go in a quick "
                            + "slot, so a container holding it cannot be emptied.");
                    }

                    if (Resources.Load<Sprite>(IconPathFor(entry.Kind)) == null)
                    {
                        notes.Add(
                            $"'{table.name}': {entry.Kind} has no icon at "
                            + $"'{IconPathFor(entry.Kind)}' and will draw as an "
                            + "empty slot with a letter in it.");
                    }
                }

                // A table that cannot fill the slots it is asked for is not broken,
                // but it is worth saying: with duplicates capped at two, three
                // entries can only ever fill six slots.
                int reachable = table.Entries
                    .Where(entry => entry.Weight > 0)
                    .Select(entry => entry.Kind)
                    .Distinct()
                    .Count() * table.MaximumDuplicateCount;
                if (reachable < table.MaximumItemCount)
                {
                    notes.Add(
                        $"'{table.name}' asks for up to {table.MaximumItemCount} "
                        + $"items but can only reach {reachable} with its duplicate "
                        + "cap, so it will often come up short.");
                }
            }

            SearchableContainer[] containers = Object
                .FindObjectsByType<SearchableContainer>(FindObjectsSortMode.None);
            var seenIds = new HashSet<string>();
            foreach (SearchableContainer container in containers)
            {
                if (container.Table == null)
                {
                    faults.Add(
                        $"Container '{container.ContainerId}' has no loot table, "
                        + "so it always opens empty.");
                }

                if (!seenIds.Add(container.ContainerId))
                {
                    faults.Add(
                        $"Two containers share the id '{container.ContainerId}'. "
                        + "Ids seed the contents, so both hold the same things.");
                }
            }

            foreach (string note in notes)
            {
                Debug.LogWarning($"[LOOT] {note}");
            }

            string summary =
                $"[LOOT] {tables.Length} table(s), {containers.Length} container(s) "
                + $"in the open scene, {faults.Count} fault(s), "
                + $"{notes.Count} note(s).";

            if (faults.Count > 0)
            {
                throw new System.InvalidOperationException(
                    summary + "\n - " + string.Join("\n - ", faults));
            }

            Debug.Log(summary);
        }

        /// <summary>
        /// The same path the HUD resolves an icon from. Duplicated deliberately and
        /// narrowly: the validator has to fail when the HUD would draw nothing, and
        /// reaching into the HUD to ask would mean loading it.
        /// </summary>
        private static string IconPathFor(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Rock => "UI/ItemIcons/rock",
                ThrowableKind.Banana => "UI/ItemIcons/banana",
                ThrowableKind.GlueTrap => "UI/ItemIcons/catnip pouch",
                ThrowableKind.SensorLight => "UI/ItemIcons/police lantern alarm",
                ThrowableKind.TunaCan => "UI/ItemIcons/fish can",
                ThrowableKind.DogTreat => "UI/ItemIcons/bone",
                ThrowableKind.RubberChicken => "UI/ItemIcons/yellow chicken",
                ThrowableKind.Firework => "UI/ItemIcons/can",
                ThrowableKind.FrozenOctopus => "UI/ItemIcons/can",
                _ => string.Empty
            };
        }
    }
}
