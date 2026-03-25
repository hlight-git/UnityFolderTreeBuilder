using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hlight.Unity.Editor.StructureTreeBuilder
{
    public class StructureTree : ScriptableObject
    {
        [SerializeField] private string rootPath = "Assets";
        [SerializeReference] private StructureNode root;

        public string RootPath => rootPath;
        public StructureNode Root => root;

        internal void Initialize(string path, StructureNode rootNode)
        {
            rootPath = path ?? "Assets";
            root = rootNode;
        }

        public void CreateStructure()
        {
            if (root == null)
                return;

            if (!rootPath.StartsWith("Assets"))
            {
                Debug.LogError("[StructureTree] Root path must start with 'Assets'.");
                return;
            }

            var visited = new HashSet<StructureNode>();
            CreateNodeRecursive(rootPath, root, visited);
            AssetDatabase.Refresh();
        }

        private static void CreateNodeRecursive(string parentPath, StructureNode node, HashSet<StructureNode> visited)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.name))
                return;

            if (!visited.Add(node))
                return;

            var currentPath = Path.Combine(parentPath, node.name);
            if (!Directory.Exists(currentPath))
                Directory.CreateDirectory(currentPath);

            if (node.asmdef != null && !string.IsNullOrWhiteSpace(node.asmdef.assemblyName))
                WriteAsmdef(currentPath, node.asmdef);

            if (node.children == null)
                return;

            foreach (var child in node.children)
                CreateNodeRecursive(currentPath, child, visited);
        }

        private static void WriteAsmdef(string folderPath, AsmdefConfig config)
        {
            var filePath = Path.Combine(folderPath, $"{config.assemblyName}.asmdef");
            if (File.Exists(filePath))
                return;

            var refs = config.references ?? new List<string>();
            var refsJson = refs.Count > 0
                ? $"[\n        \"{string.Join("\",\n        \"", refs)}\"\n    ]"
                : "[]";

            var json = $@"{{
    ""name"": ""{config.assemblyName}"",
    ""rootNamespace"": ""{config.assemblyName}"",
    ""references"": {refsJson},
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}}";
            File.WriteAllText(filePath, json);
        }

        #region Export

        [MenuItem("Assets/Export Structure Tree", true)]
        private static bool ValidateExport() => TryGetTargetFolderPath(out _);

        [MenuItem("Assets/Export Structure Tree")]
        private static void Export()
        {
            if (!TryGetTargetFolderPath(out var path))
            {
                EditorUtility.DisplayDialog(
                    "Export Structure Tree",
                    "Please select a folder in the Project window.",
                    "OK");
                return;
            }

            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            var defaultFileName = $"{Path.GetFileName(path.TrimEnd('/', '\\'))}StructureTree";
            var savePath = EditorUtility.SaveFilePanelInProject(
                "Save Structure Tree", defaultFileName, "asset",
                "Choose where to save the StructureTree asset", path);

            if (string.IsNullOrEmpty(savePath))
                return;

            var rootNode = BuildNodeRecursive(fullPath);
            var tree = CreateInstance<StructureTree>();
            var parentPath = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "";
            tree.Initialize(parentPath, rootNode);

            AssetDatabase.CreateAsset(tree, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(tree);
            Selection.activeObject = tree;
            Debug.Log($"[StructureTree] Exported from '{path}' to '{savePath}'.");
        }

        private static StructureNode BuildNodeRecursive(string fullPath)
        {
            var dirInfo = new DirectoryInfo(fullPath);
            var node = new StructureNode { name = dirInfo.Name };

            // Detect existing .asmdef in this folder
            var asmdefFiles = dirInfo.GetFiles("*.asmdef");
            if (asmdefFiles.Length > 0)
            {
                try
                {
                    var json = File.ReadAllText(asmdefFiles[0].FullName);
                    var parsed = JsonUtility.FromJson<AsmdefJsonDto>(json);
                    node.asmdef = new AsmdefConfig
                    {
                        assemblyName = parsed.name ?? "",
                        references = parsed.references != null
                            ? new List<string>(parsed.references)
                            : new List<string>()
                    };
                }
                catch
                {
                    // Skip malformed asmdef
                }
            }

            foreach (var subDir in dirInfo.GetDirectories())
            {
                if (subDir.Name.StartsWith("."))
                    continue;

                var child = BuildNodeRecursive(subDir.FullName);
                if (child != null)
                    node.children.Add(child);
            }

            return node;
        }

        [Serializable]
        private class AsmdefJsonDto
        {
            public string name;
            public string[] references;
        }

        #endregion

        #region Target Path Resolution

        private static bool TryGetTargetFolderPath(out string path)
        {
            path = null;

            if (Selection.objects is { Length: 1 })
            {
                var selPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selPath))
                {
                    var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", selPath));
                    if (Directory.Exists(full))
                    {
                        path = selPath.Replace("\\", "/");
                        return true;
                    }
                }
            }

            var method = typeof(ProjectWindowUtil).GetMethod(
                "GetActiveFolderPath", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                return false;

            try
            {
                var folderPath = (string)method.Invoke(null, null);
                if (string.IsNullOrEmpty(folderPath))
                    return false;

                var full2 = Path.GetFullPath(Path.Combine(Application.dataPath, "..", folderPath));
                if (!Directory.Exists(full2))
                    return false;

                path = folderPath.Replace("\\", "/");
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    [Serializable]
    public class StructureNode
    {
        public string name;

        [SerializeReference]
        public AsmdefConfig asmdef;

        [SerializeReference]
        public List<StructureNode> children = new();
    }

    [Serializable]
    public class AsmdefConfig
    {
        public string assemblyName;
        public List<string> references = new();
    }
}
