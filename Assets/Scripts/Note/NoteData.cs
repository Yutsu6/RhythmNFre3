using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoteData
{
    public int rowId;   //��
    public float position;  //�ڸ��е���ʼλ��
    public string type;     //����
    public float length;    //����

    public bool isJudged = false;
    public string judgmentResult = "";
    public GameObject noteObject;
    public float triggerTime;

    public int indentLevel = 0;                    // ��������

    public bool hasEnteredJudgmentQueue = false;

    // ������������ֶ�
    public bool hasCommand = false;
    public string commandName = "";
    public string commandParams = "";
    public int[] executionCodes = new int[0]; // ִ��������
    public int currentReadCount = 0; // ��ǰ����ȡ����

    // ����ִ��״̬
    public bool isCommandExecuted = false;
    public int executionIndex = 0; // ��ǰִ������

    // �������ж��Ƿ�ΪMulti����
    public bool isMultiNote = false;

    // Ϊ�˷��㴦���������ת������
    public MultiNoteData AsMultiNote()
    {
        if (isMultiNote && this is MultiNoteData)
            return (MultiNoteData)this;
        return null;
    }


    // === �ṹ����ϵͳ ===

    // ͳһ�Ľṹ�����б�
    public List<StructureSymbol> structureSymbols = new List<StructureSymbol>();

    // ������� - ѭ������
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

    // ������� - �жϷ���
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

    // ������� - ��������
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

    // ������� - ���ٷ���
    public bool hasSpeedSymbol
    {
        get { return GetSymbol<SpeedSymbol>() != null; }
    }

    // === �������� ===

    // ��ȡ�ض����͵Ľṹ����
    public T GetSymbol<T>() where T : StructureSymbol
    {
        foreach (var symbol in structureSymbols)
        {
            if (symbol is T typedSymbol)
                return typedSymbol;
        }
        return null;
    }

    // ����Ƿ�����ض����͵Ľṹ����
    public bool HasSymbol<T>() where T : StructureSymbol
    {
        return GetSymbol<T>() != null;
    }

    // ��ӽṹ����
    public void AddStructureSymbol(StructureSymbol symbol)
    {
        if (symbol != null)
        {
            structureSymbols.Add(symbol);

            // ���������ض����¼��������Ҫ��
            OnSymbolAdded(symbol);
        }
    }

    // �Ƴ��ṹ����
    public void RemoveStructureSymbol<T>() where T : StructureSymbol
    {
        var symbol = GetSymbol<T>();
        if (symbol != null)
        {
            structureSymbols.Remove(symbol);
        }
    }

    // �������ʱ�Ĵ���
    private void OnSymbolAdded(StructureSymbol symbol)
    {
        // ���������ӷ������ʱ�����⴦���߼�
        // ���磺����Ĭ�ϵĴ���״̬��
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

    // ��ȡ���з��ŵĵ�����Ϣ
    public string GetSymbolsDebugInfo()
    {
        if (structureSymbols.Count == 0)
            return "�޽ṹ����";

        List<string> symbolInfos = new List<string>();
        foreach (var symbol in structureSymbols)
        {
            string info = $"{symbol.symbolType}";

            // Ϊ�ض��������������ϸ��Ϣ
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

    // �������з��ŵĴ���״̬
    public void ResetSymbolTriggers()
    {
        loopSymbolTriggered = false;
        ifSymbolTriggered = false;
        // ��������������ŵĴ���״̬����
    }

    // ����Ƿ�����κ����̿��Ʒ���
    public bool HasFlowControlSymbol()
    {
        return hasLoopSymbol || hasIfSymbol || hasBreakSymbol ||
               hasContinueSymbol || hasReturnSymbol;
    }

    // ����������ִ�з���
    public bool ShouldExecuteCommand()
    {
        if (!hasCommand) return false;

        // ���ִ����
        if (executionCodes.Length == 0)
        {
            // û��ִ���룬Ĭ��ִ��1�Σ���һ�ζ�ȡʱִ�У�
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
            return currentReadCount < 1; // Ĭ��ֻ�ܶ�1��
        return currentReadCount < executionCodes.Length;
    }

    // ��ȡ��Ҫ�������ͣ�������ʾ��
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