using UnityEngine;

public class CursorDebugger : MonoBehaviour
{
    public CursorController cursor;
    public JudgmentManager judgmentManager;
    public ChartParser parser;

    void Update()
    {
        if (cursor == null || parser == null) return;

        Debug.Log($"=== 光标状态诊断 ===");
        Debug.Log($"光标时间: {cursor.cursorTime:F3}s");
        Debug.Log($"当前行: {cursor.GetCurrentRowId()}");
        Debug.Log($"音符总数: {parser.notes.Count}");
   
        // 检查当前行有哪些音符应该进入判定序列
        CheckCurrentRowNotes();

        Debug.Log($"===================");
    }

    void CheckCurrentRowNotes()
    {
        int currentRow = cursor.GetCurrentRowId();
        int notesInRow = 0;
        int notesInJudgmentWindow = 0;

        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRow)
            {
                notesInRow++;
                float timeDiff = note.triggerTime - cursor.cursorTime;

                if (Mathf.Abs(timeDiff) <= 0.16f && !note.isJudged)
                {
                    notesInJudgmentWindow++;
                    Debug.Log($"应进入序列: {note.type} at {note.position}, 时间差: {timeDiff:F3}s");
                    Debug.Log($"应进入序列: {note.type} at {note.position}, 时间差: {timeDiff:F3}s");
                }
            }
        }

        Debug.Log($"当前行{currentRow}: {notesInRow}个音符, {notesInJudgmentWindow}个应在判定窗口内");
    }
}