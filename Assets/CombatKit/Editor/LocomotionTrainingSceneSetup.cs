using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using CombatSystem.Player;
using CombatSystem.Training;

namespace CombatSystem.EditorTools
{
    /// <summary>기존 플레이어/카메라를 복제해 독립된 선형 이동 훈련 씬을 만든다.</summary>
    public static class LocomotionTrainingSceneSetup
    {
        public const string ScenePath = "Assets/CombatKit/Content/Scenes/LocomotionTraining.unity";
        private const string MaterialsPath = "Assets/CombatKit/Content/Data/Training/LocomotionCourse";
        private static Transform root;
        private static Material floor, wall, cyan, amber, white, metal, green, pit;

        private static Material Material(string name, Color color)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/CombatKit/Content/Data/Training/LocomotionStone.mat");
            result = new Material(source) { name = name, color = color };
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(root, false);
            go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Deck(string name, float from, float to, float y, float width, float x = 0f)
        {
            Box(name, new Vector3(x, y - .3f, (from + to) / 2), new Vector3(width, .6f, to - from), floor);
            foreach (float side in new[] { -1f, 1f })
                Box(name + " Edge", new Vector3(x + side * (width / 2 - .06f), y + .015f, (from + to) / 2),
                    new Vector3(.1f, .025f, to - from), cyan, false);
            Box(name + " Landing Stripe", new Vector3(x, y + .02f, from + .16f), new Vector3(width - .25f, .025f, .2f), amber, false);
        }

        private static void Bar(float z, int index)
        {
            int layer = LayerMask.NameToLayer("HangRail");
            foreach (float x in new[] { -2.5f, 2.5f })
                Box("Bar " + index.ToString("00") + " Support", new Vector3(x, .55f, z), new Vector3(.16f, 6.1f, .16f), metal).layer = layer;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "Training Hang Rail " + index.ToString("00");
            go.transform.SetParent(root, false); go.transform.position = new Vector3(0, 3.6f, z);
            go.transform.rotation = Quaternion.Euler(0, 0, 90); go.transform.localScale = new Vector3(.12f, 2.6f, .12f);
            go.GetComponent<Renderer>().sharedMaterial = metal; go.layer = layer;
            var rail = go.AddComponent<HangRail>(); rail.localStart = new Vector3(0, -.88f, 0); rail.localEnd = new Vector3(0, .88f, 0);
        }

        private static void Link(string name, float from, float to, float fromHeight, float toHeight)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            var link = go.AddComponent<NavMeshLink>();
            link.startPoint = new Vector3(0, fromHeight + .05f, from); link.endPoint = new Vector3(0, toHeight + .05f, to);
            link.width = 3.5f; link.bidirectional = true; link.activated = true;
            foreach (Vector3 endpoint in new[] { link.startPoint, link.endPoint })
            {
                Box(name + " Pad", endpoint - Vector3.up * .025f, new Vector3(3.6f, .035f, 1.3f), cyan, false);
            }
        }

