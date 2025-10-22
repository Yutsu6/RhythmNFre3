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
        parser.CalculateNoteTimestamps(speed);

        UpdateWorldPosition();
        isActive = true;

        Debug.Log($"����ʼ����ɣ���ʼ��: {currentRowId}");
    }

    void Update()
    {
        if (!isActive) return;

        // ���ѭ�����źͽ���ѭ����
        CheckLoopSymbolsAndEnterLoopBody();

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

    void CheckLoopSymbolsAndEnterLoopBody()
    {
        // �����׸������
        if (currentGridX < 0.1f)
        {
            // ���ѭ������
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasLoopSymbol && !note.loopSymbolTriggered)
                {
                    note.loopSymbolTriggered = true;
                    loopManager.OnEncounterLoopSymbol(note);
                    loopManager.DebugLoopStates();
                }
            }

            // ����Ƿ����ѭ���忪ʼ�У���ӷ��ظ�������
            var loopForCurrentRow = loopManager.FindLoopForRow(currentRowId);
            if (loopForCurrentRow != null && loopForCurrentRow.loopBodyStartRow == currentRowId)
            {
                // ֻ�ڵ�һ�ν���ʱ���ٴ���
                if (!loopForCurrentRow.hasEnteredThisTime && loopForCurrentRow.remainingLoops > 0)
                {
                    loopForCurrentRow.remainingLoops--;
                    loopForCurrentRow.hasEnteredThisTime = true; // ����ѽ���
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


    // ���ѭ������
    void CheckLoopSymbols()
    {
        // �����׸������
        if (currentGridX < 0.1f)
        {
            foreach (var note in parser.notes)
            {
                if (note.rowId == currentRowId && note.hasLoopSymbol && !note.loopSymbolTriggered)
                {
                    note.loopSymbolTriggered = true;
                    loopManager.OnEncounterLoopSymbol(note);

                    // ������Ϣ
                    loopManager.DebugLoopStates();
                }
            }
        }
    }

    // �н�������
    void ProcessRowEnd()
    {
        int nextRow = currentRowId - 1;

        // 1. �����һ���Ƿ���ĳ��ѭ�����ڣ��Ҹ�ѭ��û��ʣ�����
        var loopForNextRow = loopManager.FindLoopForRow(nextRow);
        if (loopForNextRow != null && loopForNextRow.remainingLoops <= 0)
        {
            // û�д����ˣ���������ѭ����
            int skipTarget = loopManager.GetSkipTargetRow(currentRowId);
            if (skipTarget != currentRowId)
            {
                JumpToRow(skipTarget);
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