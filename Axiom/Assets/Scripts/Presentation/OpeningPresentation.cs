using GeometricFlight.Core;
using UnityEngine;

namespace GeometricFlight.Presentation
{
    /// <summary>Same-scene camera handoff and construction. All scenery is non-colliding and outside
    /// the flight corridor; this component never changes input, progression or damage rules.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class OpeningPresentation : MonoBehaviour
    {
        [SerializeField] private OpeningFlow flow;
        [SerializeField] private Camera view;
        [SerializeField] private Transform aircraft;
        [SerializeField] private Renderer[] hullRenderers;
        [SerializeField] private Transform[] towers;
        [SerializeField] private LineRenderer outline;
        [SerializeField] private Renderer[] reticles;
        private Vector3 cameraStart;
        private Quaternion rotationStart;
        private Vector3[] towerScales;
        private Vector3[] towerPositions;
        private Renderer shadow;
        private LineRenderer verification;
        private bool authorityAccepted;
        private float verificationStart = -10f;
        private MaterialPropertyBlock block;
        private static readonly int Front = Shader.PropertyToID("_BuildFront");
        private static readonly Vector3[] ShipEdges = {
            new Vector3(0,0,2.5f), new Vector3(-2,0,-1.4f), new Vector3(0,0.5f,-0.5f),
            new Vector3(2,0,-1.4f), new Vector3(0,0,2.5f), new Vector3(0,0.5f,-0.5f),
            new Vector3(-2,0,-1.4f), new Vector3(2,0,-1.4f) };
        public Camera View => view;
        public Vector3 RoutePoint(float z) => new Vector3(-13, 0.08f, flow.Flight.transform.position.z + z);

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            cameraStart = view.transform.position;
            rotationStart = view.transform.rotation;
            towerScales = new Vector3[towers.Length];
            towerPositions = new Vector3[towers.Length];
            shadow = flow.Flight.transform.Find("GroundShadow").GetComponent<Renderer>();
            verification = new GameObject("Weapons verification sweep").AddComponent<LineRenderer>();
            verification.transform.SetParent(aircraft, false);
            verification.sharedMaterial = outline.sharedMaterial;
            verification.useWorldSpace = false;
            verification.widthMultiplier = 0.025f;
            verification.positionCount = 2;
            for (int i = 0; i < towers.Length; i++)
            {
                towerScales[i] = towers[i].localScale;
                towerPositions[i] = towers[i].position;
            }
            flow.StageChanged += ResetScenery;
        }

        private void OnDestroy() { if (flow != null) flow.StageChanged -= ResetScenery; }

        private void ResetScenery()
        {
            if (flow.Stage != OpeningStage.Menu && flow.Stage != OpeningStage.Arrival) return;
            for (int i = 0; i < towers.Length; i++) towers[i].position = towerPositions[i];
        }

        private void LateUpdate()
        {
            bool menu = flow.Stage == OpeningStage.Menu;
            bool arriving = flow.Stage == OpeningStage.Arrival;
            float construction = arriving ? Mathf.Clamp01(flow.ArrivalTime / Mathf.Min(2.4f, flow.ArrivalDuration)) : menu ? 0 : 1;
            if (flow.ReducedAnimation) construction = menu ? 0 : 1;
            aircraft.gameObject.SetActive(!menu);
            Vector3 player = flow.Flight.transform.position;
            Vector3 cameraTarget = new Vector3(0, 10, player.z - 16);
            Quaternion rotationTarget = Quaternion.Euler(12, 0, 0);
            float blend = Mathf.SmoothStep(0, 1, construction);
            view.transform.SetPositionAndRotation(menu ? cameraStart : Vector3.Lerp(cameraStart, cameraTarget, blend),
                menu ? rotationStart : Quaternion.Slerp(rotationStart, rotationTarget, blend));
            float surface = Mathf.Clamp01((construction - 0.35f) / 0.65f);
            block.SetFloat(Front, player.z + Mathf.Lerp(3.5f, -4f, surface));
            foreach (var renderer in hullRenderers) renderer.SetPropertyBlock(block);

            shadow.enabled = !menu && construction > 0.7f;
            outline.gameObject.SetActive(!menu);
            outline.widthMultiplier = construction < 1 ? 0.035f : 0.018f;
            outline.positionCount = ShipEdges.Length;
            float lineProgress = Mathf.Clamp01(construction / 0.55f) * (ShipEdges.Length - 1);
            for (int i = 0; i < ShipEdges.Length; i++)
            {
                float at = Mathf.Min(i, lineProgress);
                int start = Mathf.Min(Mathf.FloorToInt(at), ShipEdges.Length - 2);
                outline.SetPosition(i, Vector3.Lerp(ShipEdges[start], ShipEdges[start + 1], at - start));
            }
            foreach (var reticle in reticles)
                reticle.enabled = !menu && !arriving && flow.Authority.WeaponsAvailable;
            bool accepted = !menu && !arriving && flow.Authority.WeaponsAvailable && flow.Authority.Armed;
            if (accepted && !authorityAccepted) verificationStart = Time.time;
            authorityAccepted = accepted;
            float scan = (Time.time - verificationStart) / 0.65f;
            verification.enabled = accepted && scan < 1 && !flow.ReducedAnimation;
            float scanZ = Mathf.Lerp(2.6f, -1.8f, scan);
            verification.SetPosition(0, new Vector3(-2.3f, 0.5f, scanZ));
            verification.SetPosition(1, new Vector3(2.3f, 0.5f, scanZ));

            for (int i = 0; i < towers.Length; i++)
            {
                Transform edges = towers[i].Find("LightEdges");
                if (edges != null) edges.localScale = Vector3.one * 1.003f;
                // The near menu architecture remains continuous. Only the distant extension assembles.
                float scale = i < 8 || flow.ReducedAnimation ? 1 : menu ? 0.03f : !arriving ? 1 :
                    Mathf.SmoothStep(0.03f, 1, Mathf.Clamp01(construction * 1.6f - (i % 8) * 0.07f));
                towers[i].localScale = new Vector3(towerScales[i].x, towerScales[i].y * scale, towerScales[i].z);
                // Decoration wraps far behind the player. It never changes a collision or score object.
                if (!menu && towers[i].position.z < player.z - 70)
                    towers[i].position += Vector3.forward * 1100;
            }
        }
    }
}
