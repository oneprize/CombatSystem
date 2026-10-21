using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.EditorTools
{
    /// <summary>CombatKit 폴더를 독립 .unitypackage로 내보내는 에디터 메뉴. 폴더 밖 에셋을 참조하면 내보내기를 막는다.</summary>
    public static class CombatKitExporter
    {
        public const string Root = "Assets/CombatKit";

        /// <summary>저장 위치를 묻는 대화상자를 띄운 뒤 선택한 경로로 패키지를 내보낸다.</summary>
        [MenuItem("Tools/Combat Kit/Export unitypackage")]
        public static void Export()
        {
            string path = EditorUtility.SaveFilePanel("Export Combat Kit", "", "CombatKit", "unitypackage");
            if (!string.IsNullOrEmpty(path)) ExportTo(path);
        }

        /// <summary>
        /// CombatKit 안 모든 에셋의 의존성을 검사해 폴더 밖 Assets 참조가 하나라도 있으면 목록과 함께 예외를 던지고,
        /// 없으면 에셋을 저장한 뒤 CombatKit 폴더만 지정 경로로 내보낸다. 배치/CI에서 직접 호출할 수도 있다.
        /// </summary>
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
            // Unity 패키지 의존성은 따로 설치하므로 프로젝트 설정은 함께 내보내지 않는다.
            AssetDatabase.ExportPackage(Root, path, ExportPackageOptions.Recurse);
            Debug.Log("Combat Kit exported: " + path);
        }
    }
}
