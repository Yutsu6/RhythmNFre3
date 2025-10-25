using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class LoopState
{
    public int startRowId;          // ѭ������������
    public int indentLevel;         // ѭ��������������
    public int[] loopCodes;         // ѭ��������
    public int currentLoopIndex = 0; // ��ǰѭ��������
    public int remainingLoops = 0;   // ��ǰʣ��ѭ������
    public bool isActive = false;    // ѭ���Ƿ��Ծ
    public bool hasReducedThisCycle = false; // ����ѭ���Ƿ��Ѽ��ٴ���

    // ѭ���巶Χ��Ϣ����̬���㣩
    public int loopBodyStartRow = -1;
    public int loopBodyEndRow = -1;

    public LoopState(int row, int indent, int[] codes)
    {
        startRowId = row;
        indentLevel = indent;
        loopCodes = codes;
    }

    public void Reset()
    {
        currentLoopIndex = 0;
        if (loopCodes.Length > 0)
        {
            remainingLoops = loopCodes[0];
        }
        isActive = true;
        hasReducedThisCycle = false; // ���ñ��
        Debug.Log($"ѭ������: ��{startRowId}, ʣ�����{remainingLoops}");
    }

    // ��ʼ��ѭ������ʱ����
    public void StartNewCycle()
    {
        hasReducedThisCycle = false;
    }


public bool IsCompleted()
    {
        return currentLoopIndex >= loopCodes.Length && remainingLoops <= 0;
    }

    public bool MoveToNextCode()
    {
        currentLoopIndex++;
        if (currentLoopIndex < loopCodes.Length)
        {
            remainingLoops = loopCodes[currentLoopIndex];
            return true;
        }
        return false;
    }
}

public class LoopManager : MonoBehaviour
{
    private Dictionary<int, LoopState> loopStatesByRow = new Dictionary<int, LoopState>();
    private List<LoopState> activeLoops = new List<LoopState>();
    private ChartParser parser;

    public void Initialize(ChartParser chartParser)
    {
        parser = chartParser;
        loopStatesByRow.Clear();
        activeLoops.Clear();
        Debug.Log("ѭ����������ʼ��");
    }

    // ����ѭ������ʱ����
    public void OnEncounterLoopSymbol(NoteData loopNote)
    {
        int rowId = loopNote.rowId;
        int indent = loopNote.indentLevel;

        Debug.Log($"����ѭ������: ��{rowId}, ����{indent}, ѭ����[{string.Join(",", loopNote.loopCodes)}]");

        // �������ȡѭ��״̬
        if (!loopStatesByRow.ContainsKey(rowId))
        {
            loopStatesByRow[rowId] = new LoopState(rowId, indent, loopNote.loopCodes);
        }

        var loopState = loopStatesByRow[rowId];
        loopState.Reset();

        // ����ѭ���巶Χ
        CalculateLoopBodyRange(loopState);

        // ��ӵ���Ծѭ��
        if (!activeLoops.Contains(loopState))
        {
            activeLoops.Add(loopState);
        }

        // �����������������������ǰ���ڲ����ȣ�
        activeLoops.Sort((a, b) => b.indentLevel.CompareTo(a.indentLevel));

        DebugLoopStates();
    }

    // ���������������ѭ���巶Χ
    private void CalculateLoopBodyRange(LoopState loop)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(loop.startRowId);

        if (startIndex == -1 || startIndex >= sortedRowIds.Count - 1)
        {
            Debug.LogError($"�޷��ҵ�ѭ����ʼ��: {loop.startRowId}");
            return;
        }

        // ѭ�������һ�п�ʼ
        loop.loopBodyStartRow = sortedRowIds[startIndex + 1];
        loop.loopBodyEndRow = loop.loopBodyStartRow; // ��ʼֵ

        // ����ѭ���������
        for (int i = startIndex + 1; i < sortedRowIds.Count; i++)
        {
            int currentRowId = sortedRowIds[i];
            int currentIndent = GetRowIndent(currentRowId);

            // �ؼ��߼��������ǰ������ <= ѭ������������˵��ѭ�������
            if (currentIndent <= loop.indentLevel)
            {
                // ��һ����ѭ�������һ��
                if (i > startIndex + 1)
                {
                    loop.loopBodyEndRow = sortedRowIds[i - 1];
                }
                else
                {
                    // ��������ž�������<=���У�˵��ѭ����Ϊ��
                    loop.loopBodyEndRow = loop.startRowId;
                }
                break;
            }

            // ��������һ�У����о���ѭ�������
            if (i == sortedRowIds.Count - 1)
            {
                loop.loopBodyEndRow = currentRowId;
            }
        }

