using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class IfState
{
    public int startRowId;          // if����������
    public int indentLevel;         // ��������
    public int[] conditionCodes;    // �ж������� {1,0,1}��
    public int currentReadCount = 0; // ��ǰ��ȡ����
    public bool shouldEnterBody = false; // ��ǰ�Ƿ�Ӧ�ý����ж���

    // �ж��巶Χ��Ϣ
    public int ifBodyStartRow = -1; // �ж���ʵ�ʿ�ʼ��
    public int endRowId = -1;       // �ж������һ��
    public bool isRangeCalculated = false;

    public IfState(int row, int indent, int[] codes)
    {
        startRowId = row;
        indentLevel = indent;
        conditionCodes = codes;
        currentReadCount = 0;
        shouldEnterBody = false;

    }

    // ���µ�ǰ�ж�����
    public bool UpdateAndGetCondition()
    {
        // ��ʹ�õ�ǰ��������
        bool result;
        if (currentReadCount < conditionCodes.Length)
        {
            result = conditionCodes[currentReadCount] == 1;
        }
        else
        {
            result = false;
        }

        // ����״̬
        shouldEnterBody = result;

        // Ȼ������
        currentReadCount++;

        return result;
    }



    // ���ö�ȡ����������ѭ��������
    public void ResetForNewLoop()
    {
        currentReadCount = 0;
        shouldEnterBody = false;
    }
}

public class IfManager : MonoBehaviour
{
    private List<IfState> activeIfs = new List<IfState>();
    private ChartParser parser;

    public void Initialize(ChartParser chartParser)
    {
        parser = chartParser;
        activeIfs.Clear();
        Debug.Log("=== �жϹ�������ʼ�� ===");
    }

    // ����if����ʱ����
    public void OnEncounterIfSymbol(NoteData ifNote)
    {
        var existingIf = activeIfs.Find(i =>
            i.startRowId == ifNote.rowId && i.indentLevel == ifNote.indentLevel);

        if (existingIf != null)
        {
            existingIf.UpdateAndGetCondition();
        }
        else
        {
            var newIf = new IfState(ifNote.rowId, ifNote.indentLevel, ifNote.ifConditionCodes);
            CalculateIfRange(newIf); // ȷ������
            newIf.UpdateAndGetCondition();
            activeIfs.Add(newIf);

            Debug.Log($"��if״̬�������: isRangeCalculated={newIf.isRangeCalculated}");
        }
    }

    public int GetReadCount(NoteData note)
    {
        var ifState = activeIfs.Find(i =>
            i.startRowId == note.rowId && i.indentLevel == note.indentLevel);
        return ifState?.currentReadCount ?? 0;
    }


    // �����ж��巶Χ
    private void CalculateIfRange(IfState ifState)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(ifState.startRowId);

        if (startIndex == -1 || startIndex >= sortedRowIds.Count - 1)
        {
            Debug.LogError($"�޷������жϷ�Χ: ��{ifState.startRowId}");
            return;
        }

        // �ж������һ�п�ʼ
        ifState.ifBodyStartRow = sortedRowIds[startIndex + 1];

        // �ҵ��ж������һ��
        for (int i = startIndex + 1; i < sortedRowIds.Count; i++)
        {
            int rowId = sortedRowIds[i];
            int rowIndent = GetRowIndent(rowId);

            if (rowIndent <= ifState.indentLevel)
            {
                // ��һ�����ж������һ��
                ifState.endRowId = sortedRowIds[i - 1];
                break;
            }

            // ��������һ��
            if (i == sortedRowIds.Count - 1)
            {
                ifState.endRowId = rowId;
            }
        }

