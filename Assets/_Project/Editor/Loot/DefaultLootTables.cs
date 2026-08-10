using System.Collections.Generic;
using System.IO;
using PawliceAndPurrglar.Gameplay.Items;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Creates the loot tables the containers draw from.
    ///
    /// Assets rather than a table in code, and generated rather than hand-built, so
    /// that the weights live in one reviewable place and a fresh clone has them
    /// without anybody clicking through the Project window.
    ///
    /// Only props that already have art are in these tables. The spec lists bread,
    /// milk, cheese and thirty more, and none of them exist as either an icon or a
    /// model; a table full of identical grey boxes distinguishable only by their
    /// label would fail the spec's own "tell the items apart at a glance" check.
    /// `Validate Loot Setup` reports what is missing rather than this quietly
    /// papering over it.
    ///
    /// Police installables are in their own table. A kitchen cupboard that turns up
    /// a glue trap hands the thief the officer's equipment, which is the one thing
    /// the prop economy is built to stop.
    /// </summary>
    public static class DefaultLootTables
    {
        internal const string OutputFolder = "Assets/_Project/Data/LootTables";

        [MenuItem("PawliceAndPurrglar/Loot/Create Default Loot Tables")]
        public static void Create()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));

            // Kitchens hold food and the odd bottle. The octopus is the rare one:
            // it is the only prop in here that changes a chase.
            Write(
                "House_Kitchen_Loot",
                "주방 찬장",
                new[]
                {
                    new LootTableEntry(ThrowableKind.TunaCan, 26, 1, 2),
                    new LootTableEntry(ThrowableKind.Banana, 22, 1, 2),
                    new LootTableEntry(ThrowableKind.Rock, 14),
                    new LootTableEntry(ThrowableKind.Firework, 7),
                    new LootTableEntry(ThrowableKind.FrozenOctopus, 4)
                },
                1,
                3);

            // Living rooms and bedrooms: what a household leaves lying about. The
            // dog treat is worth more here than in a kitchen, because taking the
            // officer's dog off the trail is what a house is for.
            Write(
                "House_Living_Loot",
                "서랍장",
                new[]
                {
                    new LootTableEntry(ThrowableKind.Rock, 24),
                    new LootTableEntry(ThrowableKind.DogTreat, 20),
                    new LootTableEntry(ThrowableKind.RubberChicken, 16),
                    new LootTableEntry(ThrowableKind.Banana, 14),
                    new LootTableEntry(ThrowableKind.Firework, 8)
                },
                1,
                3);

            // A shop has more of everything and runs out less often, so the counts
            // start at two.
            Write(
                "Market_Loot",
                "계산대",
                new[]
                {
                    new LootTableEntry(ThrowableKind.TunaCan, 30, 1, 3),
                    new LootTableEntry(ThrowableKind.Banana, 26, 1, 3),
                    new LootTableEntry(ThrowableKind.FrozenOctopus, 12),
                    new LootTableEntry(ThrowableKind.Rock, 10),
                    new LootTableEntry(ThrowableKind.Firework, 8)
                },
                2,
                4);

            // Police installables, and nothing else. Reached through a container
            // whose access is set to everyone, so an officer can restock - not by a
            // thief finding one in a kitchen.
            Write(
                "PoliceStation_Loot",
                "경찰 비품함",
                new[]
                {
                    new LootTableEntry(ThrowableKind.GlueTrap, 30),
                    new LootTableEntry(ThrowableKind.SensorLight, 24)
                },
                1,
                2,
                allowDuplicates: false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[LOOT] Wrote 4 loot tables to '{OutputFolder}'. The jeweller and "
                + "the bookshop have none: their contents are treasure to sell, and "
                + "selling what you searched out needs a bag that holds more than "
                + "one thing. Tracked separately.");
        }

        private static void Write(
            string assetName,
            string displayName,
            IReadOnlyList<LootTableEntry> entries,
            int minimumCount,
            int maximumCount,
            bool allowDuplicates = true)
        {
            string path = $"{OutputFolder}/{assetName}.asset";
            var table = AssetDatabase.LoadAssetAtPath<LootTable>(path);
            bool isNew = table == null;
            if (isNew)
            {
                table = ScriptableObject.CreateInstance<LootTable>();
            }

            table.Configure(
                displayName,
                entries,
                minimumCount,
                maximumCount,
                allowDuplicates,
                allowDuplicates ? 2 : 1);

            if (isNew)
            {
                AssetDatabase.CreateAsset(table, path);
            }
            else
            {
                EditorUtility.SetDirty(table);
            }
        }
    }
}
