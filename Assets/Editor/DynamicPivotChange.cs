/// <summary>
/// <param>/<Author> mjayjinad
/// Editor window that allows users to select a target mesh, place a dynamic pivot point,
/// and rotate the model around that pivot using drag gestures.
/// </summary>

using UnityEngine;
using UnityEditor;

namespace mjayjinad
{
    public class PivotToolWindow : EditorWindow
    {
        private Transform targetModel; // The Transform of the GameObject to affect
        private bool toolActive = false; // Whether the pivot tool is currently active
        private Vector3 pivotPoint; // World-space position of the custom pivot
        private bool hasPivot = false; // Tracks if a pivot has been placed
        private float rotationSpeed = 0.4f; // Sensitivity for drag-based rotation
        private Vector2 lastMousePos; // Stores mouse position from last frame to compute delta movement
        private Tool previousTool = Tool.Move; // The Unity tool (move/rotate/scale) active before hiding it
        private string errorMessage = null; // Holds error messages for invalid target selections

        /// <summary>
        /// Adds the Pivot Tool window to the Unity menu under Tools/mjayjinad.
        /// </summary>
        [MenuItem("Tools/mjayjinad/Pivot Tool")]
        public static void ShowWindow()
        {
            GetWindow<PivotToolWindow>("Pivot Tool");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI; //Subscribe to the SceneView GUI delegate when the window is enabled.
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI; //Unsubscribe on disable and restore the previous Unity tool.
            Tools.current = previousTool;
        }

        /// <summary>
        /// Draws the custom editor GUI in the window.
        /// </summary>
        private void OnGUI()
        {
            GUILayout.Label("Pivot Tool Settings", EditorStyles.boldLabel);

            // Toggle to activate/deactivate the pivot tool
            toolActive = EditorGUILayout.Toggle("Enable Pivot Tool", toolActive);

            // Field for assigning the target model (must be Transform)
            EditorGUI.BeginChangeCheck();
            Transform newTarget = (Transform)EditorGUILayout.ObjectField("Target Model", targetModel, typeof(Transform), true);
            if (EditorGUI.EndChangeCheck())
            {
                targetModel = newTarget;
                // Validate that the selected object has a mesh and collider
                ValidateTargetModel();
            }

            // Float field to adjust rotation speed at runtime
            rotationSpeed = EditorGUILayout.FloatField("Rotation Speed", rotationSpeed);

            // Show error messages in the tool window if validation failed
            if (!string.IsNullOrEmpty(errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }

            // Button to clear the current pivot and restore the default gizmo
            if (GUILayout.Button("Clear Pivot"))
            {
                hasPivot = false;
                Tools.current = previousTool;
                Repaint();
            }
        }

        /// <summary>
        /// Validates the selected targetModel to ensure it has mesh and collider components.
        /// Displays errors and logs to console if invalid.
        /// </summary>
        private void ValidateTargetModel()
        {
            errorMessage = null;
            if (targetModel == null)
                return;

            // Check for MeshFilter or SkinnedMeshRenderer
            bool hasMesh = targetModel.GetComponent<MeshFilter>() != null
                           || targetModel.GetComponent<SkinnedMeshRenderer>() != null;
            if (!hasMesh)
            {
                errorMessage = "Selected GameObject has no MeshFilter or SkinnedMeshRenderer.";
                Debug.LogError(errorMessage, targetModel.gameObject);
                return;
            }

            // Check for Collider component for raycasting
            if (targetModel.GetComponent<Collider>() == null)
            {
                errorMessage = "Selected GameObject has no Collider component for pivot placement.";
                Debug.LogError(errorMessage, targetModel.gameObject);
            }
        }

        /// <summary>
        /// Handles Scene View interactions: pivot placement and drag based rotation.
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            // Only run logic if tool is active, target is valid, and no errors exist
            if (!toolActive || targetModel == null || !string.IsNullOrEmpty(errorMessage)) return;

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;

            // Handle mouse down to set or clear pivot
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                // Raycast against the registered model
                if (Physics.Raycast(ray, out hit) && (hit.transform == targetModel || hit.transform.IsChildOf(targetModel)))
                {
                    pivotPoint = hit.point;
                    hasPivot = true;
                    lastMousePos = e.mousePosition;

                    // Store and disable Unity's default tool to hide its gizmo
                    previousTool = Tools.current;
                    Tools.current = Tool.None;

                    // Keep the target selected in the Inspector
                    Selection.activeTransform = targetModel;

                    e.Use();
                }
                else
                {
                    // Clicked outside target: disable tool and clear pivot
                    toolActive = false;
                    hasPivot = false;
                    Tools.current = previousTool;
                    Repaint();
                    e.Use();
                }
            }

            // Handles drag to rotate around the custom pivot
            if (hasPivot && e.type == EventType.MouseDrag && e.button == 0 && !e.alt)
            {
                Vector2 delta = e.mousePosition - lastMousePos;
                Undo.RecordObject(targetModel, "Rotate Around Pivot");

                // Perform horizontal drag around camera up axis
                targetModel.RotateAround(pivotPoint, sceneView.camera.transform.up, delta.x * rotationSpeed);
                // Perform vertical drag around camera right axis
                targetModel.RotateAround(pivotPoint, sceneView.camera.transform.right, -delta.y * rotationSpeed);

                lastMousePos = e.mousePosition;
                e.Use();
                sceneView.Repaint();
            }

            // Draw only the custom move gizmo at the pivot location
            if (hasPivot)
            {
                Handles.PositionHandle(pivotPoint, targetModel.rotation);
            }
        }
    }
}