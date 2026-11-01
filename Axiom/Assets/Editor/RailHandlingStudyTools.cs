using System;
using System.Collections.Generic;
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
    /// <summary>Explicit one-time study setup from the authored scenes. Never rebuilds an existing scene.</summary>
    public static class RailHandlingStudyTools
    {
        public const string ScenePath = "Assets/Scenes/RailHandlingStudy.unity";
        private const string PrototypePath = "Assets/Scenes/Prototype.unity";
        private const string ArenaPath = "Assets/Scenes/Arena.unity";

        [MenuItem("Tools/Axiom/Open Rail Handling Study")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating the study.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("RailHandlingStudy already exists. Open and edit it; do not regenerate it.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene edits before creating the study.");
            if (!AssetDatabase.CopyAsset(PrototypePath, ScenePath))
                throw new InvalidOperationException("Could not copy the original Prototype scene.");

            Scene studyScene = EditorSceneManager.OpenScene(ScenePath);
            GameObject oldPlayer = Root(studyScene, "10_PlayerPrototype");
            GameObject environment = Root(studyScene, "20_EnvironmentBlockout");
            LevelFlow flow = ComponentIn<LevelFlow>(studyScene);
            LevelUi ui = ComponentIn<LevelUi>(studyScene);
            LevelLook look = ComponentIn<LevelLook>(studyScene);
            Camera camera = ComponentIn<Camera>(studyScene);
            ClosingIris iris = ComponentIn<ClosingIris>(studyScene);
            GameObject player, arenaObject;

            // Clone only the actor and environment. ArenaSession, encounter and second UI/camera stay
            // in their source scene, avoiding duplicate lifecycle, presentation and device writers.
            Scene arenaSource = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(studyScene);
                player = Object.Instantiate(Root(arenaSource, "10_Player"));
                arenaObject = Object.Instantiate(Root(arenaSource, "20_Arena"));
                SceneManager.MoveGameObjectToScene(player, studyScene);
                SceneManager.MoveGameObjectToScene(arenaObject, studyScene);
                player.name = "10_StudyPlayer";
                arenaObject.name = "21_StudyArena";

                ChaseCamera sourceCamera = ComponentIn<ChaseCamera>(arenaSource);
                var follow = camera.GetComponent<ForwardFollowCamera>();
                if (follow != null) Object.DestroyImmediate(follow);
                ChaseCamera chase = camera.gameObject.AddComponent<ChaseCamera>();
                EditorUtility.CopySerialized(sourceCamera, chase);
                Set(chase, "target", player.GetComponent<ArenaFlight>());
            }
            finally
            {
                // Never save the source scene; generated preview objects are not source edits.
                EditorSceneManager.CloseScene(arenaSource, true);
                SceneManager.SetActiveScene(studyScene);
            }

            player.transform.SetPositionAndRotation(new Vector3(0f, 90f, 0f), Quaternion.identity);
            arenaObject.transform.position = new Vector3(0f, 0f, 1500f);
            ArenaFlight flight = player.GetComponent<ArenaFlight>();
            ArenaBounds bounds = arenaObject.GetComponent<ArenaBounds>();
            Set(flight, "arena", bounds);
            Set(bounds, "focus", player.transform);
            Set(bounds, "wallMaterial", null); // A transparent cylinder must not visually seal the entry.
            Edit(bounds, so => so.FindProperty("posts").intValue = 35);
            foreach (Transform part in arenaObject.GetComponentsInChildren<Transform>(true))
                if (part.name.StartsWith("Column_") && part.position.z < 850f && Mathf.Abs(part.position.x) < 200f)
                    part.gameObject.SetActive(false); // Reused arena's front columns must leave the aperture open.
            Transform arenaFloor = arenaObject.transform.Find("GridFloor");
            if (arenaFloor != null)
            {
                // The broad arena floor overlaps the last rail metres; separate coplanar surfaces.
                Vector3 floorPosition = arenaFloor.localPosition;
                floorPosition.y -= 0.1f;
                arenaFloor.localPosition = floorPosition;
            }
            Transform recognizer = arenaObject.transform.Find("Recognizer");
            if (recognizer != null) recognizer.gameObject.SetActive(false);

            RemapPlayerReferences(studyScene, oldPlayer, player);
            Set(flow, "flight", flight);
            Set(flow, "weapon", player.GetComponent<PlayerWeapon>());
            Set(flow, "shield", player.GetComponent<ShipShield>());
            Set(flow, "hull", player.GetComponent<ShipHull>());
            Set(flow, "shipDestruction", player.GetComponent<ShatterEffect>());
            Set(ui, "flight", flight);
            Set(iris, "ship", flight);
            Set(iris, "shield", player.GetComponent<ShipShield>());
            Set(iris, "shipHull", player.GetComponent<BoxCollider>());
            Object.DestroyImmediate(oldPlayer);

            CombinedFlightInput combined = player.GetComponent<CombinedFlightInput>();
            Set(combined, "keyboard", player.GetComponent<KeyboardFlightInput>());
            MasterArmInput authority = player.AddComponent<MasterArmInput>();
            Set(authority, "source", combined);
            Set(flight, "inputSource", authority);

            Transform[] hoops = ConfigureCourse(environment.transform, iris, flow);
            ConfigureTargets(Root(studyScene, "30_TargetPlaceholders").transform, player.transform);
            RailHandlingStudy study = player.AddComponent<RailHandlingStudy>();
            Edit(study, so =>
            {
                so.FindProperty("flow").objectReferenceValue = flow;
                so.FindProperty("iris").objectReferenceValue = iris;
                so.FindProperty("releaseZ").floatValue = 900f;
                so.FindProperty("headingLimit").floatValue = 45f;
                so.FindProperty("envelopeX").vector2Value = new Vector2(-110f, 110f);
                so.FindProperty("envelopeY").vector2Value = new Vector2(10f, 180f);
                so.FindProperty("hoopRadius").floatValue = 12f;
                SerializedProperty array = so.FindProperty("hoops");
                array.arraySize = hoops.Length;
                for (int i = 0; i < hoops.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = hoops[i];
            });
            Set(ui.gameObject.AddComponent<RailStudyDisplay>(), "study", study);
            MissileDisplay missileDisplay = ui.gameObject.AddComponent<MissileDisplay>();
            Set(missileDisplay, "weapon", player.GetComponent<LockOnWeapon>());
            Edit(missileDisplay, so =>
            {
                so.FindProperty("writeCockpit").boolValue = false;
                so.FindProperty("bottomMargin").floatValue = 92f;
            });

            camera.fieldOfView = 62f;
            camera.farClipPlane = 2200f;
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 2f, -7.5f),
                Quaternion.Euler(4f, 0f, 0f));
            Edit(look, so =>
            {
                so.FindProperty("levelCamera").objectReferenceValue = camera;
                so.FindProperty("atmosphere.fogRange").vector2Value = new Vector2(180f, 1100f);
                so.FindProperty("atmosphere.stars").boolValue = false;
                so.FindProperty("atmosphere.dustDrift").floatValue = 0f;
                so.FindProperty("previewArcade").boolValue = false;
            });
            EditorSceneManager.MarkSceneDirty(studyScene);
            EditorSceneManager.SaveScene(studyScene);
            Selection.activeGameObject = player;
            Debug.Log("Rail handling study created from the original Prototype and Arena. Source scenes unchanged; study is not in build settings.");
        }

        private static Transform[] ConfigureCourse(Transform environment, ClosingIris iris, LevelFlow flow)
        {
            Transform floor = Required(environment, "Floor");
            Transform lattice = Required(environment, "Lattice");
            Transform slow = Required(environment, "ThrottleZone_Slow");
            Transform fast = Required(environment, "ThrottleZone_Fast");
            Transform end = Required(environment, "CourseEnd");
            Transform gateTemplate = Required(environment, "HexagonalGate_1");
            Transform edgeTemplate = Required(environment, "EdgeStrip_-1");
            foreach (Transform child in environment) child.gameObject.SetActive(false);
            foreach (Transform keep in new[] { floor, lattice, slow, fast, end, iris.transform })
                keep.gameObject.SetActive(true);

            floor.SetPositionAndRotation(new Vector3(0f, -0.2f, 450f), Quaternion.identity);
            floor.localScale = new Vector3(220f, 0.4f, 900f);
            floor.GetComponent<Renderer>().sharedMaterial = LoadMaterial("GridFloor");
            end.position = new Vector3(0f, 90f, 100000f);
            Set(flow, "courseEnd", end);
            Edit(flow, so => so.FindProperty("parTime").floatValue = 0f);

            Transform wallA = Required(lattice, "LatticeWall_1_GapRight");
            Transform wallB = Required(lattice, "LatticeWall_2_GapLeft");
            foreach (Transform child in lattice) child.gameObject.SetActive(child == wallA || child == wallB);
            wallA.position = new Vector3(-30f, 90f, 360f);
            wallB.position = new Vector3(30f, 90f, 560f);
            wallA.localScale = wallB.localScale = new Vector3(8f, 10f, 1f);
            ConfigureZone(slow, 280f, 320f, new Vector2(0.6f, 0.85f), 6f);
            ConfigureZone(fast, 620f, 180f, new Vector2(1.25f, 1.5f), 3f);
            iris.transform.SetPositionAndRotation(new Vector3(0f, 90f, 780f), Quaternion.identity);
            iris.transform.localScale = Vector3.one;
            Edit(iris, so =>
            {
                so.FindProperty("openApothem").floatValue = 55f;
                so.FindProperty("outerApothem").floatValue = 90f;
                so.FindProperty("triggerDistance").floatValue = 160f;
                so.FindProperty("closeDuration").floatValue = 7.5f;
            });

            var hoops = new Transform[4];
            float[] z = { 240f, 440f, 640f, 850f };
            float[] x = { 0f, 20f, -20f, 0f };
            for (int i = 0; i < hoops.Length; i++)
            {
                GameObject gate = Object.Instantiate(gateTemplate.gameObject, environment);
                gate.name = "Study score hoop " + (i + 1);
                gate.transform.SetPositionAndRotation(new Vector3(x[i], 90f, z[i]), Quaternion.identity);
                gate.transform.localScale = Vector3.one;
                foreach (Collider collider in gate.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                PrototypeMesh shape = gate.GetComponent<PrototypeMesh>();
                if (shape == null) throw new InvalidOperationException("Expected the original gate's PrototypeMesh.");
                Edit(shape, so =>
                {
                    so.FindProperty("shape").enumValueIndex = (int)PrototypeMesh.Shape.PolygonRing;
                    so.FindProperty("radius").floatValue = 12f;
                    so.FindProperty("width").floatValue = 0.3f;
                    so.FindProperty("depth").floatValue = 0.3f;
                });
                shape.Rebuild();
                gate.GetComponent<Renderer>().sharedMaterial = LoadMaterial("LightLineCyan");
                gate.SetActive(true);
                hoops[i] = gate.transform;
            }
            // Reuse the existing straight edge-strip geometry as visible envelope cues, not containment.
            foreach (float side in new[] { -110f, 110f })
                foreach (float height in new[] { 10f, 180f })
                {
                    GameObject edge = Object.Instantiate(edgeTemplate.gameObject, environment);
                    edge.name = $"Study edge {side} {height}";
                    edge.transform.SetPositionAndRotation(new Vector3(side, height, 450f), Quaternion.identity);
                    edge.transform.localScale = new Vector3(0.35f, 0.35f, 900f);
                    foreach (Collider collider in edge.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                    edge.GetComponent<Renderer>().sharedMaterial = LoadMaterial("LightLineCyan");
                    edge.SetActive(true);
                }
            return hoops;
        }

        private static void ConfigureZone(Transform zone, float start, float length, Vector2 band, float cueStretch)
        {
            zone.position = new Vector3(0f, 0.1f, start);
            zone.localScale = new Vector3(8f, 1f, cueStretch);
            Edit(zone.GetComponent<ThrottleZone>(), so =>
            {
                so.FindProperty("length").floatValue = length;
                so.FindProperty("recommendedCruiseMultiple").vector2Value = band;
            });
        }

        private static void ConfigureTargets(Transform targets, Transform player)
        {
            Vector3[] positions = { new Vector3(0f, 90f, 1100f), new Vector3(-120f, 110f, 1250f),
                new Vector3(140f, 130f, 1350f) };
            ShootableTarget[] existing = targets.GetComponentsInChildren<ShootableTarget>(true);
            for (int i = 0; i < existing.Length; i++)
            {
                existing[i].gameObject.SetActive(true);
                existing[i].transform.position = positions[Mathf.Min(i, positions.Length - 1)];
                EnemyTurret turret = existing[i].GetComponent<EnemyTurret>();
                if (turret == null) continue;
                Set(turret, "player", player);
                Edit(turret, so =>
                {
                    so.FindProperty("measureDistance").boolValue = true;
                    so.FindProperty("engagementRange").vector2Value = new Vector2(20f, 200f);
                });
            }
        }

        private static void RemapPlayerReferences(Scene scene, GameObject oldPlayer, GameObject newPlayer)
        {
            var replacements = new Dictionary<Object, Object>();
            foreach (Transform oldTransform in oldPlayer.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(oldTransform, oldPlayer.transform);
                Transform replacement = path.Length == 0 ? newPlayer.transform : newPlayer.transform.Find(path);
                if (replacement == null) continue;
                replacements[oldTransform] = replacement;
                replacements[oldTransform.gameObject] = replacement.gameObject;
                foreach (Component oldComponent in oldTransform.GetComponents<Component>())
                {
                    if (oldComponent == null) continue;
                    Component next = oldComponent is ShipFlight ? replacement.GetComponent<ShipFlight>()
                        : replacement.GetComponent(oldComponent.GetType());
                    if (next != null) replacements[oldComponent] = next;
                }
            }
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null || component.transform.IsChildOf(oldPlayer.transform)) continue;
                    var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    bool changed = false;
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object original = property.objectReferenceValue;
                        if (original == null || !replacements.TryGetValue(original, out Object replacement)) continue;
                        property.objectReferenceValue = replacement;
                        changed = true;
                    }
                    if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
                }
        }

        private static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(r => r.name == name)
            ?? throw new InvalidOperationException($"Missing source root {name} in {scene.path}.");
        private static T ComponentIn<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault()
            ?? throw new InvalidOperationException($"Missing {typeof(T).Name} in {scene.path}.");
        private static Transform Required(Transform parent, string path) => parent.Find(path)
            ?? throw new InvalidOperationException($"Missing source object {parent.name}/{path}.");
        private static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/{name}.mat")
            ?? throw new InvalidOperationException($"Missing material {name}.");
        private static void Edit(Object target, Action<SerializedObject> change)
        {
            if (target == null) throw new InvalidOperationException("Missing study setup target.");
            var serialized = new SerializedObject(target);
            change(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string name, Object value) =>
            Edit(target, so => so.FindProperty(name).objectReferenceValue = value);
    }
}
