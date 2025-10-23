using System.Collections;
using UnityEngine;

public class CursorController : MonoBehaviour
{
    // �ƶ�����
    public float speed = 4.0f;

    // ����
    public ChartParser parser;
    public ChartSpawner spawner;
    public LoopManager loopManager;
    public IfManager ifManager;

    // ���״̬
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

        speed = bpm / 60f * 2f; // BPMתΪ�����ٶ�


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

        // ȷ��spawner������ȷ
        if (spawner == null)
        {
            spawner = FindObjectOfType<ChartSpawner>();
        }

        UpdateWorldPosition();
        isActive = true;

        Debug.Log($"����ʼ����ɣ���ʼ��: {currentRowId}");
    }

    void Update()
    {
        if (!isActive) return;

        CheckLoopSymbols();      // 1. �ȼ��loop����
        CheckIfSymbols();        // 2. �ټ��if����  
        CheckLoopBodyEntry();    // 3. �����ѭ�������

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

    // �޸ģ����Multi�����Ƿ񱻹��ɨ�赽
    void CheckMultiNoteScanComplete(MultiNoteData multiNote)
    {
        if (multiNote.IsComplete() || multiNote.isJudged) return;

        // ����Multi�����Ľ���λ��
        float multiNoteEndX = multiNote.position + multiNote.length;

        // ������Ƿ����Multi������Χ
        bool isCursorInMulti = currentGridX >= multiNote.position && currentGridX <= multiNoteEndX;

        if (isCursorInMulti && !multiNote.hasEnteredJudgmentQueue)
        {
            // ��һ�ν���Multi����
            multiNote.hasEnteredJudgmentQueue = true;
            Debug.Log($"��꿪ʼɨ��Multi����: ��{multiNote.rowId}, λ��[{multiNote.position}��{multiNoteEndX}], ��ǰ��={multiNote.GetCurrentLayer().type}");
        }

        // ������Ƿ�ɨ����ɣ�������Multi�����Ľ���λ�ã�
        bool isScanComplete = currentGridX >= multiNoteEndX;
        if (isScanComplete && multiNote.hasEnteredJudgmentQueue)
        {
            Debug.Log($"���ɨ�����Multi����: ���{currentGridX:F2} �� ����λ��{multiNoteEndX}");
            AdvanceMultiNoteLayer(multiNote);
        }

        // ������Ϣ����ѡ��������־���ࣩ
        if (isCursorInMulti && Time.frameCount % 30 == 0) // ÿ30֡���һ��
        {
            Debug.Log($"ɨ����: Multiλ��[{multiNote.position}��{multiNoteEndX}], ���={currentGridX:F2}, ʣ��={multiNoteEndX - currentGridX:F2}");
        }
    }

    // �޸ģ��ƽ�Multi��������һ��
    void AdvanceMultiNoteLayer(MultiNoteData multiNote)
    {
        if (multiNote.IsComplete() || multiNote.isJudged)
        {
            Debug.Log($"Multi��������ɻ����ж��������ƽ�");
            return;
        }

        var currentLayer = multiNote.GetCurrentLayer();
        Debug.Log($"=== ��ʼ�ƽ�Multi������ ===");
        Debug.Log($"λ��: ��{multiNote.rowId} λ��{multiNote.position}");
        Debug.Log($"��ǰ: ��{multiNote.currentLayerIndex}, ����{currentLayer.type}");

        // �ƽ�����һ��
        bool hasNextLayer = multiNote.MoveToNextLayer();

        // ���ý����ǣ��Ա���һ������ٴα�ɨ��
        multiNote.hasEnteredJudgmentQueue = false;

        if (hasNextLayer)
        {
            var nextLayer = multiNote.GetCurrentLayer();
            Debug.Log($"�µ�ǰ��: ����{nextLayer.type}, ʣ�����{multiNote.GetRemainingLayers()}");

            // ����Multi�������Ӿ�����
            UpdateMultiNoteAppearance(multiNote);
        }
        else
        {
            Debug.Log($"Multi����ȫ�����");
            multiNote.isJudged = true;
            // ������������������ʾ
        }

        Debug.Log($"=== Multi�������ƽ���� ===");
    }

    // �޸ģ�����Multi���������Ϊ��ǰ���Ԥ���壬ͬʱ����ָʾ��
    void UpdateMultiNoteAppearance(MultiNoteData multiNote)
    {
        if (multiNote.noteObject == null) return;

        var currentLayer = multiNote.GetCurrentLayer();
        if (currentLayer == null) return;

        // ��ȡ��ǰ���Ӧ��Ԥ����
        GameObject correctPrefab = GetPrefabByType(currentLayer.type);
        if (correctPrefab == null)
        {
            Debug.LogWarning($"�޷��ҵ������� '{currentLayer.type}' ��Ӧ��Ԥ����");
            return;
        }

        // ���浱ǰ��λ�ú͸�����
        Vector3 currentPosition = multiNote.noteObject.transform.position;
        Transform parent = multiNote.noteObject.transform.parent;
        string objectName = multiNote.noteObject.name;

        // ���پɵ���Ϸ����
        Destroy(multiNote.noteObject);

        // �����µ���Ϸ����ʹ����ȷ���Ԥ���壩
        GameObject newNoteObject = Instantiate(correctPrefab, currentPosition, Quaternion.identity);

        // ��������
        newNoteObject.transform.parent = parent;
        newNoteObject.name = objectName;

        // ���ô�С��ʹ��Multi�������ܳ��ȣ�
        SetupMultiNoteSize(newNoteObject, multiNote.length);

        // ��������
        multiNote.noteObject = newNoteObject;

        // ��������ָʾ���ͽṹ����
        var spawner = FindObjectOfType<ChartSpawner>();
        if (spawner != null)
        {
            spawner.SpawnMultiIndicator(newNoteObject, multiNote);
            spawner.SpawnStructureSymbols(multiNote, newNoteObject);
        }

        Debug.Log($" Multi������۸���Ϊ: {currentLayer.type}");

        // ����ָʾ���ı������ﲻ��Ҫ�ˣ���ΪSpawnMultiIndicator�Ѿ������ˣ�
        // ָʾ��������һ֡�Զ����£���ΪMultiIndicatorUpdater��Update�������������
    }


    // ����������Multi������С�ķ���
    void SetupMultiNoteSize(GameObject noteObject, float length)
    {
        var parser = FindObjectOfType<ChartParser>();
        if (parser == null) return;

        Vector3 currentScale = noteObject.transform.localScale;
        noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
    }

    // �������������ͻ�ȡԤ���壨����ChartSpawner���߼���
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
        // �����׸������
        if (currentGridX < 0.1f)
        {
            // ֻ���ѭ�����Ŵ���
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
        // �����׸������
        if (currentGridX < 0.1f)
        {
            // ����Ƿ����ѭ���忪ʼ��
            var loopForCurrentRow = loopManager.FindLoopForRow(currentRowId);
            if (loopForCurrentRow != null && loopForCurrentRow.loopBodyStartRow == currentRowId)
            {
                // ֻ�ڵ�һ�ν���ʱ���ٴ���
                if (!loopForCurrentRow.hasEnteredThisTime && loopForCurrentRow.remainingLoops > 0)
                {
                    loopForCurrentRow.remainingLoops--;
                    loopForCurrentRow.hasEnteredThisTime = true;
                    Debug.Log($"����ѭ����{loopForCurrentRow.startRowId}��ʼ�У�ʣ�����: {loopForCurrentRow.remainingLoops}");
                }
            }
        }
        else
        {
            // �뿪����ʱ���ý�����
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
                    Debug.Log($"����if����: ��{currentRowId}, ��{readCount}�ζ�ȡ");
                }
            }
        }
        else
        {
            // �뿪��������ʱ���ô������
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasIfSymbol)
                {
                    note.ifSymbolTriggered = false;
                }
            }
        }
    }



    // �н�������
    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;
        Debug.Log($"ProcessRowEnd: ��ǰ��{currentRowId}, ��һ��{nextRow}");

        // �׶�1: ��������ɵ�ѭ��
        loopManager.CleanupCompletedLoops();

        // �׶�2: IF��ת����
        if (ifManager.ShouldSkipRow(nextRow))
        {
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            Debug.Log($"IF��תĿ��: {ifSkipTarget}");
            if (ifSkipTarget != currentRowId)
            {
                JumpToRow(ifSkipTarget);
                return;
            }
        }

        // �׶�3: LOOP�������ߣ�ѭ������Ϊ0ʱ��������ѭ���壩
        if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            int loopSkipTarget = loopManager.GetSkipTargetRow(currentRowId);
            if (loopSkipTarget != currentRowId)
            {
                JumpToRow(loopSkipTarget);
                return;
            }
        }

        // �׶�4: LOOP��ת���ߣ�����ʣ�����ʱѭ����ת��
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            int loopBackRow = loopManager.GetLoopBackRow(currentRowId);
            currentRowId = loopBackRow;
            currentGridX = 0f;
            transform.position = GetFinalCursorPosition();
            Debug.Log($"*** ѭ����ת���: ��������{currentRowId} ***");
            return;
        }

        // �׶�5: ������ת����һ��
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
        transform.position = GetFinalCursorPosition(); // ��Ϊʹ��ͳһ����

        Debug.Log($"������ת����һ��: {currentRowId}");
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
        transform.position = GetFinalCursorPosition(); // ��Ϊʹ��ͳһ����

        Debug.Log($"*** ִ����ת: {currentRowId} ***");
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

    // ���һ����������ȡ��������ƫ�Ƶ�����λ��
    Vector2 GetFinalCursorPosition()
    {
        float scrollOffset = chartWindow != null ? chartWindow.currentScrollOffset : 0f;

        // ��������λ�ã������������͹�����
        Vector2 rawGridPos = parser.GridWorld(currentRowId, currentGridX);

        // Ӧ������ƫ��
        float indentOffset = GetCurrentRowIndentOffset();

        // ����λ�� = ����λ�� + ���� + ����
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