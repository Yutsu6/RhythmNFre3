using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JudgmentVisualizer : MonoBehaviour
{
    [Header("引用")]
    public CursorController cursor;
    public JudgmentManager judgmentManager;

    [Header("可视化设置")]
    public bool showJudgmentZones = true;
    public bool showNoteTimeDiff = true;

    [Header("判定区域颜色")]
    public Color criticalZoneColor = new Color(0, 1, 0, 0.3f);      // 绿色 - CRITICAL
    public Color fixedZoneColor = new Color(0, 0.8f, 0, 0.2f);     // 浅绿 - FIXED  
    public Color patchedZoneColor = new Color(1, 0.8f, 0, 0.2f);   // 黄色 - PATCHED
    public Color errorZoneColor = new Color(1, 0, 0, 0.1f);        // 红色 - ERROR
    public Color queueZoneColor = new Color(0, 0.5f, 1, 0.15f);    // 蓝色 - 判定序列

    [Header("显示设置")]
    public float zoneHeight = 10f;          // 判定区域的高度
    public float noteLabelOffset = 0.5f;    // 音符标签偏移

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showJudgmentZones || cursor == null)
            return;

        DrawJudgmentZones();

        if (showNoteTimeDiff)
        {
            DrawNoteTimeDifferences();
        }
    }

    void DrawJudgmentZones()
    {
        Vector3 cursorPos = cursor.transform.position;

        // 计算各区域的世界坐标宽度（距离 = 时间 × 速度）
        float criticalWidth = JudgmentManager.FIXED_THRESHOLD * cursor.speed;
        float fixedWidth = JudgmentManager.PATCHED_THRESHOLD * cursor.speed;
        float patchedWidth = JudgmentManager.JUDGMENT_WINDOW_EXIT * cursor.speed;
        float queueWidth = JudgmentManager.JUDGMENT_WINDOW_ENTER * cursor.speed;

        // 绘制判定序列区域（蓝色）
        DrawZone(cursorPos, queueWidth, queueZoneColor, "判定序列 ±0.16s");

        // 绘制ERROR区域（红色）- 太早
        DrawZone(cursorPos + Vector3.right * queueWidth, 1f, errorZoneColor, "TOO FAST");

        // 绘制PATCHED区域（黄色）
        DrawZone(cursorPos, patchedWidth, patchedZoneColor, "PATCHED ±0.10s");

        // 绘制FIXED区域（浅绿）
        DrawZone(cursorPos, fixedWidth, fixedZoneColor, "FIXED ±0.06s");

        // 绘制CRITICAL区域（绿色）
        DrawZone(cursorPos, criticalWidth, criticalZoneColor, "CRITICAL ±0.03s");

        // 绘制光标位置（白色线）
        Gizmos.color = Color.white;
        Gizmos.DrawLine(
            cursorPos + Vector3.up * zoneHeight * 0.5f,
            cursorPos + Vector3.down * zoneHeight * 0.5f
        );
    }

    void DrawZone(Vector3 center, float width, Color color, string label)
    {
        Gizmos.color = color;
        Vector3 size = new Vector3(width * 2, zoneHeight, 0.1f);
        Gizmos.DrawCube(center, size);

        // 绘制边框
        Gizmos.color = new Color(color.r, color.g, color.b, 1f);
        Gizmos.DrawWireCube(center, size);

        // 绘制标签
#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(center + Vector3.up * (zoneHeight * 0.5f + 0.3f), label);
#endif
    }

    void DrawNoteTimeDifferences()
    {
        if (judgmentManager == null || judgmentManager.allNotes == null)
            return;

        foreach (var note in judgmentManager.allNotes)
        {
            if (note.isJudged || note.noteObject == null)
                continue;

            Vector3 notePos = note.noteObject.transform.position;
            float timeDiff = note.triggerTime - cursor.cursorTime;
            float distanceDiff = timeDiff * cursor.speed;

            // 只显示附近2秒内的音符
            if (Mathf.Abs(timeDiff) > 2f)
                continue;

            // 根据时间差设置颜色
            Color labelColor = GetTimeDiffColor(timeDiff);

#if UNITY_EDITOR
            UnityEditor.Handles.color = labelColor;

            string status = judgmentManager.activeJudgmentQueue.Contains(note) ? "[队列中]" : "";
            string label = $"{note.type}\n{timeDiff:+0.000;-0.000}s\n{status}";

            UnityEditor.Handles.Label(
                notePos + Vector3.up * noteLabelOffset,
                label
            );

            // 绘制到光标的连线
            if (Mathf.Abs(timeDiff) < JudgmentManager.JUDGMENT_WINDOW_ENTER)
            {
                Gizmos.color = labelColor;
                Gizmos.DrawLine(notePos, cursor.transform.position);
            }
#endif
        }
    }

    Color GetTimeDiffColor(float timeDiff)
    {
        float absTime = Mathf.Abs(timeDiff);

        if (absTime <= JudgmentManager.FIXED_THRESHOLD)
            return Color.green;
        else if (absTime <= JudgmentManager.PATCHED_THRESHOLD)
            return Color.yellow;
        else if (absTime <= JudgmentManager.JUDGMENT_WINDOW_EXIT)
            return new Color(1, 0.5f, 0); // 橙色
        else if (absTime <= JudgmentManager.JUDGMENT_WINDOW_ENTER)
            return Color.blue;
        else
            return Color.gray;
    }

    // 在Game窗口也显示调试信息
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 150, 300, 200));
        GUILayout.Label(" 判定可视化调试");
        GUILayout.Label($"光标速度: {cursor.speed}");

        // 显示判定区域信息
        float criticalDist = JudgmentManager.FIXED_THRESHOLD * cursor.speed;
        float fixedDist = JudgmentManager.PATCHED_THRESHOLD * cursor.speed;
        float patchedDist = JudgmentManager.JUDGMENT_WINDOW_EXIT * cursor.speed;

        GUILayout.Label($"CRITICAL: ±{criticalDist:F2}单位");
        GUILayout.Label($"FIXED: ±{fixedDist:F2}单位");
        GUILayout.Label($"PATCHED: ±{patchedDist:F2}单位");

        GUILayout.EndArea();
    }
}