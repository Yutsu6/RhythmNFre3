using System.Collections;
using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Header("�ƶ�����")]
    public float speed = 4.0f;

    [Header("����")]
    public ChartParser parser;
    public ChartSpawner spawner;
    public LoopManager loopManager;
    public IfManager ifManager;
    public CommandManager commandManager;
    public ChartWindow chartWindow;

    [Header("���״̬")]
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
        Debug.Log("���ȴ���������...");
        yield return new WaitForEndOfFrame();

        float timeout = 5f;
        float startTime = Time.realtimeSinceStartup;

        while (parser == null || parser.notes == null || parser.notes.Count == 0)
        {
            if (Time.realtimeSinceStartup - startTime >= timeout)
            {
                Debug.LogError("�ȴ��������ݳ�ʱ��");
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        InitializeCursor();
    }

    void InitializeCursor()
    {
        // ��ȡBPM���������ٶ�
        float bpm = parser.GetBPM();
        float offset = parser.GetOffset();

        // ���� z ����
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

        // ��ʼ��������
        loopManager.Initialize(parser);
        ifManager.Initialize(parser);
        parser.CalculateNoteTimestamps(speed);

        // ��ʼ�����������
        if (commandManager == null)
            commandManager = FindObjectOfType<CommandManager>();
        commandManager?.ResetAllCommands();

        UpdateWorldPosition();
        isActive = true;

        Debug.Log($"����ʼ����ɣ���ʼ��: {currentRowId}");
    }

    void Update()
    {
        if (!isActive) return;

        CheckSymbolsAndCommands();

        // �����ƶ�
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

    // �ϲ���鷽��
    void CheckSymbolsAndCommands()
    {
        if (currentGridX < 0.1f) // ���׼��
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

        // ����ʵ������������
        Vector3 currentPosition = multiNote.noteObject.transform.position;
        Destroy(multiNote.noteObject);

        GameObject newNoteObject = Instantiate(correctPrefab, currentPosition, Quaternion.identity);
        newNoteObject.name = multiNote.noteObject.name;
        SetupMultiNoteSize(newNoteObject, multiNote.length);
        multiNote.noteObject = newNoteObject;

        // �������ɷ��ź�ָʾ��
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

        // ��������ת����
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

        // ֪ͨ ChartWindow ��ת
        chartWindow?.OnCursorJumpToNextRow(currentRowId, nextRowId);

        currentRowId = nextRowId;
        currentGridX = 0f;
        UpdateWorldPosition();
    }

    public void JumpToRow(int rowId)
    {
        if (!RowExists(rowId))
        {
            Debug.LogError($"��תĿ���в�����: {rowId}");
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