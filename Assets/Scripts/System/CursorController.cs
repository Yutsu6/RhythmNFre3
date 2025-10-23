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
    public IfManager ifManager;

    // 光标状态
    private float currentGridX = 0f;
    private int currentRowId = -1;
    public float cursorTime = 0f;
    public bool isActive = false;
    private bool isTimingActive = true;

    public ChartWindow chartWindow;

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
        // 获取BPM并计算光标速度
        float bpm = parser.GetBPM();
        float offset = parser.GetOffset();

        speed = bpm / 60f * 2f; // BPM转为基础速度


        var sortedRowIds = parser.GetSortedRowIds();
        if (sortedRowIds.Count > 0)
        {
            currentRowId = sortedRowIds[0];
        }

        currentGridX = 0f;
        cursorTime = offset;

        loopManager.Initialize(parser);
        ifManager.Initialize(parser);
        parser.CalculateNoteTimestamps(speed);

        // 确保spawner引用正确
        if (spawner == null)
        {
            spawner = FindObjectOfType<ChartSpawner>();
        }

        UpdateWorldPosition();
        isActive = true;

        Debug.Log($"光标初始化完成，起始行: {currentRowId}");
    }

    void Update()
    {
        if (!isActive) return;

        CheckLoopSymbols();      // 1. 先检测loop符号
        CheckIfSymbols();        // 2. 再检测if符号  
        CheckLoopBodyEntry();    // 3. 最后处理循环体进入

        // 横向移动
        if (isTimingActive)
        {
            currentGridX += speed * Time.deltaTime;
            cursorTime += Time.deltaTime;

            CheckMultiNoteScanning();
        }

        UpdateWorldPosition();

        if (ShouldJumpToNextRow())
        {
            ProcessRowEnd();
        }
    }

    void CheckMultiNoteScanning()
    {
        foreach (var note in parser.notes)
        {
            if (note.isMultiNote && !note.isJudged && note.rowId == currentRowId)
            {
                CheckMultiNoteScanComplete(note.AsMultiNote());
            }
        }
    }

    // 修改：检查Multi音符是否被光标扫描到
    void CheckMultiNoteScanComplete(MultiNoteData multiNote)
    {
        if (multiNote.IsComplete() || multiNote.isJudged) return;

        // 计算Multi音符的结束位置
        float multiNoteEndX = multiNote.position + multiNote.length;

        // 检查光标是否进入Multi音符范围
        bool isCursorInMulti = currentGridX >= multiNote.position && currentGridX <= multiNoteEndX;

        if (isCursorInMulti && !multiNote.hasEnteredJudgmentQueue)
        {
            // 第一次进入Multi音符
            multiNote.hasEnteredJudgmentQueue = true;
            Debug.Log($"光标开始扫描Multi音符: 行{multiNote.rowId}, 位置[{multiNote.position}→{multiNoteEndX}], 当前层={multiNote.GetCurrentLayer().type}");
        }

        // 检查光标是否扫描完成（超过了Multi音符的结束位置）
        bool isScanComplete = currentGridX >= multiNoteEndX;
        if (isScanComplete && multiNote.hasEnteredJudgmentQueue)
        {
            Debug.Log($"光标扫描完成Multi音符: 光标{currentGridX:F2} ≥ 结束位置{multiNoteEndX}");
            AdvanceMultiNoteLayer(multiNote);
        }

        // 调试信息（可选，避免日志过多）
        if (isCursorInMulti && Time.frameCount % 30 == 0) // 每30帧输出一次
        {
            Debug.Log($"扫描中: Multi位置[{multiNote.position}→{multiNoteEndX}], 光标={currentGridX:F2}, 剩余={multiNoteEndX - currentGridX:F2}");
        }
    }

    // 修改：推进Multi音符到下一层
    void AdvanceMultiNoteLayer(MultiNoteData multiNote)
    {
        if (multiNote.IsComplete() || multiNote.isJudged)
        {
            Debug.Log($"Multi音符已完成或已判定，跳过推进");
            return;
        }

        var currentLayer = multiNote.GetCurrentLayer();
        Debug.Log($"=== 开始推进Multi音符层 ===");
        Debug.Log($"位置: 行{multiNote.rowId} 位置{multiNote.position}");
        Debug.Log($"当前: 层{multiNote.currentLayerIndex}, 类型{currentLayer.type}");

        // 推进到下一层
        bool hasNextLayer = multiNote.MoveToNextLayer();

        // 重置进入标记，以便下一层可以再次被扫描
        multiNote.hasEnteredJudgmentQueue = false;

        if (hasNextLayer)
        {
            var nextLayer = multiNote.GetCurrentLayer();
            Debug.Log($"新当前层: 类型{nextLayer.type}, 剩余层数{multiNote.GetRemainingLayers()}");

            // 更新Multi音符的视觉表现
            UpdateMultiNoteAppearance(multiNote);
        }
        else
        {
            Debug.Log($"Multi音符全部完成");
            multiNote.isJudged = true;
            // 不隐藏音符，保持显示
        }

        Debug.Log($"=== Multi音符层推进完成 ===");
    }

    // 修改：更新Multi音符的外观为当前层的预制体，同时更新指示器
    void UpdateMultiNoteAppearance(MultiNoteData multiNote)
    {
        if (multiNote.noteObject == null) return;

        var currentLayer = multiNote.GetCurrentLayer();
        if (currentLayer == null) return;

        // 获取当前层对应的预制体
        GameObject correctPrefab = GetPrefabByType(currentLayer.type);
        if (correctPrefab == null)
        {
            Debug.LogWarning($"无法找到层类型 '{currentLayer.type}' 对应的预制体");
            return;
        }

        // 保存当前的位置和父对象
        Vector3 currentPosition = multiNote.noteObject.transform.position;
        Transform parent = multiNote.noteObject.transform.parent;
        string objectName = multiNote.noteObject.name;

        // 销毁旧的游戏对象
        Destroy(multiNote.noteObject);

        // 创建新的游戏对象（使用正确层的预制体）
        GameObject newNoteObject = Instantiate(correctPrefab, currentPosition, Quaternion.identity);

        // 设置属性
        newNoteObject.transform.parent = parent;
        newNoteObject.name = objectName;

        // 设置大小（使用Multi音符的总长度）
        SetupMultiNoteSize(newNoteObject, multiNote.length);

        // 更新引用
        multiNote.noteObject = newNoteObject;

        // 重新生成指示器和结构符号
        var spawner = FindObjectOfType<ChartSpawner>();
        if (spawner != null)
        {
            spawner.SpawnMultiIndicator(newNoteObject, multiNote);
            spawner.SpawnStructureSymbols(multiNote, newNoteObject);
        }

        Debug.Log($" Multi音符外观更新为: {currentLayer.type}");

        // 更新指示器文本（这里不需要了，因为SpawnMultiIndicator已经包含了）
        // 指示器会在下一帧自动更新，因为MultiIndicatorUpdater的Update方法会持续运行
    }


    // 新增：设置Multi音符大小的方法
    void SetupMultiNoteSize(GameObject noteObject, float length)
    {
        var parser = FindObjectOfType<ChartParser>();
        if (parser == null) return;

        Vector3 currentScale = noteObject.transform.localScale;
        noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
    }

    // 新增：根据类型获取预制体（复制ChartSpawner的逻辑）
    GameObject GetPrefabByType(string type)
    {
        var spawner = FindObjectOfType<ChartSpawner>();
        if (spawner == null) return null;

        switch (type)
        {
            case "tap": return spawner.tapNote;
            case "break": return spawner.breakNote;
            case "hold": return spawner.holdNote;
            case "track": return spawner.track;
            default: return null;
        }
    }

    void CheckLoopSymbols()
    {
        // 在行首附近检查
        if (currentGridX < 0.1f)
        {
            // 只检查循环符号触发
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasLoopSymbol && !note.loopSymbolTriggered)
                {
                    note.loopSymbolTriggered = true;
                    loopManager.OnEncounterLoopSymbol(note);
                    loopManager.DebugLoopStates();
                }
            }
        }
    }

    void CheckLoopBodyEntry()
    {
        // 在行首附近检查
        if (currentGridX < 0.1f)
        {
            // 检查是否进入循环体开始行
            var loopForCurrentRow = loopManager.FindLoopForRow(currentRowId);
            if (loopForCurrentRow != null && loopForCurrentRow.loopBodyStartRow == currentRowId)
            {
                // 只在第一次进入时减少次数
                if (!loopForCurrentRow.hasEnteredThisTime && loopForCurrentRow.remainingLoops > 0)
                {
                    loopForCurrentRow.remainingLoops--;
                    loopForCurrentRow.hasEnteredThisTime = true;
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

    void CheckIfSymbols()
    {
        if (currentGridX < 0.1f)
        {
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasIfSymbol && !note.ifSymbolTriggered)
                {
                    note.ifSymbolTriggered = true;
                    ifManager.OnEncounterIfSymbol(note);

                    int readCount = ifManager.GetReadCount(note);
                    Debug.Log($"触发if符号: 行{currentRowId}, 第{readCount}次读取");
                }
            }
        }
        else
        {
            // 离开行首区域时重置触发标记
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasIfSymbol)
                {
                    note.ifSymbolTriggered = false;
                }
            }
        }
    }



    // 行结束处理
    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;
        Debug.Log($"ProcessRowEnd: 当前行{currentRowId}, 下一行{nextRow}");

        // 阶段1: 清理已完成的循环
        loopManager.CleanupCompletedLoops();

        // 阶段2: IF跳转决策
        if (ifManager.ShouldSkipRow(nextRow))
        {
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            Debug.Log($"IF跳转目标: {ifSkipTarget}");
            if (ifSkipTarget != currentRowId)
            {
                JumpToRow(ifSkipTarget);
                return;
            }
        }

        // 阶段3: LOOP跳过决策（循环次数为0时跳过整个循环体）
        if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            int loopSkipTarget = loopManager.GetSkipTargetRow(currentRowId);
            if (loopSkipTarget != currentRowId)
            {
                JumpToRow(loopSkipTarget);
                return;
            }
        }

        // 阶段4: LOOP跳转决策（还有剩余次数时循环跳转）
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            int loopBackRow = loopManager.GetLoopBackRow(currentRowId);
            currentRowId = loopBackRow;
            currentGridX = 0f;
            transform.position = GetFinalCursorPosition();
            Debug.Log($"*** 循环跳转完成: 现在在行{currentRowId} ***");
            return;
        }

        // 阶段5: 正常跳转到下一行
        JumpToNextRow();
    }


    void JumpToNextRow()
    {
        int nextRowId = currentRowId - 1;

        if (!RowExists(nextRowId))
        {
            isTimingActive = false;
            return;
        }

        currentRowId = nextRowId;
        currentGridX = 0f;
        transform.position = GetFinalCursorPosition(); // 改为使用统一方法

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
        transform.position = GetFinalCursorPosition(); // 改为使用统一方法

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

    // 添加一个方法来获取考虑所有偏移的最终位置
    Vector2 GetFinalCursorPosition()
    {
        float scrollOffset = chartWindow != null ? chartWindow.currentScrollOffset : 0f;

        // 基础网格位置（不考虑缩进和滚动）
        Vector2 rawGridPos = parser.GridWorld(currentRowId, currentGridX);

        // 应用缩进偏移
        float indentOffset = GetCurrentRowIndentOffset();

        // 最终位置 = 基础位置 + 缩进 + 滚动
        return new Vector2(
            rawGridPos.x + indentOffset,
            rawGridPos.y + scrollOffset
        );
    }


    void UpdateWorldPosition()
    {
        transform.position = GetFinalCursorPosition();
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