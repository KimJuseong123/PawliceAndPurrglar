using UnityEngine;

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

        private LineRenderer _line;
        private GameObject _landingMarker;

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
                _line.SetPosition(index, point);
            }

            _landingMarker.transform.position =
                solution.Landing + Vector3.up * 0.04f;
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
        }

        private void EnsureVisuals()
        {
            if (_line == null)
            {
                GameObject lineObject = new("Throw Trajectory");
                lineObject.transform.SetParent(transform, false);
                _line = lineObject.AddComponent<LineRenderer>();
                _line.useWorldSpace = true;
                _line.widthMultiplier = 0.045f;
                _line.material = CreateMaterial(new Color(1f, 0.78f, 0.2f, 0.8f));
                _line.enabled = false;
            }

            if (_landingMarker == null)
            {
                _landingMarker = GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);
                _landingMarker.name = "Throw Landing Marker";
                _landingMarker.transform.localScale =
                    new Vector3(0.5f, 0.02f, 0.5f);
                _landingMarker.GetComponent<Collider>().enabled = false;
                Renderer renderer = _landingMarker.GetComponent<Renderer>();
                renderer.material = CreateMaterial(
                    new Color(1f, 0.35f, 0.15f, 0.65f));
                _landingMarker.SetActive(false);
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                color = color
            };
            return material;
        }

        private void OnDestroy()
        {
            if (_landingMarker != null)
            {
                Destroy(_landingMarker);
            }
        }
    }
}
