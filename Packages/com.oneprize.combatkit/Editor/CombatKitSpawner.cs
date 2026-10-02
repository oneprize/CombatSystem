using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using CombatSystem.Core;
using CombatSystem.Player;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// Places the package prefabs into the open scene: Tools > Combat Kit > Spawner (window)
    /// or Hierarchy right-click > Combat Kit. Always creates prefab instances (never unpacked
    /// copies) so later prefab edits keep propagating, and every action is undoable.
    /// Creating a player also makes sure a follow camera exists and tracks it.
    /// </summary>
    public sealed class CombatKitSpawner : EditorWindow
    {
        private const string PrefabFolder = "Packages/com.oneprize.combatkit/Content/Prefabs/";
        private const string CameraPrefab = "Camera";
        private const string CameraPointName = "CameraPoint";
        private const string PrefsKey = "CombatKit.Spawner.";
        private const float Clearance = 0.3f;   // extra gap between character capsules
        private const int MaxRings = 8;

        public enum Formation { Arc, Circle }

        private int count = 1;
        private float distance = 6f;
        private float spacing = 2.5f;
        private Formation formation = Formation.Arc;

        [MenuItem("Tools/Combat Kit/Spawner")]
        private static void Open() => GetWindow<CombatKitSpawner>("Combat Kit");

        // Hierarchy right-click. With several objects selected Unity calls the item once per
        // object, so only the call for the active selection does the work.
        [MenuItem("GameObject/Combat Kit/Create Player", false, 10)]
        private static void MenuPlayer(MenuCommand c) { if (FirstCall(c)) CreatePlayer(); }
        [MenuItem("GameObject/Combat Kit/Create Camera", false, 11)]
        private static void MenuCamera(MenuCommand c) { if (FirstCall(c)) CreateCamera(); }
        [MenuItem("GameObject/Combat Kit/Spawn Enemy", false, 22)]
        private static void MenuEnemy(MenuCommand c) { if (FirstCall(c)) Spawn("Enemy", 1, Settings.Distance, Settings.Spacing, Settings.Shape); }
        [MenuItem("GameObject/Combat Kit/Spawn Boss", false, 23)]
        private static void MenuBoss(MenuCommand c) { if (FirstCall(c)) Spawn("Boss", 1, Settings.Distance, Settings.Spacing, Settings.Shape); }
        [MenuItem("GameObject/Combat Kit/Spawn Practice Dummy", false, 24)]
        private static void MenuDummy(MenuCommand c) { if (FirstCall(c)) Spawn("PracticeDummy", 1, Settings.Distance, Settings.Spacing, Settings.Shape); }

        private static bool FirstCall(MenuCommand c) => c.context == null || c.context == Selection.activeObject;

        private void OnEnable()
        {
            count = EditorPrefs.GetInt(PrefsKey + "Count", 1);
            distance = EditorPrefs.GetFloat(PrefsKey + "Distance", 6f);
            spacing = EditorPrefs.GetFloat(PrefsKey + "Spacing", 2.5f);
            formation = (Formation)EditorPrefs.GetInt(PrefsKey + "Formation", 0);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
            if (GUILayout.Button("Create Player", GUILayout.Height(28))) CreatePlayer();
            if (GUILayout.Button("Create Camera")) CreateCamera();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Opponents", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            count = EditorGUILayout.IntSlider("Enemy Count", count, 1, 10);
            distance = Mathf.Max(1f, EditorGUILayout.FloatField("Distance From Player", distance));
            spacing = Mathf.Max(0.5f, EditorGUILayout.FloatField("Spacing", spacing));
            formation = (Formation)EditorGUILayout.EnumPopup("Formation", formation);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt(PrefsKey + "Count", count);
                EditorPrefs.SetFloat(PrefsKey + "Distance", distance);
                EditorPrefs.SetFloat(PrefsKey + "Spacing", spacing);
                EditorPrefs.SetInt(PrefsKey + "Formation", (int)formation);
            }
            if (GUILayout.Button($"Spawn Enemy x{count}", GUILayout.Height(24))) Spawn("Enemy", count, distance, spacing, formation);
            if (GUILayout.Button("Spawn Boss")) Spawn("Boss", 1, distance, spacing, formation);
            if (GUILayout.Button("Spawn Practice Dummy")) Spawn("PracticeDummy", 1, distance, spacing, formation);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Opponents are placed in front of the player, facing it, skipping "
                + "spots already taken. Without a player they go to the Scene view center. An enemy's "
                + "spawn point becomes its home/patrol center.", MessageType.None);
        }

        /// <summary>Window values, also used by the Hierarchy menu items.</summary>
        private static class Settings
        {
            public static float Distance => EditorPrefs.GetFloat(PrefsKey + "Distance", 6f);
            public static float Spacing => EditorPrefs.GetFloat(PrefsKey + "Spacing", 2.5f);
            public static Formation Shape => (Formation)EditorPrefs.GetInt(PrefsKey + "Formation", 0);
        }

        // ------------------------------------------------------------------ player / camera

        public static GameObject CreatePlayer()
        {
            var existing = FindPlayers();
            if (existing.Length > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex("Combat Kit",
                    "The scene already has a Player. Two players confuse the camera and enemy targeting.",
                    "Select Existing", "Cancel", "Create Another");
                if (choice == 0) { Selection.activeGameObject = existing[0].gameObject; EditorGUIUtility.PingObject(existing[0]); return existing[0].gameObject; }
                if (choice == 1) return null;
            }

            Vector3 center = SceneCenter();
            Vector3 position = GroundPoint(center, center.y);
            var player = Place("Player", position, Quaternion.LookRotation(SceneForward()));
            if (player == null) return null;

            if (ActiveCinemachineCameras().Length == 0) PlaceCamera(player.transform);
            else WireCameras(player.transform);
            Finish(player);
            return player;
        }

        public static GameObject CreateCamera()
        {
            if (ActiveCinemachineCameras().Length > 0 && !EditorUtility.DisplayDialog("Combat Kit",
                    "The scene already has an active Cinemachine camera. Add another one anyway?", "Add", "Cancel"))
                return null;
            var player = FindPlayers().FirstOrDefault();
            var cam = PlaceCamera(player != null ? player.transform : null);
            if (cam != null) Finish(cam);
            return cam;
        }

        /// <summary>
        /// Adds the camera prefab, points it at the player (if any) and turns off the scene's
        /// other cameras so only one renders and listens.
        /// </summary>
        private static GameObject PlaceCamera(Transform player)
        {
            Vector3 pos = player != null ? player.position : SceneCenter();
            Quaternion rot = player != null ? Quaternion.LookRotation(Flat(player.forward)) : Quaternion.LookRotation(SceneForward());
            var rig = Place(CameraPrefab, pos, rot);
            if (rig == null) return null;

            var disabled = new List<string>();
            foreach (var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (other.transform.IsChildOf(rig.transform) || !other.enabled) continue;
                Undo.RecordObject(other, "Combat Kit: Disable Camera");
                other.enabled = false;
                var listener = other.GetComponent<AudioListener>();
                if (listener != null) { Undo.RecordObject(listener, "Combat Kit: Disable Camera"); listener.enabled = false; }
                disabled.Add(other.name);
            }
            if (disabled.Count > 0)
                Debug.Log($"Combat Kit: disabled existing camera(s) {string.Join(", ", disabled)} so the Combat Kit camera renders.", rig);

            if (player != null) WireCameras(player);
            else Debug.LogWarning("Combat Kit: camera created without a player; create one and it will be tracked.", rig);
            return rig;
        }

        /// <summary>
        /// Points follow targets that are empty (or point at a deleted object) at the player's
        /// CameraPoint (falls back to the root). Targets already set are left alone.
        /// </summary>
        private static void WireCameras(Transform player)
        {
            Transform target = FindChild(player, CameraPointName) ?? player;
            int wired = 0;
            foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.Target.TrackingTarget != null) continue;
                Undo.RecordObject(cam, "Combat Kit: Wire Camera");
                cam.Target.TrackingTarget = target;
                EditorUtility.SetDirty(cam);
                wired++;
            }
            foreach (var anchor in Object.FindObjectsByType<CameraRigAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (anchor.player != null) continue;
                Undo.RecordObject(anchor, "Combat Kit: Wire Camera");
                anchor.player = player;
                EditorUtility.SetDirty(anchor);
                wired++;
            }
            if (wired > 0) Debug.Log($"Combat Kit: camera now follows {player.name}/{target.name}.", player);
        }

        private static CinemachineCamera[] ActiveCinemachineCameras() =>
            Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled).ToArray();

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ opponents

        public static GameObject[] Spawn(string prefabName, int amount, float dist, float gap, Formation shape)
        {
            var prefab = LoadPrefab(prefabName);
            if (prefab == null) return new GameObject[0];

            var player = FindPlayers().FirstOrDefault();
            Vector3 origin = player != null ? player.transform.position : GroundPoint(SceneCenter(), SceneCenter().y);
            Vector3 forward = player != null ? Flat(player.transform.forward) : SceneForward();

            // Everything already standing in the scene, plus what this call spawns.
            var occupied = FindCombatants().Select(c => (Flat3(c.Transform.position), Radius(c.Transform.gameObject))).ToList();
            float newRadius = Radius(prefab);

            var spawned = new List<GameObject>();
            foreach (Vector3 slot in Slots(origin, forward, amount, dist, gap, shape))
            {
                if (spawned.Count == amount) break;
                if (occupied.Any(o => (o.Item1 - Flat3(slot)).magnitude < o.Item2 + newRadius + Clearance)) continue;

                Vector3 position = GroundPoint(slot, origin.y);
                Vector3 toOrigin = Flat(origin - position);
                Quaternion rotation = Quaternion.LookRotation(toOrigin.sqrMagnitude > 0.001f ? toOrigin : -forward);
                var go = Place(prefabName, position, rotation);
                if (go == null) break;
                spawned.Add(go);
                occupied.Add((Flat3(position), newRadius));
            }

            if (spawned.Count < amount)
                Debug.LogWarning($"Combat Kit: only found room for {spawned.Count}/{amount} {prefabName}. Increase Distance or Spacing.");
            if (spawned.Count == 0) return new GameObject[0];
            Selection.objects = spawned.ToArray();
            Finish(spawned[0]);
            return spawned.ToArray();
        }

        /// <summary>
        /// Candidate positions, best first. Arc: rings in front of the origin, starting at the
        /// centre and alternating right/left, neighbours gap apart; an even amount starts
        /// half a step off centre so the group stays centred. Circle: amount points evenly
        /// around the origin (further rings rotated half a step), then the rest of the ring.
        /// </summary>
        private static IEnumerable<Vector3> Slots(Vector3 origin, Vector3 forward, int amount, float dist, float gap, Formation shape)
        {
            for (int ring = 0; ring < MaxRings; ring++)
            {
                float radius = dist + ring * gap;
                if (shape == Formation.Circle)
                {
                    // Evenly spread points first; if those are taken, every gap-sized spot on the ring.
                    int n = Mathf.Max(amount, 1);
                    for (int i = 0; i < n; i++)
                        yield return origin + Quaternion.AngleAxis(360f * (i + ring * 0.5f) / n, Vector3.up) * forward * radius;
                    int dense = Mathf.Max(n, Mathf.FloorToInt(2f * Mathf.PI * radius / gap));
                    for (int i = 0; i < dense; i++)
                        yield return origin + Quaternion.AngleAxis(360f * i / dense, Vector3.up) * forward * radius;
                    continue;
                }
                float step = gap / radius * Mathf.Rad2Deg;
                float start = amount % 2 == 0 ? 0.5f : 0f;
                int perSide = Mathf.FloorToInt(90f / step);   // stay in the front half
                for (int k = 0; k <= perSide; k++)
                {
                    float offset = start + k;
                    yield return origin + Quaternion.AngleAxis(offset * step, Vector3.up) * forward * radius;
                    if (offset > 0f)
                        yield return origin + Quaternion.AngleAxis(-offset * step, Vector3.up) * forward * radius;
                }
            }
        }

        /// <summary>Horizontal capsule radius of a character, scale included.</summary>
        private static float Radius(GameObject go)
        {
            var cc = go.GetComponent<CharacterController>();
            if (cc == null) return 0.5f;
            Vector3 s = go.transform.lossyScale;
            return cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        }

        private static IEnumerable<ICombatant> FindCombatants() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ICombatant>();

        // ------------------------------------------------------------------ helpers

        private static GameObject LoadPrefab(string prefabName)
        {
            string path = PrefabFolder + prefabName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogError($"Combat Kit: prefab not found at {path}");
            return prefab;
        }

        private static GameObject Place(string prefabName, Vector3 position, Quaternion rotation)
        {
            var prefab = LoadPrefab(prefabName);
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
            instance.transform.SetPositionAndRotation(position, rotation);
            GameObjectUtility.EnsureUniqueNameForSibling(instance);
            Undo.RegisterCreatedObjectUndo(instance, "Combat Kit: Spawn " + prefabName);
            // Later raycasts in the same call must see this object where it now stands.
            Physics.SyncTransforms();
            return instance;
        }

        private static PlayerCharacter[] FindPlayers() =>
            Object.FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static void Finish(GameObject select)
        {
            if (Selection.objects.Length <= 1) Selection.activeGameObject = select;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static Vector3 SceneCenter()
        {
            var view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }

        private static Vector3 SceneForward()
        {
            var view = SceneView.lastActiveSceneView;
            Vector3 f = view != null ? Flat(view.rotation * Vector3.forward) : Vector3.forward;
            return f.sqrMagnitude > 0.001f ? f : Vector3.forward;
        }

        /// <summary>
        /// Ground below a point, ignoring characters (so spawns never stack on each other) and
        /// triggers. Casts from just above the reference height first so roofs and ceilings
        /// are not picked; falls back to a high cast, then to the reference height.
        /// </summary>
        private static Vector3 GroundPoint(Vector3 point, float referenceY)
        {
            Physics.SyncTransforms();
            foreach (float startY in new[] { referenceY + 2f, referenceY + 50f })
            {
                var hits = Physics.RaycastAll(new Vector3(point.x, startY, point.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits.OrderBy(h => h.distance))
                    if (hit.collider.GetComponentInParent<ICombatant>() == null) return hit.point;
            }
            return new Vector3(point.x, referenceY, point.z);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }
        private static Vector3 Flat3(Vector3 v) { v.y = 0f; return v; }
    }
}
