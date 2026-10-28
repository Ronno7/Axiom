using UnityEngine;

namespace GeometricFlight.Combat
{
    /// <summary>
    /// Flies a 3D Lissajous figure around its starting point (Bowditch 1815, Lissajous 1857):
    /// x = A·sin(a·ωt + δ), y = B·sin(b·ωt), z = C·sin(c·ωt). Whole-number ratios close the loop, so the path
    /// repeats and can be drawn in advance: the player reads where the drone will be. Stops when its
    /// ShootableTarget breaks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LissajousPatrol : MonoBehaviour
    {
        [SerializeField] private Vector3 amplitude = new Vector3(30f, 10f, 30f);
        [Tooltip("Whole-number frequency ratios a, b, c. The ratio sets the number of lobes.")]
        [SerializeField] private Vector3Int ratio = new Vector3Int(3, 2, 1);
        [Tooltip("Phase offset δ on x, degrees.")]
        [SerializeField] private float phase = 90f;
        [Tooltip("Seconds per full loop.")]
        [SerializeField, Min(1f)] private float period = 16f;
        [Tooltip("Draws the whole path as a faint line. Leave empty for no path.")]
        [SerializeField] private Material pathMaterial;
        [SerializeField, Min(0f)] private float pathWidth = 0.12f;

        private Vector3 anchor;
        private float time;
        private ShootableTarget target;
        private LineRenderer path;

        public static Vector3 Point(float t01, Vector3 amplitude, Vector3Int ratio, float phaseDegrees)
        {
            float w = t01 * Mathf.PI * 2f;
            return new Vector3(
                amplitude.x * Mathf.Sin(ratio.x * w + phaseDegrees * Mathf.Deg2Rad),
                amplitude.y * Mathf.Sin(ratio.y * w),
                amplitude.z * Mathf.Sin(ratio.z * w));
        }

        private void Awake()
        {
            anchor = transform.position;
            target = GetComponent<ShootableTarget>();
            if (target != null) target.Destroyed += OnDestroyed;
            if (pathMaterial != null) DrawPath();
        }

        private void OnDestroy()
        {
            if (target != null) target.Destroyed -= OnDestroyed;
            if (path != null) Destroy(path.gameObject);
        }

        private void OnDestroyed(ShootableTarget _)
        {
            enabled = false;
            if (path != null) path.gameObject.SetActive(false);
        }

        private void Update()
        {
            time = Mathf.Repeat(time + Time.deltaTime / period, 1f);
            transform.position = anchor + Point(time, amplitude, ratio, phase);
        }

        private void DrawPath()
        {
            const int points = 240;
            var go = new GameObject(name + " Path");
            path = go.AddComponent<LineRenderer>();
            path.sharedMaterial = pathMaterial;
            path.loop = true;
            path.widthMultiplier = pathWidth;
            path.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            path.positionCount = points;
            for (int i = 0; i < points; i++)
                path.SetPosition(i, anchor + Point(i / (float)points, amplitude, ratio, phase));
        }
    }
}
