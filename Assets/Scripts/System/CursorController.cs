using System.Collections;
using System.Linq;
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
    public MusicPlayer musicPlayer;

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

        Debug.Log($"����ʼ����ɣ���ʼ��: {currentRowId}");
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
            // �ؼ���ֻ��ѭ����Ծ��δ���ٹ�����ʱ����
            if (loopForCurrentRow.isActive && !loopForCurrentRow.hasReducedThisCycle && loopForCurrentRow.remainingLoops > 0)
            {
                loopForCurrentRow.remainingLoops--;
                loopForCurrentRow.hasReducedThisCycle = true;

                Debug.Log($"����ѭ����: ��{currentRowId}, ���ٴ���, ʣ��{loopForCurrentRow.remainingLoops}��");

                // ������ٺ�ʣ�����Ϊ0���������Ϊ����Ծ
                if (loopForCurrentRow.remainingLoops == 0)
                {
                    loopForCurrentRow.isActive = false;
                    Debug.Log($"ѭ������: ��{loopForCurrentRow.startRowId}, ʣ�����Ϊ0");
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
            Debug.LogWarning("ChartSpawner���ö�ʧ���޷�����Multi�������");
        }
    }

    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;

        Debug.Log($"�н�������: ��ǰ��{currentRowId}, ��һ��{nextRow}");

        commandManager?.OnLeaveRow(currentRowId);
        loopManager.DebugLoopStates();

        // ���ȼ�1: ѭ����ת
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            int loopBackRow = loopManager.GetLoopBackRow(currentRowId);
            currentRowId = loopBackRow;
            currentGridX = 0f;
            UpdateWorldPosition();
            Debug.Log($"ִ��ѭ������: -> ��{currentRowId}");
            return;
        }

        // ���ȼ�2: ѭ������
        if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            int skipTarget = loopManager.GetSkipTargetRow(currentRowId);
            JumpToRow(skipTarget);
            Debug.Log($"ִ��ѭ������: {currentRowId} -> ��{skipTarget}");
            return;
        }

        // ���ȼ�3: IF����
        if (ifManager.ShouldSkipRow(nextRow))
        {
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            if (ifSkipTarget != currentRowId)
            {
                JumpToRow(ifSkipTarget);
                Debug.Log($"ִ��IF����: {currentRowId} -> ��{ifSkipTarget}");
                return;
            }
        }

        // ������ת
        Debug.Log($"������ת����һ��: {currentRowId} -> {nextRow}");
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