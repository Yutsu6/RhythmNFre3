using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class LoopState
{
    public int startRowId;          // ѭ������������
    public int indentLevel;         // ѭ��������������
    public int[] loopCodes;         // ѭ��������
    public int currentReadCount = 0; // ��ǰ��ȡ����
    public int remainingLoops = 0;   // ��ǰʣ��ѭ������
    public bool hasEnteredThisTime = false; // �����������Ƿ��ѽ���

    // ѭ���巶Χ��Ϣ
    public int loopBodyStartRow = -1; // ѭ����ʵ�ʿ�ʼ��
    public int endRowId = -1;         // ѭ�������һ��
    public bool isRangeCalculated = false;

    public LoopState(int row, int indent, int[] codes)
    {
        startRowId = row;
        indentLevel = indent;
        loopCodes = codes;
        currentReadCount = 0;
        hasEnteredThisTime = false;

        if (loopCodes.Length > 0)
        {
            remainingLoops = loopCodes[0];
        }
    }

    // ����ѭ����ʱ����
    public bool OnEnterLoopBody()
    {
        if (remainingLoops > 0)
        {
            remainingLoops--;
            return true; // ���Խ���
        }
        return false; // ���ܽ���
    }


    // �л�����һ��ѭ����
    public bool MoveToNextCode()
    {
        currentReadCount++;
        if (currentReadCount < loopCodes.Length)
        {
            remainingLoops = loopCodes[currentReadCount];
            return true;
        }
        return false; // û�и���ѭ����
    }
}


public class LoopManager : MonoBehaviour
{
    private List<LoopState> activeLoops = new List<LoopState>();
    private ChartParser parser;

    public void Initialize(ChartParser chartParser)
    {
        parser = chartParser;
        activeLoops.Clear();

        Debug.Log("=== ѭ����������ʼ�� ===");
    }

    // ����ѭ������ʱ����
    public void OnEncounterLoopSymbol(NoteData loopNote)
    {
        // ����Ƿ��Ѵ�����ͬ���������ѭ��
        var existingLoop = activeLoops.FirstOrDefault(l => l.indentLevel == loopNote.indentLevel);

        if (existingLoop != null)
        {
            // ��������ѭ�� - ����Ҫ���������� currentReadCount
            // ֻ�� OnOuterLoopCompleted ������
            Debug.Log($"����ѭ������: ��{loopNote.rowId}, ��ǰ��ȡ����{existingLoop.currentReadCount}, ʣ�����{existingLoop.remainingLoops}");
        }
        else
        {
            // ������ѭ��״̬
            var newLoop = new LoopState(loopNote.rowId, loopNote.indentLevel, loopNote.loopCodes);
            CalculateLoopRange(newLoop);
            activeLoops.Add(newLoop);
            Debug.Log($"������ѭ��: ��{loopNote.rowId}, ����{loopNote.indentLevel}, ѭ����[{string.Join(",", loopNote.loopCodes)}], ��Χ{newLoop.loopBodyStartRow}->{newLoop.endRowId}");
        }

        // ���������������ڲ���ǰ��
        activeLoops.Sort((a, b) => b.indentLevel.CompareTo(a.indentLevel));
    }

    // ����ѭ���巶Χ
    private void CalculateLoopRange(LoopState loop)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(loop.startRowId);

        if (startIndex == -1 || startIndex >= sortedRowIds.Count - 1)
        {
            Debug.LogError($"�޷�����ѭ����Χ: ��{loop.startRowId}");
            return;
        }

        // ѭ�������һ�п�ʼ
        loop.loopBodyStartRow = sortedRowIds[startIndex + 1];

        // �ҵ�ѭ�������һ��
        for (int i = startIndex + 1; i < sortedRowIds.Count; i++)
        {
            int rowId = sortedRowIds[i];
            int rowIndent = GetRowIndent(rowId);

            if (rowIndent <= loop.indentLevel)
            {
                // ��һ����ѭ�������һ��
                loop.endRowId = sortedRowIds[i - 1];
                break;
            }

            // ��������һ��
            if (i == sortedRowIds.Count - 1)
            {
                loop.endRowId = rowId;
            }
        }

