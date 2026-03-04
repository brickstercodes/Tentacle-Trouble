using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Octo.Editor
{
    /// <summary>
    /// Editor utility to automatically set up Configurable Joints and Rigidbodies
    /// on an octopus rig's tentacle bones.
    /// 
    /// USAGE:
    /// 1. Select your octopus model's root GameObject in the hierarchy.
    /// 2. Go to Tools > Octo > Setup Limb Physics
    /// 3. Configure the naming pattern for your tentacle bones.
    /// 4. Click "Setup All Limbs"
    /// </summary>
    public class LimbSetupUtility : EditorWindow
    {
        [System.Serializable]
        public class LimbSetupConfig
        {
            public string boneNamePattern = "arm";          // Bones must contain this (e.g., "arm")
            public string rootBonePattern = "01";           // AND must contain this to be a root (e.g., "01")
            public bool useHierarchyOrder = true;           // Assign limb indices by hierarchy order
            public float limbMass = 0.5f;
            public float segmentMass = 0.2f;
            public float colliderRadius = 0.02f;
            public bool addCapsuleColliders = false;  // Disabled by default - less chaos

            [Header("Joint Settings")]
            public float driveSpring = 50f;      // Much lower for stability
            public float driveDamper = 20f;      // Higher damping
            public float maxForce = 100f;        // Much lower max force
            public float angularLimit = 45f;     // Smaller range
        }

        private GameObject targetRoot;
        private LimbSetupConfig config = new LimbSetupConfig();
        private Vector2 scrollPos;
        private List<Transform> detectedLimbs = new List<Transform>();
        private bool showPreview = false;

        [MenuItem("Tools/Octo/Setup Limb Physics")]
        public static void ShowWindow()
        {
            var window = GetWindow<LimbSetupUtility>("Limb Setup Utility");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("🐙 Octo Limb Setup Utility", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool automatically configures Configurable Joints and Rigidbodies " +
                "on your octopus tentacle bones for active ragdoll physics.",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            // Target Selection
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            targetRoot = EditorGUILayout.ObjectField("Octopus Root", targetRoot, typeof(GameObject), true) as GameObject;

            if (targetRoot == null && Selection.activeGameObject != null)
            {
                if (GUILayout.Button("Use Selected Object"))
                {
                    targetRoot = Selection.activeGameObject;
                }
            }

            EditorGUILayout.Space(10);

            // Configuration
            EditorGUILayout.LabelField("Bone Detection", EditorStyles.boldLabel);
            config.boneNamePattern = EditorGUILayout.TextField("Must Contain", config.boneNamePattern);
            config.rootBonePattern = EditorGUILayout.TextField("Root Identifier", config.rootBonePattern);
            EditorGUILayout.HelpBox(
                "Root bones must contain BOTH patterns.\n" +
                "E.g., 'arm' + '01' matches: Arm01_L, ArmBK01_R, ArmFR01_L",
                MessageType.None
            );
            config.useHierarchyOrder = EditorGUILayout.Toggle("Use Hierarchy Order", config.useHierarchyOrder);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Physics Settings", EditorStyles.boldLabel);
            config.limbMass = EditorGUILayout.FloatField("Limb Root Mass", config.limbMass);
            config.segmentMass = EditorGUILayout.FloatField("Segment Mass", config.segmentMass);
            config.addCapsuleColliders = EditorGUILayout.Toggle("Add Capsule Colliders", config.addCapsuleColliders);
            if (config.addCapsuleColliders)
            {
                config.colliderRadius = EditorGUILayout.FloatField("Collider Radius", config.colliderRadius);
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Joint Settings", EditorStyles.boldLabel);
            config.driveSpring = EditorGUILayout.FloatField("Drive Spring", config.driveSpring);
            config.driveDamper = EditorGUILayout.FloatField("Drive Damper", config.driveDamper);
            config.maxForce = EditorGUILayout.FloatField("Max Force", config.maxForce);
            config.angularLimit = EditorGUILayout.Slider("Angular Limit (°)", config.angularLimit, 10f, 180f);

            EditorGUILayout.Space(15);

            // Preview Section
            if (targetRoot != null)
            {
                if (GUILayout.Button("🔍 Detect Limbs"))
                {
                    DetectLimbs();
                    showPreview = true;
                }

                if (showPreview && detectedLimbs.Count > 0)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField($"Detected {detectedLimbs.Count} Limbs:", EditorStyles.boldLabel);

                    EditorGUI.indentLevel++;
                    for (int i = 0; i < detectedLimbs.Count; i++)
                    {
                        EditorGUILayout.LabelField($"Limb {i}: {detectedLimbs[i].name}");
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(10);

                GUI.backgroundColor = Color.green;
                if (GUILayout.Button("⚡ Setup All Limbs", GUILayout.Height(40)))
                {
                    SetupAllLimbs();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(5);

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("🗑 Remove Physics Components"))
                {
                    if (EditorUtility.DisplayDialog("Confirm",
                        "This will remove all Rigidbodies, Configurable Joints, and LimbPhysicsController components from the target. Continue?",
                        "Yes", "Cancel"))
                    {
                        RemovePhysicsComponents();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(20);

            // Quick Tips
            EditorGUILayout.LabelField("Quick Tips", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• Bone names should contain 'tentacle' or your custom pattern\n" +
                "• Each tentacle should have child segments for realistic physics\n" +
                "• Adjust Drive Spring for stiffness (higher = snappier)\n" +
                "• Adjust Drive Damper to reduce oscillation",
                MessageType.None
            );

            EditorGUILayout.EndScrollView();
        }

        private void DetectLimbs()
        {
            detectedLimbs.Clear();

            if (targetRoot == null) return;

            // Find all transforms matching BOTH patterns
            var allTransforms = targetRoot.GetComponentsInChildren<Transform>();

            string pattern1 = config.boneNamePattern.ToLower();
            string pattern2 = config.rootBonePattern.ToLower();

            foreach (var t in allTransforms)
            {
                string nameLower = t.name.ToLower();

                // Must contain BOTH patterns to be a root bone
                bool matchesPattern1 = string.IsNullOrEmpty(pattern1) || nameLower.Contains(pattern1);
                bool matchesPattern2 = string.IsNullOrEmpty(pattern2) || nameLower.Contains(pattern2);

                if (matchesPattern1 && matchesPattern2)
                {
                    detectedLimbs.Add(t);
                }
            }

            // Sort by hierarchy order if needed
            if (config.useHierarchyOrder)
            {
                detectedLimbs.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
            }

            Debug.Log($"[LimbSetupUtility] Detected {detectedLimbs.Count} limbs");
        }

        private void SetupAllLimbs()
        {
            if (targetRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a target root object.", "OK");
                return;
            }

            DetectLimbs();

            if (detectedLimbs.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning",
                    $"No limbs detected! Make sure your bone names contain '{config.boneNamePattern}'.\n\n" +
                    "Tip: Check the hierarchy structure of your FBX model.",
                    "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Setup Limb Physics");

            int limbIndex = 0;
            foreach (var limbRoot in detectedLimbs)
            {
                // No hard limit - set up all detected limbs

                SetupLimbChain(limbRoot, limbIndex);
                limbIndex++;
            }

            // Add AnimationPhysicsBlender to the root
            SetupAnimationPhysicsBlender();

            Debug.Log($"[LimbSetupUtility] Successfully set up {limbIndex} limbs!");
            EditorUtility.DisplayDialog("Success", $"Set up {limbIndex} limbs with physics components!\n\nDon't forget to:\n1. Set up an Animator Controller with 'IdleChair01' animation\n2. Configure the AnimationPhysicsBlender component", "OK");
        }

        private void SetupLimbChain(Transform limbRoot, int limbIndex)
        {
            // Get all segments in this limb (including root)
            List<Transform> segments = new List<Transform> { limbRoot };
            CollectSegments(limbRoot, segments);

            Transform previousSegment = null;
            Rigidbody previousRb = null;

            for (int i = 0; i < segments.Count; i++)
            {
                Transform segment = segments[i];
                bool isRoot = (i == 0);

                // Add/configure Rigidbody
                Rigidbody rb = segment.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = segment.gameObject.AddComponent<Rigidbody>();
                }

                rb.mass = isRoot ? config.limbMass : config.segmentMass;
                rb.useGravity = true;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 2f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // Add collider if requested
                if (config.addCapsuleColliders)
                {
                    CapsuleCollider capsule = segment.GetComponent<CapsuleCollider>();
                    if (capsule == null)
                    {
                        capsule = segment.gameObject.AddComponent<CapsuleCollider>();
                    }

                    capsule.radius = config.colliderRadius;
                    capsule.height = config.colliderRadius * 4f;
                    capsule.direction = 1; // Y-axis
                }

                // Add/configure ConfigurableJoint (connected to parent)
                if (previousRb != null)
                {
                    ConfigurableJoint joint = segment.GetComponent<ConfigurableJoint>();
                    if (joint == null)
                    {
                        joint = segment.gameObject.AddComponent<ConfigurableJoint>();
                    }

                    ConfigureJoint(joint, previousRb, isRoot);
                }
                else if (isRoot)
                {
                    // Root limb needs a joint connected to the body
                    ConfigurableJoint joint = segment.GetComponent<ConfigurableJoint>();
                    if (joint == null)
                    {
                        joint = segment.gameObject.AddComponent<ConfigurableJoint>();
                    }

                    // Find parent rigidbody (the octopus body)
                    Rigidbody parentRb = FindParentRigidbody(segment);
                    ConfigureJoint(joint, parentRb, true);
                }

                previousSegment = segment;
                previousRb = rb;
            }

            // Add TentacleController to root (NEW - Octodad-style controller)
            var oldController = limbRoot.GetComponent<Octo.Physics.LimbPhysicsController>();
            if (oldController != null)
            {
                DestroyImmediate(oldController);
            }

            var tentacleController = limbRoot.GetComponent<Octo.Physics.TentacleController>();
            if (tentacleController == null)
            {
                tentacleController = limbRoot.gameObject.AddComponent<Octo.Physics.TentacleController>();
            }

            // Configure the tentacle controller
            SerializedObject so = new SerializedObject(tentacleController);
            so.FindProperty("limbIndex").intValue = limbIndex;
            so.FindProperty("reachDistance").floatValue = 1.5f;
            so.FindProperty("pullForce").floatValue = config.driveSpring;
            so.FindProperty("segmentDamping").floatValue = config.driveDamper * 0.1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[LimbSetupUtility] Set up limb {limbIndex}: {limbRoot.name} with {segments.Count} segments");
        }

        private void CollectSegments(Transform parent, List<Transform> segments)
        {
            // For your rig: Arm01 -> Arm02 -> Arm03 -> Arm04
            // Simply collect all direct children recursively
            foreach (Transform child in parent)
            {
                // Add any child bone as a segment
                // The naming pattern check is optional - if bones follow
                // a consistent naming (Arm02, Arm03...) they'll be children anyway
                segments.Add(child);
                CollectSegments(child, segments);
            }
        }

        private Rigidbody FindParentRigidbody(Transform child)
        {
            Transform current = child.parent;
            while (current != null)
            {
                Rigidbody rb = current.GetComponent<Rigidbody>();
                if (rb != null) return rb;
                current = current.parent;
            }
            return null;
        }

        private void ConfigureJoint(ConfigurableJoint joint, Rigidbody connectedBody, bool isRoot)
        {
            joint.connectedBody = connectedBody;

            // Lock position
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            // Free angular motion
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            // Slerp drive
            JointDrive slerpDrive = new JointDrive
            {
                positionSpring = isRoot ? config.driveSpring : config.driveSpring * 0.5f,
                positionDamper = config.driveDamper,
                maximumForce = config.maxForce
            };

            joint.slerpDrive = slerpDrive;
            joint.rotationDriveMode = RotationDriveMode.Slerp;

            // Auto-configure connected anchor
            joint.autoConfigureConnectedAnchor = true;
        }

        private void RemovePhysicsComponents()
        {
            if (targetRoot == null) return;

            Undo.RegisterFullObjectHierarchyUndo(targetRoot, "Remove Limb Physics");

            var oldControllers = targetRoot.GetComponentsInChildren<Octo.Physics.LimbPhysicsController>();
            var tentacleControllers = targetRoot.GetComponentsInChildren<Octo.Physics.TentacleController>();
            var blenders = targetRoot.GetComponentsInChildren<Octo.Animation.AnimationPhysicsBlender>();
            var joints = targetRoot.GetComponentsInChildren<ConfigurableJoint>();
            var rigidbodies = targetRoot.GetComponentsInChildren<Rigidbody>();
            var colliders = targetRoot.GetComponentsInChildren<CapsuleCollider>();

            foreach (var c in oldControllers) DestroyImmediate(c);
            foreach (var c in tentacleControllers) DestroyImmediate(c);
            foreach (var b in blenders) DestroyImmediate(b);
            foreach (var j in joints) DestroyImmediate(j);
            foreach (var r in rigidbodies) DestroyImmediate(r);
            foreach (var c in colliders) DestroyImmediate(c);

            Debug.Log($"[LimbSetupUtility] Removed all physics components");
        }

        private void SetupAnimationPhysicsBlender()
        {
            // Find the object with Animator (usually the root or a child with the skeleton)
            Animator animator = targetRoot.GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogWarning("[LimbSetupUtility] No Animator found. AnimationPhysicsBlender not added. " +
                    "You'll need to add it manually once you set up the Animator.");
                return;
            }

            // Add AnimationPhysicsBlender if not already present
            var blender = animator.gameObject.GetComponent<Octo.Animation.AnimationPhysicsBlender>();
            if (blender == null)
            {
                blender = animator.gameObject.AddComponent<Octo.Animation.AnimationPhysicsBlender>();
                Debug.Log($"[LimbSetupUtility] Added AnimationPhysicsBlender to {animator.gameObject.name}");
            }
            else
            {
                Debug.Log($"[LimbSetupUtility] AnimationPhysicsBlender already exists on {animator.gameObject.name}");
            }
        }
    }
}
