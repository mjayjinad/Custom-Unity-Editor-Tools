/// <summary>
/// <param>/<Author> mjayjinad
/// MiniMapNavigatorWindow provides a dockable Unity Editor Window
/// displaying a clickable top-down mini-map of a selected model in the Scene View.
/// Users can click on the mini-map to reposition the Scene View camera pivot,
/// frame the model, and visualize the model's bounding box.
/// </summary>

using UnityEngine;
using UnityEditor;

namespace mjayjinad
{
    public class MiniMapNavigatorWindow : EditorWindow
    {
        // Constants defining mini-map dimensions and screen margin
        private const float mapWidth = 256f;
        private const float mapHeight = 256f;
        private const float margin = 10f;

        private bool smoothMove = true;     // Enable smooth camera transition
        private float flyTime = 0.3f;       // Duration of smooth camera fly-to
        private float paddingFactor = 2f;   // Extra padding around model when framing
        private bool isEnabled = false;     // Toggle mini-map overlay on/off

        private GameObject targetModel;     // The GameObject the mini-map focuses on

        private Camera mapCamera;           // Hidden camera rendering the top-down view
        private RenderTexture mapTexture;   // RenderTexture holding the mini-map image
        private Bounds modelBounds;         // Cached bounds of the target model

        // State for camera fly-to animation
        private bool isDragging = false;
        private Vector3 flyStartPos;
        private Vector3 flyEndPos;
        private float flyStartTime;

        /// <summary>
        /// Adds a menu item under Tools to open the MiniMap Navigator Window.
        /// </summary>
        [MenuItem("Tools/MiniMap Navigator")]
        public static void OpenWindow()
        {
            var win = GetWindow<MiniMapNavigatorWindow>("MiniMap Navigator");
            win.minSize = new Vector2(300, 420);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            InitializeCamera();
            SceneView.RepaintAll();  // Force an initial Scene View redraw
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CleanupCamera();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Draws the custom EditorWindow GUI controls.
        /// Allows selection of target model, toggling settings, and manual commands.
        /// </summary>
        private void OnGUI()
        {
            GUILayout.Label("Mini-Map Settings", EditorStyles.boldLabel);

            // Detect changes to the Target Model field
            EditorGUI.BeginChangeCheck();
            GameObject newTarget = (GameObject)EditorGUILayout.ObjectField( "Target Model", targetModel, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                // Update target, reset transform change flag, recalculate bounds/camera
                targetModel = newTarget;
                if (targetModel != null)
                    targetModel.transform.hasChanged = false;
                CalculateModelBounds();
                PositionMapCamera();
                Repaint();
                SceneView.RepaintAll();

                // Log an error if the selection has no Renderer
                if (targetModel != null && !HasRenderer(targetModel))
                {
                    Debug.LogError($"MiniMap Navigator: '{targetModel.name}' has no Renderer component.");
                }
            }

            // If no valid model assigned, show error message
            if (targetModel != null && !HasRenderer(targetModel))
            {
                EditorGUILayout.HelpBox(
                    "Selected Target Model has no Renderer. Please choose a GameObject with a valid Renderer.",
                    MessageType.Error);
            }

            smoothMove = EditorGUILayout.Toggle("Smooth Move", smoothMove);
            flyTime = EditorGUILayout.FloatField("Fly Time (s)", flyTime);
            paddingFactor = EditorGUILayout.Slider("Frame Padding", paddingFactor, 1.0f, 5.0f);

            // Toggle mini-map overlay
            bool newEnabled = EditorGUILayout.Toggle("Enable Mini-Map", isEnabled);
            if (newEnabled != isEnabled)
            {
                isEnabled = newEnabled;
                SceneView.RepaintAll();
            }

            GUILayout.Space(20);
            if (GUILayout.Button("Recalculate Bounds & Camera"))
            {
                CalculateModelBounds();
                PositionMapCamera();
                Repaint();
                SceneView.RepaintAll();
            }
            GUILayout.Space(5);
            if (GUILayout.Button("Frame Model"))
            {
                FrameSceneViewOnModel();
            }
        }

        /// <summary>
        /// Called for each Scene View repaint. Renders the mini-map overlay,
        /// bounding box, and handles click-to-move logic.
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            // Do nothing if disabled or no valid target
            if (!isEnabled || targetModel == null || !HasRenderer(targetModel))
                return;

            // Handle automatic recalculation if transform changed
            Transform t = targetModel.transform;
            if (t.hasChanged)
            {
                CalculateModelBounds();
                PositionMapCamera();
                t.hasChanged = false;
                Repaint();
            }

            // Always recalc to account for runtime changes
            CalculateModelBounds();
            PositionMapCamera();

            // Define the minimap's on-screen rectangle (bottom-right corner)
            Rect miniMapRect = new Rect( sceneView.position.width - mapWidth - margin, sceneView.position.height - mapHeight - margin, mapWidth, mapHeight);

            // Render the hidden map camera into the texture
            mapCamera.Render();

            // Draw the mini-map GUI
            Handles.BeginGUI();
            GUI.Box(miniMapRect, GUIContent.none);
            GUI.DrawTexture( new Rect( miniMapRect.x + 4, miniMapRect.y + 4, miniMapRect.width - 8, miniMapRect.height - 8), mapTexture, ScaleMode.ScaleToFit, false);
            Handles.EndGUI();

            // Overlay the model's bounding box in green wireframe
            DrawBoundsOverlay(miniMapRect);

            // Process clicks inside the mini-map
            HandleMapClicks(sceneView, miniMapRect);
            HandleFlyTransition(sceneView);
        }