        loop.isRangeCalculated = true;
    }

    // ����Ƿ�Ӧ��ѭ����ת
    public bool ShouldLoopBack(int currentRowId)
    {
        if (activeLoops.Count == 0) return false;

        // ���ڲ㵽�����
        foreach (var loop in activeLoops)
        {
            if (!loop.isRangeCalculated) continue;

            // ����Ƿ񵽴����ѭ�����ĩβ
            if (currentRowId == loop.endRowId && loop.remainingLoops > 0)
            {
                Debug.Log($"ѭ��{loop.startRowId}����ĩβ����Ҫ��ת (ʣ�����{loop.remainingLoops})");
                return true;
            }
        }

        return false;
    }

    // ��ȡ��תĿ����
    public int GetLoopBackRow(int currentRowId)
    {
        // ���ڲ㵽�����
        foreach (var loop in activeLoops)
        {
            if (!loop.isRangeCalculated) continue;

            if (currentRowId == loop.endRowId && loop.remainingLoops > 0)
            {
                Debug.Log($"ѭ��{loop.startRowId}��ת: ��{currentRowId} -> ��{loop.loopBodyStartRow}, ʣ�����{loop.remainingLoops}");

                // ��������ѭ����֪ͨ�ڲ�ѭ���л�
                if (loop.indentLevel == 0) // ѭ��1������
                {
                    OnOuterLoopCompleted(loop.indentLevel);
                }

                return loop.loopBodyStartRow;
            }
        }

        return currentRowId;
    }

    // ������Ƿ�Ӧ����������ѭ��û�ж�Ӧѭ����ʱ��
    public bool ShouldSkipRow(int currentRowId, int nextRowId)
    {
        foreach (var loop in activeLoops)
        {
            if (loop.remainingLoops == 0)
            {
                // �����һ���Ƿ���ѭ�����ڣ��ҵ�ǰ�в���ѭ������
                // ����ζ�Ź�꼴������ѭ���壬��ѭ����=0��Ӧ������
                if (IsRowInLoopBody(nextRowId, loop) && !IsRowInLoopBody(currentRowId, loop))
                {
                    Debug.Log($"����ѭ����{loop.startRowId}: ѭ����Ϊ0����{currentRowId}����ѭ������");
                    return true;
                }
            }
        }
        return false;
    }

    // ��ȡ����ѭ������Ŀ����
    public int GetSkipTargetRow(int currentRowId)
    {
        int nextRow = currentRowId - 1;

        foreach (var loop in activeLoops)
        {
            if (loop.remainingLoops == 0 &&
                IsRowInLoopBody(nextRow, loop) &&
                !IsRowInLoopBody(currentRowId, loop))
            {
                var sortedRowIds = parser.GetSortedRowIds();
                int endIndex = sortedRowIds.IndexOf(loop.endRowId);

                if (endIndex + 1 < sortedRowIds.Count)
                {
                    int skipTarget = sortedRowIds[endIndex + 1];
                    Debug.Log($"����ѭ����{loop.startRowId}: {currentRowId} -> {skipTarget}");
                    return skipTarget;
                }
            }
        }

        return currentRowId;
    }

    // �ҵ���ǰ�����������ڲ�ѭ��
    private LoopState FindInnermostLoopForRow(int rowId)
    {
        LoopState innermostLoop = null;
        int maxIndent = -1;

        foreach (var loop in activeLoops)
        {
            if (IsRowInLoopBody(rowId, loop) && loop.indentLevel > maxIndent)
            {
                innermostLoop = loop;
                maxIndent = loop.indentLevel;
            }
        }

        return innermostLoop;
    }

    // ������Ƿ���ѭ������
    private bool IsRowInLoopBody(int rowId, LoopState loop)
    {
        if (!loop.isRangeCalculated) return false;

        var sortedRowIds = parser.GetSortedRowIds();
        int rowIndex = sortedRowIds.IndexOf(rowId);
        int startIndex = sortedRowIds.IndexOf(loop.loopBodyStartRow);
        int endIndex = sortedRowIds.IndexOf(loop.endRowId);

        return rowIndex >= startIndex && rowIndex <= endIndex;
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

    // �����ѭ�����һ��ʱ����
    public void OnOuterLoopCompleted(int outerLoopIndent)
    {
        // �ҵ������ڲ�ѭ������������ģ�
        foreach (var loop in activeLoops)
        {
            if (loop.indentLevel > outerLoopIndent)
            {
                // �ڲ�ѭ���л�����һ��ѭ����
                loop.currentReadCount++;
                if (loop.currentReadCount < loop.loopCodes.Length)
                {
                    loop.remainingLoops = loop.loopCodes[loop.currentReadCount];
                    Debug.Log($"�ڲ�ѭ��{loop.startRowId}�л���ѭ����: {loop.remainingLoops}");
                }
                else
                {
                    loop.remainingLoops = 0;
                    Debug.Log($"�ڲ�ѭ��{loop.startRowId}�޸���ѭ����");
                }
            }
        }
    }

    public LoopState FindLoopForRow(int rowId)
    {
        int rowIndent = GetRowIndent(rowId);
        LoopState innermostLoop = null;
        int maxIndent = -1;

        foreach (var loop in activeLoops)
        {
            if (!loop.isRangeCalculated) continue;

            // �����һ���Ƿ������ѭ������
            if (IsRowInLoopBody(rowId, loop) && loop.indentLevel > maxIndent)
            {
                innermostLoop = loop;
                maxIndent = loop.indentLevel;
            }
        }

        return innermostLoop;
    }



    // ������Ϣ
    public void DebugLoopStates()
    {
        Debug.Log("=== ��ǰѭ��״̬ ===");
        foreach (var loop in activeLoops)
        {
            Debug.Log($"��{loop.startRowId}, ����{loop.indentLevel}, ��ȡ����{loop.currentReadCount}, ʣ�����{loop.remainingLoops}, ��Χ{loop.loopBodyStartRow}->{loop.endRowId}");
        }
    }


}