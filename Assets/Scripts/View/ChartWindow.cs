using UnityEngine;
using System.Collections.Generic;

public class ChartWindow : MonoBehaviour
{
    [Header("�����������")]
    public float scrollTriggerY = 0f; // ����������Y���꣨��Ļ���룩
    public float rowHeight = 1.5f;
    public float scrollOffset = 1f; // ÿ�ι����Ķ���ƫ����

    [Header("����")]
    public CursorController cursor;
    public ChartParser parser;
    public ChartSpawner spawner;

    // ״̬
    private bool isScrollMode = false;
    private float totalScrollOffset = 0f;
    private Transform chartContainer;
    private List<GameObject> allNoteObjects = new List<GameObject>();
    private float initialCursorY;

    void Start()
    {
        InitializeChartWindow();
    }

    void InitializeChartWindow()
    {
        // �������ȡ��������
        if (chartContainer == null)
        {
            GameObject containerObj = GameObject.Find("ChartContainer");
            if (containerObj == null)
            {
                containerObj = new GameObject("ChartContainer");
            }
            chartContainer = containerObj.transform;
        }

        // �ռ�������������
        CollectAllNoteObjects();

        // ���ô���λ�ã���Ļ���룩
        if (Camera.main != null)
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
            scrollTriggerY = Camera.main.ScreenToWorldPoint(screenCenter).y;
            Debug.Log($"��������λ��: {scrollTriggerY}");
        }

        // ��¼����ʼY����
        if (cursor != null)
        {
            initialCursorY = cursor.transform.position.y;
        }
    }

    void Update()
    {
        if (cursor == null || !cursor.isActive) return;

        CheckScrollMode();
        UpdateCursorPosition();
    }

    void CheckScrollMode()
    {
        float cursorY = cursor.transform.position.y;

        // ����Ƿ�Ӧ�ý������ģʽ
        if (!isScrollMode && cursorY <= scrollTriggerY)
        {
            EnterScrollMode();
        }
    }

    void EnterScrollMode()
    {
        isScrollMode = true;
        Debug.Log("�����������ģʽ");

        // �̶�����Y�����ڴ���λ��
        Vector3 cursorPos = cursor.transform.position;
        cursor.transform.position = new Vector3(cursorPos.x, scrollTriggerY, cursorPos.z);
    }

    void UpdateCursorPosition()
    {
        if (!isScrollMode) return;

        // �ڹ���ģʽ�£����Y����̶���ֻ����X����
        Vector3 cursorPos = cursor.transform.position;
        float currentGridX = cursor.GetCurrentGridX();
        int currentRowId = cursor.GetCurrentRowId();

        // ���㲻���ǹ�����ԭʼλ��
        Vector2 rawPos = parser.GridWorld(currentRowId, currentGridX);
        float indentOffset = GetRowIndentOffset(currentRowId);

        cursor.transform.position = new Vector3(
            rawPos.x + indentOffset,
            scrollTriggerY, // Y����̶�
            cursorPos.z
        );
    }

    // �������ת����һ��ʱ����
    public void OnCursorJumpToNextRow(int fromRow, int toRow)
    {
        if (!isScrollMode) return;

        // ����������룺һ�и߶� + ����ƫ��
        float scrollDistance = rowHeight + scrollOffset;
        totalScrollOffset += scrollDistance;

        // Ӧ�ù�������������
        ApplyScrollToAllNotes(scrollDistance);

        Debug.Log($"�������: ����{fromRow}����{toRow}, ��������: {scrollDistance}, ��ƫ��: {totalScrollOffset}");
    }

    void ApplyScrollToAllNotes(float scrollDistance)
    {
        foreach (var noteObj in allNoteObjects)
        {
            if (noteObj != null)
            {
                Vector3 pos = noteObj.transform.position;
                pos.y += scrollDistance; // ���������ƶ����������Ϲ������Ӿ�Ч����
                noteObj.transform.position = pos;
            }
        }
    }

    // �ռ�������������
    void CollectAllNoteObjects()
    {
        allNoteObjects.Clear();

        if (spawner != null)
        {
            // ��spawner���Ӷ������ռ�
            foreach (Transform child in spawner.transform)
            {
                allNoteObjects.Add(child.gameObject);
            }
        }

        Debug.Log($"�ռ��� {allNoteObjects.Count} ����������");
    }

    // ��ȡ�е�����ƫ��
    float GetRowIndentOffset(int rowId)
    {
        if (parser == null) return 0f;

        foreach (var note in parser.notes)
        {
            if (note.rowId == rowId)
                return note.indentLevel * parser.cellSize;
        }
        return 0f;
    }

    // ���ù���״̬
    public void ResetScroll()
    {
        isScrollMode = false;
        totalScrollOffset = 0f;

        // ������������λ��
        ResetAllNotePositions();
    }

    void ResetAllNotePositions()
    {
        if (parser == null) return;

        foreach (var noteObj in allNoteObjects)
        {
            if (noteObj != null)
            {
                // �Ӷ������н����кź�λ��
                if (TryParseNoteInfo(noteObj.name, out int rowId, out float position, out string type))
                {
                    Vector2 worldPos = parser.GridWorld(rowId, position);
                    float indentOffset = GetRowIndentOffset(rowId);
                    noteObj.transform.position = new Vector3(
                        worldPos.x + indentOffset,
                        worldPos.y,
                        noteObj.transform.position.z
                    );
                }
            }
        }
    }

    bool TryParseNoteInfo(string objectName, out int rowId, out float position, out string type)
    {
        rowId = -1;
        position = 0f;
        type = "";

        try
        {
            // ������������ʽ: "type_rowX_posY"
            string[] parts = objectName.Split('_');
            if (parts.Length >= 3)
            {
                type = parts[0];
                if (parts[1].StartsWith("row") && int.TryParse(parts[1].Substring(3), out rowId))
                {
                    if (parts[2].StartsWith("pos") && float.TryParse(parts[2].Substring(3), out position))
                    {
                        return true;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"��������������ʧ��: {objectName}, ����: {e.Message}");
        }

        return false;
    }

    // �����������ⲿ����
    public bool IsInScrollMode => isScrollMode;
    public float TotalScrollOffset => totalScrollOffset;

    // �����µ���������ʱ����
    public void OnNewNoteSpawned(GameObject newNote)
    {
        if (!allNoteObjects.Contains(newNote))
        {
            allNoteObjects.Add(newNote);

            // ����ڹ���ģʽ�£�Ӧ�õ�ǰ����ƫ��
            if (isScrollMode && totalScrollOffset > 0)
            {
                Vector3 pos = newNote.transform.position;
                pos.y += totalScrollOffset;
                newNote.transform.position = pos;
            }
        }
    }
}