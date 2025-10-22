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
        var sortedRowIds = parser.GetSortedRowIds();
        if (sortedRowIds.Count > 0)
        {
            currentRowId = sortedRowIds[0];
        }

        currentGridX = 0f;
        cursorTime = 0f;

        loopManager.Initialize(parser);
        ifManager.Initialize(parser);
        parser.CalculateNoteTimestamps(speed);

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
        }

        UpdateWorldPosition();

        if (ShouldJumpToNextRow())
        {
            ProcessRowEnd();
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

        // �׶�1: IF��ת����
        if (ifManager.ShouldSkipRow(nextRow))
        {
            // ����Ҫ��������(nextRow)�������ǵ�ǰ��
            int ifSkipTarget = ifManager.GetSkipTargetRow(nextRow);
            Debug.Log($"IF��תĿ��: {ifSkipTarget}");
            if (ifSkipTarget != currentRowId)
            {
                JumpToRow(ifSkipTarget);
                return;
            }
        }

        // �׶�2: LOOP��ת���ߣ��������������߼����䣩
        // 1. �����һ���Ƿ���ĳ��ѭ�����ڣ��Ҹ�ѭ��û��ʣ�����
        var loopForNextRow = loopManager.FindLoopForRow(nextRow);
        if (loopManager.ShouldSkipRow(currentRowId, nextRow))
        {
            int loopSkipTarget = loopManager.GetSkipTargetRow(currentRowId);
            if (loopSkipTarget != currentRowId)
            {
                JumpToRow(loopSkipTarget);
                return;
            }
        }

        // 2. ����Ƿ���Ҫѭ����ת����ǰ�е���ѭ����ĩβ�һ���ʣ�������
        if (loopManager.ShouldLoopBack(currentRowId))
        {
            int loopBackRow = loopManager.GetLoopBackRow(currentRowId);
            currentRowId = loopBackRow;
            currentGridX = 0f;
            UpdateWorldPosition();
            Debug.Log($"*** ѭ����ת���: ��������{currentRowId} ***");
            return;
        }

        // 3. ������ת����һ��
        JumpToNextRow();
    }


    void JumpToNextRow()
    {
        int nextRowId = currentRowId - 1;

        if (!RowExists(nextRowId))
        {
            isTimingActive = false;
            //Debug.Log("�������");
            return;
        }

        currentRowId = nextRowId;
        currentGridX = 0f;
        UpdateWorldPosition();

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
        UpdateWorldPosition();

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