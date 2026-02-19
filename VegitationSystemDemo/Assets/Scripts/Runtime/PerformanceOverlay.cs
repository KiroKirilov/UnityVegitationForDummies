using System.Text;
using Unity.Profiling;
using UnityEngine;

public class PerformanceOverlay : MonoBehaviour
{
    [SerializeField] GrassRenderer grassRenderer;
    [SerializeField, Range(0.1f, 2f)] float updateInterval = 0.5f;

    int frameCount;
    float elapsed;
    float currentFps;
    float currentMs;

    ProfilerRecorder trisRecorder;
    ProfilerRecorder vertsRecorder;
    ProfilerRecorder batchesRecorder;
    ProfilerRecorder shadowCastersRecorder;

    GUIStyle style;
    GUIStyle backgroundStyle;
    readonly StringBuilder sb = new StringBuilder(256);
    string cachedText = "";

    void OnEnable()
    {
        trisRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        vertsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
        batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        shadowCastersRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
    }

    void OnDisable()
    {
        trisRecorder.Dispose();
        vertsRecorder.Dispose();
        batchesRecorder.Dispose();
        shadowCastersRecorder.Dispose();
    }

    void Update()
    {
        frameCount++;
        elapsed += Time.unscaledDeltaTime;

        if (elapsed < updateInterval)
            return;

        currentFps = frameCount / elapsed;
        currentMs = elapsed / frameCount * 1000f;
        frameCount = 0;
        elapsed = 0f;

        RebuildText();
    }

    void RebuildText()
    {
        sb.Clear();
        sb.AppendFormat("{0:0} FPS ({1:0.0} ms)", currentFps, currentMs);

        sb.AppendFormat("\nTris: {0}", FormatCount(trisRecorder.LastValue));
        sb.AppendFormat("\nVerts: {0}", FormatCount(vertsRecorder.LastValue));
        sb.AppendFormat("\nBatches: {0}", batchesRecorder.LastValue);
        sb.AppendFormat("\nShadow casters: {0}", shadowCastersRecorder.LastValue);

        if (grassRenderer != null && grassRenderer.enabled)
        {
            var stats = grassRenderer.GetStats();
            sb.Append("\n--- Vegetation ---");
            sb.AppendFormat("\nInstances: {0}", FormatCount(stats.totalInstances));
            sb.AppendFormat("\nDraw calls: {0}", stats.meshGroupCount);
            sb.AppendFormat("\nTris: {0}", FormatCount(stats.totalTriangles));
            sb.AppendFormat("\nVerts: {0}", FormatCount(stats.totalVertices));
            sb.AppendFormat("\nShadows: {0}", stats.castsShadows ? "On" : "Off");
        }

        cachedText = sb.ToString();
    }

    void OnGUI()
    {
        if (style == null)
            InitStyles();

        var content = new GUIContent(cachedText);
        var size = style.CalcSize(content);
        var rect = new Rect(Screen.width - size.x - 10f, 10f, size.x + 12f, size.y + 8f);

        GUI.Box(rect, GUIContent.none, backgroundStyle);

        var textRect = new Rect(rect.x + 6f, rect.y + 4f, size.x, size.y);
        Color fpsColor;
        if (currentFps >= 60f)
            fpsColor = Color.green;
        else if (currentFps >= 30f)
            fpsColor = Color.yellow;
        else
            fpsColor = Color.red;

        style.normal.textColor = fpsColor;
        GUI.Label(textRect, cachedText, style);
    }

    void InitStyles()
    {
        style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            alignment = TextAnchor.UpperLeft
        };

        var bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
        bgTex.Apply();

        backgroundStyle = new GUIStyle
        {
            normal = { background = bgTex }
        };
    }

    static string FormatCount(long count)
    {
        if (count >= 1_000_000) return (count / 1_000_000f).ToString("0.0") + "M";
        if (count >= 1_000) return (count / 1_000f).ToString("0.0") + "K";
        return count.ToString();
    }
}
