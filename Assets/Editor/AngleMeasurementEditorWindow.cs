/// <summary>
/// <param>/<Authuor> mjayjinad
/// An EditorWindow that allows selecting three vertices on a mesh and visualizing the angle formed.
/// Provides both an overlay GUI label and a TextMeshPro in-scene label.
/// </summary>
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TMPro;

namespace mjayjinad
{
    public class AngleMeasurementEditorWindow : EditorWindow
    {
        public bool showOverlayLabel = true; // Toggle to show/hide the GUI overlay label.
        
        public bool showTMPLabel = true; // Toggle to create/destroy the TextMeshPro scene label.

        private GameObject targetObject; // Currently selected model
        private Vector3[] worldVertices; // world-space vertices
        private bool hasValidMesh;  // Whether targetObject has a mesh
        private List<Vector3> points = new List<Vector3>(); // Selected points
        private GUIStyle labelStyle; // Style for overlay label
        private TextMeshPro tmpLabel; // TMP label text to display angle

        [MenuItem("Tools/mjayjinad/Angle Measurement Tool")]
        public static void ShowWindow()
        {
            // Open the window (or bring it to focus)
            var window = GetWindow<AngleMeasurementEditorWindow>("Angle Tool");
            window.Init();
        }

        /// <summary>
        /// Initializes styles and subscribes to Scene GUI callbacks.
        /// </summary>
        private void Init()
        {
            points.Clear();
            hasValidMesh = false;

            // Setup label style for GUI overlay
            labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.black },
                alignment = TextAnchor.MiddleCenter
            };

