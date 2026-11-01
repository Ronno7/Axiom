using System;
using System.Linq;
using GeometricFlight.Combat;
using GeometricFlight.Core;
using GeometricFlight.Input;
using GeometricFlight.Player;
using GeometricFlight.Presentation;
using GeometricFlight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GeometricFlight.Editor
{
    /// <summary>Explicit, one-time prototype setup. Never runs at import and never rebuilds an existing scene.</summary>
    public static class OpeningPrototypeTools
    {
        public const string ScenePath = "Assets/Scenes/OpeningPrototype.unity";

        [MenuItem("Tools/Axiom/Open Opening Prototype")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("OpeningPrototype already exists; open and edit it, do not regenerate it.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before creating the prototype.");
            if (!AssetDatabase.CopyAsset("Assets/Scenes/MainMenu.unity", ScenePath))
                throw new InvalidOperationException("Could not copy the main menu.");
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            GameObject[] roots = scene.GetRootGameObjects();
            roots.First(r => r.name == "Backdrop").SetActive(false);
            var menu = Object.FindFirstObjectByType<MainMenu>();
            var view = roots.First(r => r.name == "Main Camera").GetComponent<Camera>();
            view.transform.SetPositionAndRotation(new Vector3(0, 7, -9), Quaternion.Euler(8, 0, 0));
            view.fieldOfView = 65; view.farClipPlane = 1600;
            var menuLook = Object.FindFirstObjectByType<MenuLook>();
            Edit(menuLook, so => {
                so.FindProperty("accent").colorValue = AxiomPalette.Orange;
                so.FindProperty("glitch.enabled").boolValue = false;
                so.FindProperty("glitch.holdPeak").boolValue = false;
                so.FindProperty("atmosphere.haze").colorValue = AxiomPalette.Void;
                so.FindProperty("atmosphere.fogRange").vector2Value = new Vector2(180, 800);
                so.FindProperty("atmosphere.horizonColor").colorValue = AxiomPalette.Panel;
                so.FindProperty("atmosphere.horizonDistance").floatValue = 900;
                so.FindProperty("atmosphere.horizonSize").vector2Value = new Vector2(2600, 100);
                so.FindProperty("atmosphere.stars").boolValue = false;
                so.FindProperty("atmosphere.dustDrift").floatValue = 0;
            });

            Scene source = EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity", OpenSceneMode.Additive);
            var original = source.GetRootGameObjects().First(r => r.GetComponent<PlayerFlight>() != null);
            var player = Object.Instantiate(original);
            player.name = "10_OpeningPlayer";
            SceneManager.MoveGameObjectToScene(player, scene);
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(scene);
            player.transform.position = new Vector3(0, 6, 14);
            var flight = player.GetComponent<PlayerFlight>();
            var combined = player.GetComponent<CombinedFlightInput>();
            Set(combined, "keyboard", player.GetComponent<KeyboardFlightInput>());
            var authority = player.AddComponent<MasterArmInput>();
            Set(authority, "source", combined);
            Edit(flight, so => {
                so.FindProperty("inputSource").objectReferenceValue = authority;
                so.FindProperty("forwardSpeed").floatValue = 16;
                so.FindProperty("acceleration").floatValue = 12;
                so.FindProperty("steeringSpeed").floatValue = 12;
                so.FindProperty("horizontalLimits").vector2Value = new Vector2(-12, 12);
                so.FindProperty("verticalLimits").vector2Value = new Vector2(-3, 6);
            });
            player.GetComponent<ShipHull>().enabled = false; // No damaging hazards in the opening experiment.
            var gun = player.GetComponent<PlayerWeapon>();
            Edit(gun, so => {
                so.FindProperty("aimPoint").objectReferenceValue = player.transform.Find("AimReticle");
                var muzzles = so.FindProperty("muzzles"); muzzles.arraySize = 2;
                muzzles.GetArrayElementAtIndex(0).objectReferenceValue = player.transform.Find("MuzzleLeft");
                muzzles.GetArrayElementAtIndex(1).objectReferenceValue = player.transform.Find("MuzzleRight");
                so.FindProperty("projectilePrefab").objectReferenceValue = Load<Projectile>("Prefabs/Player/PlayerLaser.prefab");
                so.FindProperty("projectileSpeed").floatValue = 140;
                so.FindProperty("shotInterval").floatValue = 1f / 14;
                so.FindProperty("aimAssistCone").floatValue = 7;
                so.FindProperty("aimAssistRange").floatValue = 80;
            });
            var missiles = player.AddComponent<LockOnWeapon>();
            Set(missiles, "missilePrefab", Load<HomingMissile>("Prefabs/Player/PlayerMissile.prefab"));
            player.AddComponent<EmpWeapon>();
            Set(player.AddComponent<EmpDisplay>(), "pulseMaterial", Load<Material>("Art/Materials/LightLineCyan.mat"));

            Material cyan = Load<Material>("Art/Materials/LightLineCyan.mat");
            Material glass = Load<Material>("Art/Materials/GridGlass.mat");
            var world = new GameObject("20_AdmissionCorridor").transform;
            var floorMaterial = Load<Material>("Art/Materials/OpeningFloor.mat");
            if (floorMaterial == null)
            {
                floorMaterial = new Material(Load<Material>("Art/Materials/GridFloor.mat"));
                floorMaterial.SetVector("_LineColor", AxiomPalette.MaterialColor(floorMaterial, "_LineColor", AxiomPalette.Cyan, 0.8f));
                floorMaterial.SetFloat("_MinorStrength", 0.2f);
                floorMaterial.SetFloat("_MajorWidth", 1.3f); floorMaterial.SetFloat("_MinorWidth", 0.8f);
                floorMaterial.SetVector("_Fade", new Vector4(80,550,0,0));
                AssetDatabase.CreateAsset(floorMaterial, "Assets/Art/Materials/OpeningFloor.mat");
            }
            Cube("Floor", world, new Vector3(0,-0.5f,4000), new Vector3(1600,1,10000), floorMaterial, true);
            for (int side = -1; side <= 1; side += 2)
                Cube("Route A-01", world, new Vector3(side * 13,0.08f,4000), new Vector3(0.12f,0.08f,10000), cyan);
            var towers = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 40; i++)
            {
                float height = 24 + (i * 17 % 70);
                var tower = Cube("Distant structure " + i, world,
                    new Vector3((i % 2 == 0 ? -1 : 1) * (38 + i % 5 * 19), height / 2, 40 + i * 27),
                    new Vector3(9 + i % 3 * 4, height, 12), glass);
                Set(tower.AddComponent<LightEdges>(), "material", cyan);
                towers.Add(tower.transform);
            }
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 0.5f;
            light.transform.rotation = Quaternion.Euler(40,-30,0);

            Transform[] gates = new Transform[8];
            for (int i = 0; i < gates.Length; i++)
            {
                var go = new GameObject("Scored gate " + (i + 1));
                go.transform.SetParent(world);
                var shape = go.AddComponent<PrototypeMesh>();
                Edit(shape, so => { so.FindProperty("shape").enumValueIndex = 2; so.FindProperty("radius").floatValue = 3.2f; });
                shape.Rebuild();
                go.GetComponent<MeshRenderer>().sharedMaterial = cyan;
                go.SetActive(false); gates[i] = go.transform;
            }
            var template = new GameObject("PracticeTargetTemplate");
            template.SetActive(false);
            var targetMesh = template.AddComponent<PrototypeMesh>();
            Edit(targetMesh, so => so.FindProperty("shape").enumValueIndex = 1);
            targetMesh.Rebuild();
            template.transform.localScale = Vector3.one * 2.5f;
            template.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>("Art/Materials/ProgramOrange.mat");
            template.AddComponent<SphereCollider>().radius = 0.8f;
            template.AddComponent<ShootableTarget>();

            var flow = new GameObject("50_OpeningFlow").AddComponent<OpeningFlow>();
            Set(flow, "menu", menu); Set(flow, "flight", flight); Set(flow, "authority", authority);
            Set(flow, "targetTemplate", template); SetArray(flow, "gates", gates);
            var presentation = flow.gameObject.AddComponent<OpeningPresentation>();
            Set(presentation, "flow", flow); Set(presentation, "view", view);
            Transform aircraft = player.transform.Find("AircraftVisual");
            Set(presentation, "aircraft", aircraft); SetArray(presentation, "towers", towers.ToArray());
            var hull = aircraft.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.GetComponent<ThrustDisplay>() == null && !r.name.StartsWith("Segment_")).ToArray();
            SetArray(presentation, "hullRenderers", hull);
            string hullPath = "Assets/Art/Materials/OpeningHull.mat";
            Material hullMaterial = AssetDatabase.LoadAssetAtPath<Material>(hullPath);
            if (hullMaterial == null)
            {
                hullMaterial = new Material(Shader.Find("GeometricFlight/Grid Construction"));
                hullMaterial.SetVector("_BaseColor", AxiomPalette.MaterialColor(hullMaterial, "_BaseColor", AxiomPalette.Edge));
                AssetDatabase.CreateAsset(hullMaterial, hullPath);
            }
            foreach (var renderer in hull) renderer.sharedMaterial = hullMaterial;
            // Preserve shield functionality, but use the prototype's construction material during the reveal.
            player.GetComponent<ShieldDisplay>().enabled = false;
            var outline = new GameObject("Ship construction edges").AddComponent<LineRenderer>();
            outline.transform.SetParent(aircraft, false); outline.useWorldSpace = false;
            outline.sharedMaterial = cyan; outline.widthMultiplier = 0.045f;
            Set(presentation, "outline", outline);
            SetArray(presentation, "reticles", new[]{player.transform.Find("NearReticle").GetComponent<Renderer>(),
                player.transform.Find("AimReticle").GetComponent<Renderer>()});
            var ui = menu.gameObject.AddComponent<OpeningView>();
            Set(ui, "flow", flow); Set(ui, "presentation", presentation);
            Set(ui, "layout", Load<VisualTreeAsset>("UI/Opening.uxml"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = flow.gameObject;
        }

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>("Assets/" + path);
        private static void Edit(Object target, Action<SerializedObject> change)
        {
            var so = new SerializedObject(target); change(so); so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string name, Object value) => Edit(target, so => so.FindProperty(name).objectReferenceValue = value);
        private static void SetArray<T>(Object target, string name, T[] values) where T : Object => Edit(target, so => {
            var array = so.FindProperty(name); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        });
        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent);
            go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
