using UnityEngine;

public class ChartWindow : MonoBehaviour
{
    [Header("视窗设置")]
    public float windowHeight = 10f;
    public float windowWidth = 10f;
    public float scrollThreshold = 3f;
    public float previewRows = 5f;

    [Header("引用")]
    public CursorController cursor;
    public ChartSpawner spawner;
    public Transform chartContainer;

    // 添加这个公共属性
    public float currentScrollOffset { get; private set; } = 0f;

    [Header("视觉显示")]
    public bool showWindowArea = true;
    public Color windowAreaColor = new Color(1, 1, 1, 0.1f);
    public bool useSpriteMask = true;

    private SpriteRenderer areaRenderer;
    private SpriteMask spriteMask;
    private int lastScrollRow = -1;

    void Start()
    {
        if (showWindowArea)
        {
            CreateWindowArea();
        }

        if (useSpriteMask)
        {
            CreateSpriteMask();
        }
    }

    void Update()
    {
        if (cursor == null || !cursor.isActive) return;

        CheckScroll();
    }

    void CheckScroll()
    {
        float cursorY = cursor.transform.position.y;
        float windowBottom = transform.position.y - windowHeight * 0.5f;
        float cursorRelativeY = cursorY - windowBottom;

        if (cursorRelativeY < scrollThreshold && lastScrollRow != cursor.GetCurrentRowId())
        {
            ScrollChart();
            lastScrollRow = cursor.GetCurrentRowId();
        }
    }

    void ScrollChart()
    {
        // 向上滚动一个行高
        currentScrollOffset += cursor.parser.rowHeight;

        // 移动所有音符
        if (chartContainer != null)
        {
            chartContainer.position += Vector3.up * cursor.parser.rowHeight;
        }

        Debug.Log($"谱面向上滚动，当前偏移: {currentScrollOffset}");

        UpdateNoteVisibility();
    }

    void UpdateNoteVisibility()
    {
        if (chartContainer == null) return;

        float windowTop = transform.position.y + windowHeight * 0.5f;
        float windowBottom = transform.position.y - windowHeight * 0.5f;

        foreach (Transform child in chartContainer)
        {
            float noteY = child.position.y;
            bool shouldBeVisible = noteY <= windowTop && noteY >= windowBottom;
            child.gameObject.SetActive(shouldBeVisible);
        }
    }

    void CreateWindowArea()
    {
        GameObject areaObject = new GameObject("WindowArea");
        areaObject.transform.SetParent(transform);
        areaObject.transform.localPosition = Vector3.zero;

        areaRenderer = areaObject.AddComponent<SpriteRenderer>();
        areaRenderer.sprite = CreateWhiteSprite();
        areaRenderer.color = windowAreaColor;
        areaRenderer.sortingOrder = -1;

        UpdateVisualSize();
    }

    void CreateSpriteMask()
    {
        spriteMask = gameObject.AddComponent<SpriteMask>();
        spriteMask.sprite = CreateWhiteSprite();
        UpdateVisualSize();
    }

    void UpdateVisualSize()
    {
        Vector3 scale = new Vector3(windowWidth, windowHeight, 1f);

        if (areaRenderer != null)
            areaRenderer.transform.localScale = scale;

        if (spriteMask != null)
            spriteMask.transform.localScale = scale;
    }

    Sprite CreateWhiteSprite()
    {
        return Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f)
        );
    }

    // 新增：重置滚动偏移的方法（如果需要）
    public void ResetScrollOffset()
    {
        currentScrollOffset = 0f;
        if (chartContainer != null)
        {
            chartContainer.position = Vector3.zero;
        }
        UpdateNoteVisibility();
    }

    // 新增：设置滚动偏移的方法
    public void SetScrollOffset(float offset)
    {
        currentScrollOffset = offset;
        if (chartContainer != null)
        {
            chartContainer.position = new Vector3(0, offset, 0);
        }
        UpdateNoteVisibility();
    }
}