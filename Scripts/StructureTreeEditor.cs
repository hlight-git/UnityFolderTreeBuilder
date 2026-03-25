using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hlight.Unity.Editor.StructureTreeBuilder
{
    [CustomEditor(typeof(StructureTree))]
    public class StructureTreeEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> ExpandedNodes = new();

        private SerializedProperty _rootPathProp;
        private SerializedProperty _rootProp;

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
            EditorGUILayout.LabelField("Structure Tree", EditorStyles.boldLabel);

            if (_rootProp != null)
            {
                if (_rootProp.managedReferenceValue == null)
                {
                    if (GUILayout.Button("Add Root"))
                        _rootProp.managedReferenceValue = new StructureNode();
                }
                else
                {
                    if (DrawNode(_rootProp, 0))
                        _rootProp.managedReferenceValue = null;
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Structure", GUILayout.Height(32)))
            {
                ((StructureTree)target).CreateStructure();
                Debug.Log("[StructureTree] Structure created.");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawNodeList(SerializedProperty listProp, int indent)
        {
            if (listProp == null)
                return;

            int removeIndex = -1;
            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (DrawNode(listProp.GetArrayElementAtIndex(i), indent))
                    removeIndex = i;
            }

            if (removeIndex >= 0 && removeIndex < listProp.arraySize)
                listProp.DeleteArrayElementAtIndex(removeIndex);
        }

        private bool DrawNode(SerializedProperty element, int indent)
        {
            if (element == null)
                return false;

            InitializeNode(element);

            var nameProp = element.FindPropertyRelative("name");
            var childrenProp = element.FindPropertyRelative("children");
            var asmdefProp = element.FindPropertyRelative("asmdef");
            if (nameProp == null)
                return false;

            bool removeThis = false;
            var path = element.propertyPath;
            var expanded = ExpandedNodes.Contains(path);
            var hasAsmdef = asmdefProp?.managedReferenceValue != null;

            // Main row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 16);

            var newExpanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true);
            if (newExpanded != expanded)
            {
                if (newExpanded) ExpandedNodes.Add(path);
                else ExpandedNodes.Remove(path);
            }

            // Tint name if has asmdef
            if (hasAsmdef)
            {
                var prev = GUI.color;
                GUI.color = new Color(0.6f, 0.9f, 1f);
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
                GUI.color = prev;
            }
            else
            {
                nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
            }

            // Toggle asmdef
            var asmLabel = hasAsmdef ? "asm" : "---";
            if (GUILayout.Button(asmLabel, GUILayout.Width(36)))
            {
                if (asmdefProp != null)
                {
                    asmdefProp.managedReferenceValue = hasAsmdef
                        ? null
                        : new AsmdefConfig { assemblyName = nameProp.stringValue };
                }
            }

            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                if (childrenProp != null)
                {
                    childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                    InitializeNode(childrenProp.GetArrayElementAtIndex(childrenProp.arraySize - 1));
                    ExpandedNodes.Add(path);
                }
            }

            if (GUILayout.Button("x", GUILayout.Width(24)))
                removeThis = true;

            EditorGUILayout.EndHorizontal();

            // Asmdef details (when expanded and has asmdef)
            if (!removeThis && newExpanded && hasAsmdef && asmdefProp != null)
                DrawAsmdefConfig(asmdefProp, indent);

            // Children
            if (!removeThis && newExpanded && childrenProp is { arraySize: > 0 })
                DrawNodeList(childrenProp, indent + 1);

            return removeThis;
        }

        private static void DrawAsmdefConfig(SerializedProperty asmdefProp, int indent)
        {
            var asmNameProp = asmdefProp.FindPropertyRelative("assemblyName");
            var refsProp = asmdefProp.FindPropertyRelative("references");
            if (asmNameProp == null)
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space((indent + 1) * 16);
            EditorGUILayout.LabelField("Assembly", GUILayout.Width(60));
            asmNameProp.stringValue = EditorGUILayout.TextField(asmNameProp.stringValue);
            EditorGUILayout.EndHorizontal();

            if (refsProp == null)
                return;

            for (int i = 0; i < refsProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space((indent + 1) * 16);
                EditorGUILayout.LabelField("Ref", GUILayout.Width(60));

                var refElement = refsProp.GetArrayElementAtIndex(i);
                refElement.stringValue = EditorGUILayout.TextField(refElement.stringValue);

                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    refsProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space((indent + 1) * 16);
            if (GUILayout.Button("+ Reference", GUILayout.Width(100)))
            {
                refsProp.InsertArrayElementAtIndex(refsProp.arraySize);
                refsProp.GetArrayElementAtIndex(refsProp.arraySize - 1).stringValue = "";
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void InitializeNode(SerializedProperty element)
        {
            if (element == null)
                return;

            if (element.propertyType == SerializedPropertyType.ManagedReference
                && element.managedReferenceValue == null)
            {
                element.managedReferenceValue = new StructureNode();
            }

            var nameProp = element.FindPropertyRelative("name");
            if (nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
                nameProp.stringValue = "New Folder";
        }
    }
}
