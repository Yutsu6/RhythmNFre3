using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class LoopState
{
    public int startRowId;          // 循环符号所在行
    public int indentLevel;         // 循环符号缩进级别
    public int[] loopCodes;         // 循环码数组
    public int currentLoopIndex = 0; // 当前循环码索引
    public int remainingLoops = 0;   // 当前剩余循环次数
    public bool isActive = false;    // 循环是否活跃
    public bool hasReducedThisCycle = false; // 本次循环是否已减少次数

    // 循环体范围信息（动态计算）
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
        hasReducedThisCycle = false; // 重置标记
        Debug.Log($"循环重置: 行{startRowId}, 剩余次数{remainingLoops}");
    }

    // 开始新循环周期时调用
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
        Debug.Log("循环管理器初始化");
    }

    // 遇到循环符号时调用
    public void OnEncounterLoopSymbol(NoteData loopNote)
    {
        int rowId = loopNote.rowId;
        int indent = loopNote.indentLevel;

        Debug.Log($"遇到循环符号: 行{rowId}, 缩进{indent}, 循环码[{string.Join(",", loopNote.loopCodes)}]");

        // 创建或获取循环状态
        if (!loopStatesByRow.ContainsKey(rowId))
        {
            loopStatesByRow[rowId] = new LoopState(rowId, indent, loopNote.loopCodes);
        }

        var loopState = loopStatesByRow[rowId];
        loopState.Reset();

        // 计算循环体范围
        CalculateLoopBodyRange(loopState);

        // 添加到活跃循环
        if (!activeLoops.Contains(loopState))
        {
            activeLoops.Add(loopState);
        }

        // 按缩进级别排序（缩进大的在前，内层优先）
        activeLoops.Sort((a, b) => b.indentLevel.CompareTo(a.indentLevel));

        DebugLoopStates();
    }

    // 根据缩进规则计算循环体范围
    private void CalculateLoopBodyRange(LoopState loop)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(loop.startRowId);

        if (startIndex == -1 || startIndex >= sortedRowIds.Count - 1)
        {
            Debug.LogError($"无法找到循环起始行: {loop.startRowId}");
            return;
        }

        // 循环体从下一行开始
        loop.loopBodyStartRow = sortedRowIds[startIndex + 1];
        loop.loopBodyEndRow = loop.loopBodyStartRow; // 初始值

        // 查找循环体结束行
        for (int i = startIndex + 1; i < sortedRowIds.Count; i++)
        {
            int currentRowId = sortedRowIds[i];
            int currentIndent = GetRowIndent(currentRowId);

            // 关键逻辑：如果当前行缩进 <= 循环符号缩进，说明循环体结束
            if (currentIndent <= loop.indentLevel)
            {
                // 上一行是循环体最后一行
                if (i > startIndex + 1)
                {
                    loop.loopBodyEndRow = sortedRowIds[i - 1];
                }
                else
                {
                    // 如果紧接着就是缩进<=的行，说明循环体为空
                    loop.loopBodyEndRow = loop.startRowId;
                }
                break;
            }

            // 如果是最后一行，该行就是循环体结束
            if (i == sortedRowIds.Count - 1)
            {
                loop.loopBodyEndRow = currentRowId;
            }
        }

        Debug.Log($"循环体范围: {loop.loopBodyStartRow} -> {loop.loopBodyEndRow} (循环符号行{loop.startRowId}, 缩进{loop.indentLevel})");
    }

    // 检查行是否在循环体内
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

    // 检查是否应该循环跳转
    public bool ShouldLoopBack(int currentRowId)
    {
        if (activeLoops.Count == 0) return false;

        foreach (var loop in activeLoops)
        {
            // 关键：只有活跃的循环才考虑跳转
            if (!loop.isActive) continue;

            if (currentRowId == loop.loopBodyEndRow)
            {
                // 只有剩余次数 > 0 时才跳转
                bool shouldJump = loop.remainingLoops > 0;
                Debug.Log($"循环跳转检查: 行{currentRowId}, 剩余{loop.remainingLoops}次, 跳转={shouldJump}");
                return shouldJump;
            }
        }

        return false;
    }


    // 获取跳转目标行
    public int GetLoopBackRow(int currentRowId)
    {
        foreach (var loop in activeLoops)
        {
            if (!loop.isActive) continue;

            if (currentRowId == loop.loopBodyEndRow && loop.remainingLoops > 0)
            {
                int targetRow = loop.loopBodyStartRow;

                Debug.Log($"执行循环跳转: {currentRowId} -> {targetRow}, 剩余{loop.remainingLoops}次");

                // 重置标记，为下一次循环做准备
                loop.hasReducedThisCycle = false;

                return targetRow;
            }
        }

        return currentRowId;
    }

    // 检查是否应该跳过循环体（当循环次数用完时）
    public bool ShouldSkipRow(int currentRowId, int nextRowId)
    {
        foreach (var loop in activeLoops)
        {
            if (!loop.isActive && IsRowInLoopBody(nextRowId, loop) && !IsRowInLoopBody(currentRowId, loop))
            {
                Debug.Log($"跳过已完成循环体: {currentRowId} -> 循环体外");
                return true;
            }
        }
        return false;
    }

    // 获取跳过目标行
    public int GetSkipTargetRow(int currentRowId)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int currentIndex = sortedRowIds.IndexOf(currentRowId);

        // 找到第一个不在任何活跃循环体内的行
        for (int i = currentIndex; i < sortedRowIds.Count; i++)
        {
            int rowId = sortedRowIds[i];
            if (!IsRowInAnyActiveLoopBody(rowId))
            {
                Debug.Log($"跳过循环体到: {rowId}");
                return rowId;
            }
        }

        return currentRowId;
    }

    // 检查行是否在任何活跃循环体内
    private bool IsRowInAnyActiveLoopBody(int rowId)
    {
        foreach (var loop in activeLoops)
        {
            if (loop.isActive && IsRowInLoopBody(rowId, loop))
                return true;
        }
        return false;
    }

    // 为指定行找到最内层的循环
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

    // 清理已完成的循环
    public void CleanupCompletedLoops()
    {
        activeLoops.RemoveAll(loop => loop.IsCompleted());
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

    // 调试信息
    public void DebugLoopStates()
    {
        Debug.Log("=== 当前循环状态 ===");
        foreach (var loop in activeLoops)
        {
            string status = loop.isActive ? "活跃" : "完成";
            Debug.Log($"行{loop.startRowId}: 缩进{loop.indentLevel}, 范围{loop.loopBodyStartRow}-{loop.loopBodyEndRow}, 剩余{loop.remainingLoops}次, 状态{status}");
        }
    }
}