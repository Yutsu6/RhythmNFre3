using System.Collections.Generic;
using UnityEngine;

// 结构符号基类
public abstract class StructureSymbol
{
    public string symbolType { get; protected set; }
    public string rawData { get; protected set; }

    public abstract void Parse(string symbolData, NoteData noteData);
}

// 循环符号实现
public class LoopSymbol : StructureSymbol
{
    public int[] loopCodes;

    public LoopSymbol()
    {
        symbolType = "loop";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;

        int startBrace = symbolData.IndexOf('{');
        int endBrace = symbolData.IndexOf('}');

        if (startBrace >= 0 && endBrace > startBrace)
        {
            string innerContent = symbolData.Substring(startBrace + 1, endBrace - startBrace - 1).Trim();
            string[] codeStrings = innerContent.Split(',');
            List<int> codes = new List<int>();

            foreach (string codeStr in codeStrings)
            {
                string trimmedCode = codeStr.Trim();
                if (int.TryParse(trimmedCode, out int code))
                {
                    codes.Add(code);
                }
                else
                {
                    Debug.LogWarning($"无法解析循环码: '{trimmedCode}'，使用默认值0");
                    codes.Add(0);
                }
            }

            loopCodes = codes.ToArray();
            Debug.Log($"解析循环符号: {string.Join(",", loopCodes)}");
        }
        else
        {
            Debug.LogError($"循环符号格式错误: {symbolData}");
            loopCodes = new int[0];
        }
    }
}
// 结构符号工厂

// 退出符号实现
public class BreakSymbol : StructureSymbol
{
    public BreakSymbol()
    {
        symbolType = "break";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"解析退出符号");
    }
}

// 跳过符号实现
public class ContinueSymbol : StructureSymbol
{
    public ContinueSymbol()
    {
        symbolType = "continue";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"解析跳过符号");
    }
}

// 终止符号实现
public class ReturnSymbol : StructureSymbol
{
    public ReturnSymbol()
    {
        symbolType = "return";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"解析终止符号");
    }
}

// 注释符号实现
public class CommentSymbol : StructureSymbol
{
    public CommentSymbol()
    {
        symbolType = "comment";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"解析注释符号");
        // 注释符号只是一个标识，不需要解析具体内容
    }
}

// 变速符号实现
public class SpeedSymbol : StructureSymbol
{
    public string speedType; // "F" 或 "L"

    public SpeedSymbol()
    {
        symbolType = "speed";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;

        if (symbolData == "F" || symbolData == "L")
        {
            speedType = symbolData;
            Debug.Log($"解析变速符号: {speedType}");
        }
        else
        {
            Debug.LogWarning($"无效的变速符号: '{symbolData}'");
            speedType = "0";
        }
    }
}


public class IfSymbol : StructureSymbol
{
    public IfSymbol()
    {
        symbolType = "if";
    }
    public int[] conditionCodes;

    public override void Parse(string symbolData, NoteData noteData)
    {
        // 解析if{1,0,1}格式
        int startBrace = symbolData.IndexOf('{');
        int endBrace = symbolData.IndexOf('}');

        if (startBrace >= 0 && endBrace > startBrace)
        {
            string innerContent = symbolData.Substring(startBrace + 1, endBrace - startBrace - 1).Trim();
            string[] codeStrings = innerContent.Split(',');
            List<int> codes = new List<int>();

            foreach (string codeStr in codeStrings)
            {
                string trimmedCode = codeStr.Trim();
                if (int.TryParse(trimmedCode, out int code) && (code == 0 || code == 1))
                {
                    codes.Add(code);
                }
                else
                {
                    Debug.LogWarning($"无效的判断码: '{trimmedCode}'，使用默认值0");
                    codes.Add(0);
                }
            }

            conditionCodes = codes.ToArray();
        }
        else
        {
            Debug.LogError($"判断符号格式错误: {symbolData}");
            conditionCodes = new int[0];
        }
    }
}

public static class StructureSymbolFactory
{
    private static Dictionary<string, System.Func<StructureSymbol>> symbolConstructors =
        new Dictionary<string, System.Func<StructureSymbol>>()
    {
        { "loop", () => new LoopSymbol() },
        { "break", () => new BreakSymbol() },
        { "continue", () => new ContinueSymbol() },
        { "return", () => new ReturnSymbol() },
        { "//", () => new CommentSymbol() },
        { "if", () => new IfSymbol() },
        { "F", () => new SpeedSymbol() },  // 新增
        { "L", () => new SpeedSymbol() }   // 新增
    };

    public static StructureSymbol CreateSymbol(string symbolType)
    {
        if (symbolConstructors.ContainsKey(symbolType))
        {
            return symbolConstructors[symbolType]();
        }
        return null;
    }

    public static bool IsStructureSymbol(string content)
    {
        return content.Contains("loop{") ||
               content.Contains("break") ||
               content.Contains("continue") ||
               content.Contains("return") ||
               content.Contains("//") ||
               content.Contains("if{") ||
               content.Contains("F") ||
               content.Contains("L");
    }
}