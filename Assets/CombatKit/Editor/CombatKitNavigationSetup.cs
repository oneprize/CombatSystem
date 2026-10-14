using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

namespace CombatSystem.EditorTools
{
    public static class CombatKitNavigationSetup
    {
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
