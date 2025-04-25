using UnityEngine;
using UnityEditor;

public class MiniMapNavigatorWindow : EditorWindow
{
    private Camera mapCamera;
    private RenderTexture mapTexture;
    private Bounds modelBounds;

    private const float mapWidth = 256f;
    private const float mapHeight = 256f;
    private const float margin = 10f;
    private bool smoothMove = true;
    private float flyTime = 0.3f;
    private GameObject targetModel;
    private float paddingFactor = 2f;

    private bool isEnabled = false;
    private bool isDragging = false;
    private Vector3 flyStartPos;
    private Vector3 flyEndPos;
    private float flyStartTime;

    [MenuItem("Tools/mjayjinad/MiniMap Navigator")]
    public static void OpenWindow()
    {
        var win = GetWindow<MiniMapNavigatorWindow>("MiniMap Navigator");
        win.minSize = new Vector2(300, 420);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        InitializeCamera();
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        CleanupCamera();
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        GUILayout.Label("Mini-Map Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        GameObject newTarget = (GameObject)EditorGUILayout.ObjectField("Target Model", targetModel, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            targetModel = newTarget;
            CalculateModelBounds();
            PositionMapCamera();
            if (targetModel != null)
            {
                targetModel.transform.hasChanged = false;
                ValidateTargetModel();
            }
            Repaint();
            SceneView.RepaintAll();
        }

        if (targetModel != null && !HasRenderer(targetModel))
        {
            EditorGUILayout.HelpBox(
                "Selected target has no Renderer component. Please choose a GameObject with a MeshRenderer or other Renderer.",
                MessageType.Error);
        }

        GUILayout.Space(5);
        smoothMove = EditorGUILayout.Toggle("Smooth Move", smoothMove);
        flyTime = EditorGUILayout.FloatField("Fly Time (s)", flyTime);
        paddingFactor = EditorGUILayout.Slider("Frame Padding", paddingFactor, 1.0f, 5.0f);

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
            FrameSceneViewOnModel();
            PositionMapCamera();
            if (targetModel != null) targetModel.transform.hasChanged = false;
            ValidateTargetModel();
            Repaint();
            SceneView.RepaintAll();
        }
        GUILayout.Space(5);
        if (GUILayout.Button("Frame Model"))
        {
            FrameSceneViewOnModel();
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isEnabled || targetModel == null) return;
        if (!HasRenderer(targetModel)) return;

        if (mapCamera == null)
            InitializeCamera();

        Transform t = targetModel.transform;
        if (t.hasChanged)
        {
            CalculateModelBounds();
            PositionMapCamera();
            t.hasChanged = false;
            Repaint();
        }

        CalculateModelBounds();
        PositionMapCamera();

        Rect miniMapRect = new Rect(
            sceneView.position.width - mapWidth - margin,
            sceneView.position.height - mapHeight - margin,
            mapWidth,
            mapHeight);
        mapCamera.Render();
        Handles.BeginGUI();
        GUI.Box(miniMapRect, GUIContent.none);
        GUI.DrawTexture(new Rect(
            miniMapRect.x + 4,
            miniMapRect.y + 4,
            miniMapRect.width - 8,
            miniMapRect.height - 8),
            mapTexture,
            ScaleMode.ScaleToFit,
            false);
        Handles.EndGUI();

        DrawBoundsOverlay(miniMapRect);

        HandleMapClicks(sceneView, miniMapRect);
        HandleFlyTransition(sceneView);
    }

    private bool HasRenderer(GameObject go)
    {
        return go.GetComponentInChildren<Renderer>() != null;
    }

    private void ValidateTargetModel()
    {
        if (targetModel != null && !HasRenderer(targetModel))
        {
            Debug.LogError($"MiniMap Navigator: '{targetModel.name}' has no Renderer. Select a proper model.");
        }
    }

    private void DrawBoundsOverlay(Rect rect)
    {
        if (modelBounds.size == Vector3.zero) return;
        Vector3[] worldCorners = new Vector3[4]
        {
            new Vector3(modelBounds.min.x, modelBounds.min.y, modelBounds.min.z),
            new Vector3(modelBounds.max.x, modelBounds.min.y, modelBounds.min.z),
            new Vector3(modelBounds.max.x, modelBounds.min.y, modelBounds.max.z),
            new Vector3(modelBounds.min.x, modelBounds.min.y, modelBounds.max.z)
        };
        Vector3[] guiCorners = new Vector3[5];
        for (int i = 0; i < 4; i++)
        {
            Vector3 vp = mapCamera.WorldToViewportPoint(worldCorners[i]);
            float x = rect.x + vp.x * rect.width;
            float y = rect.y + (1f - vp.y) * rect.height;
            guiCorners[i] = new Vector3(x, y, 0);
        }
        guiCorners[4] = guiCorners[0];
        Handles.BeginGUI();
        Handles.color = Color.green;
        Handles.DrawPolyLine(guiCorners);
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void HandleMapClicks(SceneView sceneView, Rect miniMapRect)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && miniMapRect.Contains(e.mousePosition))
        {
            Vector2 guiPos = e.mousePosition;
            float u = (guiPos.x - miniMapRect.x) / miniMapRect.width;
            float v = 1f - ((guiPos.y - miniMapRect.y) / miniMapRect.height);
            Vector3 screenPoint = new Vector3(u * mapCamera.pixelWidth, v * mapCamera.pixelHeight, 0);
            Ray ray = mapCamera.ScreenPointToRay(screenPoint);
            Plane plane = new Plane(Vector3.up, new Vector3(0, modelBounds.min.y, 0));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPos = ray.GetPoint(enter);
                worldPos.x = Mathf.Clamp(worldPos.x, modelBounds.min.x, modelBounds.max.x);
                worldPos.z = Mathf.Clamp(worldPos.z, modelBounds.min.z, modelBounds.max.z);
                MoveSceneViewPivot(sceneView, worldPos);
                e.Use();
            }
        }
    }

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

    private void OnEditorUpdate() => Repaint();

    private void HandleFlyTransition(SceneView sceneView)
    {
        if (!isDragging) return;
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

    private void InitializeCamera()
    {
        if (mapCamera != null) return;
        GameObject camGO = new GameObject("__MiniMapNavigatorCam");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        mapCamera = camGO.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.enabled = false;
        mapTexture = new RenderTexture(512, 512, 16);
        mapTexture.hideFlags = HideFlags.HideAndDontSave;
        mapCamera.targetTexture = mapTexture;
    }

    private void CleanupCamera()
    {
        if (mapCamera) Object.DestroyImmediate(mapCamera.gameObject);
        if (mapTexture) mapTexture.Release();
        mapCamera = null;
        mapTexture = null;
    }

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
        modelBounds = new Bounds(Vector3.zero, Vector3.one * 10f);
    }

    private void PositionMapCamera()
    {
        Vector3 center = modelBounds.center;
        float size = Mathf.Max(modelBounds.extents.x, modelBounds.extents.z) * paddingFactor;
        mapCamera.transform.position = center + Vector3.up * (size + 5f);
        mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        mapCamera.orthographicSize = size;
    }

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
            Bounds padded = new Bounds(modelBounds.center, modelBounds.size * paddingFactor);
            sceneView.Frame(padded, true);
        }
    }
}
