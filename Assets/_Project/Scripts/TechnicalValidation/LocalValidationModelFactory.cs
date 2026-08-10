using PawliceAndPurrglar.Animation;
using UnityEngine;

namespace PawliceAndPurrglar.TechnicalValidation
{
    /// <summary>
    /// Deterministic local-only visual fallback used when an authored FBX is a
    /// Git LFS pointer or is otherwise unavailable. It is intentionally named
    /// like the contracts consumed by the animation and interior tests.
    /// </summary>
    public static class LocalValidationModelFactory
    {
        public static GameObject CreateCharacter(
            string stem,
            Transform parent,
            float height)
        {
            bool quadruped = stem == "dog" || stem == "cat";
            GameObject root = new(stem + "_LocalFallback");
            root.transform.SetParent(parent, false);

            Animator animator = root.AddComponent<Animator>();
            root.AddComponent<AnimatorClipGuard>().Configure(animator);

            Material material = CreateMaterial(
                stem == "police"
                    ? new Color(0.12f, 0.28f, 0.85f)
                    : stem == "thief"
                        ? new Color(0.75f, 0.14f, 0.18f)
                        : stem == "dog"
                            ? new Color(0.78f, 0.48f, 0.2f)
                            : new Color(0.45f, 0.3f, 0.72f));

            GameObject body = GameObject.CreatePrimitive(
                quadruped ? PrimitiveType.Capsule : PrimitiveType.Capsule);
            body.name = stem + "_Body_Renderer";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * (height * 0.5f);
            body.transform.localScale = new Vector3(
                quadruped ? height * 0.5f : height * 0.42f,
                height * 0.5f,
                quadruped ? height * 0.75f : height * 0.42f);
            DisableCollider(body);
            body.GetComponent<Renderer>().sharedMaterial = material;
            ConvertToSkinnedRenderer(body, material);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = stem + "_Head_Renderer";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = Vector3.up * (height * 0.93f)
                + (quadruped ? Vector3.forward * height * 0.28f : Vector3.zero);
            head.transform.localScale = Vector3.one * height * 0.28f;
            DisableCollider(head);
            head.GetComponent<Renderer>().sharedMaterial = material;
            ConvertToSkinnedRenderer(head, material);

            if (quadruped)
            {
                CreateLimb(root.transform, "L_Upperarm_Front", "L_Forearm_Front", -1, 1);
                CreateLimb(root.transform, "R_Upperarm_Front", "R_Forearm_Front", 1, 1);
                CreateLimb(root.transform, "L_Upperarm_Hind", "L_Forearm_Hind", -1, -1);
                CreateLimb(root.transform, "R_Upperarm_Hind", "R_Forearm_Hind", 1, -1);
            }
            else if (stem != "raccoon")
            {
                CreateLimb(root.transform, "L_Thigh", "L_Calf", -1, -1);
                CreateLimb(root.transform, "R_Thigh", "R_Calf", 1, -1);
                CreateLimb(root.transform, "L_Upperarm", "L_Forearm", -1, 1);
                CreateLimb(root.transform, "R_Upperarm", "R_Forearm", 1, 1);
            }

            if (stem == "raccoon")
            {
                Transform left = CreateLimb(
                    root.transform,
                    "L_Upperarm",
                    "L_Forearm",
                    -1,
                    1);
                Transform leftForearm = left.Find("L_Forearm");
                leftForearm.localPosition = Vector3.zero;
                CreateChild(leftForearm, "L_Hand").localPosition =
                    new Vector3(-0.25f, 0f, 0f);
                Transform right = CreateLimb(
                    root.transform,
                    "R_Upperarm",
                    "R_Forearm",
                    1,
                    1);
                Transform rightForearm = right.Find("R_Forearm");
                rightForearm.localPosition = Vector3.zero;
                CreateChild(rightForearm, "R_Hand").localPosition =
                    new Vector3(0.25f, 0f, 0f);
            }

            BindFallbackSkeleton(root.transform);

            return root;
        }

