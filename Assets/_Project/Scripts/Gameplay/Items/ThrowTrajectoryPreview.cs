using UnityEngine;
using UnityEngine.Rendering;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Lightweight runtime preview for a charged throw. The marker is cosmetic;
    /// the host still resolves walls and hits through ThrowResolver.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThrowTrajectoryPreview : MonoBehaviour
    {
        private const int PointCount = 18;
        private const int DirectionMarkerCount = 8;
        private const float PreviewLift = 0.18f;
        private const string TrajectoryMaterialResource =
            "ThrowTrajectoryMaterial";
        private const string LandingMaterialResource =
            "ThrowLandingMarkerMaterial";

        [SerializeField]
        private Material trajectoryMaterial;

        [SerializeField]
        private Material landingMarkerMaterial;

        private LineRenderer _line;
        private GameObject _landingMarker;
        private GameObject[] _directionMarkers;
        private Material _runtimeTrajectoryMaterial;
        private Material _runtimeLandingMarkerMaterial;
        private static Mesh _arrowMesh;
        private bool _reportedMissingMaterial;

        public LineRenderer Line => _line;
        public GameObject LandingMarker => _landingMarker;
        public int VisibleDirectionMarkerCount
        {
            get
            {
                if (_directionMarkers == null)
                {
                    return 0;
                }

                int count = 0;
                for (int index = 0; index < _directionMarkers.Length; index++)
                {
                    if (_directionMarkers[index] != null
                        && _directionMarkers[index].activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Configure(
            Material configuredTrajectoryMaterial,
            Material configuredLandingMarkerMaterial)
        {
            trajectoryMaterial = configuredTrajectoryMaterial;
            landingMarkerMaterial = configuredLandingMarkerMaterial;
            ReleaseRuntimeMaterials();
            if (_line != null)
            {
                _line.sharedMaterial = ResolveTrajectoryMaterial();
            }

            if (_landingMarker != null)
            {
                Renderer markerRenderer =
                    _landingMarker.GetComponent<Renderer>();
                if (markerRenderer != null)
                {
                    markerRenderer.sharedMaterial =
                        ResolveLandingMarkerMaterial();
                }
            }
        }

        public void Show(
            Vector3 origin,
            Vector3 direction,
            float distance,
            int obstacleLayers = 0)
        {
            EnsureVisuals();
            ThrowTrajectorySolver.Solution solution =
                ThrowTrajectorySolver.Solve(
                    origin,
                    direction,
                    distance,
                    obstacleLayers,
                    0f);
            _line.positionCount = PointCount;
            for (int index = 0; index < PointCount; index++)
            {
                float t = index / (float)(PointCount - 1);
                Vector3 point = ThrowTrajectorySolver.PositionAt(
                    solution.Origin,
                    solution.Velocity,
                    solution.TravelledTime * t);
                _line.SetPosition(index, point + Vector3.up * PreviewLift);
            }

            _landingMarker.transform.position =
                solution.Landing + Vector3.up * 0.08f;
            BindDirectionMarkers(solution);
            _line.enabled = true;
            _landingMarker.SetActive(true);
        }

        public void Hide()
        {
            if (_line != null)
            {
                _line.enabled = false;
            }

            if (_landingMarker != null)
            {
                _landingMarker.SetActive(false);
            }

            HideDirectionMarkers();
        }

        private void EnsureVisuals()
        {
            if (_line == null)
            {
                GameObject lineObject = new("Throw Trajectory");
                lineObject.transform.SetParent(transform, false);
                _line = lineObject.AddComponent<LineRenderer>();
                _line.useWorldSpace = true;
                _line.alignment = LineAlignment.View;
                _line.widthMultiplier = 0.2f;
                _line.numCapVertices = 4;
                _line.numCornerVertices = 4;
                _line.textureMode = LineTextureMode.Tile;
                _line.startColor = new Color(0.05f, 1f, 0.92f, 0.95f);
                _line.endColor = new Color(0.05f, 0.75f, 1f, 0.72f);
                _line.shadowCastingMode = ShadowCastingMode.Off;
                _line.receiveShadows = false;
                _line.sortingOrder = 200;
                _line.sharedMaterial = ResolveTrajectoryMaterial();
                _line.enabled = false;
            }

            if (_landingMarker == null)
            {
                _landingMarker = GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);
                _landingMarker.name = "Throw Landing Marker";
                _landingMarker.transform.localScale =
                    new Vector3(1.05f, 0.035f, 1.05f);
                _landingMarker.GetComponent<Collider>().enabled = false;
                Renderer renderer = _landingMarker.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = ResolveLandingMarkerMaterial();
                _landingMarker.SetActive(false);
            }

            if (_directionMarkers == null
                || _directionMarkers.Length != DirectionMarkerCount)
            {
                _directionMarkers = new GameObject[DirectionMarkerCount];
                for (int index = 0; index < _directionMarkers.Length; index++)
                {
                    _directionMarkers[index] =
                        CreateDirectionMarker(index);
                }
            }
        }

        private void BindDirectionMarkers(
            ThrowTrajectorySolver.Solution solution)
        {
            for (int index = 0; index < _directionMarkers.Length; index++)
            {
                float t = (index + 1f) / (_directionMarkers.Length + 1f);
                Vector3 point = ThrowTrajectorySolver.PositionAt(
                    solution.Origin,
                    solution.Velocity,
                    solution.TravelledTime * t);
                Vector3 next = ThrowTrajectorySolver.PositionAt(
                    solution.Origin,
                    solution.Velocity,
                    solution.TravelledTime * Mathf.Min(1f, t + 0.05f));
                Vector3 flat = next - point;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.0001f)
                {
                    flat = transform.forward;
                    flat.y = 0f;
                }

                GameObject marker = _directionMarkers[index];
                marker.transform.position = point + Vector3.up * PreviewLift;
                if (flat.sqrMagnitude > 0.0001f)
                {
                    marker.transform.rotation =
                        Quaternion.LookRotation(flat.normalized);
                }

                marker.SetActive(true);
            }
        }

        private void HideDirectionMarkers()
        {
            if (_directionMarkers == null)
            {
                return;
            }

            for (int index = 0; index < _directionMarkers.Length; index++)
            {
                if (_directionMarkers[index] != null)
                {
                    _directionMarkers[index].SetActive(false);
                }
            }
        }

        private GameObject CreateDirectionMarker(int index)
        {
            var marker = new GameObject(
                $"Throw Direction Marker {index + 1}",
                typeof(MeshFilter),
                typeof(MeshRenderer));
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            MeshFilter filter = marker.GetComponent<MeshFilter>();
            filter.sharedMesh = ResolveArrowMesh();
            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = ResolveTrajectoryMaterial();
            marker.SetActive(false);
            return marker;
        }

        private static Mesh ResolveArrowMesh()
        {
            if (_arrowMesh != null)
            {
                return _arrowMesh;
            }

            _arrowMesh = new Mesh
            {
                name = "Throw Trajectory Arrow Mesh"
            };
            _arrowMesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0.38f),
                new Vector3(-0.24f, 0f, -0.22f),
                new Vector3(0.24f, 0f, -0.22f)
            };
            _arrowMesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            _arrowMesh.RecalculateNormals();
            _arrowMesh.RecalculateBounds();
            return _arrowMesh;
        }

        private Material ResolveTrajectoryMaterial()
        {
            if (_runtimeTrajectoryMaterial != null)
            {
                return _runtimeTrajectoryMaterial;
            }

            if (trajectoryMaterial == null)
            {
                trajectoryMaterial =
                    Resources.Load<Material>(TrajectoryMaterialResource);
            }

            ReportMissingMaterialIfNeeded(
                trajectoryMaterial,
                TrajectoryMaterialResource);
            _runtimeTrajectoryMaterial = CreateAlwaysVisibleMaterial(
                trajectoryMaterial,
                new Color(0.05f, 1f, 0.92f, 0.95f),
                "Throw Trajectory Always Visible");
            return _runtimeTrajectoryMaterial;
        }

        private Material ResolveLandingMarkerMaterial()
        {
            if (_runtimeLandingMarkerMaterial != null)
            {
                return _runtimeLandingMarkerMaterial;
            }

            if (landingMarkerMaterial == null)
            {
                landingMarkerMaterial =
                    Resources.Load<Material>(LandingMaterialResource);
            }

            ReportMissingMaterialIfNeeded(
                landingMarkerMaterial,
                LandingMaterialResource);
            _runtimeLandingMarkerMaterial = CreateAlwaysVisibleMaterial(
                landingMarkerMaterial,
                new Color(0.05f, 0.9f, 1f, 0.82f),
                "Throw Landing Always Visible");
            return _runtimeLandingMarkerMaterial;
        }

        private static Material CreateAlwaysVisibleMaterial(
            Material source,
            Color fallbackColor,
            string materialName)
        {
            Shader shader = source != null
                ? source.shader
                : Shader.Find("Hidden/Internal-Colored")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");
            var material = source != null
                ? new Material(source)
                : new Material(shader);
            material.name = materialName;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", fallbackColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", fallbackColor);
            }

            if (material.HasProperty("_ZTest"))
            {
                material.SetInt("_ZTest", (int)CompareFunction.Always);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }

            material.renderQueue = 5000;
            return material;
        }

        private void ReportMissingMaterialIfNeeded(
            Material material,
            string resource)
        {
            if (material != null || _reportedMissingMaterial)
            {
                return;
            }

            _reportedMissingMaterial = true;
            Debug.LogError(
                $"ThrowTrajectoryPreview requires Resources/{resource}.mat.",
                this);
        }

        private void OnDestroy()
        {
            if (_landingMarker != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_landingMarker);
                }
                else
                {
                    DestroyImmediate(_landingMarker);
                }
            }

            if (_directionMarkers == null)
            {
                return;
            }

            for (int index = 0; index < _directionMarkers.Length; index++)
            {
                if (_directionMarkers[index] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(_directionMarkers[index]);
                }
                else
                {
                    DestroyImmediate(_directionMarkers[index]);
                }
            }

            ReleaseRuntimeMaterials();
        }

        private void ReleaseRuntimeMaterials()
        {
            DestroyMaterial(_runtimeTrajectoryMaterial);
            DestroyMaterial(_runtimeLandingMarkerMaterial);
            _runtimeTrajectoryMaterial = null;
            _runtimeLandingMarkerMaterial = null;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }
    }
}
