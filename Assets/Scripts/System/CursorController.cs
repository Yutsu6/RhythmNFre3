using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorController : MonoBehaviour
{

    //移动设置
    public float speed = 4.0f;

    //引用
    public ChartParser parser;
    public ChartSpawner spawner;

    //光标状态
    private float currentGridX = 0f;//当前横坐标
    private int currentRowId = -1;//当前行数
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
        Debug.Log("光标等待谱面生成...");

        // 等待一帧，确保其他组件的Start方法执行完毕
        yield return null;

        // 等待直到谱面解析器和生成器都准备就绪
        while (parser == null || parser.notes.Count == 0)
        {
            Debug.Log("等待谱面数据...");
            yield return new WaitForSeconds(0.1f); // 每0.1秒检查一次
        }

        // 额外等待一小段时间，确保所有音符都实例化完成
        yield return new WaitForSeconds(0.2f);

        // 初始化光标
        InitializeCursor();

        Debug.Log("谱面准备就绪，光标开始扫描！");
    }

    void Update()
    {
        if (!isActive) return;

        //横向移动
        if (isTimingActive)
        {
            currentGridX += speed * Time.deltaTime;

            cursorTime += Time.deltaTime;
        }


        //更新世界坐标
        UpdateWorldPosition();

        //跳转逻辑
        if (ShouldJumpToNextRow())
        {
            JumpToNextRow();
        }
    }

    //光标位置初始化
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
            Debug.LogError("Parser为空！");
        }

        UpdateWorldPosition();

        isActive = true;

        Debug.Log($"光标初始化完成！速度: {speed}, 起始时间: {cursorTime}");


    }

    void UpdateWorldPosition()
    {
        // 计算考虑缩进的世界坐标
        Vector2 worldPos = CalculateWorldPositionWithIndent(currentRowId, currentGridX);
        transform.position = new Vector3(worldPos.x, worldPos.y, 0);

        // 通知判定系统位置已更新
        OnCursorPositionChanged?.Invoke(worldPos, currentRowId, currentGridX);
    }

    // 计算考虑缩进的世界坐标
    Vector2 CalculateWorldPositionWithIndent(int rowId, float gridX)
    {
        // 基础位置（不考虑缩进）
        Vector2 baseWorldPos = parser.GridWorld(rowId, gridX);

        // 应用当前行的缩进偏移
        float indentOffset = GetCurrentRowIndentOffset();

        Vector2 finalWorldPos = new Vector2(
            baseWorldPos.x + indentOffset,
            baseWorldPos.y
        );

        return finalWorldPos;
    }

    // 获取当前行的缩进偏移量
    float GetCurrentRowIndentOffset()
    {
        // 找到当前行的第一个音符，获取其缩进量
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId)
            {
                return note.indentLevel; // 直接使用缩进量作为偏移
            }
        }
        return 0f; // 默认无缩进
    }

    bool ShouldJumpToNextRow()
    {
        float currentRowlength = GetCurrentRowLength();

        return currentGridX > currentRowlength;
    }

    float GetCurrentRowLength()
    {
        float maxPosition = 0f;

        // 计算不考虑缩进的行长度（只关注音符本身的布局）
        foreach (var note in parser.notes)
        {
            if (note.rowId == currentRowId)
            {
                // 音符的实际结束位置 = 基础位置 + 长度（不加缩进量）
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

        //跳转
        currentRowId = nextRowId;
        currentGridX = 0f;

        UpdateWorldPosition();
    }

    //行是否存在
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

    // 公开方法，供其他系统访问当前状态
    public int GetCurrentRowId() => currentRowId;
    public float GetCurrentGridX() => currentGridX;
    public Vector2 GetWorldPosition() => transform.position;

    // 新增：获取当前行的缩进量
    public float GetCurrentRowIndent()
    {
        return GetCurrentRowIndentOffset();
    }
}