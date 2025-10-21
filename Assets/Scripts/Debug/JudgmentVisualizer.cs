using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JudgmentVisualizer : MonoBehaviour
{
    [Header("����")]
    public CursorController cursor;
    public JudgmentManager judgmentManager;

    [Header("���ӻ�����")]
    public bool showJudgmentZones = true;
    public bool showNoteTimeDiff = true;

    [Header("�ж�������ɫ")]
    public Color criticalZoneColor = new Color(0, 1, 0, 0.3f);      // ��ɫ - CRITICAL
    public Color fixedZoneColor = new Color(0, 0.8f, 0, 0.2f);     // ǳ�� - FIXED  
    public Color patchedZoneColor = new Color(1, 0.8f, 0, 0.2f);   // ��ɫ - PATCHED
    public Color errorZoneColor = new Color(1, 0, 0, 0.1f);        // ��ɫ - ERROR
    public Color queueZoneColor = new Color(0, 0.5f, 1, 0.15f);    // ��ɫ - �ж�����

    [Header("��ʾ����")]
    public float zoneHeight = 10f;          // �ж�����ĸ߶�
    public float noteLabelOffset = 0.5f;    // ������ǩƫ��

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

        // �������������������ȣ����� = ʱ�� �� �ٶȣ�
        float criticalWidth = JudgmentManager.FIXED_THRESHOLD * cursor.speed;
        float fixedWidth = JudgmentManager.PATCHED_THRESHOLD * cursor.speed;
        float patchedWidth = JudgmentManager.JUDGMENT_WINDOW_EXIT * cursor.speed;
        float queueWidth = JudgmentManager.JUDGMENT_WINDOW_ENTER * cursor.speed;

        // �����ж�����������ɫ��
        DrawZone(cursorPos, queueWidth, queueZoneColor, "�ж����� ��0.16s");

        // ����ERROR���򣨺�ɫ��- ̫��
        DrawZone(cursorPos + Vector3.right * queueWidth, 1f, errorZoneColor, "TOO FAST");

        // ����PATCHED���򣨻�ɫ��
        DrawZone(cursorPos, patchedWidth, patchedZoneColor, "PATCHED ��0.10s");

        // ����FIXED����ǳ�̣�
        DrawZone(cursorPos, fixedWidth, fixedZoneColor, "FIXED ��0.06s");

        // ����CRITICAL������ɫ��
        DrawZone(cursorPos, criticalWidth, criticalZoneColor, "CRITICAL ��0.03s");

        // ���ƹ��λ�ã���ɫ�ߣ�
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

        // ���Ʊ߿�
        Gizmos.color = new Color(color.r, color.g, color.b, 1f);
        Gizmos.DrawWireCube(center, size);

        // ���Ʊ�ǩ
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

            // ֻ��ʾ����2���ڵ�����
            if (Mathf.Abs(timeDiff) > 2f)
                continue;

            // ����ʱ���������ɫ
            Color labelColor = GetTimeDiffColor(timeDiff);

#if UNITY_EDITOR
            UnityEditor.Handles.color = labelColor;

            string status = judgmentManager.activeJudgmentQueue.Contains(note) ? "[������]" : "";
            string label = $"{note.type}\n{timeDiff:+0.000;-0.000}s\n{status}";

            UnityEditor.Handles.Label(
                notePos + Vector3.up * noteLabelOffset,
                label
            );

            // ���Ƶ���������
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
            return new Color(1, 0.5f, 0); // ��ɫ
        else if (absTime <= JudgmentManager.JUDGMENT_WINDOW_ENTER)
            return Color.blue;
        else
            return Color.gray;
    }

    // ��Game����Ҳ��ʾ������Ϣ
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 150, 300, 200));
        GUILayout.Label(" �ж����ӻ�����");
        GUILayout.Label($"����ٶ�: {cursor.speed}");

        // ��ʾ�ж�������Ϣ
        float criticalDist = JudgmentManager.FIXED_THRESHOLD * cursor.speed;
        float fixedDist = JudgmentManager.PATCHED_THRESHOLD * cursor.speed;
        float patchedDist = JudgmentManager.JUDGMENT_WINDOW_EXIT * cursor.speed;

        GUILayout.Label($"CRITICAL: ��{criticalDist:F2}��λ");
        GUILayout.Label($"FIXED: ��{fixedDist:F2}��λ");
        GUILayout.Label($"PATCHED: ��{patchedDist:F2}��λ");

        GUILayout.EndArea();
    }
}