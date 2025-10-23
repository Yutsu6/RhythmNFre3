using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class CommandManager : MonoBehaviour
{
    [Header("引用")]
    public CursorController cursor;
    public ChartParser parser;

    [Header("命令设置")]
    public float baseBPM = 120;
    public float baseSpeed = 4.0f;

    private Dictionary<string, System.Action<string>> commandHandlers;

    void Start()
    {
        InitializeCommandHandlers();

        // 获取基础BPM和速度
        if (parser != null)
        {
            baseBPM = parser.GetBPM();
            baseSpeed = cursor.speed;
        }
    }

    void InitializeCommandHandlers()
    {
        commandHandlers = new Dictionary<string, System.Action<string>>()
        {
            { "speedChange", HandleSpeedChange },
            { "bpmChange", HandleBPMChange }
            // 可以继续添加其他命令
        };
    }

    public void ExecuteCommand(NoteData note)
    {
        if (!note.hasCommand || note.isCommandExecuted) return;

        // 检查是否应该执行（基于执行码和当前读取次数）
        if (note.ShouldExecuteCommand())
        {
            string commandName = note.commandName;
            string parameters = note.commandParams;

            if (commandHandlers.ContainsKey(commandName))
            {
                Debug.Log($"执行命令: {commandName}{{{parameters}}}, 读取次数: {note.currentReadCount}");
                commandHandlers[commandName](parameters);
            }
            else
            {
                Debug.LogWarning($"未知命令: {commandName}");
            }
        }

        // 执行后立即标记为已执行（防止同一行内重复执行）
        note.isCommandExecuted = true;
    }

    public void OnLeaveRow(int rowId)
    {
        foreach (var note in parser.notes)
        {
            if (note.rowId == rowId && note.hasCommand && note.isCommandExecuted)
            {
                // 重置执行状态
                note.isCommandExecuted = false;
                // 增加读取次数
                note.IncrementReadCount();
                Debug.Log($"离开行{rowId}，命令{note.commandName}读取次数增加到: {note.currentReadCount}");
            }
        }
    }



    void HandleSpeedChange(string parameters)
    {
        if (string.IsNullOrEmpty(parameters)) return;

        // 判断是绝对值还是相对值
        if (parameters.StartsWith("+") || parameters.StartsWith("-"))
        {
            // 相对调整
            if (float.TryParse(parameters, out float adjustment))
            {
                cursor.speed += adjustment;
                Debug.Log($"速度调整: {adjustment}, 新速度: {cursor.speed}");
            }
        }
        else
        {
            // 绝对值
            if (float.TryParse(parameters, out float newSpeed))
            {
                cursor.speed = newSpeed;
                Debug.Log($"速度设置为: {newSpeed}");
            }
        }
    }

    void HandleBPMChange(string parameters)
    {
        if (string.IsNullOrEmpty(parameters)) return;

        if (float.TryParse(parameters, out float newBPM))
        {
            baseBPM = newBPM;
            Debug.Log($"BPM设置为: {newBPM}");

        }
    }

    // 重置所有命令状态（重新开始游戏时调用）
    public void ResetAllCommands()
    {
        foreach (var note in parser.notes)
        {
            note.currentReadCount = 0;
            note.isCommandExecuted = false;
        }
    }
}