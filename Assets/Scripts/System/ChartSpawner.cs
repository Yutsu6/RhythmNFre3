using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ChartSpawner : MonoBehaviour
{
    public ChartParser parser;

    public GameObject tapNote;
    public GameObject breakNote;
    public GameObject holdNote;
    public GameObject track;
    public GameObject loopSymbolPrefab; // 循环符号预制体
    public GameObject ifSymbolPrefab; // 新增：if符号预制体
    public GameObject breakSymbolPrefab;
    public GameObject continueSymbolPrefab;
    public GameObject returnSymbolPrefab;
    public GameObject commentSymbolPrefab;
    public GameObject speedSymbolF; // 加速符号预制体
    public GameObject speedSymbolL; // 减速符号预制体

    public GameObject multiIndicatorPrefab; // 新增：Multi指示器预制体


    public Transform chartContainer; // 新增：谱面容器

    private void Start()
    {
        StartCoroutine(WaitForParserReady());
    }

    IEnumerator WaitForParserReady()
    {
        Debug.Log("等待解析器准备...");

        // 等待几帧让所有组件初始化
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        // 如果parser还是null，尝试查找
        if (parser == null)
        {
            parser = FindObjectOfType<ChartParser>();
            Debug.Log($"自动查找解析器: {parser != null}");
        }

        // 等待解析器完成解析
        float timeout = 5f;
        float startTime = Time.time;

        while (parser == null || parser.notes == null || parser.notes.Count == 0)
        {
            if (Time.time - startTime > timeout)
            {
                Debug.LogError("等待解析器超时！");
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("解析器准备就绪，开始生成音符");
        SpawnAllNotes();
    }

    void SpawnAllNotes()
    {
        if (parser == null || parser.notes.Count == 0)
        {
            Debug.LogError("解析器呢喵！");
            return;
        }

        foreach (var noteData in parser.notes)
        {
            SpawnSingleNote(noteData);
        }

        Debug.Log("生成音符完毕喵！");
    }

    void SpawnSingleNote(NoteData noteData)
    {
        Debug.Log($"=== 开始生成音符 ===");
        Debug.Log($"音符数据: 行{noteData.rowId} 位置{noteData.position} 类型{noteData.type}");
        Debug.Log($"结构符号: {noteData.GetSymbolsDebugInfo()}");

        // 检查是否是Multi音符
        if (noteData.isMultiNote)
        {
            Debug.Log("检测到Multi音符");
            SpawnMultiNote(noteData.AsMultiNote());
            return;
        }

        GameObject prefabToUse = GetPrefabByType(noteData.type);
        if (prefabToUse == null)
        {
            Debug.LogWarning($"没有为类型 '{noteData.type}' 设置预制体喵！");
            return;
        }

        Debug.Log($"使用预制体: {prefabToUse.name}");

        GameObject newNote = Instantiate(prefabToUse);

        // 设置父对象
        if (chartContainer != null)
        {
            newNote.transform.SetParent(chartContainer);
        }

        // 世界坐标（考虑缩进量）
        Vector2 worldPos = CalculateWorldPositionWithIndent(noteData);

        // 位置
        newNote.transform.position = new Vector2(worldPos.x, worldPos.y);

        // 根据类型和长度设置物体大小
        SetupNoteSize(newNote, noteData.type, noteData.length);

        // 设置物体名称以便识别
        newNote.name = $"{noteData.type}_row{noteData.rowId}_pos{noteData.position}";

        // 生成结构符号（包括循环符号）
        SpawnStructureSymbols(noteData, newNote);
    }

    // 生成Multi音符 - 使用当前层的预制体

    void SpawnMultiNote(MultiNoteData multiNoteData)
    {
        // 重置状态，确保从第一层开始
        multiNoteData.hasEnteredJudgmentQueue = false;

        if (multiNoteData.IsComplete())
        {
            Debug.LogWarning("Multi音符在生成时已经完成，跳过生成");
            return;
        }

        var currentLayer = multiNoteData.GetCurrentLayer();
        if (currentLayer == null)
        {
            Debug.LogWarning("Multi音符没有有效的层！");
            return;
        }

        GameObject prefabToUse = GetPrefabByType(currentLayer.type);
        if (prefabToUse == null)
        {
            Debug.LogWarning($"没有为Multi音符的当前层类型 '{currentLayer.type}' 设置预制体！");
            return;
        }

        GameObject multiObject = Instantiate(prefabToUse);
        Vector2 worldPos = CalculateWorldPositionWithIndent(multiNoteData);
        multiObject.transform.position = new Vector2(worldPos.x, worldPos.y);

        // 使用Multi音符的总长度
        SetupNoteSize(multiObject, "multi", multiNoteData.length);

        multiObject.name = $"multi_row{multiNoteData.rowId}_pos{multiNoteData.position}";

        multiNoteData.noteObject = multiObject;

        if (chartContainer != null)
        {
            multiObject.transform.SetParent(chartContainer);
        }

        // 添加Multi指示器
        SpawnMultiIndicator(multiObject, multiNoteData);

        SpawnStructureSymbols(multiNoteData, multiObject);

        // 显示所有层信息
        string layerInfo = "层序列: ";
        for (int i = 0; i < multiNoteData.layers.Count; i++)
        {
            layerInfo += $"{multiNoteData.layers[i].type}";
            if (i < multiNoteData.layers.Count - 1) layerInfo += " → ";
        }

        Debug.Log($"生成Multi音符: 总长度={multiNoteData.length}, 当前层={currentLayer.type}({multiNoteData.currentLayerIndex + 1}/{multiNoteData.layers.Count}), {layerInfo}");
    }
    // 新增：生成Multi指示器
    public void SpawnMultiIndicator(GameObject multiObject, MultiNoteData multiNoteData)
    {
        if (multiIndicatorPrefab == null)
        {
            Debug.LogWarning("Multi指示器预制体未设置！");
            return;
        }

        GameObject indicator = Instantiate(multiIndicatorPrefab);
        indicator.transform.SetParent(multiObject.transform);

        // 设置位置：右下角
        indicator.transform.localPosition = new Vector3(
            multiNoteData.length * parser.cellSize * 0.5f - 0.1f, // 右侧
            -0.1f, // 下方
            -0.1f  // 向前显示
        );

        // 设置指示器名称
        indicator.name = "MultiIndicator";

        // 初始化指示器显示
        InitializeMultiIndicator(indicator, multiNoteData);
    }

    // 初始化Multi指示器显示
    void InitializeMultiIndicator(GameObject indicator, MultiNoteData multiNoteData)
    {
        // 在指示器的子对象中查找TextMeshPro组件
        var textMesh = indicator.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            textMesh.text = multiNoteData.GetRemainingHits().ToString();
            var updater = indicator.AddComponent<MultiIndicatorUpdater>();
            updater.multiNoteData = multiNoteData;
        }
        else
        {
            Debug.LogWarning("Multi指示器预制体中没有找到TextMeshPro组件！");
        }
    }


    // 新增：生成所有结构符号
    // 新增：生成所有结构符号
   public void SpawnStructureSymbols(NoteData noteData, GameObject parentNote)
    {
        Debug.Log($"=== 开始生成结构符号 ===");
        Debug.Log($"音符: 行{noteData.rowId} 位置{noteData.position} 类型{noteData.type}");
        Debug.Log($"结构符号数量: {noteData.structureSymbols.Count}");

        if (noteData.structureSymbols.Count == 0)
        {
            Debug.Log($"音符没有结构符号");
            return;
        }

        foreach (var symbol in noteData.structureSymbols)
        {
            Debug.Log($"准备生成结构符号: {symbol.symbolType}");

            switch (symbol.symbolType)
            {
                case "loop":
                    Debug.Log($"生成Loop符号");
                    SpawnLoopSymbol(noteData, parentNote, symbol as LoopSymbol);
                    break;
                case "if":
                    Debug.Log($"生成If符号");
                    SpawnIfSymbol(noteData, parentNote, symbol as IfSymbol);
                    break;
                case "break":
                    Debug.Log($"生成Break符号");
                    SpawnBreakSymbol(noteData, parentNote, symbol as BreakSymbol);
                    break;
                case "continue":
                    Debug.Log($"生成Continue符号");
                    SpawnContinueSymbol(noteData, parentNote, symbol as ContinueSymbol);
                    break;
                case "return":
                    Debug.Log($"生成Return符号");
                    SpawnReturnSymbol(noteData, parentNote, symbol as ReturnSymbol);
                    break;
                case "comment":
                    Debug.Log($"生成Comment符号");
                    SpawnCommentSymbol(noteData, parentNote, symbol as CommentSymbol);
                    break;
                case "speed":  // 新增
                SpawnSpeedSymbol(noteData, parentNote, symbol as SpeedSymbol);
                    break;
                default:
                    Debug.LogWarning($"未知的结构符号类型: {symbol.symbolType}");
                    break;
            }
        }
        Debug.Log($"=== 结构符号生成完成 ===");
    }

    void SpawnSpeedSymbol(NoteData noteData, GameObject parentNote, SpeedSymbol speedSymbol)
    {
        // 根据 speedType 选择对应的预制体
        GameObject prefabToUse = null;

        if (speedSymbol.speedType == "F")
        {
            prefabToUse = speedSymbolF;
        }
        else if (speedSymbol.speedType == "L")
        {
            prefabToUse = speedSymbolL;
        }

        if (prefabToUse == null)
        {
            Debug.LogWarning($"变速符号预制体未设置: {speedSymbol.speedType}");
            return;
        }

        GameObject symbolObj = Instantiate(prefabToUse);
        Vector2 basePos = CalculateWorldPositionWithIndent(noteData);
        Vector2 symbolPos = new Vector2(basePos.x, basePos.y);

        symbolObj.transform.position = symbolPos;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"SpeedSymbol_{speedSymbol.speedType}";

        Debug.Log($"生成变速符号: {speedSymbol.speedType}");
    }

    void SpawnLoopSymbol(NoteData noteData, GameObject parentNote, LoopSymbol loopSymbol)
    {
        if (loopSymbolPrefab == null)
        {
            Debug.LogWarning("循环符号预制体未设置！");
            return;
        }

        GameObject symbolObj = Instantiate(loopSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"LoopSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log($"成功生成循环符号");
    }

    void SpawnIfSymbol(NoteData noteData, GameObject parentNote, IfSymbol ifSymbol)
    {
        Debug.Log($"=== 开始生成If符号 ===");

        if (ifSymbolPrefab == null)
        {
            Debug.LogError("If符号预制体未设置！");
            return;
        }

        Debug.Log($"If符号预制体: {ifSymbolPrefab.name}");
        Debug.Log($"父对象: {parentNote.name}");

        GameObject symbolObj = Instantiate(ifSymbolPrefab);
        Debug.Log($"实例化If符号对象: {symbolObj.name}");

        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        Debug.Log($"符号位置: {symbolPosition}");

        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"IfSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log($"If符号设置完成: {symbolObj.name}, 父对象: {symbolObj.transform.parent.name}");
        Debug.Log($"=== If符号生成完成 ===");
    }

    void SpawnBreakSymbol(NoteData noteData, GameObject parentNote, BreakSymbol breakSymbol)
    {
        if (breakSymbolPrefab == null)
        {
            Debug.LogWarning("Break符号预制体未设置！");
            return;
        }

        GameObject symbolObj = Instantiate(breakSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"BreakSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("成功生成Break符号");
    }

    void SpawnContinueSymbol(NoteData noteData, GameObject parentNote, ContinueSymbol continueSymbol)
    {
        if (continueSymbolPrefab == null)
        {
            Debug.LogWarning("Continue符号预制体未设置！");
            return;
        }

        GameObject symbolObj = Instantiate(continueSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"ContinueSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("成功生成Continue符号");
    }

    void SpawnReturnSymbol(NoteData noteData, GameObject parentNote, ReturnSymbol returnSymbol)
    {
        if (returnSymbolPrefab == null)
        {
            Debug.LogWarning("Return符号预制体未设置！");
            return;
        }

        GameObject symbolObj = Instantiate(returnSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"ReturnSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("成功生成Return符号");
    }

    void SpawnCommentSymbol(NoteData noteData, GameObject parentNote, CommentSymbol commentSymbol)
    {
        if (commentSymbolPrefab == null)
        {
            Debug.LogWarning("Comment符号预制体未设置！");
            return;
        }

        GameObject symbolObj = Instantiate(commentSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"CommentSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("成功生成Comment符号");
    }

    // 简化：统一的符号位置计算方法
    Vector2 CalculateSymbolPosition(NoteData noteData)
    {
        return CalculateWorldPositionWithIndent(noteData);
    }

    Vector2 CalculateWorldPositionWithIndent(NoteData noteData)
    {
        Vector2 baseWorldPos = parser.GridWorld(noteData.rowId, noteData.position);
        float indentOffset = noteData.indentLevel * parser.cellSize;

        Vector2 finalWorldPos = new Vector2(
            baseWorldPos.x + indentOffset,
            baseWorldPos.y
        );

        return finalWorldPos;
    }


    // 统一设置音符大小的方法
    void SetupNoteSize(GameObject noteObject, string type, float length)
    {
        Vector3 currentScale = noteObject.transform.localScale;

        switch (type)
        {
            case "hold":
                // Hold - 调整宽度
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成Hold音符，长度: {length} 格, 实际宽度: {length * parser.cellSize} 单位");
                break;

            case "track":
                // 音轨 - 调整宽度
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成音轨，长度: {length} 格");
                break;

            case "tap":
            case "break":
                // Tap/Break - 调整宽度（如果长度>1）
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成{type}音符，长度: {length} 格");
                break;
            case "multi":
                // Multi音符也调整宽度
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成Multi音符，长度: {length} 格");
                break;
        }
    }

    GameObject GetPrefabByType(string type)
    {
        switch (type)
        {
            case "tap": return tapNote;
            case "break": return breakNote;
            case "hold": return holdNote;
            case "track": return track;
            default: return null;
        }
    }

    // 新增：Multi指示器更新器
    public class MultiIndicatorUpdater : MonoBehaviour
    {
        public MultiNoteData multiNoteData;
        private TextMeshPro textMesh;

        void Start()
        {
            // 在子对象中查找TextMeshPro组件
            textMesh = GetComponentInChildren<TextMeshPro>();
        }

        void Update()
        {
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            if (textMesh != null && multiNoteData != null)
            {
                // 使用剩余层数而不是打击次数
                int remainingLayers = multiNoteData.GetRemainingLayers();
                textMesh.text = remainingLayers.ToString();

                // 根据剩余层数改变颜色
                if (remainingLayers <= 1)
                    textMesh.color = Color.red;
                else if (remainingLayers <= 3)
                    textMesh.color = Color.yellow;
                else
                    textMesh.color = Color.white;
            }
        }
    }


    // 在编辑器中添加一个按钮，方便重新生成
    [ContextMenu("重新生成所有音符")]
    void RegenerateAllNotes()
    {
        // 先删除所有已生成的音符
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }


        // 重新解析和生成
        if (parser != null)
        {
            parser.ParseChart();
            SpawnAllNotes();
        }
    }
}