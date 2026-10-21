using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

namespace CombatSystem.EditorTools
{
    /// <summary>현재 씬의 NavMesh를 한 번에 베이크하는 에디터 메뉴. 적의 NavMesh 이동에 필요한 네비게이션 데이터를 준비한다.</summary>
    public static class CombatKitNavigationSetup
    {
        /// <summary>
        /// 씬에 NavMeshSurface가 없으면 물리 콜라이더 기반 표면을 새로 만들고 NavMesh를 베이크한다.
        /// 기존 NavMesh 에셋이 있으면 새 결과를 그 에셋에 덮어써 참조를 유지하고, 없으면 씬 폴더에 새 에셋으로 저장한다.
        /// 저장되지 않은 씬이나 플레이 모드에서는 실행하지 않는다.
        /// </summary>
        [MenuItem("Tools/Combat Kit/Bake Navigation in Current Scene")]
        public static void Bake()
        {
            if (EditorApplication.isPlaying) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Combat Kit", "Save this scene before baking navigation.", "OK");
                return;
            }
            NavMeshSurface surface = null;
            foreach (var root in scene.GetRootGameObjects())
                if (surface == null) surface = root.GetComponentInChildren<NavMeshSurface>();
            if (surface == null)
            {
                var root = new GameObject("Combat Navigation");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                surface = root.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            }
            var old = surface.navMeshData;
            surface.BuildNavMesh();
            var generated = surface.navMeshData;
            if (generated == null) return;
            if (old != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(old)))
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(generated, old);
                surface.navMeshData = old;
                surface.AddData();
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(old);
            }
            else
            {
                string folder = System.IO.Path.GetDirectoryName(scene.path).Replace('\\', '/');
                AssetDatabase.CreateAsset(generated, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + scene.name + "_NavMesh.asset"));
            }
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Navigation baked. Save the scene to keep its surface reference.", surface);
        }
    }
}
