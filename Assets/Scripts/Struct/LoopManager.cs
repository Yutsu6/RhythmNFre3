using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class LoopState
{
    public int startRowId;          // 循环符号所在行
    public int indentLevel;         // 循环符号缩进级别
    public int[] loopCodes;         // 循环码数组
    public int currentReadCount = 0; // 当前读取次数
    public int remainingLoops = 0;   // 当前剩余循环次数
    public bool hasEnteredThisTime = false; // 新增：本次是否已进入

    // 循环体范围信息
    public int loopBodyStartRow = -1; // 循环体实际开始行
    public int endRowId = -1;         // 循环体最后一行
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

    // 进入循环体时调用
    public bool OnEnterLoopBody()
    {
        if (remainingLoops > 0)
        {
            remainingLoops--;
            return true; // 可以进入
        }
        return false; // 不能进入
    }


    // 切换到下一个循环码
    public bool MoveToNextCode()
    {
        currentReadCount++;
        if (currentReadCount < loopCodes.Length)
        {
            remainingLoops = loopCodes[currentReadCount];
            return true;
        }
        return false; // 没有更多循环码
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

        Debug.Log("=== 循环管理器初始化 ===");
    }

    // 遇到循环符号时调用
    public void OnEncounterLoopSymbol(NoteData loopNote)
    {
        // 检查是否已存在相同缩进级别的循环
        var existingLoop = activeLoops.FirstOrDefault(l => l.indentLevel == loopNote.indentLevel);

        if (existingLoop != null)
        {
            // 更新现有循环 - 但不要在这里增加 currentReadCount
            // 只在 OnOuterLoopCompleted 中增加
            Debug.Log($"遇到循环符号: 行{loopNote.rowId}, 当前读取次数{existingLoop.currentReadCount}, 剩余次数{existingLoop.remainingLoops}");
        }
        else
        {
            // 创建新循环状态
            var newLoop = new LoopState(loopNote.rowId, loopNote.indentLevel, loopNote.loopCodes);
            CalculateLoopRange(newLoop);
            activeLoops.Add(newLoop);
            Debug.Log($"创建新循环: 行{loopNote.rowId}, 缩进{loopNote.indentLevel}, 循环码[{string.Join(",", loopNote.loopCodes)}], 范围{newLoop.loopBodyStartRow}->{newLoop.endRowId}");
        }

        // 按缩进级别排序（内层在前）
        activeLoops.Sort((a, b) => b.indentLevel.CompareTo(a.indentLevel));
    }

    // 计算循环体范围
    private void CalculateLoopRange(LoopState loop)
    {
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(loop.startRowId);

        if (startIndex == -1 || startIndex >= sortedRowIds.Count - 1)
        {
            Debug.LogError($"无法计算循环范围: 行{loop.startRowId}");
            return;
        }

        // 循环体从下一行开始
        loop.loopBodyStartRow = sortedRowIds[startIndex + 1];

        // 找到循环体最后一行
        for (int i = startIndex + 1; i < sortedRowIds.Count; i++)
        {
            int rowId = sortedRowIds[i];
            int rowIndent = GetRowIndent(rowId);

            if (rowIndent <= loop.indentLevel)
            {
                // 上一行是循环体最后一行
                loop.endRowId = sortedRowIds[i - 1];
                break;
            }

            // 如果是最后一行
            if (i == sortedRowIds.Count - 1)
            {
                loop.endRowId = rowId;
            }
        }

        loop.isRangeCalculated = true;
    }

    // 检查是否应该循环跳转
    public bool ShouldLoopBack(int currentRowId)
    {
        if (activeLoops.Count == 0) return false;

        // 从内层到外层检查
        foreach (var loop in activeLoops)
        {
            if (!loop.isRangeCalculated) continue;

            // 检查是否到达这个循环体的末尾
            if (currentRowId == loop.endRowId && loop.remainingLoops > 0)
            {
                Debug.Log($"循环{loop.startRowId}到达末尾，需要跳转 (剩余次数{loop.remainingLoops})");
                return true;
            }
        }

        return false;
    }

    // 获取跳转目标行
    public int GetLoopBackRow(int currentRowId)
    {
        // 从内层到外层检查
        foreach (var loop in activeLoops)
        {
            if (!loop.isRangeCalculated) continue;

            if (currentRowId == loop.endRowId && loop.remainingLoops > 0)
            {
                Debug.Log($"循环{loop.startRowId}跳转: 行{currentRowId} -> 行{loop.loopBodyStartRow}, 剩余次数{loop.remainingLoops}");

                // 如果是外层循环，通知内层循环切换
                if (loop.indentLevel == 0) // 循环1的缩进
                {
                    OnOuterLoopCompleted(loop.indentLevel);
                }

                return loop.loopBodyStartRow;
            }
        }

        return currentRowId;
    }

    // 检查行是否应该跳过（当循环没有对应循环码时）
    public bool ShouldSkipRow(int currentRowId, int nextRowId)
    {
        foreach (var loop in activeLoops)
        {
            if (loop.remainingLoops == 0)
            {
                // 检查下一行是否在循环体内，且当前行不在循环体内
                // 这意味着光标即将进入循环体，但循环码=0，应该跳过
                if (IsRowInLoopBody(nextRowId, loop) && !IsRowInLoopBody(currentRowId, loop))
                {
                    Debug.Log($"跳过循环体{loop.startRowId}: 循环码为0，从{currentRowId}跳到循环体外");
                    return true;
                }
            }
        }
        return false;
    }

    // 获取跳过循环体后的目标行
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
                    Debug.Log($"跳过循环体{loop.startRowId}: {currentRowId} -> {skipTarget}");
                    return skipTarget;
                }
            }
        }

        return currentRowId;
    }

    // 找到当前行所属的最内层循环
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

    // 检查行是否在循环体内
    private bool IsRowInLoopBody(int rowId, LoopState loop)
    {
        if (!loop.isRangeCalculated) return false;

        var sortedRowIds = parser.GetSortedRowIds();
        int rowIndex = sortedRowIds.IndexOf(rowId);
        int startIndex = sortedRowIds.IndexOf(loop.loopBodyStartRow);
        int endIndex = sortedRowIds.IndexOf(loop.endRowId);

        return rowIndex >= startIndex && rowIndex <= endIndex;
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

    // 当外层循环完成一次时调用
    public void OnOuterLoopCompleted(int outerLoopIndent)
    {
        // 找到所有内层循环（缩进更大的）
        foreach (var loop in activeLoops)
        {
            if (loop.indentLevel > outerLoopIndent)
            {
                // 内层循环切换到下一个循环码
                loop.currentReadCount++;
                if (loop.currentReadCount < loop.loopCodes.Length)
                {
                    loop.remainingLoops = loop.loopCodes[loop.currentReadCount];
                    Debug.Log($"内层循环{loop.startRowId}切换到循环码: {loop.remainingLoops}");
                }
                else
                {
                    loop.remainingLoops = 0;
                    Debug.Log($"内层循环{loop.startRowId}无更多循环码");
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

            // 检查这一行是否在这个循环体内
            if (IsRowInLoopBody(rowId, loop) && loop.indentLevel > maxIndent)
            {
                innermostLoop = loop;
                maxIndent = loop.indentLevel;
            }
        }

        return innermostLoop;
    }



    // 调试信息
    public void DebugLoopStates()
    {
        Debug.Log("=== 当前循环状态 ===");
        foreach (var loop in activeLoops)
        {
            Debug.Log($"行{loop.startRowId}, 缩进{loop.indentLevel}, 读取次数{loop.currentReadCount}, 剩余次数{loop.remainingLoops}, 范围{loop.loopBodyStartRow}->{loop.endRowId}");
        }
    }


}