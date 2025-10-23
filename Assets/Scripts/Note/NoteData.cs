using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoteData
{
    public int rowId;   //行
    public float position;  //在该行的起始位置
    public string type;     //类型
    public float length;    //长度

    public bool isJudged = false;
    public string judgmentResult = "";
    public GameObject noteObject;
    public float triggerTime;

    public int indentLevel = 0;                    // 缩进级别

    public bool hasEnteredJudgmentQueue = false;

    // 新增命令相关字段
    public bool hasCommand = false;
    public string commandName = "";
    public string commandParams = "";
    public int[] executionCodes = new int[0]; // 执行码数组
    public int currentReadCount = 0; // 当前被读取次数

    // 命令执行状态
    public bool isCommandExecuted = false;
    public int executionIndex = 0; // 当前执行索引

    // 新增：判断是否为Multi音符
    public bool isMultiNote = false;

    // 为了方便处理，添加类型转换方法
    public MultiNoteData AsMultiNote()
    {
        if (isMultiNote && this is MultiNoteData)
            return (MultiNoteData)this;
        return null;
    }


    // === 结构符号系统 ===

    // 统一的结构符号列表
    public List<StructureSymbol> structureSymbols = new List<StructureSymbol>();

    // 便捷属性 - 循环符号
    public bool hasLoopSymbol
    {
        get { return GetSymbol<LoopSymbol>() != null; }
    }

    public int[] loopCodes
    {
        get
        {
            var loop = GetSymbol<LoopSymbol>();
            return loop?.loopCodes ?? new int[0];
        }
    }

    public bool loopSymbolTriggered = false;

    // 便捷属性 - 判断符号
    public bool hasIfSymbol
    {
        get { return GetSymbol<IfSymbol>() != null; }
    }

    public int[] ifConditionCodes
    {
        get
        {
            var ifSymbol = GetSymbol<IfSymbol>();
            return ifSymbol?.conditionCodes ?? new int[0];
        }
    }

    public bool ifSymbolTriggered = false;

    // 便捷属性 - 其他符号
    public bool hasBreakSymbol
    {
        get { return GetSymbol<BreakSymbol>() != null; }
    }

    public bool hasContinueSymbol
    {
        get { return GetSymbol<ContinueSymbol>() != null; }
    }

    public bool hasReturnSymbol
    {
        get { return GetSymbol<ReturnSymbol>() != null; }
    }

    public bool hasCommentSymbol
    {
        get { return GetSymbol<CommentSymbol>() != null; }
    }

    // 便捷属性 - 变速符号
    public bool hasSpeedSymbol
    {
        get { return GetSymbol<SpeedSymbol>() != null; }
    }

    // === 辅助方法 ===

    // 获取特定类型的结构符号
    public T GetSymbol<T>() where T : StructureSymbol
    {
        foreach (var symbol in structureSymbols)
        {
            if (symbol is T typedSymbol)
                return typedSymbol;
        }
        return null;
    }

    // 检查是否包含特定类型的结构符号
    public bool HasSymbol<T>() where T : StructureSymbol
    {
        return GetSymbol<T>() != null;
    }

    // 添加结构符号
    public void AddStructureSymbol(StructureSymbol symbol)
    {
        if (symbol != null)
        {
            structureSymbols.Add(symbol);

            // 触发符号特定的事件（如果需要）
            OnSymbolAdded(symbol);
        }
    }

    // 移除结构符号
    public void RemoveStructureSymbol<T>() where T : StructureSymbol
    {
        var symbol = GetSymbol<T>();
        if (symbol != null)
        {
            structureSymbols.Remove(symbol);
        }
    }

    // 符号添加时的处理
    private void OnSymbolAdded(StructureSymbol symbol)
    {
        // 这里可以添加符号添加时的特殊处理逻辑
        // 例如：设置默认的触发状态等
        switch (symbol.symbolType)
        {
            case "loop":
                loopSymbolTriggered = false;
                break;
            case "if":
                ifSymbolTriggered = false;
                break;
        }
    }

    // 获取所有符号的调试信息
    public string GetSymbolsDebugInfo()
    {
        if (structureSymbols.Count == 0)
            return "无结构符号";

        List<string> symbolInfos = new List<string>();
        foreach (var symbol in structureSymbols)
        {
            string info = $"{symbol.symbolType}";

            // 为特定符号类型添加详细信息
            if (symbol is LoopSymbol loop)
            {
                info += $"[{string.Join(",", loop.loopCodes)}]";
            }
            else if (symbol is IfSymbol ifSymbol)
            {
                info += $"[{string.Join(",", ifSymbol.conditionCodes)}]";
            }

            symbolInfos.Add(info);
        }

        return string.Join(" + ", symbolInfos);
    }

    // 重置所有符号的触发状态
    public void ResetSymbolTriggers()
    {
        loopSymbolTriggered = false;
        ifSymbolTriggered = false;
        // 可以添加其他符号的触发状态重置
    }

    // 检查是否包含任何流程控制符号
    public bool HasFlowControlSymbol()
    {
        return hasLoopSymbol || hasIfSymbol || hasBreakSymbol ||
               hasContinueSymbol || hasReturnSymbol;
    }

    // 新增：命令执行方法
    public bool ShouldExecuteCommand()
    {
        if (!hasCommand) return false;

        // 检查执行码
        if (executionCodes.Length == 0)
        {
            // 没有执行码，默认执行1次（第一次读取时执行）
            return currentReadCount == 0;
        }

        if (currentReadCount < executionCodes.Length)
        {
            return executionCodes[currentReadCount] == 1;
        }

        return false;
    }

    public void IncrementReadCount()
    {
        currentReadCount++;
    }

    public bool CanBeReadAgain()
    {
        if (executionCodes.Length == 0)
            return currentReadCount < 1; // 默认只能读1次
        return currentReadCount < executionCodes.Length;
    }

    // 获取主要符号类型（用于显示）
    public string GetPrimarySymbolType()
    {
        if (hasLoopSymbol) return "loop";
        if (hasIfSymbol) return "if";
        if (hasBreakSymbol) return "break";
        if (hasContinueSymbol) return "continue";
        if (hasReturnSymbol) return "return";
        if (hasCommentSymbol) return "comment";

        return "none";
    }
}