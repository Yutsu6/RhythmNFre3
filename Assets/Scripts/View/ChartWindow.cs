using UnityEngine;
using System.Collections.Generic;

public class ChartWindow : MonoBehaviour
{
    [Header("谱面滚动设置")]
    public float scrollTriggerY = 0f; // 触发滚动的Y坐标（屏幕中央）
    public float rowHeight = 1.5f;
    public float scrollOffset = 1f; // 每次滚动的额外偏移量

    [Header("引用")]
    public CursorController cursor;
    public ChartParser parser;
    public ChartSpawner spawner;

    // 状态
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
        // 创建或获取谱面容器
        if (chartContainer == null)
        {
            GameObject containerObj = GameObject.Find("ChartContainer");
            if (containerObj == null)
            {
                containerObj = new GameObject("ChartContainer");
            }
            chartContainer = containerObj.transform;
        }

        // 收集所有音符对象
        CollectAllNoteObjects();

        // 设置触发位置（屏幕中央）
        if (Camera.main != null)
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
            scrollTriggerY = Camera.main.ScreenToWorldPoint(screenCenter).y;
            Debug.Log($"滚动触发位置: {scrollTriggerY}");
        }

        // 记录光标初始Y坐标
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

        // 检查是否应该进入滚动模式
        if (!isScrollMode && cursorY <= scrollTriggerY)
        {
            EnterScrollMode();
        }
    }

    void EnterScrollMode()
    {
        isScrollMode = true;
        Debug.Log("进入谱面滚动模式");

        // 固定光标的Y坐标在触发位置
        Vector3 cursorPos = cursor.transform.position;
        cursor.transform.position = new Vector3(cursorPos.x, scrollTriggerY, cursorPos.z);
    }

    void UpdateCursorPosition()
    {
        if (!isScrollMode) return;

        // 在滚动模式下，光标Y坐标固定，只更新X坐标
        Vector3 cursorPos = cursor.transform.position;
        float currentGridX = cursor.GetCurrentGridX();
        int currentRowId = cursor.GetCurrentRowId();

        // 计算不考虑滚动的原始位置
        Vector2 rawPos = parser.GridWorld(currentRowId, currentGridX);
        float indentOffset = GetRowIndentOffset(currentRowId);

        cursor.transform.position = new Vector3(
            rawPos.x + indentOffset,
            scrollTriggerY, // Y坐标固定
            cursorPos.z
        );
    }

    // 当光标跳转到下一行时调用
    public void OnCursorJumpToNextRow(int fromRow, int toRow)
    {
        if (!isScrollMode) return;

        // 计算滚动距离：一行高度 + 额外偏移
        float scrollDistance = rowHeight + scrollOffset;
        totalScrollOffset += scrollDistance;

        // 应用滚动到所有音符
        ApplyScrollToAllNotes(scrollDistance);

        Debug.Log($"谱面滚动: 从行{fromRow}到行{toRow}, 滚动距离: {scrollDistance}, 总偏移: {totalScrollOffset}");
    }

    void ApplyScrollToAllNotes(float scrollDistance)
    {
        foreach (var noteObj in allNoteObjects)
        {
            if (noteObj != null)
            {
                Vector3 pos = noteObj.transform.position;
                pos.y += scrollDistance; // 音符向下移动（谱面向上滚动的视觉效果）
                noteObj.transform.position = pos;
            }
        }
    }

    // 收集所有音符对象
    void CollectAllNoteObjects()
    {
        allNoteObjects.Clear();

        if (spawner != null)
        {
            // 从spawner的子对象中收集
            foreach (Transform child in spawner.transform)
            {
                allNoteObjects.Add(child.gameObject);
            }
        }

        Debug.Log($"收集到 {allNoteObjects.Count} 个音符对象");
    }

    // 获取行的缩进偏移
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

    // 重置滚动状态
    public void ResetScroll()
    {
        isScrollMode = false;
        totalScrollOffset = 0f;

        // 重置所有音符位置
        ResetAllNotePositions();
    }

    void ResetAllNotePositions()
    {
        if (parser == null) return;

        foreach (var noteObj in allNoteObjects)
        {
            if (noteObj != null)
            {
                // 从对象名中解析行号和位置
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
            // 解析对象名格式: "type_rowX_posY"
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
            Debug.LogWarning($"解析音符对象名失败: {objectName}, 错误: {e.Message}");
        }

        return false;
    }

    // 公开方法供外部调用
    public bool IsInScrollMode => isScrollMode;
    public float TotalScrollOffset => totalScrollOffset;

    // 当有新的音符生成时调用
    public void OnNewNoteSpawned(GameObject newNote)
    {
        if (!allNoteObjects.Contains(newNote))
        {
            allNoteObjects.Add(newNote);

            // 如果在滚动模式下，应用当前滚动偏移
            if (isScrollMode && totalScrollOffset > 0)
            {
                Vector3 pos = newNote.transform.position;
                pos.y += totalScrollOffset;
                newNote.transform.position = pos;
            }
        }
    }
}