        [MenuItem("Tools/Combat Kit/Create Linear Locomotion Training Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("Training scene already exists; it will not be overwritten.");
            var previous = SceneManager.GetActiveScene();
            if (previous.isDirty) throw new InvalidOperationException("Save the current scene before creating the separate training scene.");
            var sourcePlayer = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            var sourceRig = UnityEngine.Object.FindFirstObjectByType<CombatSystem.CameraControl.LockOnCamera>();
            var sourceLight = previous.GetRootGameObjects().FirstOrDefault(g => g.name == "Directional Light");
            var sourceVolume = previous.GetRootGameObjects().FirstOrDefault(g => g.name == "Global Volume");
            if (sourcePlayer == null || sourceRig == null || sourceLight == null) throw new InvalidOperationException("Open ComboPracticeGround first.");
            if (!AssetDatabase.IsValidFolder(MaterialsPath)) AssetDatabase.CreateFolder("Assets/CombatKit/Content/Data/Training", "LocomotionCourse");
            floor = Material("LT Floor", new Color(.25f, .32f, .38f)); wall = Material("LT Wall", new Color(.055f, .09f, .13f));
            cyan = Material("LT Cyan", new Color(.08f, .8f, .78f)); amber = Material("LT Amber", new Color(1f, .51f, .12f));
            white = Material("LT White", new Color(.86f, .92f, .95f)); metal = Material("LT Metal", new Color(.65f, .74f, .8f));
            green = Material("LT Finish", new Color(.25f, .88f, .48f)); pit = Material("LT Pit", new Color(.12f, .025f, .04f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            root = new GameObject("Locomotion Training Course").transform;
            var player = UnityEngine.Object.Instantiate(sourcePlayer.gameObject).GetComponent<PlayerCharacter>(); player.name = "Player";
            var rig = UnityEngine.Object.Instantiate(sourceRig.gameObject); rig.name = "Camera";
            var light = UnityEngine.Object.Instantiate(sourceLight); light.name = "Directional Light";
            light.transform.rotation = Quaternion.Euler(48, -28, 0); light.GetComponent<Light>().intensity = 1.5f;
            if (sourceVolume != null) UnityEngine.Object.Instantiate(sourceVolume).name = "Global Volume";
            player.transform.SetPositionAndRotation(new Vector3(0, 1.08f, 3), Quaternion.identity);
            var wallRun = player.GetComponent<PlayerWallRun>();
            wallRun.maxDuration = 2f; wallRun.downwardSpeed = .25f;
            wallRun.allowOppositeWallChain = true; wallRun.maxAttachesPerAirtime = 4;
            var status = player.GetComponent<PlayerStatusUI>(); if (status != null) UnityEngine.Object.DestroyImmediate(status);
            var camera = rig.GetComponentInChildren<Camera>(true);
            var ps = new SerializedObject(player); ps.FindProperty("cameraTransform").objectReferenceValue = camera.transform; ps.ApplyModifiedPropertiesWithoutUndo();
            var feedback = player.GetComponent<ParryFeedback>();
            if (feedback != null) { var fs = new SerializedObject(feedback); fs.FindProperty("targetCamera").objectReferenceValue = camera; fs.ApplyModifiedPropertiesWithoutUndo(); }
            var cameraPoint = player.transform.Find("CameraPoint");
            foreach (var anchor in rig.GetComponentsInChildren<CameraRigAnchor>(true)) anchor.player = player.transform;
            var free = rig.GetComponentsInChildren<CinemachineCamera>(true).First(c => c.name == "FreeLook Camera");
            free.Follow = cameraPoint; free.LookAt = cameraPoint; free.Lens.FieldOfView = 75f;
            var orbit = free.GetComponent<CinemachineOrbitalFollow>(); var rings = orbit.Orbits;
            rings.Top.Radius = 4.5f; rings.Top.Height = 4.5f; rings.Center.Radius = 5f; rings.Center.Height = 2.7f;
            rings.Bottom.Radius = 4f; rings.Bottom.Height = .5f; orbit.Orbits = rings; orbit.HorizontalAxis.Value = 0f;
            var deoccluder = free.GetComponent<CinemachineDeoccluder>();
            if (deoccluder != null) deoccluder.CollideAgainst = ~LayerMask.GetMask("Player", "Enemy", "HangRail", "Ignore Raycast");
            foreach (var follow in rig.GetComponentsInChildren<CinemachineThirdPersonFollow>(true))
                follow.AvoidObstacles.CollisionFilter &= ~LayerMask.GetMask("Enemy", "HangRail");
            rig.GetComponentInChildren<CinemachineTargetGroup>(true).Targets.Clear();
            EditorSceneManager.CloseScene(previous, true);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.58f, .65f, .72f); RenderSettings.fog = false;

            // 떠 있는 발판과 깊은 낙하 공간. 측벽은 코스 바깥으로 돌아가는 우회를 막는다.
            Box("Abyss Visual", new Vector3(0, -9, 99f), new Vector3(18, .3f, 206f), pit, false);
            foreach (float x in new[] { -9f, 9f })
            {
                Box("Hall Side Wall", new Vector3(x, 3.5f, 99f), new Vector3(.4f, 18f, 206f), wall);
                Box("Hall Light Strip", new Vector3(x * .974f, 10.8f, 99f), new Vector3(.08f, .08f, 204f), cyan, false);
            }
            Box("Start Back Wall", new Vector3(0, 3.5f, -4), new Vector3(18, 18, .4f), wall);
            Box("Finish Back Wall", new Vector3(0, 3.5f, 202f), new Vector3(18, 18, .4f), wall);
            for (int z = 0; z <= 201; z += 14)
                Box("Roof Crossbeam " + z, new Vector3(0, 12.5f, z), new Vector3(18, .25f, .3f), metal, false);

            Deck("Start Deck", 0, 14, 0, 8);
            Box("Start Line", new Vector3(0, .025f, 5), new Vector3(7.8f, .035f, .25f), green, false);
            Deck("Jump Platform A", 20, 24, .5f, 4, -1.5f);
            Deck("Jump Platform B", 31, 36, 1, 4, 1.5f);
            Deck("Wall Run Approach", 44, 52, 0, 8);

            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = 55f + i * 10f;
                string label = "Wall Run " + (i + 1).ToString("00") + (side < 0 ? " Left" : " Right");
                Box(label, new Vector3(side * 2.8f, 2.5f, z), new Vector3(.8f, 7, 14), cyan);
                Box(label + " Stripe", new Vector3(side * 2.38f, 2, z), new Vector3(.03f, .15f, 13.5f), white, false);
            }
            Deck("Wall Run Landing", 89, 102, 0, 8);

            Deck("Bar Launch Deck", 102, 107, 0, 5);
            for (int i = 0; i < 4; i++) Bar(108.5f + i * 4f, i + 1);
            Deck("Bar Landing Deck", 123, 130, 0, 5);

            for (int i = 0; i < 18; i++)
            {
                float h = (i + 1) * .25f;
                Box("Slide Access Step " + (i + 1), new Vector3(0, h / 2, 130.25f + i * .5f), new Vector3(5, h, .51f), floor);
                Box("Step Nosing " + (i + 1), new Vector3(0, h + .01f, 130.04f + i * .5f), new Vector3(4.7f, .02f, .07f), cyan, false);
            }
            Deck("Slide Summit", 139, 143, 4.5f, 5);
            float angle = Mathf.Atan2(4.5f, 10) * Mathf.Rad2Deg;
            var slopeRotation = Quaternion.Euler(angle, 0, 0);
            var ramp = Box("24 Degree Downhill Slide", new Vector3(0, 2.25f, 148) - slopeRotation * Vector3.up * .125f,
                new Vector3(5, .25f, Mathf.Sqrt(120.25f)), cyan);
            ramp.transform.rotation = slopeRotation;
            Deck("Slide Runout", 153, 164, 0, 6);

            Deck("Link Landing Island", 172, 179, 3, 6);
            Deck("Finish Deck", 185, 199, 6, 8);
            Link("Link Jump 01", 163, 173, 0, 3); Link("Link Jump 02", 178, 186, 3, 6);
            Box("Finish Line", new Vector3(0, 6.03f, 192), new Vector3(7.8f, .04f, .45f), green, false);
            foreach (float x in new[] { -3.8f, 3.8f })
                Box("Finish Gate Post", new Vector3(x, 8.2f, 193), new Vector3(.22f, 4.4f, .22f), green);
            Box("Finish Gate Header", new Vector3(0, 10.4f, 193), new Vector3(7.8f, .25f, .25f), green);
            var finish = new GameObject("Finish Area"); finish.transform.SetParent(root, false); finish.transform.position = new Vector3(0, 8, 193);
            var finishBox = finish.AddComponent<BoxCollider>(); finishBox.size = new Vector3(8, 4, 3); finishBox.isTrigger = true;
            var course = root.gameObject.AddComponent<LocomotionTrainingCourse>(); course.player = player; course.finishArea = finishBox;
            var surface = new GameObject("Training Navigation").AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            EditorSceneManager.SaveScene(scene, ScenePath);
            Physics.SyncTransforms(); CombatKitNavigationSetup.Bake();
            foreach (var link in UnityEngine.Object.FindObjectsByType<NavMeshLink>(FindObjectsSortMode.None)) link.UpdateLink();
            EditorSceneManager.SaveScene(scene);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets(); Selection.activeGameObject = root.gameObject;
            Debug.Log("Locomotion training scene created: five traversal stations, fall restart, two navigation links and finish area.");
        }
    }
}
