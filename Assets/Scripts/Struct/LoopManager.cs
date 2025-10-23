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
    public bool hasBeenTriggered = false; // �������Ƿ��Ѵ�����

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

    // ����ѭ��״̬
    public void Reset()
    {
        currentReadCount = 0;
        hasEnteredThisTime = false;
        hasBeenTriggered = false;

        if (loopCodes.Length > 0)
        {
            remainingLoops = loopCodes[0];
        }
    }

    // ���ѭ���Ƿ����
    public bool IsCompleted()
    {
        return currentReadCount >= loopCodes.Length && remainingLoops <= 0;
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
    private Dictionary<int, LoopState> loopStatesByRow = new Dictionary<int, LoopState>(); // ��������ѭ��״̬
    private List<LoopState> activeLoops = new List<LoopState>(); // ��ǰ��Ծ��ѭ��
    private ChartParser parser;

    public void Initialize(ChartParser chartParser)
    {
        parser = chartParser;
        loopStatesByRow.Clear();
        activeLoops.Clear();
        Debug.Log("=== ѭ����������ʼ�� ===");
    }

    // ����ѭ������ʱ����
    public void OnEncounterLoopSymbol(NoteData loopNote)
    {
        int rowId = loopNote.rowId;

        // ����Ƿ��Ѿ���������ѭ��
        if (loopStatesByRow.ContainsKey(rowId) && loopStatesByRow[rowId].hasBeenTriggered)
        {
            Debug.Log($"ѭ�������Ѵ�����: ��{rowId}");
            return;
        }

        // ����������ѭ��״̬
        if (!loopStatesByRow.ContainsKey(rowId))
        {
            loopStatesByRow[rowId] = new LoopState(rowId, loopNote.indentLevel, loopNote.loopCodes);
            CalculateLoopRange(loopStatesByRow[rowId]);
        }

        var loopState = loopStatesByRow[rowId];
        loopState.Reset(); // ����Ϊ��ʼ״̬
        loopState.hasBeenTriggered = true;

        // ��ӵ���Ծѭ���б�
        if (!activeLoops.Contains(loopState))
        {
            activeLoops.Add(loopState);
        }

        // ���������������ڲ���ǰ��
        activeLoops.Sort((a, b) => b.indentLevel.CompareTo(a.indentLevel));

        Debug.Log($"����/����ѭ��: ��{rowId}, ����{loopNote.indentLevel}, ѭ����[{string.Join(",", loopNote.loopCodes)}], ��Χ{loopState.loopBodyStartRow}->{loopState.endRowId}");

        DebugLoopStates();
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

            // ����Ƿ񵽴����ѭ�����ĩβ�һ���ʣ�����
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

    // ������Ƿ�Ӧ����������ѭ��û��ʣ�����ʱ��
    public bool ShouldSkipRow(int currentRowId, int nextRowId)
    {
        foreach (var loop in activeLoops)
        {
            if (loop.remainingLoops == 0 && loop.IsCompleted())
            {
                // �����һ���Ƿ���ѭ�����ڣ��ҵ�ǰ�в���ѭ������
                if (IsRowInLoopBody(nextRowId, loop) && !IsRowInLoopBody(currentRowId, loop))
                {
                    Debug.Log($"����ѭ����{loop.startRowId}: ѭ������ɣ���{currentRowId}����ѭ������");
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
            if (loop.remainingLoops == 0 && loop.IsCompleted() &&
                IsRowInLoopBody(nextRow, loop) &&
                !IsRowInLoopBody(currentRowId, loop))
            {
                var sortedRowIds = parser.GetSortedRowIds();
                int endIndex = sortedRowIds.IndexOf(loop.endRowId);

                if (endIndex + 1 < sortedRowIds.Count)
                {
                    int skipTarget = sortedRowIds[endIndex + 1];
                    Debug.Log($"���������ѭ����{loop.startRowId}: {currentRowId} -> {skipTarget}");

                    // �Ƴ�����ɵ�ѭ��
                    activeLoops.Remove(loop);
                    return skipTarget;
                }
            }
        }

        return currentRowId;
    }

    // �����ѭ�����һ��ʱ����
    public void OnOuterLoopCompleted(int outerLoopIndent)
    {
        // �ҵ������ڲ�ѭ������������ģ�
        foreach (var loop in activeLoops.ToList()) // ʹ��ToList�����޸ļ���
        {
            if (loop.indentLevel > outerLoopIndent)
            {
                // �ڲ�ѭ���л�����һ��ѭ����
                if (loop.MoveToNextCode())
                {
                    Debug.Log($"�ڲ�ѭ��{loop.startRowId}�л���ѭ����: {loop.remainingLoops}");
                }
                else
                {
                    Debug.Log($"�ڲ�ѭ��{loop.startRowId}�޸���ѭ���룬���Ϊ���");
                    // �����������Ƴ�����ɵ��ڲ�ѭ��
                }
            }
        }
    }

    public LoopState FindLoopForRow(int rowId)
    {
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

    // ��������ɵ�ѭ��
    public void CleanupCompletedLoops()
    {
        for (int i = activeLoops.Count - 1; i >= 0; i--)
        {
            if (activeLoops[i].IsCompleted())
            {
                Debug.Log($"���������ѭ��: ��{activeLoops[i].startRowId}");
                activeLoops.RemoveAt(i);
            }
        }
    }

    // ������Ϣ
    public void DebugLoopStates()
    {
        Debug.Log("=== ��ǰѭ��״̬ ===");
        foreach (var loop in activeLoops)
        {
            Debug.Log($"��{loop.startRowId}, ����{loop.indentLevel}, ��ȡ����{loop.currentReadCount}, ʣ�����{loop.remainingLoops}, ��Χ{loop.loopBodyStartRow}->{loop.endRowId}, �����{loop.IsCompleted()}");
        }

        Debug.Log("=== ����ע���ѭ�� ===");
        foreach (var kvp in loopStatesByRow)
        {
            var loop = kvp.Value;
            Debug.Log($"��{loop.startRowId}, �Ѵ���{loop.hasBeenTriggered}, �����{loop.IsCompleted()}");
        }
    }
}