using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.EditorTools
{
    public static class CombatKitExporter
    {
        public const string Root = "Assets/CombatKit";

        [MenuItem("Tools/Combat Kit/Export unitypackage")]
        public static void Export()
        {
            string path = EditorUtility.SaveFilePanel("Export Combat Kit", "", "CombatKit", "unitypackage");
            if (!string.IsNullOrEmpty(path)) ExportTo(path);
        }

        public static void ExportTo(string path)
        {
            var external = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Root }))
            {
                string asset = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(asset)) continue;
                foreach (var dependency in AssetDatabase.GetDependencies(asset, true))
                    if (dependency.StartsWith("Assets/", StringComparison.Ordinal) &&
                        !dependency.StartsWith(Root + "/", StringComparison.Ordinal))
                        external.Add(dependency);
            }
            if (external.Count > 0)
                throw new InvalidOperationException("CombatKit has external asset references. Move or copy these dependencies before export:\n" +
                    string.Join("\n", external));
            AssetDatabase.SaveAssets();
            // Unity package dependencies are installed separately; do not export project settings.
            AssetDatabase.ExportPackage(Root, path, ExportPackageOptions.Recurse);
            Debug.Log("Combat Kit exported: " + path);
        }
    }
}
