using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;
using CombatSystem.Player;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// Places the package prefabs into the open scene: Tools > Combat Kit > Spawner (window)
    /// or Hierarchy right-click > Combat Kit. Always creates prefab instances (never unpacked
    /// copies) so later prefab edits keep propagating, and every action is undoable.
    /// Creating a player also wires empty camera follow targets to it.
    /// </summary>
    public sealed class CombatKitSpawner : EditorWindow
    {
        private const string PrefabFolder = "Packages/com.oneprize.combatkit/Content/Prefabs/";
        private const string PrefsKey = "CombatKit.Spawner.";

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
        [MenuItem("GameObject/Combat Kit/Spawn Enemy", false, 11)]
        private static void MenuEnemy(MenuCommand c) { if (FirstCall(c)) Spawn("Enemy", 1, Settings.Distance, Settings.Spacing, Settings.Shape); }
        [MenuItem("GameObject/Combat Kit/Spawn Boss", false, 12)]
        private static void MenuBoss(MenuCommand c) { if (FirstCall(c)) Spawn("Boss", 1, Settings.Distance, Settings.Spacing, Settings.Shape); }
        [MenuItem("GameObject/Combat Kit/Spawn Practice Dummy", false, 13)]
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
            EditorGUILayout.HelpBox("Opponents are placed in front of the player, facing it. "
                + "Without a player they go to the Scene view center. An enemy's spawn point "
                + "becomes its home/patrol center.", MessageType.None);
        }

        /// <summary>Window values, also used by the Hierarchy menu items.</summary>
        private static class Settings
        {
            public static float Distance => EditorPrefs.GetFloat(PrefsKey + "Distance", 6f);
            public static float Spacing => EditorPrefs.GetFloat(PrefsKey + "Spacing", 2.5f);
            public static Formation Shape => (Formation)EditorPrefs.GetInt(PrefsKey + "Formation", 0);
        }

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

            Vector3 position = GroundPoint(SceneCenter());
            Vector3 forward = SceneForward();
            var player = Place("Player", position, Quaternion.LookRotation(forward));
            if (player == null) return null;
            WireCamera(player.transform);
            Finish(player);
            return player;
        }

        public static GameObject[] Spawn(string prefabName, int amount, float dist, float gap, Formation shape)
        {
            var player = FindPlayers().FirstOrDefault();
            Vector3 origin = player != null ? player.transform.position : GroundPoint(SceneCenter());
            Vector3 forward = player != null ? Flat(player.transform.forward) : SceneForward();

            var spawned = new GameObject[amount];
            for (int i = 0; i < amount; i++)
            {
                Vector3 position = GroundPoint(SlotPosition(origin, forward, i, amount, dist, gap, shape));
                Vector3 toOrigin = Flat(origin - position);
                Quaternion rotation = Quaternion.LookRotation(toOrigin.sqrMagnitude > 0.001f ? toOrigin : -forward);
                spawned[i] = Place(prefabName, position, rotation);
                if (spawned[i] == null) return spawned.Where(g => g != null).ToArray();
            }
            Selection.objects = spawned;
            Finish(spawned[0]);
            return spawned;
        }

        /// <summary>
        /// Arc: centred in front of the origin on a circle of radius dist, neighbours gap apart.
        /// Circle: evenly around the origin (gap ignored).
        /// </summary>
        private static Vector3 SlotPosition(Vector3 origin, Vector3 forward, int index, int amount, float dist, float gap, Formation shape)
        {
            float angle = shape == Formation.Circle
                ? 360f * index / amount
                : (index - (amount - 1) * 0.5f) * (gap / dist) * Mathf.Rad2Deg;
            return origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * dist;
        }

        private static GameObject Place(string prefabName, Vector3 position, Quaternion rotation)
        {
            string path = PrefabFolder + prefabName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Combat Kit: prefab not found at {path}");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
            instance.transform.SetPositionAndRotation(position, rotation);
            GameObjectUtility.EnsureUniqueNameForSibling(instance);
            Undo.RegisterCreatedObjectUndo(instance, "Combat Kit: Spawn " + prefabName);
            return instance;
        }

        /// <summary>
        /// Points follow targets that are empty (or point at a deleted object) at the new player.
        /// Targets that already point at something are left alone, so hand-made rigs that
        /// follow a helper transform keep working.
        /// </summary>
        private static void WireCamera(Transform player)
        {
            int wired = 0, found = 0;
            foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                found++;
                if (cam.Target.TrackingTarget != null) continue;
                Undo.RecordObject(cam, "Combat Kit: Wire Camera");
                cam.Target.TrackingTarget = player;
                EditorUtility.SetDirty(cam);
                wired++;
            }
            foreach (var anchor in Object.FindObjectsByType<CameraRigAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                found++;
                if (anchor.player != null) continue;
                Undo.RecordObject(anchor, "Combat Kit: Wire Camera");
                anchor.player = player;
                EditorUtility.SetDirty(anchor);
                wired++;
            }
            if (found == 0) Debug.LogWarning("Combat Kit: no CinemachineCamera or CameraRigAnchor in the scene; the camera will not follow the player.", player);
            else if (wired > 0) Debug.Log($"Combat Kit: camera follow target set to {player.name} on {wired} component(s).", player);
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

        /// <summary>Drops a point onto the first non-trigger collider below it, else keeps it.</summary>
        private static Vector3 GroundPoint(Vector3 point)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(point + Vector3.up * 50f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point : point;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }
    }
}