        Debug.Log($"ѭ���巶Χ: {loop.loopBodyStartRow} -> {loop.loopBodyEndRow} (ѭ��������{loop.startRowId}, ����{loop.indentLevel})");
    }

    // ������Ƿ���ѭ������
    public bool IsRowInLoopBody(int rowId, LoopState loop)
    {
        if (loop.loopBodyStartRow == -1 || loop.loopBodyEndRow == -1)
            return false;

        var sortedRowIds = parser.GetSortedRowIds();
        int rowIndex = sortedRowIds.IndexOf(rowId);
        int startIndex = sortedRowIds.IndexOf(loop.loopBodyStartRow);
        int endIndex = sortedRowIds.IndexOf(loop.loopBodyEndRow);

        return rowIndex >= startIndex && rowIndex <= endIndex;
    }

    // ����Ƿ�Ӧ��ѭ����ת
    public bool ShouldLoopBack(int currentRowId)
    {
        if (activeLoops.Count == 0) return false;

        foreach (var loop in activeLoops)
        {
            // �ؼ���ֻ�л�Ծ��ѭ���ſ�����ת
            if (!loop.isActive) continue;

            if (currentRowId == loop.loopBodyEndRow)
            {
                // ֻ��ʣ����� > 0 ʱ����ת
                bool shouldJump = loop.remainingLoops > 0;
                Debug.Log($"ѭ����ת���: ��{currentRowId}, ʣ��{loop.remainingLoops}��, ��ת={shouldJump}");
                return shouldJump;
            }
        }

        return false;
    }


    // ��ȡ��תĿ����
    public int GetLoopBackRow(int currentRowId)
    {
        foreach (var loop in activeLoops)
        {
            if (!loop.isActive) continue;

            if (currentRowId == loop.loopBodyEndRow && loop.remainingLoops > 0)
            {
                int targetRow = loop.loopBodyStartRow;

                Debug.Log($"ִ��ѭ����ת: {currentRowId} -> {targetRow}, ʣ��{loop.remainingLoops}��");

                // ���ñ�ǣ�Ϊ��һ��ѭ����׼��
                loop.hasReducedThisCycle = false;

                return targetRow;
            }
        }

        return currentRowId;
    }

    // ����Ƿ�Ӧ������ѭ���壨��ѭ����������ʱ��
    public bool ShouldSkipRow(int currentRowId, int nextRowId)
    {
        foreach (var loop in activeLoops)
        {
            if (!loop.isActive && IsRowInLoopBody(nextRowId, loop) && !IsRowInLoopBody(currentRowId, loop))
            {
                Debug.Log($"���������ѭ����: {currentRowId} -> ѭ������");
                return true;
            }
        }
        return false;
    }

    // ��ȡ����Ŀ����
    public int GetSkipTargetRow(int currentRowId)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int currentIndex = sortedRowIds.IndexOf(currentRowId);

        // �ҵ���һ�������κλ�Ծѭ�����ڵ���
        for (int i = currentIndex; i < sortedRowIds.Count; i++)
        {
            int rowId = sortedRowIds[i];
            if (!IsRowInAnyActiveLoopBody(rowId))
            {
                Debug.Log($"����ѭ���嵽: {rowId}");
                return rowId;
            }
        }

        return currentRowId;
    }

    // ������Ƿ����κλ�Ծѭ������
    private bool IsRowInAnyActiveLoopBody(int rowId)
    {
        foreach (var loop in activeLoops)
        {
            if (loop.isActive && IsRowInLoopBody(rowId, loop))
                return true;
        }
        return false;
    }

    // Ϊָ�����ҵ����ڲ��ѭ��
    public LoopState FindInnermostLoopForRow(int rowId)
    {
        LoopState innermost = null;
        int maxIndent = -1;

        foreach (var loop in activeLoops)
        {
            if (loop.isActive && IsRowInLoopBody(rowId, loop) && loop.indentLevel > maxIndent)
            {
                innermost = loop;
                maxIndent = loop.indentLevel;
            }
        }

        return innermost;
    }

    // ��������ɵ�ѭ��
    public void CleanupCompletedLoops()
    {
        activeLoops.RemoveAll(loop => loop.IsCompleted());
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

    // ������Ϣ
    public void DebugLoopStates()
    {
        Debug.Log("=== ��ǰѭ��״̬ ===");
        foreach (var loop in activeLoops)
        {
            string status = loop.isActive ? "��Ծ" : "���";
            Debug.Log($"��{loop.startRowId}: ����{loop.indentLevel}, ��Χ{loop.loopBodyStartRow}-{loop.loopBodyEndRow}, ʣ��{loop.remainingLoops}��, ״̬{status}");
        }
    }
}