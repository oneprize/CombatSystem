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
    /// 패키지 프리팹(플레이어, 카메라, 적, 보스, 연습 허수아비)을 열린 씬에 배치하는 에디터 창과 Hierarchy 우클릭 메뉴.
    /// 진입점: Tools > Combat Kit > Spawner(창) 또는 Hierarchy 우클릭 > Combat Kit.
    /// 항상 언팩하지 않은 프리팹 인스턴스로 만들어 이후 프리팹 수정이 계속 반영되게 하고, 모든 동작은 Undo할 수 있다.
    /// 플레이어를 만들면 추적 카메라가 있는지 확인해 없으면 만들고 플레이어를 따라가게 연결한다.
    /// </summary>
    public sealed class CombatKitSpawner : EditorWindow
    {
        #region 필드

        private const string PrefabFolder = "Assets/CombatKit/Content/Prefabs/";
        private const string CameraPrefab = "Camera";
        private const string CameraPointName = "CameraPoint";
        private const string PrefsKey = "CombatKit.Spawner.";
        private const float Clearance = 0.3f;   // 캐릭터 캡슐 사이에 추가로 두는 여유 간격
        private const int MaxRings = 8;

        /// <summary>적 배치 대형. Arc는 플레이어 앞 반원, Circle은 플레이어 둘레 원형.</summary>
        public enum Formation { Arc, Circle }

        private int count = 1;
        private float distance = 6f;
        private float spacing = 2.5f;
        private Formation formation = Formation.Arc;

        #endregion

        #region 메뉴와 창 UI

        /// <summary>Spawner 에디터 창을 연다.</summary>
        [MenuItem("Tools/Combat Kit/Spawner")]
        private static void Open() => GetWindow<CombatKitSpawner>("Combat Kit");

        // Hierarchy 우클릭 메뉴. 여러 오브젝트를 선택하면 Unity가 오브젝트마다 한 번씩 호출하므로,
        // 활성 선택 오브젝트에 대한 호출에서만 실제 작업을 한다.
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

        /// <summary>다중 선택 시 중복 실행을 막기 위해, 메뉴 호출이 활성 선택(또는 선택 없음)에 대한 것인지 판정한다.</summary>
        private static bool FirstCall(MenuCommand c) => c.context == null || c.context == Selection.activeObject;

        /// <summary>EditorPrefs에 저장된 마지막 배치 설정을 불러온다.</summary>
        private void OnEnable()
        {
            count = EditorPrefs.GetInt(PrefsKey + "Count", 1);
            distance = EditorPrefs.GetFloat(PrefsKey + "Distance", 6f);
            spacing = EditorPrefs.GetFloat(PrefsKey + "Spacing", 2.5f);
            formation = (Formation)EditorPrefs.GetInt(PrefsKey + "Formation", 0);
        }

        /// <summary>플레이어/카메라 생성 버튼과 적 수·거리·간격·대형 설정, 적/보스/허수아비 배치 버튼을 그린다. 설정이 바뀌면 EditorPrefs에 저장한다.</summary>
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

        /// <summary>창에서 저장한 배치 설정값. 창을 열지 않고 쓰는 Hierarchy 메뉴도 같은 값을 쓰도록 EditorPrefs에서 읽는다.</summary>
        private static class Settings
        {
            public static float Distance => EditorPrefs.GetFloat(PrefsKey + "Distance", 6f);
            public static float Spacing => EditorPrefs.GetFloat(PrefsKey + "Spacing", 2.5f);
            public static Formation Shape => (Formation)EditorPrefs.GetInt(PrefsKey + "Formation", 0);
        }

        #endregion

        #region 플레이어와 카메라

        /// <summary>
        /// 씬 뷰 중심의 지면에 플레이어 프리팹을 배치하고, 활성 Cinemachine 카메라가 없으면 카메라도 만들어 연결한다.
        /// 이미 플레이어가 있으면 카메라와 적 타게팅이 혼란스러워지므로 기존 선택/취소/추가 생성 중에서 고르게 한다.
        /// </summary>
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

        /// <summary>카메라 프리팹을 추가해 첫 번째 플레이어를 따라가게 한다. 이미 활성 Cinemachine 카메라가 있으면 추가 여부를 묻는다.</summary>
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
        /// 카메라 프리팹을 추가하고 (플레이어가 있으면) 플레이어를 추적하게 연결한다.
        /// 한 카메라만 렌더링하고 오디오를 듣도록 씬의 다른 카메라와 AudioListener는 Undo 가능한 방식으로 끈다.
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
        /// 추적 대상이 비어 있는(또는 삭제된 오브젝트를 가리키는) Cinemachine 카메라와 CameraRigAnchor를
        /// 플레이어의 CameraPoint(없으면 루트)에 연결한다. 이미 대상이 지정된 카메라는 건드리지 않는다.
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

        /// <summary>씬에서 활성화되어 동작 중인 Cinemachine 카메라 목록.</summary>
        private static CinemachineCamera[] ActiveCinemachineCameras() =>
            Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None).Where(c => c.isActiveAndEnabled).ToArray();

        /// <summary>비활성 자식까지 포함해 이름이 일치하는 첫 하위 Transform을 찾는다.</summary>
        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        #endregion

        #region 적 배치

        /// <summary>
        /// 플레이어(없으면 씬 뷰 중심) 앞에 대형에 따라 프리팹을 amount개 배치한다. 각 개체는 기준점을 바라보며,
        /// 이미 다른 전투원이 서 있거나 이번에 배치한 개체와 캡슐이 겹치는 자리는 건너뛴다.
        /// </summary>
        /// <returns>실제로 배치된 인스턴스 배열. 자리가 부족하면 요청 수보다 적을 수 있다.</returns>
        public static GameObject[] Spawn(string prefabName, int amount, float dist, float gap, Formation shape)
        {
            var prefab = LoadPrefab(prefabName);
            if (prefab == null) return new GameObject[0];

            var player = FindPlayers().FirstOrDefault();
            Vector3 origin = player != null ? player.transform.position : GroundPoint(SceneCenter(), SceneCenter().y);
            Vector3 forward = player != null ? Flat(player.transform.forward) : SceneForward();

            // 씬에 이미 서 있는 모든 전투원에, 이번 호출에서 배치하는 개체를 계속 추가해 겹침을 판정한다.
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
        /// 배치 후보 위치를 우선순위 순으로 돌려준다. 자리가 막히면 바깥 고리(gap만큼 멀어짐)로 넘어간다.
        /// Arc: 기준점 앞쪽 고리에서 정면부터 좌우를 번갈아 gap 간격으로 놓는다. 짝수 개면 반 칸 비켜 시작해 무리가 중앙에 오게 한다.
        /// Circle: 기준점 둘레에 amount개를 균등 분할해 놓고(바깥 고리는 반 칸 회전), 그다음 고리 위 나머지 자리를 채운다.
        /// </summary>
        private static IEnumerable<Vector3> Slots(Vector3 origin, Vector3 forward, int amount, float dist, float gap, Formation shape)
        {
            for (int ring = 0; ring < MaxRings; ring++)
            {
                float radius = dist + ring * gap;
                if (shape == Formation.Circle)
                {
                    // 먼저 균등 분할 지점을 시도하고, 그 자리가 차 있으면 고리 위 gap 간격의 모든 자리를 시도한다.
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
                int perSide = Mathf.FloorToInt(90f / step);   // 기준점 앞쪽 반원 안에서만 배치
                for (int k = 0; k <= perSide; k++)
                {
                    float offset = start + k;
                    yield return origin + Quaternion.AngleAxis(offset * step, Vector3.up) * forward * radius;
                    if (offset > 0f)
                        yield return origin + Quaternion.AngleAxis(-offset * step, Vector3.up) * forward * radius;
                }
            }
        }

        /// <summary>스케일을 반영한 캐릭터 캡슐의 수평 반지름. CharacterController가 없으면 0.5로 가정한다.</summary>
        private static float Radius(GameObject go)
        {
            var cc = go.GetComponent<CharacterController>();
            if (cc == null) return 0.5f;
            Vector3 s = go.transform.lossyScale;
            return cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        }

        /// <summary>씬에 있는 모든 전투원(플레이어/적/보스)을 찾는다. 배치 겹침 판정에 쓴다.</summary>
        private static IEnumerable<ICombatant> FindCombatants() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ICombatant>();

        #endregion

        #region 내부 도우미

        /// <summary>패키지 프리팹 폴더에서 이름으로 프리팹을 불러온다. 없으면 에러 로그를 남기고 null을 반환한다.</summary>
        private static GameObject LoadPrefab(string prefabName)
        {
            string path = PrefabFolder + prefabName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogError($"Combat Kit: prefab not found at {path}");
            return prefab;
        }

        /// <summary>활성 씬에 프리팹 인스턴스를 만들어 위치/회전을 지정하고, 형제 중 고유한 이름을 붙인 뒤 Undo에 등록한다.</summary>
        private static GameObject Place(string prefabName, Vector3 position, Quaternion rotation)
        {
            var prefab = LoadPrefab(prefabName);
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, EditorSceneManager.GetActiveScene());
            instance.transform.SetPositionAndRotation(position, rotation);
            GameObjectUtility.EnsureUniqueNameForSibling(instance);
            Undo.RegisterCreatedObjectUndo(instance, "Combat Kit: Spawn " + prefabName);
            // 같은 호출 안의 이후 레이캐스트가 이 오브젝트를 새 위치에서 인식하도록 물리 변환을 동기화한다.
            Physics.SyncTransforms();
            return instance;
        }

        /// <summary>비활성 오브젝트까지 포함해 씬의 모든 플레이어를 찾는다.</summary>
        private static PlayerCharacter[] FindPlayers() =>
            Object.FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        /// <summary>다중 선택이 아니면 생성한 오브젝트를 선택하고, 활성 씬을 변경됨으로 표시한다.</summary>
        private static void Finish(GameObject select)
        {
            if (Selection.objects.Length <= 1) Selection.activeGameObject = select;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>마지막으로 활성화된 씬 뷰의 피벗. 씬 뷰가 없으면 원점.</summary>
        private static Vector3 SceneCenter()
        {
            var view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }

        /// <summary>씬 뷰 카메라가 바라보는 수평 방향. 씬 뷰가 없거나 수직으로 내려다보면 월드 전방을 쓴다.</summary>
        private static Vector3 SceneForward()
        {
            var view = SceneView.lastActiveSceneView;
            Vector3 f = view != null ? Flat(view.rotation * Vector3.forward) : Vector3.forward;
            return f.sqrMagnitude > 0.001f ? f : Vector3.forward;
        }

        /// <summary>
        /// 지정한 지점 아래의 지면 위치를 찾는다. 캐릭터(배치 개체끼리 쌓이지 않도록)와 트리거는 무시한다.
        /// 지붕이나 천장이 잡히지 않도록 기준 높이 바로 위에서 먼저 쏘고, 실패하면 높은 곳에서 다시 쏘며, 그래도 없으면 기준 높이를 쓴다.
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

        /// <summary>높이 성분을 없앤 수평 단위 방향 벡터.</summary>
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }
        /// <summary>높이 성분만 0으로 만든 수평 위치(정규화하지 않음).</summary>
        private static Vector3 Flat3(Vector3 v) { v.y = 0f; return v; }

        #endregion
    }
}
