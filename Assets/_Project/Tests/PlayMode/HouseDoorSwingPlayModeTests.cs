using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PawliceAndPurrglar.Animation;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// The front door actually swings, without the house being taken apart.
    ///
    /// It used to swing by having its five parts reparented onto a hinge object,
    /// which meant unpacking the prefab and writing 186 loose parts per house into
    /// the scene. Turning them where they stand costs nothing and should look the
    /// same — but "looks the same" is the claim, and a door that quietly stops
    /// moving is exactly the failure the bin lid had, with nothing in any log.
    ///
    /// So the parts are measured: they turn, they turn together, and they turn
    /// outward whichever way the house faces.
    /// </summary>
    public sealed class HouseDoorSwingPlayModeTests
    {
        private readonly List<GameObject> _created = new();

        /// <summary>
        /// Frames in batch mode last a fraction of a millisecond, so a door that
        /// takes 0.35 s to open has barely started after a hundred of them. Fixing
        /// the frame length makes the wait mean what it says.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = 1f / 60f;
        }

        /// <summary>
        /// Destroyed immediately, not at the end of the frame. A leftover house
        /// from one test is found by the next one, which then fails against the
        /// wrong door.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            foreach (GameObject created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        /// <summary>
        /// A stand-in house: the five parts the animator looks for, named as the
        /// model names them, with the leaf a door-shaped slab on the +Z face.
        /// </summary>
        private HouseDoorLeaf BuildHouse(Quaternion houseRotation)
        {
            var house = new GameObject("house_1f");
            _created.Add(house);
            house.transform.SetPositionAndRotation(
                new Vector3(12f, 0f, -4f),
                houseRotation);

            (string name, Vector3 local, Vector3 size)[] parts =
            {
                ("BD_House1F_Door_Front_Leaf",
                    new Vector3(0f, 1f, 3.1f), new Vector3(1.1f, 2f, 0.1f)),
                ("BD_House1F_Door_Front_Handle",
                    new Vector3(0.4f, 1f, 3.05f), new Vector3(0.1f, 0.1f, 0.1f)),
                ("BD_House1F_Door_Front_Panel_1",
                    new Vector3(0f, 1.4f, 3.1f), new Vector3(0.8f, 0.5f, 0.1f)),
                ("BD_House1F_Door_Front_Panel_2",
                    new Vector3(0f, 0.6f, 3.1f), new Vector3(0.8f, 0.5f, 0.1f)),
                ("BD_House1F_Door_Front_VerticalGlass",
                    new Vector3(0f, 1.2f, 3.1f), new Vector3(0.3f, 0.9f, 0.1f))
            };

            foreach ((string name, Vector3 local, Vector3 size) in parts)
            {
                GameObject part = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                part.name = name;
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.transform.SetParent(house.transform, false);
                part.transform.localPosition = local;
                part.transform.localScale = size;
            }

            var door = new GameObject("Front Door");
            door.transform.SetParent(house.transform, false);
            HouseDoorLeaf leaf = door.AddComponent<HouseDoorLeaf>();
            leaf.Configure(house.transform, -95f);
            return leaf;
        }

        private static Transform Part(HouseDoorLeaf door, string name)
        {
            return door.transform.parent.Find(name);
        }

        private static IEnumerator OpenFully(HouseDoorLeaf door)
        {
            door.Swing();
            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator EveryDoorPartTurnsAboutTheLeafEdge()
        {
            HouseDoorLeaf door = BuildHouse(Quaternion.identity);
            yield return null;

            Assert.That(
                door.PartCount,
                Is.EqualTo(5),
                "The animator found no door to move, which is the silent "
                + "failure this test exists for.");

            Transform house = door.transform.parent;
            Transform leaf = Part(door, "BD_House1F_Door_Front_Leaf");
            Transform handle = Part(door, "BD_House1F_Door_Front_Handle");

            // The edge, not the middle. A door hinged at its centre sweeps
            // through whoever is standing at it.
            Assert.That(
                door.HingePoint.x,
                Is.LessThan(leaf.position.x - 0.4f),
                "The hinge is not at the leaf's near edge.");

            Vector3 leafBefore = leaf.position;
            Vector3 handleBefore = handle.position;
            float spacingBefore =
                Vector3.Distance(leafBefore, handleBefore);
            float outwardBefore =
                house.InverseTransformPoint(leafBefore).z;

            yield return OpenFully(door);

            Assert.That(
                door.IsOpen,
                Is.True,
                "The door reports itself shut after being used.");

            // Moved, and by enough to see.
            Assert.That(
                Vector3.Distance(leaf.position, leafBefore),
                Is.GreaterThan(0.3f),
                "The leaf did not move.");
            Assert.That(
                Vector3.Distance(handle.position, handleBefore),
                Is.GreaterThan(0.3f),
                "The handle stayed behind while the leaf moved, so the door "
                + "came apart.");

            // Rigid: the parts keep their spacing. A hinge gave that for free
            // and turning each part separately has to be checked for it.
            Assert.That(
                Vector3.Distance(leaf.position, handle.position),
                Is.EqualTo(spacingBefore).Within(0.01f),
                "The door parts drifted apart instead of turning together.");

            // Outward. Measured in the house's own space, because "away from the
            // building" is a local direction.
            Assert.That(
                house.InverseTransformPoint(leaf.position).z,
                Is.GreaterThan(outwardBefore + 0.1f),
                "The leaf swung into the house instead of out of it.");
        }

        [UnityTest]
        public IEnumerator ARotatedHouseOpensTheSameWayRound()
        {
            // Turned to face the other way, as the districts do. The parts are
            // turned about world up, so this is the case that would break if that
            // choice were wrong: the offsets rotate with the house, and the door
            // has to still open away from it.
            HouseDoorLeaf door = BuildHouse(Quaternion.Euler(0f, 180f, 0f));
            yield return null;

            Transform house = door.transform.parent;
            Transform leaf = Part(door, "BD_House1F_Door_Front_Leaf");
            Vector3 worldBefore = leaf.position;
            float outwardBefore =
                house.InverseTransformPoint(worldBefore).z;

            yield return OpenFully(door);

            Assert.That(
                house.InverseTransformPoint(leaf.position).z,
                Is.GreaterThan(outwardBefore + 0.1f),
                "On a house facing the other way the door swung inward.");

            // And it genuinely went somewhere else in the world, so the check
            // above cannot be passed by the door not moving at all.
            Assert.That(
                Vector3.Distance(leaf.position, worldBefore),
                Is.GreaterThan(0.3f));
        }

        [UnityTest]
        public IEnumerator ADoorWithNoPartsIsQuietRatherThanBroken()
        {
            var bare = new GameObject("house_empty");
            _created.Add(bare);
            HouseDoorLeaf door = bare.AddComponent<HouseDoorLeaf>();
            door.Configure(bare.transform, -95f);

            door.Swing();
            yield return null;
            yield return null;

            Assert.That(door.PartCount, Is.Zero);
            Assert.That(
                door.IsOpen,
                Is.False,
                "A door with nothing to move should not claim to be open.");
        }
    }
}
