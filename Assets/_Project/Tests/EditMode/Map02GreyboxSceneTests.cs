using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// Asks whether MAP-002 is a town you could walk around, not whether it
    /// matches a list of numbers.
    ///
    /// The first version repeated every coordinate the generator held: thirteen
    /// roads, sixteen buildings, seven bins, eighteen trees. That asserted only
    /// that two lists typed by the same person agreed, it made re-cutting the
    /// layout a two-file edit, and it missed the thing that mattered — the roads
    /// were 2 m wide, below the 2.4 m a route has to stay clear for, so the
    /// village validator would have rejected the whole town while every test
    /// passed.
    ///
    /// The duplication was not laziness. <c>Assets/_Project/Editor</c> has no
    /// assembly definition, so it compiles into <c>Assembly-CSharp-Editor</c>,
    /// and an asmdef test assembly cannot reference that. The generator is
    /// unreachable from here by construction.
    ///
    /// So these measure the scene instead of the recipe, which is the better
    /// question anyway: a model that scales wrongly puts a wall in the road while
    /// the coordinates that asked for it stay perfectly clear. Exact counts stay
    /// in the generator's own <c>ValidateScene</c>, where the arrays are in
    /// scope.
    /// </summary>
    public sealed class Map02GreyboxSceneTests
    {
        private const string Map02ScenePath =
            "Assets/_Project/Scenes/GameMap02.unity";
        private const string RoadModelPath =
            "Assets/_Project/Art/Environment/env_road_section.fbx";
        private const string RoadTexturePath =
            "Assets/_Project/Art/Environment/env_road_section.fbm/"
            + "road+section+3d+model_basecolor.jpg";
        private const string RoadDisplayMaterialPath =
            "Assets/_Project/Materials/Greybox/Map02RoadFbx.mat";

        /// <summary>
        /// The village is 80 x 72 m and this has to be the same, because carrying
        /// the layout across is meant to be a translation rather than a re-fit —
        /// and because the four-minute match was balanced against those walking
        /// distances.
        /// </summary>
        private const float VillageWidth = 80f;
        private const float VillageDepth = 72f;

        [Test]
        public void Map02GroundMatchesTheVillageFootprint()
        {
            GreyboxMapDefinition map = LoadMap02();

            Assert.That(map, Is.Not.Null);
            Assert.DoesNotThrow(() => map.ValidateOrThrow());
            Assert.That(map.EnvironmentContentCleared, Is.True);
            Assert.That(map.MapWidthMeters, Is.EqualTo(VillageWidth));
            Assert.That(map.MapDepthMeters, Is.EqualTo(VillageDepth));
            Assert.That(
                map.transform.parent.name,
                Is.EqualTo("MAP-002 Greybox Layout"));

            Transform environment =
                map.transform.parent.Find("Environment");
            Assert.That(environment, Is.Not.Null);

            // The ground the map claims and the ground that was built.
            Bounds ground = GetRendererBounds(environment.Find("Ground"));
            Assert.That(
                ground.size.x,
                Is.EqualTo(map.MapWidthMeters).Within(0.01f));
            Assert.That(
                ground.size.z,
                Is.EqualTo(map.MapDepthMeters).Within(0.01f));

            foreach (string side in
                new[]
                {
                    "North Boundary",
                    "South Boundary",
                    "West Boundary",
                    "East Boundary"
                })
            {
                Assert.That(
                    environment.Find(side),
                    Is.Not.Null,
                    $"{side} is missing, so the map has an edge to fall off.");
            }
        }

        /// <summary>
        /// Streets a chase fits down.
        ///
        /// This is the check that was missing while the town was cut at 2 m. A
        /// route has to stay <see cref="GreyboxMapDefinition.RequiredMinimumClearWidth"/>
        /// clear, and a town whose streets are narrower cannot host one however
        /// good it looks from above.
        /// </summary>
        [Test]
        public void Map02StreetsAreWideEnoughForARoute()
        {
            Transform roadRoot = Environment().Find("MAP-002 Roads");
            Assert.That(
                roadRoot.childCount,
                Is.GreaterThanOrEqualTo(10),
                "A town this size needs a street network, not a few paths.");

            foreach (Transform road in roadRoot)
            {
                Bounds bounds = GetRendererBounds(road);
                float across = Mathf.Min(bounds.size.x, bounds.size.z);
                Assert.That(
                    across,
                    Is.GreaterThanOrEqualTo(
                        GreyboxMapDefinition.RequiredMinimumClearWidth),
                    $"'{road.name}' is {across:0.##}m across.");
            }
        }

        /// <summary>
        /// Nothing that was built stands in a street.
        ///
        /// Measured from renderers on both sides. The tolerance is for eaves: a
        /// roof overhanging a flat road decal by a few centimetres blocks nobody,
        /// but half a house in the carriageway does.
        /// </summary>
        [Test]
        public void Map02BuildingsDoNotStandInTheRoads()
        {
            Transform environment = Environment();
            Transform roadRoot = environment.Find("MAP-002 Roads");
            Transform buildingRoot = environment.Find("MAP-002 Buildings");

            Assert.That(
                buildingRoot.childCount,
                Is.GreaterThanOrEqualTo(12),
                "The town lost its buildings.");

            Bounds[] roads = roadRoot
                .Cast<Transform>()
                .Select(GetRendererBounds)
                .ToArray();

            foreach (Transform slot in buildingRoot)
            {
                Bounds building = GetRendererBounds(slot);
                Bounds body = Shrink(building, 0.5f);

                for (int index = 0; index < roads.Length; index++)
                {
                    Assert.That(
                        OverlapsOnXZ(body, roads[index]),
                        Is.False,
                        $"'{slot.name}' stands in "
                        + $"'{roadRoot.GetChild(index).name}'.");
                }
            }
        }

        [Test]
        public void Map02BuildingsDoNotStandInsideEachOther()
        {
            Transform buildingRoot = Environment().Find("MAP-002 Buildings");
            Transform[] slots = buildingRoot.Cast<Transform>().ToArray();
            Bounds[] bounds = slots.Select(GetRendererBounds).ToArray();

            for (int index = 0; index < bounds.Length; index++)
            {
                Assert.That(
                    slots[index].childCount,
                    Is.EqualTo(1),
                    $"'{slots[index].name}' was not built.");
                Assert.That(
                    slots[index].GetChild(0).GetComponent<BoxCollider>(),
                    Is.Not.Null,
                    $"'{slots[index].name}' has nothing to walk into.");

                for (int other = index + 1; other < bounds.Length; other++)
                {
                    Assert.That(
                        OverlapsOnXZ(
                            Shrink(bounds[index], 0.5f),
                            Shrink(bounds[other], 0.5f)),
                        Is.False,
                        $"'{slots[index].name}' and '{slots[other].name}' "
                        + "overlap.");
                }
            }
        }

        /// <summary>
        /// The officer starts on open ground, not in a wall or on the road.
        /// </summary>
        [Test]
        public void Map02PoliceStartsOnClearGround()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform policeSpawn =
                map.GetLocation(GreyboxLocationId.PoliceSpawn);
            Assert.That(policeSpawn, Is.Not.Null);

            PlayerRoleIdentity police = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsInactive.Include)
                .Single(identity => identity.Role == PlayerRole.Police);
            Assert.That(
                Vector3.Distance(
                    police.transform.position,
                    policeSpawn.position + Vector3.up),
                Is.LessThan(0.001f),
                "The officer is not standing on their own spawn.");

            Transform environment = map.transform.parent.Find("Environment");
            var footing = new Bounds(
                new Vector3(
                    policeSpawn.position.x,
                    0.5f,
                    policeSpawn.position.z),
                new Vector3(1f, 1f, 1f));

            foreach (string group in
                new[] { "MAP-002 Roads", "MAP-002 Buildings" })
            {
                foreach (Transform piece in environment.Find(group))
                {
                    Assert.That(
                        OverlapsOnXZ(footing, GetRendererBounds(piece)),
                        Is.False,
                        $"The officer starts inside '{piece.name}'.");
                }
            }

            Assert.That(
                policeSpawn.position.x,
                Is.InRange(0f, map.MapWidthMeters));
            Assert.That(
                policeSpawn.position.z,
                Is.InRange(0f, map.MapDepthMeters));
        }

        /// <summary>
        /// Roads are the textured FBX and carry no collider — they are painted on
        /// the ground, and one with a collider would stop a player dead.
        /// </summary>
        [Test]
        public void Map02BuildsRoadsFromTheTexturedModel()
        {
            Transform roadRoot = Environment().Find("MAP-002 Roads");

            foreach (Transform road in roadRoot)
            {
                Assert.That(road.childCount, Is.EqualTo(1));
                Assert.That(
                    road.GetComponentsInChildren<Collider>(true),
                    Is.Empty,
                    $"'{road.name}' has a collider.");

                Transform model = road.GetChild(0);
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        model.gameObject),
                    Is.EqualTo(RoadModelPath));

                foreach (Renderer renderer in
                    model.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.sharedMaterials, Is.Not.Empty);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null);
                        Assert.That(
                            AssetDatabase.GetAssetPath(material),
                            Is.EqualTo(RoadDisplayMaterialPath));
                        Assert.That(
                            material.shader.name,
                            Is.EqualTo("Universal Render Pipeline/Lit"));
                        Assert.That(
                            AssetDatabase.GetAssetPath(
                                material.GetTexture("_BaseMap")),
                            Is.EqualTo(RoadTexturePath));
                    }
                }
            }
        }

        [Test]
        public void Map02HasGreenTrashBinsOnClearGround()
        {
            Transform environment = Environment();
            Transform trashBinRoot = environment.Find("MAP-002 Trash Bins");
            Assert.That(trashBinRoot.childCount, Is.GreaterThanOrEqualTo(4));

            Bounds[] roads = environment
                .Find("MAP-002 Roads")
                .Cast<Transform>()
                .Select(GetRendererBounds)
                .ToArray();
            Bounds[] buildings = environment
                .Find("MAP-002 Buildings")
                .Cast<Transform>()
                .Select(GetRendererBounds)
                .ToArray();

            foreach (Transform bin in trashBinRoot)
            {
                Assert.That(bin.GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(bin.childCount, Is.EqualTo(1));

                Renderer[] renderers =
                    bin.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty);
                foreach (Renderer renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null);
                    Color color = renderer.sharedMaterial.color;
                    Assert.That(
                        color.g,
                        Is.GreaterThan(color.r),
                        "Trash-bin material should read green.");
                    Assert.That(
                        color.g,
                        Is.GreaterThan(color.b),
                        "Trash-bin material should read green.");
                }

                Bounds footing = GetRendererBounds(bin);
                foreach (Bounds road in roads)
                {
                    Assert.That(
                        OverlapsOnXZ(footing, road),
                        Is.False,
                        $"'{bin.name}' sits in a road.");
                }

                foreach (Bounds building in buildings)
                {
                    Assert.That(
                        OverlapsOnXZ(footing, building),
                        Is.False,
                        $"'{bin.name}' sits inside a building.");
                }
            }
        }

        /// <summary>
        /// Trees fill the gaps and only the gaps. Measured from the renderers,
        /// because a canopy is wider than the point it was planted at and it is
        /// the canopy that ends up inside a wall.
        /// </summary>
        [Test]
        public void Map02TreesAreSparseAndAvoidRoadsAndBuildings()
        {
            Transform environment = Environment();
            Transform treeRoot = environment.Find("MAP-002 Trees");
            Transform roadRoot = environment.Find("MAP-002 Roads");
            Transform buildingRoot = environment.Find("MAP-002 Buildings");

            Assert.That(treeRoot.childCount, Is.GreaterThanOrEqualTo(10));

            Bounds[] roads = roadRoot
                .Cast<Transform>()
                .Select(GetRendererBounds)
                .ToArray();
            Bounds[] buildings = buildingRoot
                .Cast<Transform>()
                .Select(GetRendererBounds)
                .ToArray();
            Transform[] treeSlots = treeRoot.Cast<Transform>().ToArray();
            Bounds[] trees = treeSlots.Select(GetRendererBounds).ToArray();

            for (int index = 0; index < trees.Length; index++)
            {
                string name = treeSlots[index].name;

                foreach (Bounds road in roads)
                {
                    Assert.That(
                        OverlapsOnXZ(trees[index], road),
                        Is.False,
                        $"'{name}' grows in a road.");
                }

                foreach (Bounds building in buildings)
                {
                    Assert.That(
                        OverlapsOnXZ(trees[index], building),
                        Is.False,
                        $"'{name}' grows through a building.");
                }

                for (int other = index + 1; other < trees.Length; other++)
                {
                    Assert.That(
                        OverlapsOnXZ(trees[index], trees[other]),
                        Is.False,
                        $"'{name}' overlaps '{treeSlots[other].name}'.");
                }
            }
        }

        [Test]
        public void Map02AllHousesUseSameLargerOneStoreyModel()
        {
            Transform buildingRoot = Environment().Find("MAP-002 Buildings");

            Transform[] houses = buildingRoot
                .Cast<Transform>()
                .Where(slot => slot.name.Contains("House"))
                .ToArray();
            Assert.That(houses, Is.Not.Empty);

            float? footprint = null;
            foreach (Transform slot in houses)
            {
                Transform model = slot.GetChild(0).GetChild(0);
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        model.gameObject),
                    Does.Contain("building_house_1f"),
                    $"'{slot.name}' uses a different house model.");

                Bounds bounds = GetRendererBounds(slot);
                float size = Mathf.Max(bounds.size.x, bounds.size.z);
                footprint ??= size;
                Assert.That(
                    size,
                    Is.EqualTo(footprint.Value).Within(0.25f),
                    $"'{slot.name}' is a different size from the others.");
            }
        }

        /// <summary>
        /// The grid is a measuring tool, so its counts follow from the ground
        /// size rather than being three numbers to remember. They were literals,
        /// which meant resizing the map failed here instead of where the size
        /// was changed.
        /// </summary>
        [Test]
        public void Map02CoordinateGridCoversTheGround()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform gridRoot = map
                .transform.parent
                .Find("Environment/MAP-002 Coordinate Grid");
            Assert.That(gridRoot, Is.Not.Null);

            int width = Mathf.RoundToInt(map.MapWidthMeters);
            int depth = Mathf.RoundToInt(map.MapDepthMeters);

            Assert.That(
                gridRoot.Find("Grid Lines").childCount,
                Is.EqualTo(width + depth + 2));
            Assert.That(
                gridRoot.Find("Axis Labels").childCount,
                Is.EqualTo(width / 5 + depth / 5 + 4));
            Assert.That(
                gridRoot.Find("Coordinate Labels").childCount,
                Is.EqualTo((width - 1) / 10 * ((depth - 1) / 10)));

            Assert.That(
                gridRoot.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "The grid is drawn on the ground, not walked into.");
        }

        [Test]
        public void Map02RemainsOutsideNormalBuildOrder()
        {
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Does.Not.Contain(Map02ScenePath),
                "MAP-002 is a sandbox and must not ship in the build order.");
        }

        private static GreyboxMapDefinition LoadMap02()
        {
            Scene scene = EditorSceneManager.OpenScene(
                Map02ScenePath,
                OpenSceneMode.Single);
            return scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<GreyboxMapDefinition>(true))
                .FirstOrDefault();
        }

        private static Transform Environment()
        {
            GreyboxMapDefinition map = LoadMap02();
            Assert.That(map, Is.Not.Null);
            Transform environment = map.transform.parent.Find("Environment");
            Assert.That(environment, Is.Not.Null);
            return environment;
        }

        private static Bounds GetRendererBounds(Transform root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            Assert.That(
                renderers,
                Is.Not.Empty,
                $"'{root.name}' has nothing to measure.");
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        /// <summary>
        /// Pulls a box in on X and Z so touching edges and eaves do not read as
        /// an overlap. The height is left alone; nothing here is stacked.
        /// </summary>
        private static Bounds Shrink(Bounds bounds, float metres)
        {
            bounds.size = new Vector3(
                Mathf.Max(0.01f, bounds.size.x - metres),
                bounds.size.y,
                Mathf.Max(0.01f, bounds.size.z - metres));
            return bounds;
        }

        private static bool OverlapsOnXZ(Bounds first, Bounds second)
        {
            return first.min.x < second.max.x
                && first.max.x > second.min.x
                && first.min.z < second.max.z
                && first.max.z > second.min.z;
        }
    }
}