        /// <summary>
        /// Draws the base rectangle of the model bounds atop the mini-map.
        /// </summary>
        private void DrawBoundsOverlay(Rect rect)
        {
            if (modelBounds.size == Vector3.zero)
                return;

            // Define the four bottom corners of the bounding box
            Vector3[] worldCorners = new Vector3[]
            {
            new Vector3(modelBounds.min.x, modelBounds.min.y, modelBounds.min.z),
            new Vector3(modelBounds.max.x, modelBounds.min.y, modelBounds.min.z),
            new Vector3(modelBounds.max.x, modelBounds.min.y, modelBounds.max.z),
            new Vector3(modelBounds.min.x, modelBounds.min.y, modelBounds.max.z)
            };

            // Convert to GUI coordinates
            Vector3[] guiCorners = new Vector3[5];
            for (int i = 0; i < 4; i++)
            {
                Vector3 vp = mapCamera.WorldToViewportPoint(worldCorners[i]);
                float x = rect.x + vp.x * rect.width;
                float y = rect.y + (1f - vp.y) * rect.height;
                guiCorners[i] = new Vector3(x, y, 0);
            }
            guiCorners[4] = guiCorners[0];

            // Draw the green wireframe
            Handles.BeginGUI();
            Handles.color = Color.green;
            Handles.DrawPolyLine(guiCorners);
            Handles.color = Color.white;
            Handles.EndGUI();
        }