        ifState.isRangeCalculated = true; // �������������
        Debug.Log($"�ж��巶Χ�������: {ifState.ifBodyStartRow}->{ifState.endRowId}, isRangeCalculated={ifState.isRangeCalculated}");
    }

    // ��ȡ���е���������ͨ�����еĵ�һ��������
    private int GetRowIndentLevel(int rowId)
    {
        foreach (var note in parser.notes)
        {
            if (note.rowId == rowId)
                return note.indentLevel;
        }
        return 0;
    }

    // ������Ƿ�Ӧ������
    public bool ShouldSkipRow(int rowId)
    {
        Debug.Log($"ShouldSkipRow: �����{rowId}����Ծif����: {activeIfs.Count}");

        foreach (var ifState in activeIfs)
        {
            Debug.Log($"���if״̬: ��{ifState.startRowId}, ��Χ{ifState.ifBodyStartRow}->{ifState.endRowId}, ����{ifState.shouldEnterBody}");

            bool inBody = IsRowInIfBody(rowId, ifState);
            Debug.Log($"��{rowId}���ж���{ifState.startRowId}��: {inBody}");

            if (inBody && !ifState.shouldEnterBody)
            {
                Debug.Log($"��Ҫ������{rowId}");
                return true;
            }
        }

        Debug.Log($"����Ҫ������{rowId}");
        return false;
    }

    // ��ȡ�����ж�����Ŀ����
    public int GetSkipTargetRow(int rowToSkip)
    {
        // ����Ӧ����Ҫ��������(-2)�������ǵ�ǰ��(-1)
        var innermostIf = FindInnermostIfForRow(rowToSkip);
        if (innermostIf != null && !innermostIf.shouldEnterBody)
        {
            var sortedRowIds = parser.GetSortedRowIds();
            int endIndex = sortedRowIds.IndexOf(innermostIf.endRowId);

            if (endIndex + 1 < sortedRowIds.Count)
            {
                int skipTarget = sortedRowIds[endIndex + 1];
                Debug.Log($"�����ж���{innermostIf.startRowId}: Ҫ��������{rowToSkip} -> Ŀ��{skipTarget}");
                return skipTarget;
            }
        }

        return rowToSkip;
    }

    // ������Ƿ����ж�����
    private bool IsRowInIfBody(int rowId, IfState ifState)
    {
        Debug.Log($"IsRowInIfBody��ʼ: rowId={rowId}, isRangeCalculated={ifState.isRangeCalculated}");

        if (!ifState.isRangeCalculated)
        {
            Debug.Log("��Χδ���㣬����false");
            return false;
        }
        var sortedRowIds = parser.GetSortedRowIds();
        int rowIndex = sortedRowIds.IndexOf(rowId);
        int startIndex = sortedRowIds.IndexOf(ifState.ifBodyStartRow);  // Ӧ����-2
        int endIndex = sortedRowIds.IndexOf(ifState.endRowId);          // Ӧ����-2

        Debug.Log($"IsRowInIfBody: ��{rowId}��[{ifState.ifBodyStartRow}->{ifState.endRowId}]��Χ��? rowIndex={rowIndex}, startIndex={startIndex}, endIndex={endIndex}");

        return rowIndex >= startIndex && rowIndex <= endIndex;
    }

    // �ҵ���ǰ�����������ڲ��ж�
    private IfState FindInnermostIfForRow(int rowId)
    {
        IfState innermostIf = null;
        int maxIndent = -1;

        foreach (var ifState in activeIfs)
        {
            if (IsRowInIfBody(rowId, ifState) && ifState.indentLevel > maxIndent)
            {
                innermostIf = ifState;
                maxIndent = ifState.indentLevel;
            }
        }

        return innermostIf;
    }

    // ��ȡ�е���������
    private int GetRowIndent(int rowId)
    {
        if (parser == null) return 0;
        foreach (var note in parser.notes)
        {
            if (note.rowId == rowId)
                return note.indentLevel;
        }
        return 0;
    }

    // �����ѭ�����ʱ�������ڲ��жϵļ���
    public void OnOuterLoopCompleted(int outerLoopIndent)
    {
        foreach (var ifState in activeIfs)
        {
            if (ifState.indentLevel > outerLoopIndent)
            {
                ifState.ResetForNewLoop();
                Debug.Log($"�ڲ��ж�{ifState.startRowId}����: �µ��ж���{ifState.conditionCodes[0]}");
            }
        }
    }

    // ������Ϣ
    public void DebugIfStates()
    {
        Debug.Log("=== ��ǰ�ж�״̬ ===");
        foreach (var ifState in activeIfs)
        {
            Debug.Log($"��{ifState.startRowId}, ����{ifState.indentLevel}, ��ȡ����{ifState.currentReadCount}, ����{ifState.shouldEnterBody}, ��Χ{ifState.ifBodyStartRow}->{ifState.endRowId}");
        }
    }
}