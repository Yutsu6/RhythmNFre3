using UnityEngine;

public class CursorDebugger : MonoBehaviour
{
    public CursorController cursor;
    public JudgmentManager judgmentManager;
    public ChartParser parser;

    void Update()
    {
        if (cursor == null || parser == null) return;

        Debug.Log($"=== ���״̬��� ===");
        Debug.Log($"���ʱ��: {cursor.cursorTime:F3}s");
        Debug.Log($"��ǰ��: {cursor.GetCurrentRowId()}");
        Debug.Log($"��������: {parser.notes.Count}");
   
        // ��鵱ǰ������Щ����Ӧ�ý����ж�����
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
                    Debug.Log($"Ӧ��������: {note.type} at {note.position}, ʱ���: {timeDiff:F3}s");
                    Debug.Log($"Ӧ��������: {note.type} at {note.position}, ʱ���: {timeDiff:F3}s");
                }
            }
        }

        Debug.Log($"��ǰ��{currentRow}: {notesInRow}������, {notesInJudgmentWindow}��Ӧ���ж�������");
    }
}