        /// <summary>
        /// Handles mouse clicks within the mini-map rectangle.
        /// </summary>
        private void HandleMapClicks(SceneView sceneView, Rect miniMapRect)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && miniMapRect.Contains(e.mousePosition))
            {
                Vector2 p = e.mousePosition;
                float u = (p.x - miniMapRect.x) / miniMapRect.width;
                float v = 1f - ((p.y - miniMapRect.y) / miniMapRect.height);
                Vector3 screenPoint = new Vector3(u * mapCamera.pixelWidth, v * mapCamera.pixelHeight, 0);
                Ray ray = mapCamera.ScreenPointToRay(screenPoint);
                Plane ground = new Plane(Vector3.up, new Vector3(0, modelBounds.min.y, 0));
                if (ground.Raycast(ray, out float enter))
                {
                    Vector3 worldPos = ray.GetPoint(enter);
                    worldPos.x = Mathf.Clamp(worldPos.x, modelBounds.min.x, modelBounds.max.x);
                    worldPos.z = Mathf.Clamp(worldPos.z, modelBounds.min.z, modelBounds.max.z);
                    MoveSceneViewPivot(sceneView, worldPos);
                    e.Use();
                }
            }
        }

        /// <summary>
        /// Initiates the SceneView pivot movement, with optional smoothing.
        /// </summary>
        private void MoveSceneViewPivot(SceneView sceneView, Vector3 targetPos)
        {
            if (smoothMove)
            {
                flyStartPos = sceneView.pivot;
                flyEndPos = targetPos;
                flyStartTime = (float)EditorApplication.timeSinceStartup;
                isDragging = true;
                EditorApplication.update += OnEditorUpdate;
            }
            else
            {
                sceneView.pivot = targetPos;
                sceneView.Repaint();
            }
        }

        /// <summary>
        /// Called every editor update to drive the smooth fly-to animation.
        /// </summary>
        private void OnEditorUpdate() => Repaint();

        /// <summary>
        /// Processes smooth pivot transition each frame until complete.
        /// </summary>
        private void HandleFlyTransition(SceneView sceneView)
        {
            if (!isDragging)
                return;

            float t = ((float)EditorApplication.timeSinceStartup - flyStartTime) / flyTime;
            if (t >= 1f)
            {
                t = 1f;
                isDragging = false;
                EditorApplication.update -= OnEditorUpdate;
            }
            sceneView.pivot = Vector3.Lerp(flyStartPos, flyEndPos, t);
            sceneView.Repaint();
        }

        /// <summary>
        /// Ensures the hidden camera and texture exist for rendering the mini-map.
        /// </summary>
        private void InitializeCamera()
        {
            if (mapCamera != null)
                return;

            GameObject camGO = new GameObject("__MiniMapNavigatorCam");
            camGO.hideFlags = HideFlags.HideAndDontSave;
            mapCamera = camGO.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.enabled = false;
            mapTexture = new RenderTexture(512, 512, 16);
            mapTexture.hideFlags = HideFlags.HideAndDontSave;
            mapCamera.targetTexture = mapTexture;
        }

        /// <summary>
        /// Releases the hidden camera and texture when the window closes.
        /// </summary>
        private void CleanupCamera()
        {
            if (mapCamera)
                Object.DestroyImmediate(mapCamera.gameObject);
            if (mapTexture)
                mapTexture.Release();
            mapCamera = null;
            mapTexture = null;
        }

        /// <summary>
        /// Calculates and caches the world-space bounds of the target model.
        /// </summary>
        private void CalculateModelBounds()
        {
            if (targetModel != null && HasRenderer(targetModel))
            {
                var renderers = targetModel.GetComponentsInChildren<Renderer>();
                modelBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    modelBounds.Encapsulate(renderers[i].bounds);
                return;
            }
            modelBounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        /// <summary>
        /// Positions and sizes the mini-map camera to fully cover the model bounds with padding.
        /// </summary>
        private void PositionMapCamera()
        {
            Vector3 center = modelBounds.center;
            float size = Mathf.Max(modelBounds.extents.x, modelBounds.extents.z) * paddingFactor;
            mapCamera.transform.position = center + Vector3.up * (size + 5f);
            mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mapCamera.orthographicSize = size;
        }

        /// <summary>
        /// Frames the Scene View camera to fit the padded model bounds.
        /// </summary>
        private void FrameSceneViewOnModel()
        {
            if (targetModel == null)
            {
                Debug.LogWarning("MiniMap Navigator: No target model set.");
                return;
            }
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                Bounds padded = new Bounds(modelBounds.center,modelBounds.size * paddingFactor);
                sceneView.Frame(padded, true);
            }
        }

        /// <summary>
        /// Checks if the GameObject or any of its children has a Renderer component.
        /// </summary>
        private bool HasRenderer(GameObject go)
        {
            return go.GetComponentInChildren<Renderer>() != null;
        }
    }
}