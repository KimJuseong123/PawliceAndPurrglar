using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatCops.Editor
{
    public static class CatCopsPrototypeBuilder
    {
        private const string Root = "Assets/CatCops";
        private const string ScenePath = Root + "/Scenes/CatCopsPrototype.unity";
        private const string RenderPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("CatCops/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            EnsureRenderPipeline();
            EnsureFolders();
            AssetDatabase.Refresh();

            Dictionary<string, Material> mats = CreateMaterials();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CatCopsPrototype";

            CreateLighting(mats);
            Dictionary<string, Transform> points = CreateVillage(mats);
            CreateTopDownEngineDecor();

            GameObject controllerObject = new GameObject("CatCops Prototype Controller");
            CatCopsTopDownEngineBridge bridge = controllerObject.AddComponent<CatCopsTopDownEngineBridge>();
            CatCopsPrototypeController controller = controllerObject.AddComponent<CatCopsPrototypeController>();

            CatCopsActor police = CreateActor("Police", CatCopsActorRole.Police, "Police_LowPoly.fbx", points["PoliceStart"].position, 4.6f, mats);
            CatCopsActor thief = CreateActor("Thief", CatCopsActorRole.Thief, "Thief_LowPoly.fbx", points["ThiefStart"].position, 4.45f, mats);
            CatCopsActor dog = CreateActor("Dog", CatCopsActorRole.Dog, "Dog_LowPoly.fbx", points["PoliceStart"].position + new Vector3(-1.2f, 0f, -0.8f), 5.4f, mats);
            CatCopsActor cat = CreateActor("Cat", CatCopsActorRole.Cat, "Cat_LowPoly.fbx", points["CatRooftop"].position, 4.1f, mats);
            CatCopsActor merchant = CreateActor("Black Market Merchant", CatCopsActorRole.Merchant, "Merchant_LowPoly.fbx", points["BlackMarket"].position + new Vector3(0.35f, 0f, 0.1f), 0f, mats);

            controller.Police = police;
            controller.Thief = thief;
            controller.Dog = dog;
            controller.Cat = cat;
            controller.Merchant = merchant;
            controller.PoliceStart = points["PoliceStart"];
            controller.ThiefStart = points["ThiefStart"];
            controller.TreasureShop = points["TreasureShop"];
            controller.BlackMarket = points["BlackMarket"];
            controller.CatRooftop = points["CatRooftop"];
            controller.ThiefEscapeRoute = new[]
            {
                points["RouteA"],
                points["RouteB"],
                points["RouteC"],
                points["BlackMarket"]
            };

            controller.StoneProjectilePrefab = CreateProjectilePrefab("StoneProjectile", mats["Stone"], CatCopsActorRole.Thief, 0.28f);
            controller.BoneProjectilePrefab = CreateProjectilePrefab("BoneProjectile", mats["Bone"], CatCopsActorRole.Dog, 0.36f);
            controller.StarBurstPrefab = CreateBurstPrefab(mats);

            CreateHud(controller, mats);
            CreateCamera();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = controllerObject;
            Debug.Log("CatCops prototype scene built at " + ScenePath);
        }

        [MenuItem("CatCops/Run Prototype _F5")]
        public static void RunPrototype()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            EnsureRenderPipeline();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildPrototypeScene();
            }
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Debug.Log("CatCops prototype scene opened. Entering Play Mode.");
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static void EnsureRenderPipeline()
        {
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(RenderPipelinePath);
            if (pipeline == null)
            {
                Debug.LogError("CatCops URP asset was not found at " + RenderPipelinePath);
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
                     {
                         Root,
                         Root + "/Scenes",
                         Root + "/Materials",
                         Root + "/Models",
                         Root + "/Prefabs",
                         Root + "/Docs"
                     })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = System.IO.Path.GetDirectoryName(folder)?.Replace("\\", "/");
                    string name = System.IO.Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            var mats = new Dictionary<string, Material>
            {
                ["Grass"] = SaveMat("Grass", new Color(0.32f, 0.58f, 0.34f)),
                ["Road"] = SaveMat("Road", new Color(0.26f, 0.27f, 0.29f)),
                ["Stone"] = SaveMat("Stone", new Color(0.56f, 0.55f, 0.52f)),
                ["Bone"] = SaveMat("Bone", new Color(0.93f, 0.82f, 0.58f)),
                ["PoliceBlue"] = SaveMat("PoliceBlue", new Color(0.05f, 0.22f, 0.58f)),
                ["ThiefDark"] = SaveMat("ThiefDark", new Color(0.04f, 0.04f, 0.05f)),
                ["DogTan"] = SaveMat("DogTan", new Color(0.9f, 0.49f, 0.12f)),
                ["CatPurple"] = SaveMat("CatPurple", new Color(0.28f, 0.23f, 0.34f)),
                ["RoofRed"] = SaveMat("RoofRed", new Color(0.64f, 0.18f, 0.12f)),
                ["RoofBlue"] = SaveMat("RoofBlue", new Color(0.06f, 0.22f, 0.52f)),
                ["WallWarm"] = SaveMat("WallWarm", new Color(0.67f, 0.55f, 0.41f)),
                ["ShopDark"] = SaveMat("ShopDark", new Color(0.12f, 0.11f, 0.1f)),
                ["Gold"] = SaveMat("Gold", new Color(1f, 0.68f, 0.1f)),
                ["Jewel"] = SaveMat("Jewel", new Color(0.55f, 0.22f, 1f)),
                ["UIBlue"] = SaveMat("UIBlue", new Color(0.06f, 0.19f, 0.41f, 0.88f)),
                ["UIRed"] = SaveMat("UIRed", new Color(0.43f, 0.09f, 0.06f, 0.88f)),
                ["UIGreen"] = SaveMat("UIGreen", new Color(0.1f, 0.34f, 0.16f, 0.88f)),
                ["UIBrown"] = SaveMat("UIBrown", new Color(0.31f, 0.18f, 0.09f, 0.88f)),
            };
            return mats;
        }

        private static Material SaveMat(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateLighting(Dictionary<string, Material> mats)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.58f, 0.66f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.11f);
            RenderSettings.fogDensity = 0.015f;

            GameObject sun = new GameObject("Warm TopDown Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.82f, 0.62f);
            sun.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

            CreatePrimitive("Ground", PrimitiveType.Cube, Vector3.zero, new Vector3(28f, 0.2f, 20f), mats["Grass"]);
            CreatePrimitive("Main Road NS", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(3.8f, 0.12f, 20f), mats["Road"]);
            CreatePrimitive("Main Road EW", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(28f, 0.12f, 3.2f), mats["Road"]);
            CreatePrimitive("Alley Road", PrimitiveType.Cube, new Vector3(8.3f, 0.05f, -4f), new Vector3(3.2f, 0.12f, 10f), mats["Road"]);
        }

        private static Dictionary<string, Transform> CreateVillage(Dictionary<string, Material> mats)
        {
            var points = new Dictionary<string, Transform>();

            CreateBuilding("Police Station", new Vector3(-2.2f, 0f, 3.8f), new Vector3(4.2f, 2.4f, 3.2f), mats["WallWarm"], mats["RoofBlue"]);
            CreateBadge(new Vector3(-2.2f, 2.65f, 2.12f), mats["Gold"]);

            CreateBuilding("Treasure Shop", new Vector3(5.9f, 0f, 2.5f), new Vector3(3.7f, 2.3f, 3.0f), mats["ShopDark"], mats["RoofRed"]);
            CreateGem(new Vector3(5.9f, 2.78f, 0.95f), mats["Jewel"]);

            CreateBuilding("Supermarket", new Vector3(-8.6f, 0f, -1.9f), new Vector3(3.4f, 1.9f, 2.8f), mats["WallWarm"], mats["RoofRed"]);
            CreateBuilding("Bookshop", new Vector3(2.5f, 0f, -5.4f), new Vector3(3.2f, 1.9f, 2.7f), mats["WallWarm"], mats["RoofBlue"]);
            CreateBuilding("Two Floor House", new Vector3(-8.3f, 0f, 4.7f), new Vector3(3.1f, 2.7f, 2.7f), mats["WallWarm"], mats["RoofRed"]);
            CreateBuilding("Rooftop House", new Vector3(8.7f, 0f, -3.7f), new Vector3(3.3f, 2.6f, 2.9f), mats["WallWarm"], mats["RoofBlue"]);

            CreateTrashMarket(new Vector3(10.3f, 0f, -7.0f), mats);
            CreateLadder(new Vector3(10.15f, 0f, -2.25f), mats["Bone"]);
            CreateFenceLine(-12.5f, 12.5f, 8.4f, mats["WallWarm"]);
            CreateFenceLine(-12.5f, 12.5f, -8.4f, mats["WallWarm"]);
            CreateFenceLine(-12.5f, -12.5f, 8.4f, mats["WallWarm"], true);
            CreateFenceLine(12.5f, 12.5f, 8.4f, mats["WallWarm"], true);

            points["PoliceStart"] = CreatePoint("PoliceStart", new Vector3(-2.1f, 0.2f, 0.9f));
            points["ThiefStart"] = CreatePoint("ThiefStart", new Vector3(5.6f, 0.2f, 0.0f));
            points["TreasureShop"] = CreatePoint("TreasureShop", new Vector3(5.9f, 0.2f, 1.0f));
            points["BlackMarket"] = CreatePoint("BlackMarket", new Vector3(10.3f, 0.2f, -7.0f));
            points["CatRooftop"] = CreatePoint("CatRooftop", new Vector3(8.7f, 2.95f, -3.7f));
            points["RouteA"] = CreatePoint("RouteA", new Vector3(6.9f, 0.2f, -0.7f));
            points["RouteB"] = CreatePoint("RouteB", new Vector3(8.4f, 0.2f, -3.4f));
            points["RouteC"] = CreatePoint("RouteC", new Vector3(10.0f, 0.2f, -5.4f));

            CreateRouteHint(points["ThiefStart"].position, points["RouteA"].position, mats["Gold"]);
            CreateRouteHint(points["RouteA"].position, points["RouteB"].position, mats["Gold"]);
            CreateRouteHint(points["RouteB"].position, points["RouteC"].position, mats["Gold"]);
            CreateRouteHint(points["RouteC"].position, points["BlackMarket"].position, mats["Gold"]);

            return points;
        }

        private static void CreateTopDownEngineDecor()
        {
            string coinPath = "Assets/TopDownEngine/Demos/Minimal3D/Prefabs/Props/Minimal3DCoin.prefab";
            GameObject coin = AssetDatabase.LoadAssetAtPath<GameObject>(coinPath);
            if (coin == null)
            {
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(coin);
                instance.name = "TopDownEngine Coin Decor " + (i + 1);
                instance.transform.position = new Vector3(8.8f + i * 0.35f, 0.4f, -6.4f + (i % 2) * 0.25f);
                instance.transform.localScale = Vector3.one * 0.55f;
            }
        }

        private static CatCopsActor CreateActor(string name, CatCopsActorRole role, string fbxName, Vector3 position, float speed, Dictionary<string, Material> mats)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            string modelPath = Root + "/Models/" + fbxName;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one;
            }
            else
            {
                CreateActorFallback(role, root.transform, mats);
            }

            CatCopsActor actor = root.AddComponent<CatCopsActor>();
            actor.Role = role;
            actor.MoveSpeed = speed;
            GameObject carry = new GameObject("Carry Point");
            carry.transform.SetParent(root.transform, false);
            carry.transform.localPosition = new Vector3(0f, 1.1f, -0.55f);
            actor.CarryPoint = carry.transform;

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.radius = role == CatCopsActorRole.Dog || role == CatCopsActorRole.Cat ? 0.48f : 0.55f;
            collider.height = role == CatCopsActorRole.Dog || role == CatCopsActorRole.Cat ? 1.25f : 1.85f;
            return actor;
        }

        private static void CreateActorFallback(CatCopsActorRole role, Transform root, Dictionary<string, Material> mats)
        {
            Material primary = role switch
            {
                CatCopsActorRole.Police => mats["PoliceBlue"],
                CatCopsActorRole.Thief => mats["ThiefDark"],
                CatCopsActorRole.Dog => mats["DogTan"],
                CatCopsActorRole.Cat => mats["CatPurple"],
                _ => mats["ShopDark"]
            };
            CreatePrimitive("Body", PrimitiveType.Capsule, root, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), primary);
            CreatePrimitive("Head", PrimitiveType.Sphere, root, new Vector3(0f, 1.55f, -0.15f), new Vector3(0.62f, 0.48f, 0.62f), primary);
            CreatePrimitive("Nose", PrimitiveType.Sphere, root, new Vector3(0f, 1.48f, -0.57f), Vector3.one * 0.14f, mats["Stone"]);
        }

        private static GameObject CreateProjectilePrefab(string name, Material mat, CatCopsActorRole targetRole, float size)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                CatCopsProjectile existing = prefab.GetComponent<CatCopsProjectile>();
                if (existing != null)
                {
                    existing.TargetRole = targetRole;
                }
                return prefab;
            }

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = name;
            root.transform.localScale = Vector3.one * size;
            root.GetComponent<Renderer>().sharedMaterial = mat;
            CatCopsProjectile projectile = root.AddComponent<CatCopsProjectile>();
            projectile.TargetRole = targetRole;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject CreateBurstPrefab(Dictionary<string, Material> mats)
        {
            string path = Root + "/Prefabs/StarBurst.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }

            GameObject root = new GameObject("StarBurst");
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                star.name = "Star";
                star.transform.SetParent(root.transform, false);
                star.transform.localPosition = new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle)) * 0.35f;
                star.transform.localScale = Vector3.one * 0.18f;
                star.GetComponent<Renderer>().sharedMaterial = mats["Gold"];
            }
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void CreateHud(CatCopsPrototypeController controller, Dictionary<string, Material> mats)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 18);
            }

            GameObject canvasObject = new GameObject("CatCops HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            Text police = CreatePanelText(canvasRect, "Police Score", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(170f, -55f), new Vector2(260f, 80f), "경찰 0", 34, TextAnchor.MiddleCenter, new Color(0.04f, 0.14f, 0.34f, 0.86f), font);
            Text timer = CreatePanelText(canvasRect, "Timer", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(260f, 78f), "02:35", 44, TextAnchor.MiddleCenter, new Color(0.04f, 0.05f, 0.07f, 0.9f), font);
            Text thief = CreatePanelText(canvasRect, "Thief Score", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -55f), new Vector2(300f, 80f), "도둑 $0", 34, TextAnchor.MiddleCenter, new Color(0.42f, 0.08f, 0.05f, 0.86f), font);
            Text dog = CreatePanelText(canvasRect, "Dog Status", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -145f), new Vector2(250f, 54f), "강아지 추적 중", 24, TextAnchor.MiddleCenter, new Color(0.08f, 0.29f, 0.13f, 0.84f), font);
            Text cat = CreatePanelText(canvasRect, "Cat Status", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -145f), new Vector2(300f, 54f), "고양이 은신/배달", 24, TextAnchor.MiddleCenter, new Color(0.32f, 0.12f, 0.08f, 0.84f), font);
            Text command = CreatePanelText(canvasRect, "Command Status", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(660f, 78f), "음성 명령 대기", 28, TextAnchor.MiddleCenter, new Color(0.04f, 0.12f, 0.23f, 0.88f), font);
            Text round = CreatePanelText(canvasRect, "Round Status", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(800f, 42f), "WASD 이동 | R 던지기 | 1/V 강아지 추적", 20, TextAnchor.MiddleCenter, new Color(0.02f, 0.03f, 0.04f, 0.72f), font);

            controller.TimerText = timer;
            controller.PoliceScoreText = police;
            controller.ThiefScoreText = thief;
            controller.DogStatusText = dog;
            controller.CatStatusText = cat;
            controller.CommandStatusText = command;
            controller.RoundStatusText = round;
        }

        private static Text CreatePanelText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, string value, int fontSize, TextAnchor align, Color panelColor, Font font)
        {
            GameObject panel = new GameObject(name + " Panel", typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = panelColor;
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            GameObject textObject = new GameObject(name, typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = align;
            text.color = Color.white;
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
            return text;
        }

        private static void CreateCamera()
        {
            GameObject camObj = new GameObject("2.5D TopDown Camera", typeof(Camera), typeof(AudioListener));
            Camera cam = camObj.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10.8f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            camObj.transform.position = new Vector3(0f, 17.5f, -15.5f);
            camObj.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private static void CreateBuilding(string name, Vector3 position, Vector3 size, Material wall, Material roof)
        {
            CreatePrimitive(name + " Walls", PrimitiveType.Cube, position + new Vector3(0f, size.y * 0.5f, 0f), size, wall);
            CreatePrimitive(name + " Roof", PrimitiveType.Cube, position + new Vector3(0f, size.y + 0.28f, 0f), new Vector3(size.x + 0.35f, 0.45f, size.z + 0.35f), roof);
            CreatePrimitive(name + " Door", PrimitiveType.Cube, position + new Vector3(0f, 0.58f, -size.z * 0.51f), new Vector3(0.75f, 1.1f, 0.08f), roof);
            CreatePrimitive(name + " Window L", PrimitiveType.Cube, position + new Vector3(-size.x * 0.28f, 1.22f, -size.z * 0.52f), new Vector3(0.5f, 0.45f, 0.08f), SaveMat("WindowWarm", new Color(1f, 0.72f, 0.28f)));
            CreatePrimitive(name + " Window R", PrimitiveType.Cube, position + new Vector3(size.x * 0.28f, 1.22f, -size.z * 0.52f), new Vector3(0.5f, 0.45f, 0.08f), SaveMat("WindowWarm", new Color(1f, 0.72f, 0.28f)));
        }

        private static void CreateTrashMarket(Vector3 position, Dictionary<string, Material> mats)
        {
            GameObject can = CreatePrimitive("Black Market Trash Can", PrimitiveType.Cylinder, position + new Vector3(0f, 0.6f, 0f), new Vector3(1f, 0.65f, 1f), mats["Stone"]);
            can.transform.rotation = Quaternion.Euler(0f, 24f, 0f);
            CreatePrimitive("Trash Lid", PrimitiveType.Cylinder, position + new Vector3(0f, 1.25f, 0f), new Vector3(1.1f, 0.16f, 1.1f), mats["ShopDark"]);
            CreateGem(position + new Vector3(-0.65f, 0.35f, -0.7f), mats["Jewel"]);
            for (int i = 0; i < 4; i++)
            {
                CreatePrimitive("Market Coin " + i, PrimitiveType.Sphere, position + new Vector3(-0.5f + i * 0.28f, 0.24f, -1.0f), Vector3.one * 0.18f, mats["Gold"]);
            }
        }

        private static void CreateLadder(Vector3 position, Material mat)
        {
            CreatePrimitive("Ladder Rail L", PrimitiveType.Cube, position + new Vector3(-0.25f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 0.08f), mat);
            CreatePrimitive("Ladder Rail R", PrimitiveType.Cube, position + new Vector3(0.25f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 0.08f), mat);
            for (int i = 0; i < 5; i++)
            {
                CreatePrimitive("Ladder Step " + i, PrimitiveType.Cube, position + new Vector3(0f, 0.35f + i * 0.42f, 0f), new Vector3(0.6f, 0.07f, 0.08f), mat);
            }
        }

        private static void CreateFenceLine(float x0, float x1, float z, Material mat, bool vertical = false)
        {
            int count = 10;
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                Vector3 pos = vertical
                    ? new Vector3(x0, 0.42f, Mathf.Lerp(-z, z, t))
                    : new Vector3(Mathf.Lerp(x0, x1, t), 0.42f, z);
                CreatePrimitive("Fence Post", PrimitiveType.Cube, pos, new Vector3(0.13f, 0.85f, 0.13f), mat);
            }
        }

        private static void CreateRouteHint(Vector3 start, Vector3 end, Material mat)
        {
            Vector3 mid = (start + end) * 0.5f + Vector3.up * 0.09f;
            Vector3 delta = end - start;
            GameObject hint = CreatePrimitive("Thief Route Hint", PrimitiveType.Cube, mid, new Vector3(0.12f, 0.08f, delta.magnitude), mat);
            hint.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        private static Transform CreatePoint(string name, Vector3 position)
        {
            GameObject point = new GameObject(name);
            point.transform.position = position;
            return point.transform;
        }

        private static void CreateBadge(Vector3 position, Material mat)
        {
            GameObject badge = CreatePrimitive("Police Badge", PrimitiveType.Sphere, position, new Vector3(0.55f, 0.12f, 0.55f), mat);
            badge.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void CreateGem(Vector3 position, Material mat)
        {
            GameObject gem = CreateOctahedron("Gem");
            gem.transform.position = position;
            gem.transform.localScale = Vector3.one * 0.5f;
            gem.GetComponent<Renderer>().sharedMaterial = mat;
            gem.transform.rotation = Quaternion.Euler(20f, 25f, 10f);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
            }
            return obj;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType primitive, Transform parent, Vector3 localPosition, Vector3 scale, Material mat)
        {
            GameObject obj = CreatePrimitive(name, primitive, Vector3.zero, scale, mat);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            return obj;
        }

        private static GameObject CreateOctahedron(string name)
        {
            Mesh mesh = new Mesh { name = name + " Mesh" };
            Vector3[] vertices =
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, -1f),
                new Vector3(0f, -1f, 0f)
            };
            int[] triangles =
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,
                5, 2, 1,
                5, 3, 2,
                5, 4, 3,
                5, 1, 4
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            GameObject obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            return obj;
        }
    }

    [InitializeOnLoad]
    public static class CatCopsCommandLineRunner
    {
        private const string SessionKey = "CatCops.CommandLineRunHandled";

        static CatCopsCommandLineRunner()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            foreach (string argument in System.Environment.GetCommandLineArgs())
            {
                if (argument != "-catCopsRun")
                {
                    continue;
                }

                SessionState.SetBool(SessionKey, true);
                EditorApplication.delayCall += CatCopsPrototypeBuilder.RunPrototype;
                break;
            }
        }
    }
}
