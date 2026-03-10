using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hlight.Unity.Editor.FolderTreeBuilder
{
    public class FolderTree : ScriptableObject
    {
        [SerializeField] private string rootPath = "Assets";

        [SerializeReference] private FolderNode root;

        public string RootPath => rootPath;
        public FolderNode Root => root;

        internal void Initialize(string path, FolderNode rootNode)
        {
            rootPath = path ?? "Assets";
            root = rootNode;
        }

        public void CreateFolders()
        {
            if (root == null)
                return;
            if (!rootPath.StartsWith("Assets"))
            {
                Debug.LogError("Root path must be starting with 'Assets'.");
                return;
            }

            var visited = new HashSet<FolderNode>();
            CreateFolderRecursive(rootPath, root, visited);
        }

        /// <summary>
        /// Gets the target folder path for export. Uses Selection when valid, otherwise falls back to
        /// ProjectWindowUtil.GetActiveFolderPath (via reflection) for right-click on the folder tree.
        /// </summary>
        private static bool TryGetTargetFolderPath(out string path)
        {
            path = null;

            // 1. Try Selection (works when folder is selected in the right panel)
            if (Selection.objects != null && Selection.objects.Length == 1)
            {
                var selectionPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selectionPath))
                {
                    var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", selectionPath));
                    if (Directory.Exists(fullPath))
                    {
                        path = selectionPath.Replace("\\", "/");
                        return true;
                    }
                }
            }

            // 2. Fallback: ProjectWindowUtil.GetActiveFolderPath (works when right-clicking on folder tree)
            var method = typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                try
                {
                    var folderPath = (string)method.Invoke(null, null);
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", folderPath));
                        if (Directory.Exists(fullPath))
                        {
                            path = folderPath.Replace("\\", "/");
                            return true;
                        }
                    }
                }
                catch
                {
                    // Ignore reflection failures
                }
            }

            return false;
        }

        [MenuItem("Assets/Export Folder Tree", true)]
        private static bool ValidateExportFolderTree()
        {
            return TryGetTargetFolderPath(out _);
        }

        [MenuItem("Assets/Export Folder Tree")]
        private static void ExportFolderTree()
        {
            if (!TryGetTargetFolderPath(out var path))
            {
                EditorUtility.DisplayDialog("Export Folder Tree", "Please select a folder in the Project window or right-click on a folder in the tree.", "OK");
                return;
            }
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            var normalizedPath = path;

            var defaultFileName = $"{Path.GetFileName(path.TrimEnd('/', '\\'))}FolderTree";
            var savePath = EditorUtility.SaveFilePanelInProject(
                "Save Folder Tree",
                defaultFileName,
                "asset",
                "Choose where to save the FolderTree asset",
                path
            );

            if (string.IsNullOrEmpty(savePath))
                return;

            var root = BuildRootFromDirectory(fullPath);
            var folderTree = CreateInstance<FolderTree>();
            var parentPath = Path.GetDirectoryName(normalizedPath)?.Replace("\\", "/") ?? "";
            folderTree.Initialize(parentPath, root);

            AssetDatabase.CreateAsset(folderTree, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(folderTree);
            Selection.activeObject = folderTree;

            Debug.Log($"[FolderTree] Exported folder structure from '{normalizedPath}' to '{savePath}'.");
        }

        private static FolderNode BuildRootFromDirectory(string fullPath)
        {
            return BuildFolderNodeRecursive(fullPath);
        }

        private static FolderNode BuildFolderNodeRecursive(string fullPath)
        {
            var dirInfo = new DirectoryInfo(fullPath);
            var node = new FolderNode { name = dirInfo.Name };

            foreach (var subDir in dirInfo.GetDirectories())
            {
                if (subDir.Name.StartsWith("."))
                    continue;

                var child = BuildFolderNodeRecursive(subDir.FullName);
                if (child != null)
                    node.children.Add(child);
            }

            return node;
        }

        private static void CreateFolderRecursive(string parentPath, FolderNode node, HashSet<FolderNode> visited)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.name))
                return;

            // Prevent infinite loops if there is an accidental cycle in the data.
            if (!visited.Add(node))
                return;

            var currentPath = Path.Combine(parentPath, node.name);
            if (!Directory.Exists(currentPath))
            {
                Directory.CreateDirectory(currentPath);
            }

            if (node.children == null)
                return;

            foreach (var child in node.children)
            {
                CreateFolderRecursive(currentPath, child, visited);
            }
        }
    }

    [Serializable]
    public class FolderNode
    {
        public string name;

        [SerializeReference]
        public List<FolderNode> children = new();
    }
}

