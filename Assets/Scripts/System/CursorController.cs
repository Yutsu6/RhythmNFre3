using System.Collections;
using UnityEngine;

public class CursorController : MonoBehaviour
{
    // 移动设置
    public float speed = 4.0f;

    // 引用
    public ChartParser parser;
    public ChartSpawner spawner;
    public LoopManager loopManager;

    // 光标状态
    private float currentGridX = 0f;
    private int currentRowId = -1;
    public float cursorTime = 0f;
    public bool isActive = false;
    private bool isTimingActive = true;

    void Start()
    {
        StartCoroutine(WaitForChartReady());
    }

    IEnumerator WaitForChartReady()
    {
        Debug.Log("光标等待谱面生成...");
        yield return new WaitForEndOfFrame();

        float timeout = 5f;
        float startTime = Time.realtimeSinceStartup;

        while (parser == null || parser.notes == null || parser.notes.Count == 0)
        {
            if (Time.realtimeSinceStartup - startTime >= timeout)
            {
                Debug.LogError("等待谱面数据超时！");
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        InitializeCursor();
    }

    void InitializeCursor()
    {
        var sortedRowIds = parser.GetSortedRowIds();
        if (sortedRowIds.Count > 0)
        {
            currentRowId = sortedRowIds[0];
        }

        currentGridX = 0f;
        cursorTime = 0f;

        loopManager.Initialize(parser);
        parser.CalculateNoteTimestamps(speed);

        UpdateWorldPosition();
        isActive = true;

        Debug.Log($"光标初始化完成，起始行: {currentRowId}");
    }

    void Update()
    {
        if (!isActive) return;

        // 检查循环符号和进入循环体
        CheckLoopSymbolsAndEnterLoopBody();

        // 横向移动
        if (isTimingActive)
        {
            currentGridX += speed * Time.deltaTime;
            cursorTime += Time.deltaTime;
        }

        UpdateWorldPosition();

        if (ShouldJumpToNextRow())
        {
            ProcessRowEnd();
        }
    }

    void CheckLoopSymbolsAndEnterLoopBody()
    {
        // 在行首附近检查
        if (currentGridX < 0.1f)
        {
            // 检查循环符号
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasLoopSymbol && !note.loopSymbolTriggered)
                {
                    note.loopSymbolTriggered = true;
                    loopManager.OnEncounterLoopSymbol(note);
                    loopManager.DebugLoopStates();
                }
            }

            // 检查是否进入循环体开始行（添加防重复触发）
            var loopForCurrentRow = loopManager.FindLoopForRow(currentRowId);
            if (loopForCurrentRow != null && loopForCurrentRow.loopBodyStartRow == currentRowId)
            {
                // 只在第一次进入时减少次数
                if (!loopForCurrentRow.hasEnteredThisTime && loopForCurrentRow.remainingLoops > 0)
                {
                    loopForCurrentRow.remainingLoops--;
                    loopForCurrentRow.hasEnteredThisTime = true; // 标记已进入
                    Debug.Log($"进入循环体{loopForCurrentRow.startRowId}开始行，剩余次数: {loopForCurrentRow.remainingLoops}");
                }
            }
        }
        else
        {
            // 离开行首时重置进入标记
            var loopForCurrentRow = loopManager.FindLoopForRow(currentRowId);
            if (loopForCurrentRow != null)
            {
                loopForCurrentRow.hasEnteredThisTime = false;
            }
        }
    }


    // 检查循环符号
    void CheckLoopSymbols()
    {
        // 在行首附近检查
        if (currentGridX < 0.1f)
        {
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasLoopSymbol && !note.loopSymbolTriggered)
                {
                    note.loopSymbolTriggered = true;
                    loopManager.OnEncounterLoopSymbol(note);

                    // 调试信息
                    loopManager.DebugLoopStates();
                }
            }
        }
    }

    // 行结束处理
    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;

        // 1. 检查下一行是否在某个循环体内，且该循环没有剩余次数
        var loopForNextRow = loopManager.FindLoopForRow(nextRow);
        if (loopForNextRow != null && loopForNextRow.remainingLoops <= 0)
        {
            // 没有次数了，跳过整个循环体
            int skipTarget = loopManager.GetSkipTargetRow(currentRowId);
            if (skipTarget != currentRowId)
            {
                JumpToRow(skipTarget);
                return;
            }
        }

        // 2. 检查是否需要循环跳转（当前行到达循环体末尾且还有剩余次数）
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            int loopBackRow = loopManager.GetLoopBackRow(currentRowId);
            currentRowId = loopBackRow;
            currentGridX = 0f;
            UpdateWorldPosition();
            Debug.Log($"*** 循环跳转完成: 现在在行{currentRowId} ***");
            return;
        }

        // 3. 正常跳转到下一行
        JumpToNextRow();
    }


    void JumpToNextRow()
    {
        int nextRowId = currentRowId - 1;

        if (!RowExists(nextRowId))
        {
            isTimingActive = false;
            //Debug.Log("谱面结束");
            return;
        }

        currentRowId = nextRowId;
        currentGridX = 0f;
        UpdateWorldPosition();

        Debug.Log($"正常跳转到下一行: {currentRowId}");
    }

    public void JumpToRow(int rowId)
    {
        if (!RowExists(rowId))
        {
            Debug.LogError($"跳转目标行不存在: {rowId}");
            return;
        }

        currentRowId = rowId;
        currentGridX = 0f;
        UpdateWorldPosition();

        Debug.Log($"*** 执行跳转: {currentRowId} ***");
    }

    bool ShouldJumpToNextRow()
    {
        float currentRowLength = GetCurrentRowLength();
        return currentGridX > currentRowLength;
    }

    float GetCurrentRowLength()
    {
        float maxPosition = 0f;
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId)
            {
                float noteEnd = note.position + note.length;
                if (noteEnd > maxPosition) maxPosition = noteEnd;
            }
        }
        return maxPosition;
    }

    bool RowExists(int rowId)
    {
        if (parser == null) return false;
        foreach (var note in parser.notes)
        {
            if (note.rowId == rowId) return true;
        }
        return false;
    }

    void UpdateWorldPosition()
    {
        Vector2 baseWorldPos = parser.GridWorld(currentRowId, currentGridX);
        float indentOffset = GetCurrentRowIndentOffset();
        Vector2 finalPos = new Vector2(baseWorldPos.x + indentOffset, baseWorldPos.y);
        transform.position = finalPos;
    }

    float GetCurrentRowIndentOffset()
    {
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId)
                return note.indentLevel * parser.cellSize;
        }
        return 0f;
    }

    public int GetCurrentRowId() => currentRowId;
    public float GetCurrentGridX() => currentGridX;
}