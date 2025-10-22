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

public class IfSymbol : StructureSymbol
{
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
        // 未来可以在这里添加其他符号：
        { "if", () => new IfSymbol() }
        // { "switch", () => new SwitchSymbol() }
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
        return content.Contains("loop{") || content.Contains("if{");
    }
}