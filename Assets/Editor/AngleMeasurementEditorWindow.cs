using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TMPro;

public class AngleMeasurementEditorWindow : EditorWindow
{
    private GameObject targetObject;
    private Vector3[] worldVertices;
    private List<Vector3> points = new List<Vector3>();
    private GUIStyle labelStyle;
    private TextMeshPro tmpLabel;

    [MenuItem("Tools/mjayjinad/Angle Measurement Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<AngleMeasurementEditorWindow>("Angle Tool");
        window.Init();
    }

    private void Init()
    {
        points.Clear();

        labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.black },
            alignment = TextAnchor.MiddleCenter
        };

        if (tmpLabel != null)
        {
            DestroyImmediate(tmpLabel.gameObject);
            tmpLabel = null;
        }
        SceneView.duringSceneGui += OnSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        if (tmpLabel != null)
        {
            DestroyImmediate(tmpLabel.gameObject);
            tmpLabel = null;
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("3D Point Angle Tool", EditorStyles.largeLabel);

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
        }

        string instruction = points.Count == 0 ? "Select first point on model" :
                             points.Count == 1 ? "Select second point" :
                             points.Count == 2 ? "Select third point" :
                             "Measurement complete";
        EditorGUILayout.HelpBox(instruction, MessageType.Info);

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
        }
        GUILayout.EndHorizontal();
        GUILayout.Label($"Points Selected: {points.Count}/3");
    }

    private void UpdateTargetMesh()
    {
        worldVertices = null;
        if (targetObject == null) return;

        Mesh m = targetObject.GetComponent<MeshFilter>()?.sharedMesh;
        if (m == null && targetObject.TryGetComponent(out MeshCollider mc))
            m = mc.sharedMesh;

        if (m != null)
        {

            var verts = m.vertices;
            worldVertices = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                worldVertices[i] = targetObject.transform.TransformPoint(verts[i]);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (worldVertices != null)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = Color.cyan;
            foreach (var v in worldVertices)
            {
                Handles.SphereHandleCap(0, v, Quaternion.identity, 0.03f, EventType.Repaint);
            }
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt &&
            targetObject != null && points.Count < 3 && worldVertices != null)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.gameObject == targetObject)
            {
                Vector3 closest = worldVertices[0];
                float minDist = Vector3.Distance(hit.point, closest);
                foreach (var v in worldVertices)
                {
                    float d = Vector3.Distance(hit.point, v);
                    if (d < minDist)
                    {
                        minDist = d; closest = v;
                    }
                }
                if (!points.Contains(closest)) points.Add(closest);
                e.Use();
                SceneView.RepaintAll();
                this.Repaint();
            }
        }

        if (points.Count > 0)
        {
            Handles.color = Color.yellow;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            foreach (var p in points)
            {
                Handles.SphereHandleCap(0, p, Quaternion.identity, 0.03f, EventType.Repaint);
            }
        }

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
            float radius = 0.1f;
            Handles.DrawSolidArc(B, normal, BA, angle, radius);

            Vector3 bis = (BA + BC).normalized;
            Vector3 basePos = B + bis * radius;

            Vector3 offsetUp = Vector3.up * radius * 0.2f;
            Vector3 labelPos = basePos + offsetUp;

            Handles.Label(labelPos, $"{angle:F1}", labelStyle);

            if (tmpLabel == null)
            {
                var go = new GameObject("AngleLabel_TMP");
                go.hideFlags = HideFlags.None;
                tmpLabel = go.AddComponent<TextMeshPro>();
                tmpLabel.alignment = TextAlignmentOptions.Center;
                tmpLabel.fontSize = 24;
                tmpLabel.color = Color.black;
                var renderer = tmpLabel.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(renderer.sharedMaterial);
                renderer.sharedMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                renderer.sharedMaterial.renderQueue = 4000;
                renderer.sortingOrder = 32767;
                renderer.sortingLayerName = "TextMeshProLayer";
            }
            tmpLabel.text = $"{angle:F1}";
            tmpLabel.transform.position = labelPos;
            tmpLabel.transform.rotation = sceneView.camera.transform.rotation;
            tmpLabel.transform.localScale = Vector3.one * 0.03f;
        }

        sceneView.Repaint();
    }
}