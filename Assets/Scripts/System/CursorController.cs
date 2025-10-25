using System.Collections;
using System.Linq;
using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Header("移动设置")]
    public float speed = 4.0f;

    [Header("引用")]
    public ChartParser parser;
    public ChartSpawner spawner;
    public LoopManager loopManager;
    public IfManager ifManager;
    public CommandManager commandManager;
    public ChartWindow chartWindow;
    public MusicPlayer musicPlayer;

    [Header("光标状态")]
    private float currentGridX = 0f;
    private int currentRowId = -1;
    public float cursorTime = 0f;
    public bool isActive = false;
    private bool isTimingActive = true;

    void Start()
    {
        Debug.Log("CursorController Start开始");

        // 确保所有组件引用正确
        if (parser == null) parser = FindObjectOfType<ChartParser>();
        if (spawner == null) spawner = FindObjectOfType<ChartSpawner>();
        if (loopManager == null) loopManager = FindObjectOfType<LoopManager>();
        if (ifManager == null) ifManager = FindObjectOfType<IfManager>();

        Debug.Log($"组件引用: Parser={parser != null}, Spawner={spawner != null}, LoopManager={loopManager != null}, IfManager={ifManager != null}");

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
        float bpm = parser.GetBPM();
        float offset = parser.GetOffset();

        Vector3 pos = transform.position;
        pos.z = -2f;
        transform.position = pos;

        speed = bpm / 60f * 2f;

        var sortedRowIds = parser.GetSortedRowIds();
        if (sortedRowIds.Count > 0)
        {
            currentRowId = sortedRowIds[0];
        }

        currentGridX = 0f;
        cursorTime = offset;

        // 初始化管理器
        loopManager.Initialize(parser);
        ifManager.Initialize(parser);
        parser.CalculateNoteTimestamps(speed);

        // 检查所有IF符号
        Debug.Log("=== 初始化时检查所有IF符号 ===");
        foreach (var note in parser.notes)
        {
            if (note.hasIfSymbol)
            {
                Debug.Log($"发现IF符号: 行{note.rowId}, 位置{note.position}, 条件码[{string.Join(",", note.ifConditionCodes)}]");
            }
        }

        if (commandManager == null)
            commandManager = FindObjectOfType<CommandManager>();
        commandManager?.ResetAllCommands();

        if (musicPlayer != null)
        {
            musicPlayer.PlayMusic();
        }

        UpdateWorldPosition();
        isActive = true;

        Debug.Log($"光标初始化完成，起始行: {currentRowId}");
    }

    void Update()
    {
        if (!isActive) return;

        CheckSymbolsAndCommands();

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

    void CheckSymbolsAndCommands()
    {
        if (currentGridX < 0.1f)
        {
            // *** 先检查IF符号，再处理循环体进入 ***
            CheckIfSymbols();
            CheckLoopSymbols();
            CheckLoopBodyEntry();
        }
        CheckCommands();
    }

    void CheckCommands()
    {
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId && note.hasCommand && !note.isCommandExecuted)
            {
                float distanceToNote = Mathf.Abs(currentGridX - note.position);
                if (distanceToNote < 0.05f)
                {
                    commandManager?.ExecuteCommand(note);
                }
            }
        }
    }

    void CheckLoopSymbols()
    {
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId && note.hasLoopSymbol && !note.loopSymbolTriggered)
            {
                note.loopSymbolTriggered = true;
                loopManager.OnEncounterLoopSymbol(note);
            }
        }
    }

    void CheckLoopBodyEntry()
    {
        var loopForCurrentRow = loopManager.FindInnermostLoopForRow(currentRowId);
        if (loopForCurrentRow != null && loopForCurrentRow.loopBodyStartRow == currentRowId)
        {
            if (loopForCurrentRow.isActive && !loopForCurrentRow.hasReducedThisCycle)
            {
                loopForCurrentRow.hasReducedThisCycle = true;

                if (loopForCurrentRow.remainingLoops > 0)
                {
                    loopForCurrentRow.remainingLoops--;

                    // *** 关键逻辑：根据是否是第一次进入决定是否重置IF符号 ***
                    if (loopForCurrentRow.isFirstEntry)
                    {
                        // 第一次进入：不重置IF符号，让IF符号正常触发一次
                        Debug.Log($"第一次进入循环体{loopForCurrentRow.startRowId}，不重置IF符号触发状态");
                        loopForCurrentRow.isFirstEntry = false; // 标记为已不是第一次
                    }
                    else
                    {
                        // 非第一次进入：重置IF符号触发状态，让IF符号可以再次触发
                        ResetIfSymbolsInLoopBody(loopForCurrentRow);
                        Debug.Log($"非第一次进入循环体{loopForCurrentRow.startRowId}，重置IF符号触发状态");
                    }

                    Debug.Log($"进入循环体: 行{currentRowId}, 减少次数, 剩余{loopForCurrentRow.remainingLoops}次");
                }

                if (loopForCurrentRow.remainingLoops == 0)
                {
                    loopForCurrentRow.isActive = false;
                    Debug.Log($"循环结束: 行{loopForCurrentRow.startRowId}, 剩余次数为0");
                }
            }
        }
    }


    // 重置循环体内的所有IF符号触发状态
    private void ResetIfSymbolsInLoopBody(LoopState loop)
    {
        Debug.Log($"=== 重置循环体{loop.startRowId}内的IF符号触发状态 ===");

        int resetCount = 0;

        // 获取循环体内的所有行
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(loop.loopBodyStartRow);
        int endIndex = sortedRowIds.IndexOf(loop.loopBodyEndRow);

        if (startIndex == -1 || endIndex == -1)
        {
            Debug.LogError($"无法找到循环体范围: {loop.loopBodyStartRow} -> {loop.loopBodyEndRow}");
            return;
        }

        Debug.Log($"循环体范围: 行{loop.loopBodyStartRow}(索引{startIndex}) -> 行{loop.loopBodyEndRow}(索引{endIndex})");

        // 遍历循环体内的所有行，重置IF符号触发状态
        for (int i = startIndex; i <= endIndex; i++)
        {
            int rowId = sortedRowIds[i];
            foreach (var note in parser.notes)
            {
                if (note.hasIfSymbol && note.rowId == rowId)
                {
                    // *** 重置所有IF符号的触发状态，不管当前状态如何 ***
                    bool wasTriggered = note.ifSymbolTriggered;
                    note.ifSymbolTriggered = false;

                    if (wasTriggered)
                    {
                        resetCount++;
                        Debug.Log($"重置IF符号触发状态: 行{note.rowId}, 位置{note.position}");
                    }
                    else
                    {
                        Debug.Log($"IF符号未触发，保持状态: 行{note.rowId}, 位置{note.position}");
                    }
                }
            }
        }

        Debug.Log($"总共重置了{resetCount}个IF符号的触发状态");
    }

 

    void CheckIfSymbols()
    {
        Debug.Log($"=== 检查IF符号开始: 当前行{currentRowId}, 光标位置{currentGridX} ===");

        var rowNotes = parser.notes.Where(n => n.rowId == currentRowId).ToList();
        Debug.Log($"当前行{currentRowId}共有{rowNotes.Count}个音符");

        bool foundIf = false;

        foreach (var note in rowNotes)
        {
            Debug.Log($"检查音符: 行{note.rowId}, 位置{note.position}, 类型{note.type}, 有IF符号{note.hasIfSymbol}");

            if (note.hasIfSymbol)
            {
                foundIf = true;
                Debug.Log($"找到IF符号: 行{currentRowId}, 位置{note.position}, 已触发{note.ifSymbolTriggered}, 条件码[{string.Join(",", note.ifConditionCodes)}]");

                if (!note.ifSymbolTriggered)
                {
                    float distanceToNote = Mathf.Abs(currentGridX - note.position);
                    Debug.Log($"IF符号距离计算: 光标位置{currentGridX}, 音符位置{note.position}, 距离{distanceToNote}");

                    if (distanceToNote < 0.1f)
                    {
                        Debug.Log($"*** 触发IF符号 ***: 行{currentRowId}, 位置{note.position}");
                        note.ifSymbolTriggered = true;
                        ifManager.OnEncounterIfSymbol(note);
                    }
                    else
                    {
                        Debug.Log($"IF符号距离太远: {distanceToNote} >= 0.1f，未触发");
                    }
                }
                else
                {
                    Debug.Log($"IF符号已触发过，跳过");
                }
            }
        }

        if (!foundIf)
        {
            Debug.Log($"当前行{currentRowId}没有找到任何IF符号");
        }

        Debug.Log($"=== 检查IF符号结束 ===\n");
    }



    // 统一的跳转方法（删除之前的IF符号处理）
    private void PerformJump(int targetRowId)
    {
        if (!RowExists(targetRowId))
        {
            Debug.LogError($"跳转目标行不存在: {targetRowId}");
            isTimingActive = false;
            return;
        }

        // 记录跳转前行
        int fromRow = currentRowId;

        // 执行跳转
        currentRowId = targetRowId;
        currentGridX = 0f;

        // 通知图表窗口
        chartWindow?.OnCursorJumpToNextRow(fromRow, targetRowId);

        UpdateWorldPosition();

        Debug.Log($"执行跳转: {fromRow} -> {targetRowId}");
    }

    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;

        Debug.Log($"行结束处理: 当前行{currentRowId}, 下一行{nextRow}");

        commandManager?.OnLeaveRow(currentRowId);
        loopManager.DebugLoopStates();

        int targetRow = nextRow; // 默认目标行

        // 优先级1: 循环跳转
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            targetRow = loopManager.GetLoopBackRow(currentRowId);
            Debug.Log($"循环跳转: {currentRowId} -> {targetRow}");
        }
        // 优先级2: 循环跳过
        else if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            targetRow = loopManager.GetSkipTargetRow(currentRowId);
            Debug.Log($"循环跳过: {currentRowId} -> {targetRow}");
        }
        // 优先级3: IF跳过
        else if (ifManager.ShouldSkipRow(nextRow))
        {
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            if (ifSkipTarget != currentRowId)
            {
                targetRow = ifSkipTarget;
                Debug.Log($"IF跳过: {currentRowId} -> {targetRow}");
            }
        }
        // 正常跳转
        else
        {
            Debug.Log($"正常跳转: {currentRowId} -> {targetRow}");
        }

        // 统一执行跳转
        PerformJump(targetRow);
    }

    void UpdateWorldPosition()
    {
        Vector2 baseWorldPos = parser.GridWorld(currentRowId, currentGridX);
        float indentOffset = GetCurrentRowIndentOffset() * parser.visualScale;

        Vector3 finalPos = new Vector3(
            baseWorldPos.x + indentOffset,
            baseWorldPos.y,
            transform.position.z
        );

        transform.position = finalPos;
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

    void CheckMultiNoteScanComplete(MultiNoteData multiNote)
    {
        if (multiNote.IsComplete() || multiNote.isJudged) return;

        float multiNoteEndX = multiNote.position + multiNote.length;
        bool isCursorInMulti = currentGridX >= multiNote.position && currentGridX <= multiNoteEndX;

        if (isCursorInMulti && !multiNote.hasEnteredJudgmentQueue)
        {
            multiNote.hasEnteredJudgmentQueue = true;
        }

        bool isScanComplete = currentGridX >= multiNoteEndX;
        if (isScanComplete && multiNote.hasEnteredJudgmentQueue)
        {
            AdvanceMultiNoteLayer(multiNote);
        }
    }

    void AdvanceMultiNoteLayer(MultiNoteData multiNote)
    {
        if (multiNote.IsComplete() || multiNote.isJudged) return;

        bool hasNextLayer = multiNote.MoveToNextLayer();
        multiNote.hasEnteredJudgmentQueue = false;

        if (hasNextLayer)
        {
            UpdateMultiNoteAppearance(multiNote);
        }
        else
        {
            multiNote.isJudged = true;
            if (multiNote.noteObject != null)
                multiNote.noteObject.SetActive(false);
        }
    }

    void UpdateMultiNoteAppearance(MultiNoteData multiNote)
    {
        if (multiNote.noteObject == null) return;

        var currentLayer = multiNote.GetCurrentLayer();
        if (currentLayer == null) return;

        Vector3 currentPosition = multiNote.noteObject.transform.position;
        Destroy(multiNote.noteObject);

        if (spawner != null)
        {
            spawner.SpawnMultiNote(multiNote);
            multiNote.noteObject.transform.position = currentPosition;
        }
        else
        {
            Debug.LogWarning("ChartSpawner引用丢失，无法更新Multi音符外观");
        }
    }

    public void JumpToRow(int rowId)
    {
        // 使用统一的跳转方法
        PerformJump(rowId);
    }

    bool ShouldJumpToNextRow()
    {
        return currentGridX > GetCurrentRowLength();
    }

    float GetCurrentRowLength()
    {
        var rowNotes = parser.notes
            .Where(n => n.rowId == currentRowId)
            .OrderBy(n => n.position)
            .ToList();

        if (rowNotes.Count == 0) return 0f;

        var lastNote = rowNotes.Last();
        int lastNoteIndex = rowNotes.Count - 1;
        float gapOffset = lastNoteIndex * parser.noteGap;
        float rowLength = lastNote.position + lastNote.length + gapOffset;

        return rowLength;
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