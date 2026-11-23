using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class LipsSyncTimelineWindow : EditorWindow
{
    private static LipsSyncTimelineAsset _asset;

    private const float BasePixelsPerSecond = 240f;
    private const float TileHeight = 30f;
    private const float VisualInnerPaddingPx = 4f;
    private const float MinTileWidth = 8f;
    private const float TimeScaleHeight = 28f;
    private const float EdgeGrabPx = 2f;

    private float[] _startTimes = new float[0];

    private Texture2D _whiteTexture;
    private GUIStyle _phonemeStyle;
    private GUIStyle _timeStyle;

    private Vector2 _scroll;
    private Vector2 _dragStartMouse;

    private float _pixelsPerSecond => Mathf.Max(1f, BasePixelsPerSecond * _dynamicScale);

    private float _dragStartDuration;
    private float _minDurationSeconds = 0.0005f;
    private float _dynamicScale = 1f;
    private int _activeIndex = -1;
    private bool _resizing;
    private bool _showMenu;

    public static void Open(LipsSyncTimelineAsset asset)
    {
        _asset = asset;
        var window = GetWindow<LipsSyncTimelineWindow>($"LipsSync Timeline ({_asset.name})");
        window.RecalculateScale();
    }

    private void OnEnable()
    {
        _phonemeStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
        _timeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, fontSize = 10 };

        _whiteTexture = Texture2D.whiteTexture;

        EditorApplication.update += Repaint;
    }

    public void RecalculateScale()
    {
        if (_asset == null || _asset.TimelineEntries == null || _asset.TimelineEntries.Length == 0)
        {
            _dynamicScale = 1f;
            return;
        }

        float minDur = _asset.TimelineEntries.Min(e => Mathf.Max(e.Duration, _minDurationSeconds));

        if (minDur <= 0f)
            minDur = _minDurationSeconds;

        float needed = MinTileWidth / (BasePixelsPerSecond * minDur);
        _dynamicScale = Mathf.Max(1f, needed);
    }

    private void EnsureStartTimes()
    {
        if (_asset == null || _asset.TimelineEntries == null)
        {
            _startTimes = null;
            Close();

            return;
        }

        if (_startTimes.Length != _asset.TimelineEntries.Length)
            RecalculateStartTimes();
    }

    private void RecalculateStartTimes(int startIndex = 0)
    {
        _startTimes = new float[_asset.TimelineEntries.Length];
        float t = 0f;
        for (int i = 0; i < _asset.TimelineEntries.Length; i++)
        {
            _startTimes[i] = t;
            float dur = _asset.TimelineEntries[i].Duration;
            t += dur;
        }
    }

    private void OnGUI()
    {
        EnsureStartTimes();

        Span<LipsSyncTimelineEntry> entriesSpan = _asset.TimelineEntries;

        int lenght = _asset.TimelineEntries.Length;
        int endIndex = lenght - 1;

        float totalEnd = _startTimes[endIndex] + _asset.TimelineEntries[endIndex].Duration;

        float pps = _pixelsPerSecond;

        float totalWidth = Mathf.Max(position.width, totalEnd * pps + 40f);
        Rect positionRect = GUILayoutUtility.GetRect(position.width, TileHeight + TimeScaleHeight + 40f);
        Rect viewRect = new Rect(0, 0, totalWidth, positionRect.height);

        EditorGUI.DrawRect(positionRect, new Color(0.12f, 0.12f, 0.12f));

        using (var scrollScope = new GUI.ScrollViewScope(positionRect, _scroll, viewRect, true, false, GUI.skin.horizontalScrollbar, GUIStyle.none))
        {
            _scroll = scrollScope.scrollPosition;

            Rect visibleRect = positionRect;
            visibleRect.position = _scroll;

            float tileY = 8f;
            Event e = Event.current;

            Vector2 mousePosition = e.mousePosition;

            if (_showMenu)
            {
                float localMouseX = mousePosition.x + _scroll.x;

                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Before"), false, () =>
                {
                    float time = _startTimes[_activeIndex];
                    LipsSyncTimelineEntry ne = new LipsSyncTimelineEntry { Phoneme = ' ', Duration = _asset.TimelineEntries[_activeIndex].Duration };
                    InsertTimelineEntry(_activeIndex, ne);
                });
                menu.AddItem(new GUIContent("Add After"), false, () =>
                {
                    float time = _startTimes[_activeIndex] + _asset.TimelineEntries[_activeIndex].Duration;
                    LipsSyncTimelineEntry ne = new LipsSyncTimelineEntry { Phoneme = ' ', Duration = _asset.TimelineEntries[_activeIndex].Duration };
                    InsertTimelineEntry(_activeIndex + 1, ne);
                });
                menu.AddItem(new GUIContent("Delete"), false, () => { RemoveTimelineEntry(_activeIndex); });
                menu.ShowAsContext();

                _showMenu = false;
            }

            float viewMin = _scroll.x;

            int startIndex = FindFirstVisibleIndex(viewMin);

            for (int i = startIndex; i < lenght; i++)
            {
                float start = _startTimes[i];

                float x = start * pps;

                if (x > visibleRect.xMax)
                {
                    endIndex = i;
                    break;
                }

                ref var entry = ref entriesSpan[i];
                float w = Mathf.Max(entry.Duration, _minDurationSeconds) * pps;

                Rect rect = new Rect(x, tileY, w - 2f, TileHeight);

                if (rect.xMax < visibleRect.xMin)
                    continue;

                float roundSize = Mathf.Min(5f, w - 1f);
                float borderSize = Mathf.Min(2f, w - 1f);

                if (_activeIndex == i)
                {
                    GUI.DrawTexture(rect, _whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.gray, w, roundSize);
                }
                else
                {
                    GUI.DrawTexture(rect, _whiteTexture, ScaleMode.StretchToFill, false, 0f, Color.gray, borderSize, roundSize);
                }

                Rect inner = new Rect(
                    rect.x + VisualInnerPaddingPx,
                    rect.y + VisualInnerPaddingPx,
                    Mathf.Max(0f, rect.width - VisualInnerPaddingPx * 2f),
                    Mathf.Max(0f, rect.height - VisualInnerPaddingPx * 2f));

                if (rect.width > 14f)
                    GUI.Label(inner, entry.Phoneme.ToString(), _phonemeStyle);

                Rect timeRect = new Rect(rect.x + 4, rect.y + 2, 120, 14);
                Rect rightEdge = new Rect(rect.xMax - EdgeGrabPx, rect.y, EdgeGrabPx * 2f + VisualInnerPaddingPx, rect.height);

                EditorGUIUtility.AddCursorRect(rightEdge, MouseCursor.ResizeHorizontal);

                if (e.type == EventType.MouseDown)
                {
                    if (rightEdge.Contains(mousePosition))
                    {
                        _activeIndex = i;
                        _resizing = true;
                        _dragStartMouse = mousePosition;
                        _dragStartDuration = entry.Duration;

                        if (e.button == 1)
                            _showMenu = true;

                        e.Use();
                    }
                    else if (rect.Contains(mousePosition))
                    {
                        _activeIndex = i;

                        if (e.button == 1)
                            _showMenu = true;

                        e.Use();
                    }
                }
            }

            if (e.type == EventType.ScrollWheel && e.control)
            {
                float oldWidth = viewRect.width;

                float zoomStep = 1.15f;
                float zoom = Mathf.Pow(zoomStep, -e.delta.y);

                float oldScale = _dynamicScale;
                float wantedScale = oldScale * zoom;
                _dynamicScale = Mathf.Clamp(wantedScale, .1f, 100f);

                if (!Mathf.Approximately(oldScale, _dynamicScale))
                {
                    float actualZoom = (Mathf.Approximately(oldScale, 0f)) ? zoom : (_dynamicScale / oldScale);

                    float newWidth = oldWidth * actualZoom;

                    float center = positionRect.width * 0.5f;
                    float worldCenter = _scroll.x + center;

                    float newScroll = worldCenter * actualZoom - center;

                    float maxScroll = Mathf.Max(0f, newWidth - positionRect.width);
                    _scroll.x = Mathf.Clamp(newScroll, 0f, maxScroll);

                    e.Use();
                    Repaint();

                    pps = _pixelsPerSecond;
                }
            }

            if (_resizing && _activeIndex > -1)
            {
                if (e.type == EventType.MouseDrag || e.type == EventType.MouseMove)
                {
                    float deltaPx = mousePosition.x - _dragStartMouse.x;

                    float deltaSec = deltaPx / pps;
                    float newDur = Mathf.Max(_minDurationSeconds, _dragStartDuration + deltaSec);
                    float w = newDur * pps;

                    if (w < 12f)
                    {
                        newDur = 12f / pps;
                    }

                    _asset.TimelineEntries[_activeIndex].Duration = newDur;

                    RecalculateStartTimes(_activeIndex);

                    EditorUtility.SetDirty(_asset);

                    Repaint();
                }

                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    _resizing = false;

                    e.Use();
                }
            }

            float lineY = tileY + TileHeight + 6f;
            GUI.DrawTexture(new Rect(0, lineY, totalWidth, 1), _whiteTexture);
            DrawTimeScale(new Rect(0, lineY + 2f, totalWidth, TimeScaleHeight), positionRect, totalEnd);
        }
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (_activeIndex >= 0 && _activeIndex < _asset.TimelineEntries.Length)
        {
            var eEntry = _asset.TimelineEntries[_activeIndex];
            EditorGUILayout.BeginVertical(GUILayout.Width(320));
            EditorGUILayout.LabelField($"Index: {_activeIndex}");
            string newPh = EditorGUILayout.TextField("Phoneme", eEntry.Phoneme.ToString());
            float newDur = EditorGUILayout.FloatField("Duration (s)", eEntry.Duration);
            if (newPh != eEntry.Phoneme.ToString() || !Mathf.Approximately(newDur, eEntry.Duration))
            {
                eEntry.Phoneme = newPh[0];
                eEntry.Duration = Mathf.Max(_minDurationSeconds, newDur);
                _asset.TimelineEntries[_activeIndex] = eEntry;

                RecalculateStartTimes();
                EditorUtility.SetDirty(_asset);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Total visual end: {totalEnd:0.000}s  ·  scale: {_dynamicScale:0.###}  ·  pixels/s: {_pixelsPerSecond:0.##}", EditorStyles.boldLabel);

    }

    private void InsertTimelineEntry(int index, LipsSyncTimelineEntry entry)
    {
        var list = _asset.TimelineEntries.ToList();

        if (index < 0)
            index = 0;

        if (index > list.Count)
            index = list.Count;

        list.Insert(index, entry);

        _asset.TimelineEntries = list.ToArray();

        RecalculateStartTimes();

        EditorUtility.SetDirty(_asset);

        _activeIndex = index;

        Repaint();
    }

    private void RemoveTimelineEntry(int index)
    {
        if (_asset == null || _asset.TimelineEntries == null)
            return;

        if (index < 0 || index >= _asset.TimelineEntries.Length)
            return;

        var list = _asset.TimelineEntries.ToList();
        list.RemoveAt(index);

        _asset.TimelineEntries = list.ToArray();

        RecalculateStartTimes();

        EditorUtility.SetDirty(_asset);

        Repaint();
    }

    private int FindFirstVisibleIndex(float viewMinPx)
    {
        int lo = 0;
        int hi = _startTimes.Length - 1;
        float pps = _pixelsPerSecond;

        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            float tileStartPx = _startTimes[mid] * pps;

            if (tileStartPx < viewMinPx)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return Mathf.Max(0, lo - 1);
    }

    private void DrawTimeScale(Rect rect, Rect viewRect, float totalSec)
    {
        float[] candidates = { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f };
        float secPerTick = candidates[candidates.Length - 1];

        foreach (var c in candidates)
        {
            if (c * _pixelsPerSecond >= 80f)
            {
                secPerTick = c;
                break;
            }
        }

        float width = _pixelsPerSecond * secPerTick;
        int count = Mathf.CeilToInt(viewRect.width / width);
        int startIndex = Mathf.CeilToInt(_scroll.x / width);
        int endIndex = startIndex + count;

        for (int i = startIndex; i <= endIndex; i++)
        {
            float t = i * secPerTick;
            float x = t * _pixelsPerSecond;
            Handles.color = Color.white;
            Handles.DrawLine(new Vector3(x, rect.y), new Vector3(x, rect.y + rect.height * 0.6f));
            string label = $"{t:0.##}s";
            Vector2 size = _timeStyle.CalcSize(new GUIContent(label));
            Rect lr = new Rect(x - size.x * 0.5f, rect.y + rect.height * 0.6f, size.x, size.y);
            EditorGUI.LabelField(lr, label, _timeStyle);
        }
    }

    private void OnDisable() => EditorApplication.update -= Repaint;
}
