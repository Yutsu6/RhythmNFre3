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
        Debug.Log("CursorController Start��ʼ");

        // ȷ���������������ȷ
        if (parser == null) parser = FindObjectOfType<ChartParser>();
        if (spawner == null) spawner = FindObjectOfType<ChartSpawner>();
        if (loopManager == null) loopManager = FindObjectOfType<LoopManager>();
        if (ifManager == null) ifManager = FindObjectOfType<IfManager>();

        Debug.Log($"�������: Parser={parser != null}, Spawner={spawner != null}, LoopManager={loopManager != null}, IfManager={ifManager != null}");

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

        // ��ʼ��������
        loopManager.Initialize(parser);
        ifManager.Initialize(parser);
        parser.CalculateNoteTimestamps(speed);

        // �������IF����
        Debug.Log("=== ��ʼ��ʱ�������IF���� ===");
        foreach (var note in parser.notes)
        {
            if (note.hasIfSymbol)
            {
                Debug.Log($"����IF����: ��{note.rowId}, λ��{note.position}, ������[{string.Join(",", note.ifConditionCodes)}]");
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
            // *** �ȼ��IF���ţ��ٴ���ѭ������� ***
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

                    // *** �ؼ��߼��������Ƿ��ǵ�һ�ν�������Ƿ�����IF���� ***
                    if (loopForCurrentRow.isFirstEntry)
                    {
                        // ��һ�ν��룺������IF���ţ���IF������������һ��
                        Debug.Log($"��һ�ν���ѭ����{loopForCurrentRow.startRowId}��������IF���Ŵ���״̬");
                        loopForCurrentRow.isFirstEntry = false; // ���Ϊ�Ѳ��ǵ�һ��
                    }
                    else
                    {
                        // �ǵ�һ�ν��룺����IF���Ŵ���״̬����IF���ſ����ٴδ���
                        ResetIfSymbolsInLoopBody(loopForCurrentRow);
                        Debug.Log($"�ǵ�һ�ν���ѭ����{loopForCurrentRow.startRowId}������IF���Ŵ���״̬");
                    }

                    Debug.Log($"����ѭ����: ��{currentRowId}, ���ٴ���, ʣ��{loopForCurrentRow.remainingLoops}��");
                }

                if (loopForCurrentRow.remainingLoops == 0)
                {
                    loopForCurrentRow.isActive = false;
                    Debug.Log($"ѭ������: ��{loopForCurrentRow.startRowId}, ʣ�����Ϊ0");
                }
            }
        }
    }


    // ����ѭ�����ڵ�����IF���Ŵ���״̬
    private void ResetIfSymbolsInLoopBody(LoopState loop)
    {
        Debug.Log($"=== ����ѭ����{loop.startRowId}�ڵ�IF���Ŵ���״̬ ===");

        int resetCount = 0;

        // ��ȡѭ�����ڵ�������
        var sortedRowIds = parser.GetSortedRowIds();
        int startIndex = sortedRowIds.IndexOf(loop.loopBodyStartRow);
        int endIndex = sortedRowIds.IndexOf(loop.loopBodyEndRow);

        if (startIndex == -1 || endIndex == -1)
        {
            Debug.LogError($"�޷��ҵ�ѭ���巶Χ: {loop.loopBodyStartRow} -> {loop.loopBodyEndRow}");
            return;
        }

        Debug.Log($"ѭ���巶Χ: ��{loop.loopBodyStartRow}(����{startIndex}) -> ��{loop.loopBodyEndRow}(����{endIndex})");

        // ����ѭ�����ڵ������У�����IF���Ŵ���״̬
        for (int i = startIndex; i <= endIndex; i++)
        {
            int rowId = sortedRowIds[i];
            foreach (var note in parser.notes)
            {
                if (note.hasIfSymbol && note.rowId == rowId)
                {
                    // *** ��������IF���ŵĴ���״̬�����ܵ�ǰ״̬��� ***
                    bool wasTriggered = note.ifSymbolTriggered;
                    note.ifSymbolTriggered = false;

                    if (wasTriggered)
                    {
                        resetCount++;
                        Debug.Log($"����IF���Ŵ���״̬: ��{note.rowId}, λ��{note.position}");
                    }
                    else
                    {
                        Debug.Log($"IF����δ����������״̬: ��{note.rowId}, λ��{note.position}");
                    }
                }
            }
        }

        Debug.Log($"�ܹ�������{resetCount}��IF���ŵĴ���״̬");
    }

 

    void CheckIfSymbols()
    {
        Debug.Log($"=== ���IF���ſ�ʼ: ��ǰ��{currentRowId}, ���λ��{currentGridX} ===");

        var rowNotes = parser.notes.Where(n => n.rowId == currentRowId).ToList();
        Debug.Log($"��ǰ��{currentRowId}����{rowNotes.Count}������");

        bool foundIf = false;

        foreach (var note in rowNotes)
        {
            Debug.Log($"�������: ��{note.rowId}, λ��{note.position}, ����{note.type}, ��IF����{note.hasIfSymbol}");

            if (note.hasIfSymbol)
            {
                foundIf = true;
                Debug.Log($"�ҵ�IF����: ��{currentRowId}, λ��{note.position}, �Ѵ���{note.ifSymbolTriggered}, ������[{string.Join(",", note.ifConditionCodes)}]");

                if (!note.ifSymbolTriggered)
                {
                    float distanceToNote = Mathf.Abs(currentGridX - note.position);
                    Debug.Log($"IF���ž������: ���λ��{currentGridX}, ����λ��{note.position}, ����{distanceToNote}");

                    if (distanceToNote < 0.1f)
                    {
                        Debug.Log($"*** ����IF���� ***: ��{currentRowId}, λ��{note.position}");
                        note.ifSymbolTriggered = true;
                        ifManager.OnEncounterIfSymbol(note);
                    }
                    else
                    {
                        Debug.Log($"IF���ž���̫Զ: {distanceToNote} >= 0.1f��δ����");
                    }
                }
                else
                {
                    Debug.Log($"IF�����Ѵ�����������");
                }
            }
        }

        if (!foundIf)
        {
            Debug.Log($"��ǰ��{currentRowId}û���ҵ��κ�IF����");
        }

        Debug.Log($"=== ���IF���Ž��� ===\n");
    }



    // ͳһ����ת������ɾ��֮ǰ��IF���Ŵ���
    private void PerformJump(int targetRowId)
    {
        if (!RowExists(targetRowId))
        {
            Debug.LogError($"��תĿ���в�����: {targetRowId}");
            isTimingActive = false;
            return;
        }

        // ��¼��תǰ��
        int fromRow = currentRowId;

        // ִ����ת
        currentRowId = targetRowId;
        currentGridX = 0f;

        // ֪ͨͼ����
        chartWindow?.OnCursorJumpToNextRow(fromRow, targetRowId);

        UpdateWorldPosition();

        Debug.Log($"ִ����ת: {fromRow} -> {targetRowId}");
    }

    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;

        Debug.Log($"�н�������: ��ǰ��{currentRowId}, ��һ��{nextRow}");

        commandManager?.OnLeaveRow(currentRowId);
        loopManager.DebugLoopStates();

        int targetRow = nextRow; // Ĭ��Ŀ����

        // ���ȼ�1: ѭ����ת
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            targetRow = loopManager.GetLoopBackRow(currentRowId);
            Debug.Log($"ѭ����ת: {currentRowId} -> {targetRow}");
        }
        // ���ȼ�2: ѭ������
        else if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            targetRow = loopManager.GetSkipTargetRow(currentRowId);
            Debug.Log($"ѭ������: {currentRowId} -> {targetRow}");
        }
        // ���ȼ�3: IF����
        else if (ifManager.ShouldSkipRow(nextRow))
        {
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            if (ifSkipTarget != currentRowId)
            {
                targetRow = ifSkipTarget;
                Debug.Log($"IF����: {currentRowId} -> {targetRow}");
            }
        }
        // ������ת
        else
        {
            Debug.Log($"������ת: {currentRowId} -> {targetRow}");
        }

        // ͳһִ����ת
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
            Debug.LogWarning("ChartSpawner���ö�ʧ���޷�����Multi�������");
        }
    }

    public void JumpToRow(int rowId)
    {
        // ʹ��ͳһ����ת����
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