            // Remove any existing TMP label object
            if (tmpLabel != null)
            {
                DestroyImmediate(tmpLabel.gameObject);
                tmpLabel = null;
            }

            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Cleanup when window closes.
        /// </summary>
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            if (tmpLabel != null)
            {
                DestroyImmediate(tmpLabel.gameObject);
                tmpLabel = null;
            }
        }

        /// <summary>
        /// Draws the custom EditorWindow UI.
        /// </summary>
        private void OnGUI()
        {
            GUILayout.Label("3D Point Angle Tool", EditorStyles.largeLabel);

            // Toggles for displaying each label type
            showOverlayLabel = EditorGUILayout.Toggle("Show Overlay Label", showOverlayLabel);
            showTMPLabel = EditorGUILayout.Toggle("Show TMP Label", showTMPLabel);

            // If TMP label disabled, remove existing gameobject
            if (!showTMPLabel && tmpLabel != null)
            {
                DestroyImmediate(tmpLabel.gameObject);
                tmpLabel = null;
                SceneView.RepaintAll();
            }

            // Select the target model in the scene
            EditorGUI.BeginChangeCheck();
            targetObject = (GameObject)EditorGUILayout.ObjectField("Target Model:", targetObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateTargetMesh();
                points.Clear();
                if (tmpLabel != null)
                {
                    DestroyImmediate(tmpLabel.gameObject);
                    tmpLabel = null;
                }
                SceneView.RepaintAll();
                Repaint();
            }

            // If selected object is invalid, show an error
            if (targetObject != null && !hasValidMesh)
            {
                EditorGUILayout.HelpBox("Selected object has no MeshFilter or MeshCollider with a mesh.", MessageType.Error);
            }

            // Show instructions based on number of points selected
            string instruction = points.Count == 0 ? "Select first point on model" :
                                 points.Count == 1 ? "Select second point" :
                                 points.Count == 2 ? "Select third point" :
                                 "Measurement complete";
            EditorGUILayout.HelpBox(instruction, MessageType.Info);

            // Buttons to clear points
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Last Point") && points.Count > 0)
            {
                points.RemoveAt(points.Count - 1);
                if (tmpLabel != null)
                {
                    DestroyImmediate(tmpLabel.gameObject);
                    tmpLabel = null;
                }
                SceneView.RepaintAll();
                Repaint();
            }
            if (GUILayout.Button("Clear All Points"))
            {
                points.Clear();
                if (tmpLabel != null)
                {
                    DestroyImmediate(tmpLabel.gameObject);
                    tmpLabel = null;
                }
                SceneView.RepaintAll();
                Repaint();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"Points Selected: {points.Count}/3");
        }

        /// <summary>
        /// Gets the mesh vertices from the selected object.
        /// </summary>
        private void UpdateTargetMesh()
        {
            hasValidMesh = false;
            worldVertices = null;
            if (targetObject == null) return;

            // Try MeshFilter first, then MeshCollider
            Mesh mesh = null;
            MeshFilter mf = targetObject.GetComponent<MeshFilter>();
            if (mf != null)
                mesh = mf.sharedMesh;
            else if (targetObject.TryGetComponent(out MeshCollider mc))
                mesh = mc.sharedMesh;

            if (mesh != null)
            {
                hasValidMesh = true;
                Vector3[] verts = mesh.vertices;
                worldVertices = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                    worldVertices[i] = targetObject.transform.TransformPoint(verts[i]);
            }
            else
            {
                Debug.LogError($"AngleMeasurementTool: Target object '{targetObject.name}' has no mesh or collider.");
            }
        }

        /// <summary>
        /// Handles Scene view input and drawing of handles and labels.
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            // Draw vertex highlights
            if (worldVertices != null)
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual; //Enables handles occlusion by mesh
                Handles.color = Color.cyan;
                foreach (Vector3 v in worldVertices)
                    Handles.SphereHandleCap(0, v, Quaternion.identity, 0.03f, EventType.Repaint);
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            }

            // On click, select nearest vertex with a maximum of 3
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && //Check to ensure we are not in flymode scene navigation mode with ALT pressed.
                targetObject != null && points.Count < 3 && worldVertices != null)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == targetObject)
                {
                    // Find closest vertex
                    Vector3 closest = worldVertices[0];
                    float minDist = Vector3.Distance(hit.point, closest);
                    foreach (Vector3 v in worldVertices)
                    {
                        float d = Vector3.Distance(hit.point, v);
                        if (d < minDist)
                        {
                            minDist = d; closest = v;
                        }
                    }
                    if (!points.Contains(closest))
                        points.Add(closest);

                    e.Use();
                    SceneView.RepaintAll();
                    Repaint();
                }
            }

            // Draw selected point handles
            if (points.Count > 0)
            {
                Handles.color = Color.yellow;
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                foreach (Vector3 p in points)
                    Handles.SphereHandleCap(0, p, Quaternion.identity, 0.03f, EventType.Repaint);
            }

            // When 3 points are selected, draw angle
            if (points.Count == 3)
            {
                Vector3 A = points[0], B = points[1], C = points[2];
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                Handles.DrawLine(B, A);
                Handles.DrawLine(B, C);

                Vector3 BA = (A - B).normalized;
                Vector3 BC = (C - B).normalized;
                float angle = Vector3.Angle(BA, BC);

                Vector3 normal = Vector3.Cross(BA, BC).normalized;
                float radius = 0.03f;
                Handles.DrawSolidArc(B, normal, BA, angle, radius);

                // Calculate label position above arc
                Vector3 bis = (BA + BC).normalized;
                Vector3 basePos = B + bis * radius;
                Vector3 offsetUp = Vector3.up * radius * 0.2f;
                Vector3 labelPos = basePos + offsetUp;

                // Draw overlay GUI label if enabled
                if (showOverlayLabel)
                {
                    Vector2 guiPos = HandleUtility.WorldToGUIPoint(labelPos);
                    Handles.BeginGUI();
                    Rect rect = new Rect(guiPos.x - 20, guiPos.y - 10, 40, 20);
                    GUI.Label(rect, $"{angle:F1}°", labelStyle);
                    Handles.EndGUI();
                }

                // Draw or update TMP label if enabled
                if (showTMPLabel)
                {
                    if (tmpLabel == null)
                    {
                        GameObject go = new GameObject("AngleLabel_TMP");
                        go.hideFlags = HideFlags.NotEditable;
                        tmpLabel = go.AddComponent<TextMeshPro>();
                        tmpLabel.alignment = TextAlignmentOptions.Center;
                        tmpLabel.fontSize = 24;
                        tmpLabel.color = Color.black;
                    }
                    tmpLabel.text = $"{angle:F1}°";
                    tmpLabel.transform.position = labelPos;
                    tmpLabel.transform.rotation = sceneView.camera.transform.rotation;
                    tmpLabel.transform.localScale = Vector3.one * 0.03f;
                }
            }

            sceneView.Repaint();
        }
    }
}
