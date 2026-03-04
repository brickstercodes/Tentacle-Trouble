using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Octo.Editor
{
    /// <summary>
    /// SIMPLIFIED limb setup - NO JOINTS, just rigidbodies.
    /// The AnimationPhysicsBlender will control whether animation or physics is active.
    /// </summary>
    public class SimpleLimbSetup : EditorWindow
    {
        private GameObject targetRoot;
        private string armPattern = "arm";
        private string rootPattern = "01";

        [MenuItem("Tools/Octo/Simple Limb Setup (No Joints)")]
        public static void ShowWindow()
        {
            var window = GetWindow<SimpleLimbSetup>("Simple Limb Setup");
            window.minSize = new Vector2(350, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🐙 Simple Limb Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This creates a SIMPLE physics setup:\n" +
                "• NO ConfigurableJoints (no stretching!)\n" +
                "• Just Rigidbodies for physics\n" +
                "• Animation controls bones when idle\n" +
                "• Forces control bones when input detected",
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
            if (GUILayout.Button("🗑️ Remove ALL Physics Components", GUILayout.Height(30)))
            {
                RemoveAllPhysics();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("✨ Setup Simple Physics", GUILayout.Height(40)))
            {
                SetupSimplePhysics();
            }
            GUI.backgroundColor = Color.white;
        }

        private void RemoveAllPhysics()
        {
            if (targetRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target first.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Remove Physics");

            // Remove everything
            var joints = targetRoot.GetComponentsInChildren<ConfigurableJoint>();
            var rigidbodies = targetRoot.GetComponentsInChildren<Rigidbody>();
            var colliders = targetRoot.GetComponentsInChildren<CapsuleCollider>();
            var oldControllers = targetRoot.GetComponentsInChildren<Octo.Physics.LimbPhysicsController>();
            var tentacleControllers = targetRoot.GetComponentsInChildren<Octo.Physics.TentacleController>();
            var blenders = targetRoot.GetComponentsInChildren<Octo.Animation.AnimationPhysicsBlender>();

            foreach (var c in joints) DestroyImmediate(c);
            foreach (var c in oldControllers) DestroyImmediate(c);
            foreach (var c in tentacleControllers) DestroyImmediate(c);
            foreach (var c in blenders) DestroyImmediate(c);
            foreach (var c in rigidbodies) DestroyImmediate(c);
            foreach (var c in colliders) DestroyImmediate(c);

            Debug.Log("[SimpleLimbSetup] Removed all physics components!");
            EditorUtility.DisplayDialog("Done", "All physics components removed.", "OK");
        }

        private void SetupSimplePhysics()
        {
            if (targetRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a target first.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Setup Simple Physics");

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
                SetupLimb(limbRoot, limbIndex);
                limbIndex++;
            }

            // Add AnimationPhysicsBlender
            var animator = targetRoot.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var blender = animator.gameObject.GetComponent<Octo.Animation.AnimationPhysicsBlender>();
                if (blender == null)
                {
                    blender = animator.gameObject.AddComponent<Octo.Animation.AnimationPhysicsBlender>();
                }
            }

            Debug.Log($"[SimpleLimbSetup] Set up {limbIndex} limbs with simple physics!");
            EditorUtility.DisplayDialog("Success",
                $"Set up {limbIndex} limbs!\n\n" +
                "The octopus will:\n" +
                "• Play idle animation normally\n" +
                "• Switch to physics when you press WASD/IJKL/Arrows", "OK");
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

        private void SetupLimb(Transform limbRoot, int limbIndex)
        {
            // Collect all segments (the limb and its children)
            var segments = new List<Transform> { limbRoot };
            CollectChildren(limbRoot, segments);

            // Add Rigidbody to each segment (NO JOINTS!)
            foreach (var segment in segments)
            {
                var rb = segment.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = segment.gameObject.AddComponent<Rigidbody>();
                }

                // Configure rigidbody for stability
                rb.mass = 0.1f;
                rb.linearDamping = 5f;
                rb.angularDamping = 5f;
                rb.useGravity = false;  // No gravity - we control with forces
                rb.isKinematic = true;  // Start kinematic (animation mode)
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            // Add TentacleController to root
            var controller = limbRoot.GetComponent<Octo.Physics.TentacleController>();
            if (controller == null)
            {
                controller = limbRoot.gameObject.AddComponent<Octo.Physics.TentacleController>();
            }

            // Configure controller
            var so = new SerializedObject(controller);
            so.FindProperty("limbIndex").intValue = limbIndex;
            so.FindProperty("reachDistance").floatValue = 1.5f;
            so.FindProperty("pullForce").floatValue = 30f;
            so.FindProperty("segmentDamping").floatValue = 3f;
            so.FindProperty("returnForce").floatValue = 15f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[SimpleLimbSetup] Limb {limbIndex}: {limbRoot.name} ({segments.Count} segments)");
        }

        private void CollectChildren(Transform parent, List<Transform> list)
        {
            foreach (Transform child in parent)
            {
                list.Add(child);
                CollectChildren(child, list);
            }
        }
    }
}