        public static GameObject CreateBuilding(
            string stem,
            Transform parent)
        {
            bool furnished = stem.Contains("with_interior");
            GameObject root = new(stem + "_LocalFallback");
            root.transform.SetParent(parent, false);
            Material material = CreateMaterial(new Color(0.48f, 0.52f, 0.58f));

            CreatePart(root.transform, "BD_House1F_Foundation", Vector3.zero,
                new Vector3(10f, 0.25f, 9f), material);
            CreatePart(root.transform, "BD_House1F_Wall_Left",
                new Vector3(-4.5f, 2f, 0f), new Vector3(0.35f, 4f, 9f), material);
            CreatePart(root.transform, "BD_House1F_Wall_Right",
                new Vector3(4.5f, 2f, 0f), new Vector3(0.35f, 4f, 9f), material);
            CreatePart(root.transform, "BD_House1F_Wall_Front",
                new Vector3(0f, 2f, -4.5f), new Vector3(9f, 4f, 0.35f), material);
            CreatePart(root.transform, "BD_House1F_Wall_Back",
                new Vector3(0f, 2f, 4.5f), new Vector3(9f, 4f, 0.35f), material);
            CreatePart(root.transform, "BD_House1F_Door_Front_Leaf",
                new Vector3(0f, 1.2f, -4.3f), new Vector3(1.2f, 2.4f, 0.12f), material);
            CreatePart(root.transform, "BD_House1F_Door_Back_Leaf",
                new Vector3(0f, 1.2f, 4.3f), new Vector3(1.2f, 2.4f, 0.12f), material);

            CreateWallDetails(
                root.transform,
                "BD_House1F_Wall_Front_Detail_",
                true,
                material);
            CreateWallDetails(
                root.transform,
                "BD_House1F_Wall_Back_Detail_",
                true,
                material);
            CreateWallDetails(
                root.transform,
                "BD_House1F_Wall_Left_Detail_",
                false,
                material);
            CreateWallDetails(
                root.transform,
                "BD_House1F_Wall_Right_Detail_",
                false,
                material);

            int count = furnished ? 36 : 128;
            for (int index = 0; index < count; index++)
            {
                float x = -4f + (index % 16) * 0.52f;
                float z = -3.8f + (index / 16) * 0.85f;
                CreatePart(
                    root.transform,
                    "BD_" + stem + "_ValidationPart_" + index,
                    new Vector3(x, 0.35f + (index % 3) * 0.15f, z),
                    new Vector3(0.35f, 0.4f, 0.35f),
                    material);
            }

            if (furnished)
            {
                string[] furniture =
                {
                    "IN_Bedroom_Bed_Frame",
                    "IN_Bedroom_Wardrobe",
                    "IN_Kitchen_Fridge",
                    "IN_LivingRoom_Sofa_Base",
                    "IN_LivingRoom_CoffeeTable",
                    "IN_Bathroom_Bathtub",
                    "IN_House1F_Wall_Bedroom",
                    "IN_House1F_Wall_Kitchen",
                    "IN_House1F_Wall_LivingRoom",
                    "IN_House1F_Wall_Bathroom_Back"
                };
                for (int index = 0; index < furniture.Length; index++)
                {
                    CreatePart(
                        root.transform,
                        furniture[index],
                        new Vector3(-2f + index, 0.6f, 1.5f),
                        new Vector3(1.2f, 0.8f, 0.8f),
                        material);
                }
            }

            return root;
        }

        private static Transform CreateLimb(
            Transform parent,
            string upperName,
            string lowerName,
            int side,
            int front)
        {
            Transform upper = CreateChild(parent, upperName);
            upper.localPosition = new Vector3(
                side * 0.25f,
                0.65f,
                front * 0.28f);
            Transform lower = CreateChild(upper, lowerName);
            lower.localPosition = Vector3.down * 0.35f;
            return upper;
        }

        private static GameObject CreatePart(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            DisableCollider(part);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static void CreateWallDetails(
            Transform parent,
            string prefix,
            bool horizontal,
            Material material)
        {
            for (int index = 0; index < 6; index++)
            {
                float offset = -3.75f + index * 1.5f;
                Vector3 position = horizontal
                    ? new Vector3(offset, 2f, prefix.Contains("Front") ? -4.72f : 4.72f)
                    : new Vector3(prefix.Contains("Left") ? -4.72f : 4.72f, 2f, offset);
                Vector3 scale = horizontal
                    ? new Vector3(1.25f, 4f, 0.12f)
                    : new Vector3(0.12f, 4f, 1.25f);
                CreatePart(parent, prefix + index, position, scale, material);
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void DisableCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static void ConvertToSkinnedRenderer(
            GameObject gameObject,
            Material material)
        {
            MeshFilter filter = gameObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null)
            {
                return;
            }

            SkinnedMeshRenderer skinned =
                gameObject.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = filter.sharedMesh;
            skinned.sharedMaterial = material;
            Transform root = gameObject.transform.parent;
            if (root != null)
            {
                skinned.rootBone = root;
                skinned.bones = root.GetComponentsInChildren<Transform>(true);
            }
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(filter);
        }

        private static void BindFallbackSkeleton(Transform root)
        {
            Transform[] bones = root.GetComponentsInChildren<Transform>(true);
            foreach (SkinnedMeshRenderer skin in
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.rootBone = root;
                skin.bones = bones;
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            Material material = new(shader)
            {
                color = color
            };
            return material;
        }
    }
}
