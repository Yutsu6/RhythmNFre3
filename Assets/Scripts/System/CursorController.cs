using System.Collections;
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

    [Header("光标状态")]
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
        // 获取BPM并计算光标速度
        float bpm = parser.GetBPM();
        float offset = parser.GetOffset();

        // 设置 z 坐标
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

        // 初始化命令管理器
        if (commandManager == null)
            commandManager = FindObjectOfType<CommandManager>();
        commandManager?.ResetAllCommands();

        UpdateWorldPosition();
        isActive = true;

        Debug.Log($"光标初始化完成，起始行: {currentRowId}");
    }

    void Update()
    {
        if (!isActive) return;

        CheckSymbolsAndCommands();

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

    // 合并检查方法
    void CheckSymbolsAndCommands()
    {
        if (currentGridX < 0.1f) // 行首检查
        {
            CheckLoopSymbols();
            CheckIfSymbols();
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
        var loopForCurrentRow = loopManager.FindLoopForRow(currentRowId);
        if (loopForCurrentRow != null && loopForCurrentRow.loopBodyStartRow == currentRowId)
        {
            if (!loopForCurrentRow.hasEnteredThisTime && loopForCurrentRow.remainingLoops > 0)
            {
                loopForCurrentRow.remainingLoops--;
                loopForCurrentRow.hasEnteredThisTime = true;
            }
        }
    }

    void CheckIfSymbols()
    {
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId && note.hasIfSymbol && !note.ifSymbolTriggered)
            {
                note.ifSymbolTriggered = true;
                ifManager.OnEncounterIfSymbol(note);
            }
        }
    }

    void UpdateWorldPosition()
    {
        Vector2 baseWorldPos = parser.GridWorld(currentRowId, currentGridX);
        float indentOffset = GetCurrentRowIndentOffset();

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
        }
    }

    void UpdateMultiNoteAppearance(MultiNoteData multiNote)
    {
        if (multiNote.noteObject == null) return;

        var currentLayer = multiNote.GetCurrentLayer();
        if (currentLayer == null) return;

        GameObject correctPrefab = GetPrefabByType(currentLayer.type);
        if (correctPrefab == null) return;

        // 重新实例化音符对象
        Vector3 currentPosition = multiNote.noteObject.transform.position;
        Destroy(multiNote.noteObject);

        GameObject newNoteObject = Instantiate(correctPrefab, currentPosition, Quaternion.identity);
        newNoteObject.name = multiNote.noteObject.name;
        SetupMultiNoteSize(newNoteObject, multiNote.length);
        multiNote.noteObject = newNoteObject;

        // 重新生成符号和指示器
        spawner?.SpawnMultiIndicator(newNoteObject, multiNote);
        spawner?.SpawnStructureSymbols(multiNote, newNoteObject);
    }

    void SetupMultiNoteSize(GameObject noteObject, float length)
    {
        Vector3 currentScale = noteObject.transform.localScale;
        noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
    }

    GameObject GetPrefabByType(string type)
    {
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

    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;

        commandManager?.OnLeaveRow(currentRowId);
        loopManager.CleanupCompletedLoops();

        // 检查各种跳转条件
        if (TryIfSkip(nextRow)) return;
        if (TryLoopSkip(nextRow)) return;
        if (TryLoopBack()) return;

        JumpToNextRow();
    }

    bool TryIfSkip(int nextRow)
    {
        if (ifManager.ShouldSkipRow(nextRow))
        {
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            if (ifSkipTarget != currentRowId)
            {
                JumpToRow(ifSkipTarget);
                return true;
            }
        }
        return false;
    }

    bool TryLoopSkip(int nextRow)
    {
        if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            int loopSkipTarget = loopManager.GetSkipTargetRow(currentRowId);
            if (loopSkipTarget != currentRowId)
            {
                JumpToRow(loopSkipTarget);
                return true;
            }
        }
        return false;
    }

    bool TryLoopBack()
    {
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            int loopBackRow = loopManager.GetLoopBackRow(currentRowId);
            currentRowId = loopBackRow;
            currentGridX = 0f;
            UpdateWorldPosition();
            return true;
        }
        return false;
    }

    void JumpToNextRow()
    {
        int nextRowId = currentRowId - 1;

        if (!RowExists(nextRowId))
        {
            isTimingActive = false;
            return;
        }

        // 通知 ChartWindow 跳转
        chartWindow?.OnCursorJumpToNextRow(currentRowId, nextRowId);

        currentRowId = nextRowId;
        currentGridX = 0f;
        UpdateWorldPosition();
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
    }

    bool ShouldJumpToNextRow()
    {
        return currentGridX > GetCurrentRowLength();
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