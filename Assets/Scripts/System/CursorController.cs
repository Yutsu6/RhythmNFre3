using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorController : MonoBehaviour
{

    //�ƶ�����
    public float speed = 4.0f;

    //����
    public ChartParser parser;
    public ChartSpawner spawner;

    //���״̬
    private float currentGridX = 0f;//��ǰ������
    private int currentRowId = -1;//��ǰ����
    public float cursorTime = 0f;
    public bool isActive = false;
    private bool isTimingActive = true;
    //private bool isInitialized = false;

    public System.Action<Vector2, int, float> OnCursorPositionChanged;

    void Start()
    {
        StartCoroutine(WaitForChartReady());
    }

    System.Collections.IEnumerator WaitForChartReady()
    {
        Debug.Log("���ȴ���������...");

        // �ȴ�һ֡��ȷ�����������Start����ִ�����
        yield return null;

        // �ȴ�ֱ���������������������׼������
        while (parser == null || parser.notes.Count == 0)
        {
            Debug.Log("�ȴ���������...");
            yield return new WaitForSeconds(0.1f); // ÿ0.1����һ��
        }

        // ����ȴ�һС��ʱ�䣬ȷ������������ʵ�������
        yield return new WaitForSeconds(0.2f);

        // ��ʼ�����
        InitializeCursor();

        Debug.Log("����׼����������꿪ʼɨ�裡");
    }

    void Update()
    {
        if (!isActive) return;

        //�����ƶ�
        if (isTimingActive)
        {
            currentGridX += speed * Time.deltaTime;

            cursorTime += Time.deltaTime;
        }


        //������������
        UpdateWorldPosition();

        //��ת�߼�
        if (ShouldJumpToNextRow())
        {
            JumpToNextRow();
        }
    }

    //���λ�ó�ʼ��
    void InitializeCursor()
    {

        currentRowId = -1;
        currentGridX = 0f;
        cursorTime = 0f;

        if (parser != null)
        {
            parser.CalculateNoteTimestamps(speed);
        }
        else
        {
            Debug.LogError("ParserΪ�գ�");
        }

        UpdateWorldPosition();

        isActive = true;

        Debug.Log($"����ʼ����ɣ��ٶ�: {speed}, ��ʼʱ��: {cursorTime}");


    }

    void UpdateWorldPosition()
    {
        // ���㿼����������������
        Vector2 worldPos = CalculateWorldPositionWithIndent(currentRowId, currentGridX);
        transform.position = new Vector3(worldPos.x, worldPos.y, 0);

        // ֪ͨ�ж�ϵͳλ���Ѹ���
        OnCursorPositionChanged?.Invoke(worldPos, currentRowId, currentGridX);
    }

    // ���㿼����������������
    Vector2 CalculateWorldPositionWithIndent(int rowId, float gridX)
    {
        // ����λ�ã�������������
        Vector2 baseWorldPos = parser.GridWorld(rowId, gridX);

        // Ӧ�õ�ǰ�е�����ƫ��
        float indentOffset = GetCurrentRowIndentOffset();

        Vector2 finalWorldPos = new Vector2(
            baseWorldPos.x + indentOffset,
            baseWorldPos.y
        );

        return finalWorldPos;
    }

    // ��ȡ��ǰ�е�����ƫ����
    float GetCurrentRowIndentOffset()
    {
        // �ҵ���ǰ�еĵ�һ����������ȡ��������
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId)
            {
                return note.indentLevel; // ֱ��ʹ����������Ϊƫ��
            }
        }
        return 0f; // Ĭ��������
    }

    bool ShouldJumpToNextRow()
    {
        float currentRowlength = GetCurrentRowLength();

        return currentGridX > currentRowlength;
    }

    float GetCurrentRowLength()
    {
        float maxPosition = 0f;

        // ���㲻�����������г��ȣ�ֻ��ע��������Ĳ��֣�
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId)
            {
                // ������ʵ�ʽ���λ�� = ����λ�� + ���ȣ�������������
                float noteEnd = note.position + note.length;
                if (noteEnd > maxPosition) maxPosition = noteEnd;
            }
        }

        return maxPosition;
    }

    void JumpToNextRow()
    {
        int nextRowId = currentRowId - 1;

        if (!RowExists(nextRowId))
        {
            isTimingActive = false;
            return;
        }

        //��ת
        currentRowId = nextRowId;
        currentGridX = 0f;

        UpdateWorldPosition();
    }

    //���Ƿ����
    bool RowExists(int rowId)
    {
        if (parser == null) return false;


        foreach (var note in parser.notes)
        {
            if (note.rowId == rowId)
            {
                return true;
            }
        }
        return false;
    }

    // ����������������ϵͳ���ʵ�ǰ״̬
    public int GetCurrentRowId() => currentRowId;
    public float GetCurrentGridX() => currentGridX;
    public Vector2 GetWorldPosition() => transform.position;

    // ��������ȡ��ǰ�е�������
    public float GetCurrentRowIndent()
    {
        return GetCurrentRowIndentOffset();
    }
}