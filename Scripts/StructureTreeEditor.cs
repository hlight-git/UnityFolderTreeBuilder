using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hlight.Unity.Editor.StructureTreeBuilder
{
    [CustomEditor(typeof(StructureTree))]
    public class StructureTreeEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> ExpandedNodes = new();
        private static readonly Color AsmBtnColor = new(0.3f, 0.7f, 1f);
        private static readonly Color RemoveBtnColor = new(1f, 0.4f, 0.4f);
        private static readonly Color WarningColor = new(1f, 0.8f, 0.2f);

        private SerializedProperty _rootPathProp;
        private SerializedProperty _rootProp;
        private string[] _availableAsmNames = System.Array.Empty<string>();
        private bool _hasDuplicateAsmNames;

        private void OnEnable()
        {
            _rootPathProp = serializedObject.FindProperty("rootPath");
            _rootProp = serializedObject.FindProperty("root");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RefreshAvailableAsmNames();

            DrawRootPath();
            EditorGUILayout.Space(6);
            DrawHeader();
            EditorGUILayout.Space(2);

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

            // Duplicate warning
            if (_hasDuplicateAsmNames)
            {
                EditorGUILayout.Space(4);
                var prev = GUI.contentColor;
                GUI.contentColor = WarningColor;
                EditorGUILayout.HelpBox("Duplicate assembly names detected!", MessageType.Warning);
                GUI.contentColor = prev;
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Create Structure", GUILayout.Height(28)))
            {
                ((StructureTree)target).CreateStructure();
                Debug.Log("[StructureTree] Structure created.");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawRootPath()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(_rootPathProp, new GUIContent("Root Path"));
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                var selected = EditorUtility.OpenFolderPanel("Select Root Path", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    var dataPath = Application.dataPath;
                    if (selected.StartsWith(dataPath))
                    {
                        _rootPathProp.stringValue = "Assets" + selected.Substring(dataPath.Length);
                        _rootPathProp.serializedObject.ApplyModifiedProperties();
                    }
                    else if (selected.EndsWith("/Assets") || selected.EndsWith("\\Assets"))
                    {
                        _rootPathProp.stringValue = "Assets";
                        _rootPathProp.serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        Debug.LogWarning("[StructureTree] Selected path must be inside the Assets folder.");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Structure Tree", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (_availableAsmNames.Length > 0)
                EditorGUILayout.LabelField($"{_availableAsmNames.Length} asm",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Width(40));

            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(28)))
                SetAllExpanded(_rootProp, true);
            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(36)))
                SetAllExpanded(_rootProp, false);

            EditorGUILayout.EndHorizontal();
        }

        private static void SetAllExpanded(SerializedProperty rootProp, bool expand)
        {
            if (expand)
                CollectAllPaths(rootProp, ExpandedNodes);
            else
                ExpandedNodes.Clear();
        }

        private static void CollectAllPaths(SerializedProperty prop, HashSet<string> paths)
        {
            if (prop == null || prop.managedReferenceValue == null) return;
            paths.Add(prop.propertyPath);
            var children = prop.FindPropertyRelative("children");
            if (children == null) return;
            for (int i = 0; i < children.arraySize; i++)
                CollectAllPaths(children.GetArrayElementAtIndex(i), paths);
        }

        private void RefreshAvailableAsmNames()
        {
            var tree = (StructureTree)target;
            var names = new List<string>();
            CollectAsmNames(tree.Root, names);
            _hasDuplicateAsmNames = names.Count != names.Distinct().Count();
            _availableAsmNames = names.ToArray();
        }

        private static void CollectAsmNames(StructureNode node, List<string> names)
        {
            if (node == null) return;
            if (node.asmdef != null && !string.IsNullOrWhiteSpace(node.asmdef.assemblyName))
                names.Add(node.asmdef.assemblyName);
            if (node.children == null) return;
            foreach (var child in node.children)
                CollectAsmNames(child, names);
        }

        private void DrawNodeList(SerializedProperty listProp, int indent)
        {
            if (listProp == null) return;

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
            if (element == null) return false;
            InitializeNode(element);

            var nameProp = element.FindPropertyRelative("name");
            var childrenProp = element.FindPropertyRelative("children");
            var asmdefProp = element.FindPropertyRelative("asmdef");
            if (nameProp == null) return false;

            var propPath = element.propertyPath;
            var expanded = ExpandedNodes.Contains(propPath);
            var hasAsmdef = asmdefProp?.managedReferenceValue != null;

            // --- Main row ---
            const float indentStep = 14f;
            const float foldoutW = 14f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * indentStep);

            // Fixed-width foldout
            var childCount = childrenProp?.arraySize ?? 0;
            var canExpand = childCount > 0 || hasAsmdef;
            var foldoutRect = GUILayoutUtility.GetRect(foldoutW, EditorGUIUtility.singleLineHeight,
                GUILayout.Width(foldoutW));
            if (canExpand)
            {
                var newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
                if (newExpanded != expanded)
                {
                    if (newExpanded) ExpandedNodes.Add(propPath);
                    else ExpandedNodes.Remove(propPath);
                    expanded = newExpanded;
                }
            }

            // Name
            nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);

            // Child count badge (when collapsed)
            if (!expanded && childCount > 0)
                EditorGUILayout.LabelField($"({childCount})", EditorStyles.centeredGreyMiniLabel,
                    GUILayout.Width(26));

            // ASM toggle
            if (hasAsmdef)
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = AsmBtnColor;
                if (GUILayout.Button("ASM", EditorStyles.miniButtonLeft, GUILayout.Width(36)))
                    asmdefProp.managedReferenceValue = null;
                GUI.backgroundColor = prev;
            }
            else
            {
                if (GUILayout.Button("+a", EditorStyles.miniButtonLeft, GUILayout.Width(36)))
                    asmdefProp.managedReferenceValue = new AsmdefConfig
                        { assemblyName = nameProp.stringValue };
            }

            // Add child
            if (GUILayout.Button("+", EditorStyles.miniButtonMid, GUILayout.Width(20)))
            {
                if (childrenProp != null)
                {
                    childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                    InitializeNode(childrenProp.GetArrayElementAtIndex(childrenProp.arraySize - 1));
                    ExpandedNodes.Add(propPath);
                }
            }

            // Remove
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = RemoveBtnColor;
            if (GUILayout.Button("\u2212", EditorStyles.miniButtonRight, GUILayout.Width(20)))
            {
                if (childCount > 0)
                {
                    if (!EditorUtility.DisplayDialog("Delete Node",
                            $"'{nameProp.stringValue}' has {childCount} children. Delete?",
                            "Delete", "Cancel"))
                    {
                        GUI.backgroundColor = prevBg;
                        EditorGUILayout.EndHorizontal();
                        if (expanded && hasAsmdef && asmdefProp != null)
                            DrawAsmdefConfig(asmdefProp, indent);
                        if (expanded && childCount > 0)
                            DrawNodeList(childrenProp, indent + 1);
                        return false;
                    }
                }

                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();
                return true;
            }
            GUI.backgroundColor = prevBg;

            EditorGUILayout.EndHorizontal();

            // --- Asmdef config (indented box) ---
            if (expanded && hasAsmdef && asmdefProp != null)
                DrawAsmdefConfig(asmdefProp, indent);

            // --- Children ---
            if (expanded && childCount > 0)
                DrawNodeList(childrenProp, indent + 1);

            return false;
        }

        private void DrawAsmdefConfig(SerializedProperty asmdefProp, int indent)
        {
            var asmNameProp = asmdefProp.FindPropertyRelative("assemblyName");
            var refsProp = asmdefProp.FindPropertyRelative("references");
            if (asmNameProp == null) return;

            var pad = (indent + 1) * 14f + 14f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(pad);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Assembly name
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Assembly", GUILayout.Width(58));
            asmNameProp.stringValue = EditorGUILayout.TextField(asmNameProp.stringValue);
            EditorGUILayout.EndHorizontal();

            if (refsProp != null)
                DrawReferenceList(refsProp, asmNameProp.stringValue);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawReferenceList(SerializedProperty refsProp, string selfName)
        {
            var choices = _availableAsmNames.Where(n => n != selfName).Distinct().ToList();
            // Append "Custom..." option at end
            var customTag = "\u270e Custom...";
            var popupChoices = new List<string>(choices) { customTag };
            var popupArray = popupChoices.ToArray();

            int removeIdx = -1;
            for (int i = 0; i < refsProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("\u2192", GUILayout.Width(16));

                var refElement = refsProp.GetArrayElementAtIndex(i);
                var currentVal = refElement.stringValue;
                var isCustom = choices.Count == 0 || !choices.Contains(currentVal);

                if (isCustom)
                {
                    // Editable text field for custom values
                    refElement.stringValue = EditorGUILayout.TextField(refElement.stringValue);
                    // Button to switch back to dropdown
                    if (choices.Count > 0 && GUILayout.Button("\u25bc", EditorStyles.miniButton,
                            GUILayout.Width(20)))
                        refElement.stringValue = choices[0];
                }
                else
                {
                    int idx = choices.IndexOf(currentVal);
                    if (idx < 0) idx = 0;
                    int newIdx = EditorGUILayout.Popup(idx, popupArray);
                    if (newIdx == popupChoices.Count - 1)
                        refElement.stringValue = ""; // Switch to custom mode
                    else
                        refElement.stringValue = choices[newIdx];
                }

                if (GUILayout.Button("\u2212", EditorStyles.miniButton, GUILayout.Width(20)))
                    removeIdx = i;

                EditorGUILayout.EndHorizontal();
            }

            if (removeIdx >= 0)
                refsProp.DeleteArrayElementAtIndex(removeIdx);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Ref", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                refsProp.InsertArrayElementAtIndex(refsProp.arraySize);
                refsProp.GetArrayElementAtIndex(refsProp.arraySize - 1).stringValue =
                    choices.Count > 0 ? choices[0] : "";
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void InitializeNode(SerializedProperty element)
        {
            if (element == null) return;

            if (element.propertyType == SerializedPropertyType.ManagedReference
                && element.managedReferenceValue == null)
                element.managedReferenceValue = new StructureNode();

            var nameProp = element.FindPropertyRelative("name");
            if (nameProp != null && string.IsNullOrEmpty(nameProp.stringValue))
                nameProp.stringValue = "New Folder";
        }
    }
}
