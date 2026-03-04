using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Octo.Editor
{
    /// <summary>
    /// Sets up PROCEDURAL tentacles - no physics at all!
    /// Just adds ProceduralTentacle components to limb roots.
    /// </summary>
    public class ProceduralTentacleSetup : EditorWindow
    {
        private GameObject targetRoot;
        private string armPattern = "arm";
        private string rootPattern = "01";

        [MenuItem("Tools/Octo/Procedural Tentacle Setup (Recommended)")]
        public static void ShowWindow()
        {
            var window = GetWindow<ProceduralTentacleSetup>("Procedural Setup");
            window.minSize = new Vector2(350, 350);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🐙 Procedural Tentacle Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "RECOMMENDED APPROACH!\n\n" +
                "This uses NO PHYSICS - just procedural animation.\n" +
                "• Idle animation plays normally\n" +
                "• Joystick input ADDS rotations on top\n" +
                "• No rigidbodies, no joints, no chaos!\n" +
                "• Works perfectly with Animator",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            targetRoot = EditorGUILayout.ObjectField("Octopus Root", targetRoot, typeof(GameObject), true) as GameObject;

            if (targetRoot == null && Selection.activeGameObject != null)
            {
                if (GUILayout.Button("Use Selected Object"))
                {
                    targetRoot = Selection.activeGameObject;
                }
            }

            EditorGUILayout.Space(10);
            armPattern = EditorGUILayout.TextField("Arm Pattern", armPattern);
            rootPattern = EditorGUILayout.TextField("Root Pattern", rootPattern);

            EditorGUILayout.Space(20);

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("🗑️ Remove ALL Physics & Procedural Components", GUILayout.Height(30)))
            {
                RemoveAll();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("✨ Setup Procedural Tentacles", GUILayout.Height(40)))
            {
                SetupProcedural();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(20);

            EditorGUILayout.LabelField("Keyboard Controls (Editor Testing)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Limb 0: WASD\n" +
                "Limb 1: TFGH\n" +
                "Limb 2: IJKL\n" +
                "Limb 3: Arrow Keys\n" +
                "Limb 4: Numpad 8456\n" +
                "Limb 5: YGHJ",
                MessageType.None
            );
        }

        private void RemoveAll()
        {
            if (targetRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target first.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Remove All");

            // Remove everything physics-related
            var joints = targetRoot.GetComponentsInChildren<ConfigurableJoint>();
            var rigidbodies = targetRoot.GetComponentsInChildren<Rigidbody>();
            var colliders = targetRoot.GetComponentsInChildren<CapsuleCollider>();
            var oldControllers = targetRoot.GetComponentsInChildren<Octo.Physics.LimbPhysicsController>();
            var tentacleControllers = targetRoot.GetComponentsInChildren<Octo.Physics.TentacleController>();
            var blenders = targetRoot.GetComponentsInChildren<Octo.Animation.AnimationPhysicsBlender>();
            var proceduralTentacles = targetRoot.GetComponentsInChildren<Octo.Animation.ProceduralTentacle>();

            foreach (var c in joints) DestroyImmediate(c);
            foreach (var c in oldControllers) DestroyImmediate(c);
            foreach (var c in tentacleControllers) DestroyImmediate(c);
            foreach (var c in blenders) DestroyImmediate(c);
            foreach (var c in proceduralTentacles) DestroyImmediate(c);
            foreach (var c in rigidbodies) DestroyImmediate(c);
            foreach (var c in colliders) DestroyImmediate(c);

            Debug.Log("[ProceduralSetup] Removed all components!");
            EditorUtility.DisplayDialog("Done", "All physics and procedural components removed.", "OK");
        }

        private void SetupProcedural()
        {
            if (targetRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target first.", "OK");
                return;
            }

            // First remove all old stuff
            RemoveAll();

            Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Setup Procedural");

            // Find limb roots
            var limbRoots = FindLimbRoots();
            if (limbRoots.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning",
                    $"No limb roots found matching '{armPattern}' + '{rootPattern}'", "OK");
                return;
            }

            int limbIndex = 0;
            foreach (var limbRoot in limbRoots)
            {
                // Add ProceduralTentacle component
                var tentacle = limbRoot.gameObject.AddComponent<Octo.Animation.ProceduralTentacle>();

                // Set limb index
                var so = new SerializedObject(tentacle);
                so.FindProperty("limbIndex").intValue = limbIndex;
                so.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log($"[ProceduralSetup] Limb {limbIndex}: {limbRoot.name}");
                limbIndex++;
            }

            Debug.Log($"[ProceduralSetup] Set up {limbIndex} procedural tentacles!");
            EditorUtility.DisplayDialog("Success",
                $"Set up {limbIndex} procedural tentacles!\n\n" +
                "Test with keyboard:\n" +
                "• WASD for limb 0\n" +
                "• TFGH for limb 1\n" +
                "• IJKL for limb 2\n" +
                "• Arrows for limb 3", "OK");
        }

        private List<Transform> FindLimbRoots()
        {
            var roots = new List<Transform>();
            var allTransforms = targetRoot.GetComponentsInChildren<Transform>();

            foreach (var t in allTransforms)
            {
                string name = t.name.ToLower();
                if (name.Contains(armPattern.ToLower()) && name.Contains(rootPattern.ToLower()))
                {
                    roots.Add(t);
                }
            }

            // Sort by sibling index for consistent ordering
            roots.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            return roots;
        }
    }
}
