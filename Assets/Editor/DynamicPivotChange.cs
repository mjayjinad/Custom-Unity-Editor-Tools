using UnityEngine;
using UnityEditor;

public class PivotToolWindow : EditorWindow
{
    private Transform targetModel;
    private bool toolActive = false;
    private Vector3 pivotPoint;
    private bool hasPivot = false;
    private float rotationSpeed = 0.4f;
    private Vector2 lastMousePos;
    private Tool previousTool = Tool.Move;

    [MenuItem("Tools/mjayjinad/Pivot Tool")]
    public static void ShowWindow() => GetWindow<PivotToolWindow>("Pivot Tool");

    private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Tools.current = previousTool;
    }

    private void OnGUI()
    {
        GUILayout.Label("Pivot Tool", EditorStyles.boldLabel);
        toolActive = EditorGUILayout.Toggle("Enable Pivot Tool", toolActive);
        targetModel = (Transform)EditorGUILayout.ObjectField("Target Model", targetModel, typeof(Transform), true);
        if (GUILayout.Button("Clear Pivot"))
        {
            hasPivot = false;
            Tools.current = previousTool;
            Repaint();
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!toolActive || targetModel == null) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        RaycastHit hit;

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (Physics.Raycast(ray, out hit) && (hit.transform == targetModel || hit.transform.IsChildOf(targetModel)))
            {
                pivotPoint = hit.point;
                hasPivot = true;
                lastMousePos = e.mousePosition;

                previousTool = Tools.current;
                Tools.current = Tool.None;

                Selection.activeTransform = targetModel;

                e.Use();
            }
        }

        if (hasPivot && e.type == EventType.MouseDrag && e.button == 0 && !e.alt)
        {
            Vector2 delta = e.mousePosition - lastMousePos;
            Undo.RecordObject(targetModel, "Rotate Around Pivot");
            targetModel.RotateAround(pivotPoint, sceneView.camera.transform.up, delta.x * rotationSpeed);
            targetModel.RotateAround(pivotPoint, sceneView.camera.transform.right, -delta.y * rotationSpeed);

            lastMousePos = e.mousePosition;
            e.Use();
            sceneView.Repaint();
        }

        if (hasPivot)
        {
            Handles.PositionHandle(pivotPoint, targetModel.rotation);
        }
    }
}
