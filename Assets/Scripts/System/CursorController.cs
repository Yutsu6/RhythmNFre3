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

        loopManager.Initialize(parser);
        ifManager.Initialize(parser);
        parser.CalculateNoteTimestamps(speed);

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
        var loopForCurrentRow = loopManager.FindInnermostLoopForRow(currentRowId);
        if (loopForCurrentRow != null && loopForCurrentRow.loopBodyStartRow == currentRowId)
        {
            // 关键：只在循环活跃且未减少过次数时减少
            if (loopForCurrentRow.isActive && !loopForCurrentRow.hasReducedThisCycle && loopForCurrentRow.remainingLoops > 0)
            {
                loopForCurrentRow.remainingLoops--;
                loopForCurrentRow.hasReducedThisCycle = true;

                Debug.Log($"进入循环体: 行{currentRowId}, 减少次数, 剩余{loopForCurrentRow.remainingLoops}次");

                // 如果减少后剩余次数为0，立即标记为不活跃
                if (loopForCurrentRow.remainingLoops == 0)
                {
                    loopForCurrentRow.isActive = false;
                    Debug.Log($"循环结束: 行{loopForCurrentRow.startRowId}, 剩余次数为0");
                }
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

    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;

        Debug.Log($"行结束处理: 当前行{currentRowId}, 下一行{nextRow}");

        commandManager?.OnLeaveRow(currentRowId);
        loopManager.DebugLoopStates();

        // 优先级1: 循环跳转
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            int loopBackRow = loopManager.GetLoopBackRow(currentRowId);
            currentRowId = loopBackRow;
            currentGridX = 0f;
            UpdateWorldPosition();
            Debug.Log($"执行循环回跳: -> 行{currentRowId}");
            return;
        }

        // 优先级2: 循环跳过
        if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            int skipTarget = loopManager.GetSkipTargetRow(currentRowId);
            JumpToRow(skipTarget);
            Debug.Log($"执行循环跳过: {currentRowId} -> 行{skipTarget}");
            return;
        }

        // 优先级3: IF跳过
        if (ifManager.ShouldSkipRow(nextRow))
        {
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            if (ifSkipTarget != currentRowId)
            {
                JumpToRow(ifSkipTarget);
                Debug.Log($"执行IF跳过: {currentRowId} -> 行{ifSkipTarget}");
                return;
            }
        }

        // 正常跳转
        Debug.Log($"正常跳转到下一行: {currentRowId} -> {nextRow}");
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