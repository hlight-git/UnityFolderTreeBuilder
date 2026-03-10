using UnityEditor;
using UnityEngine;

namespace Hlight.Unity.Editor.FolderTreeBuilder
{
    [CustomEditor(typeof(FolderTree))]
    public class FolderTreeEditor : UnityEditor.Editor
    {
        private SerializedProperty _rootPathProp;
        private SerializedProperty _rootProp;

        // Track foldout state per node using the serialized property path as key.
        private static readonly System.Collections.Generic.HashSet<string> ExpandedNodes =
            new System.Collections.Generic.HashSet<string>();

        private void OnEnable()
        {
            _rootPathProp = serializedObject.FindProperty("rootPath");
            _rootProp = serializedObject.FindProperty("root");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_rootPathProp, new GUIContent("Root Path"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Folder Tree", EditorStyles.boldLabel);

            if (_rootProp != null)
            {
                if (_rootProp.managedReferenceValue == null)
                {
                    if (GUILayout.Button("Add Root"))
                        _rootProp.managedReferenceValue = new FolderNode();
                }
                else
                {
                    if (DrawFolderNode(_rootProp, 0))
                        _rootProp.managedReferenceValue = null;
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Folders", GUILayout.Height(32)))
            {
                var def = (FolderTree)target;
                def.CreateFolders();
                AssetDatabase.Refresh();
                Debug.Log("Folder structure created from definition.");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFolderNodeList(SerializedProperty listProp, int indent)
        {
            if (listProp == null)
                return;

            int removeIndex = -1;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                if (DrawFolderNode(element, indent))
                {
                    removeIndex = i;
                }
            }

            if (removeIndex >= 0 && removeIndex < listProp.arraySize)
            {
                listProp.DeleteArrayElementAtIndex(removeIndex);
            }
        }

        private bool DrawFolderNode(SerializedProperty element, int indent)
        {
            if (element == null)
                return false;

            InitializeFolderNode(element);

            var nameProp = element.FindPropertyRelative("name");
            var childrenProp = element.FindPropertyRelative("children");

            if (nameProp == null)
                return false;

            bool removeThis = false;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 16);
            
            // Foldout toggle for this node
            var path = element.propertyPath;
            var expanded = ExpandedNodes.Contains(path);
            var newExpanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true);
            if (newExpanded != expanded)
            {
                if (newExpanded)
                    ExpandedNodes.Add(path);
                else
                    ExpandedNodes.Remove(path);
            }

            nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);

            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                if (childrenProp != null)
                {
                    childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                    var child = childrenProp.GetArrayElementAtIndex(childrenProp.arraySize - 1);
                    InitializeFolderNode(child);
                    // Automatically expand this node when a child is added.
                    ExpandedNodes.Add(path);
                }
            }
            
            if (GUILayout.Button("x", GUILayout.Width(30)))
            {
                removeThis = true;
            }

            EditorGUILayout.EndHorizontal();

            if (!removeThis && newExpanded && childrenProp != null && childrenProp.arraySize > 0)
            {
                DrawFolderNodeList(childrenProp, indent + 1);
            }

            return removeThis;
        }

        private static void InitializeFolderNode(SerializedProperty element)
        {
            if (element == null)
                return;

            if (element.propertyType == SerializedPropertyType.ManagedReference &&
                element.managedReferenceValue == null)
            {
                element.managedReferenceValue = new FolderNode();
            }

            var nameProp = element.FindPropertyRelative("name");
            if (nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
            {
                nameProp.stringValue = "New Folder";
            }
        }
    }
}

