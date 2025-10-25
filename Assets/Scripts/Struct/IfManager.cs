using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class IfState
{
    public int startRowId;          // if符号所在行
    public int indentLevel;         // 缩进级别
    public int[] conditionCodes;    // 判断码数组 {1,0,1}等
    public int currentReadCount = 0; // 当前读取次数
    public bool shouldEnterBody = false; // 当前是否应该进入判断体

    // 判断体范围信息
    public int ifBodyStartRow = -1; // 判断体实际开始行
    public int endRowId = -1;       // 判断体最后一行
    public bool isRangeCalculated = false;

    public IfState(int row, int indent, int[] codes)
    {
        startRowId = row;
        indentLevel = indent;
        conditionCodes = codes;
        currentReadCount = 0;
        shouldEnterBody = false;
    }

    // 更新当前判断条件
    public bool UpdateAndGetCondition()
    {
        if (conditionCodes == null || conditionCodes.Length == 0)
        {
            shouldEnterBody = false;
            Debug.LogWarning($"IF条件码为空: 行{startRowId}");
            return false;
        }

        // 计算当前应该使用的条件码
        int currentConditionCode;
        if (currentReadCount < conditionCodes.Length)
        {
            // 在条件码数组范围内：使用对应的条件码
            currentConditionCode = conditionCodes[currentReadCount];
        }
        else
        {
            // 超出条件码数组范围：一律为0
            currentConditionCode = 0;
        }

        bool result = currentConditionCode == 1;
        shouldEnterBody = result;

        Debug.Log($"IF条件更新: " +
                 $"行{startRowId}, " +
                 $"当前计数{currentReadCount}, " +
                 $"条件码[{string.Join(",", conditionCodes)}], " +
                 $"当前码{currentConditionCode}, " +
                 $"结果{result}, " +
                 $"进入判断体{shouldEnterBody}");

        // *** 移除这里的自增，让外部控制 ***
        // currentReadCount++; // 注释掉这一行

        return result;
    }

    // 重置读取计数（用于循环场景）
    public void ResetForNewLoop()
    {
        currentReadCount = 0;
        shouldEnterBody = false;
        Debug.Log($"IF重置: 行{startRowId}, 计数归零");
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
        Debug.Log("=== 判断管理器初始化 ===");
    }

    // 遇到if符号时调用
    public void OnEncounterIfSymbol(NoteData ifNote)
    {
        Debug.Log($"=== 遇到IF符号开始处理 ===");
        Debug.Log($"IF符号数据: 行{ifNote.rowId}, 缩进{ifNote.indentLevel}, 条件码[{string.Join(",", ifNote.ifConditionCodes)}]");

        var existingIf = activeIfs.Find(i =>
            i.startRowId == ifNote.rowId && i.indentLevel == ifNote.indentLevel);

        if (existingIf != null)
        {
            Debug.Log($"找到现有IF状态: 行{existingIf.startRowId}, 当前计数{existingIf.currentReadCount}");

            // *** 步骤1：使用当前读取次数获取条件结果 ***
            bool conditionResult = existingIf.UpdateAndGetCondition();
            Debug.Log($"IF条件最终结果: {conditionResult}");

            // *** 步骤2：增加读取次数，为下一次循环做准备 ***
            existingIf.currentReadCount++;
            Debug.Log($"IF符号读取次数增加: {existingIf.currentReadCount - 1} -> {existingIf.currentReadCount}");
        }
        else
        {
            Debug.Log($"创建新的IF状态");
            var newIf = new IfState(ifNote.rowId, ifNote.indentLevel, ifNote.ifConditionCodes);
            CalculateIfRange(newIf);

            // *** 步骤1：使用当前读取次数（0）获取条件结果 ***
            bool conditionResult = newIf.UpdateAndGetCondition();
            Debug.Log($"IF条件最终结果: {conditionResult}");

            // *** 步骤2：增加读取次数，为下一次循环做准备 ***
            newIf.currentReadCount++;
            Debug.Log($"IF符号读取次数增加: {newIf.currentReadCount - 1} -> {newIf.currentReadCount}");

            activeIfs.Add(newIf);

            Debug.Log($"新IF状态创建完成: 条件结果{conditionResult}, 范围{newIf.ifBodyStartRow}->{newIf.endRowId}");
        }

        DebugIfStates();
        Debug.Log($"=== 遇到IF符号处理结束 ===\n");
    }

    public int GetReadCount(NoteData note)
    {
        var ifState = activeIfs.Find(i =>
            i.startRowId == note.rowId && i.indentLevel == note.indentLevel);
        return ifState?.currentReadCount ?? 0;
    }

    // 计算判断体范围
    private void CalculateIfRange(IfState ifState)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(ifState.startRowId);

        if (startIndex == -1 || startIndex >= sortedRowIds.Count - 1)
        {
            Debug.LogError($"无法计算判断范围: 行{ifState.startRowId}");
            return;
        }

        // 判断体从下一行开始
        ifState.ifBodyStartRow = sortedRowIds[startIndex + 1];

        // 找到判断体最后一行
        for (int i = startIndex + 1; i < sortedRowIds.Count; i++)
        {
            int rowId = sortedRowIds[i];
            int rowIndent = GetRowIndent(rowId);

            if (rowIndent <= ifState.indentLevel)
            {
                // 上一行是判断体最后一行
                ifState.endRowId = sortedRowIds[i - 1];
                break;
            }

            // 如果是最后一行
            if (i == sortedRowIds.Count - 1)
            {
                ifState.endRowId = rowId;
            }
        }

        ifState.isRangeCalculated = true;
        Debug.Log($"判断体范围计算完成: {ifState.ifBodyStartRow}->{ifState.endRowId}, isRangeCalculated={ifState.isRangeCalculated}");
    }

    // 检查行是否应该跳过
    public bool ShouldSkipRow(int rowId)
    {
        Debug.Log($"=== 检查是否跳过行{rowId} ===");

        foreach (var ifState in activeIfs)
        {
            Debug.Log($"检查IF状态: 行{ifState.startRowId}, 范围{ifState.ifBodyStartRow}->{ifState.endRowId}, 进入{ifState.shouldEnterBody}");

            bool inBody = IsRowInIfBody(rowId, ifState);
            Debug.Log($"行{rowId}在判断体{ifState.startRowId}内: {inBody}");

            if (inBody && !ifState.shouldEnterBody)
            {
                Debug.Log($"需要跳过行{rowId}");
                return true;
            }
        }

        Debug.Log($"不需要跳过行{rowId}");
        return false;
    }

    // 获取跳过判断体后的目标行
    public int GetSkipTargetRow(int rowToSkip)
    {
        var innermostIf = FindInnermostIfForRow(rowToSkip);
        if (innermostIf != null && !innermostIf.shouldEnterBody)
        {
            var sortedRowIds = parser.GetSortedRowIds();
            int endIndex = sortedRowIds.IndexOf(innermostIf.endRowId);

            if (endIndex >= 0 && endIndex + 1 < sortedRowIds.Count)
            {
                int skipTarget = sortedRowIds[endIndex + 1];
                Debug.Log($"跳过判断体{innermostIf.startRowId}: {rowToSkip} -> {skipTarget}");
                return skipTarget;
            }
            else
            {
                Debug.LogWarning($"无法找到判断体{innermostIf.startRowId}的结束位置");
            }
        }

        return rowToSkip;
    }

    // 检查行是否在判断体内
    private bool IsRowInIfBody(int rowId, IfState ifState)
    {
        Debug.Log($"IsRowInIfBody开始: rowId={rowId}, isRangeCalculated={ifState.isRangeCalculated}");

        if (!ifState.isRangeCalculated)
        {
            Debug.Log("范围未计算，返回false");
            return false;
        }
        var sortedRowIds = parser.GetSortedRowIds();
        int rowIndex = sortedRowIds.IndexOf(rowId);
        int startIndex = sortedRowIds.IndexOf(ifState.ifBodyStartRow);
        int endIndex = sortedRowIds.IndexOf(ifState.endRowId);

        Debug.Log($"IsRowInIfBody: 行{rowId}在[{ifState.ifBodyStartRow}->{ifState.endRowId}]范围内? rowIndex={rowIndex}, startIndex={startIndex}, endIndex={endIndex}");

        return rowIndex >= startIndex && rowIndex <= endIndex;
    }

    // 找到当前行所属的最内层判断
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

    // 获取行的缩进级别
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

    // 当外层循环完成时，重置内层判断的计数
    public void OnOuterLoopCompleted(int outerLoopIndent)
    {
        foreach (var ifState in activeIfs)
        {
            if (ifState.indentLevel > outerLoopIndent)
            {
                ifState.ResetForNewLoop();
                Debug.Log($"内层判断{ifState.startRowId}重置: 新的计数{ifState.currentReadCount}, 条件码{ifState.conditionCodes[0]}");
            }
        }
    }

    // 调试信息
    public void DebugIfStates()
    {
        Debug.Log("=== 当前判断状态 ===");
        foreach (var ifState in activeIfs)
        {
            Debug.Log($"行{ifState.startRowId}, 缩进{ifState.indentLevel}, 读取次数{ifState.currentReadCount}, 进入{ifState.shouldEnterBody}, 范围{ifState.ifBodyStartRow}->{ifState.endRowId}");
        }
    }
}