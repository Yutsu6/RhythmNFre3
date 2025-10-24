using System.Collections.Generic;
using UnityEngine;

public class HoldNoteManager : MonoBehaviour
{
    [Header("“˝”√")]
    public CursorController cursor;
    public ChartParser parser;
    public JudgmentManager judgmentManager;

    [Header("Hold…Ë÷√")]
    public float protectionWindow = 0.1f;
    public float earlyReleaseWindow = 0.1f;

    private Dictionary<NoteData, HoldState> activeHolds = new Dictionary<NoteData, HoldState>();

    private KeyCode[] fixKeys = new KeyCode[] {
        KeyCode.S, KeyCode.D, KeyCode.F,
        KeyCode.J, KeyCode.K, KeyCode.L
    };

    [System.Serializable]
    public class HoldState
    {
        public bool isHolding = false;
        public KeyCode heldKey;
        public float releaseTime = -1f;
        public bool inProtection = false;
        public bool isJudged = false;
        public string judgmentResult = "";
        public bool hasStarted = false;

        public bool IsInProtection(float currentTime)
        {
            return inProtection && (currentTime - releaseTime) <= 0.1f;
        }
    }

    void Start()
    {
        if (judgmentManager == null)
            judgmentManager = FindObjectOfType<JudgmentManager>();
    }

    void Update()
    {
        if (!cursor.isActive) return;

        UpdateHoldNotes();
        CheckHoldInput();
        UpdateHoldVisuals();
    }

    void UpdateHoldNotes()
    {
        RemoveJudgedHolds();
        AddNewHolds();
        CheckActiveHolds();
    }

    void RemoveJudgedHolds()
    {
        var toRemove = new List<NoteData>();
        foreach (var kvp in activeHolds)
        {
            if (kvp.Key.isJudged)
                toRemove.Add(kvp.Key);
        }

        foreach (var note in toRemove)
            activeHolds.Remove(note);
    }

    void AddNewHolds()
    {
        foreach (var note in judgmentManager.activeJudgmentQueue)
        {
            if (note.type == "hold" && !note.isJudged && !activeHolds.ContainsKey(note))
            {
                activeHolds[note] = new HoldState();
            }
        }
    }

    void CheckActiveHolds()
    {
        var toRemove = new List<NoteData>();

        foreach (var kvp in activeHolds)
        {
            var note = kvp.Key;
            var state = kvp.Value;

            if (state.isJudged) continue;

            float holdEndTime = note.triggerTime + (note.length / cursor.speed);
            float timeUntilEnd = holdEndTime - cursor.cursorTime;

            if (CheckEarlyRelease(note, state, timeUntilEnd, toRemove)) continue;
            if (CheckNaturalEnd(note, state, timeUntilEnd, toRemove)) continue;
            if (CheckMissedHold(note, state, timeUntilEnd, toRemove)) continue;
            CheckMissedStart(note, state, toRemove);
        }

        foreach (var note in toRemove)
            activeHolds.Remove(note);
    }

    bool CheckEarlyRelease(NoteData note, HoldState state, float timeUntilEnd, List<NoteData> toRemove)
    {
        if (timeUntilEnd > earlyReleaseWindow) return false;

        if (state.isHolding)
        {
            CompleteHoldNote(note, state, "Critical Fixed");
            toRemove.Add(note);
            return true;
        }

        if (state.hasStarted && !state.isHolding && !state.IsInProtection(cursor.cursorTime))
        {
            judgmentManager.combo = 0;
            judgmentManager.errors++;
            CompleteHoldNote(note, state, "Too Late");
            toRemove.Add(note);
            return true;
        }

        return false;
    }

    bool CheckNaturalEnd(NoteData note, HoldState state, float timeUntilEnd, List<NoteData> toRemove)
    {
        if (timeUntilEnd > 0 || !state.isHolding) return false;

        CompleteHoldNote(note, state, "Critical Fixed");
        toRemove.Add(note);
        return true;
    }

    bool CheckMissedHold(NoteData note, HoldState state, float timeUntilEnd, List<NoteData> toRemove)
    {
        if (!state.hasStarted || state.isHolding || state.IsInProtection(cursor.cursorTime) || timeUntilEnd <= 0)
            return false;

        judgmentManager.combo = 0;
        judgmentManager.errors++;
        CompleteHoldNote(note, state, "Too Late");
        toRemove.Add(note);
        return true;
    }

    void CheckMissedStart(NoteData note, HoldState state, List<NoteData> toRemove)
    {
        if (state.hasStarted) return;

        float startTimeDiff = note.triggerTime - cursor.cursorTime;
        if (startTimeDiff < -JudgmentManager.JUDGMENT_WINDOW_EXIT)
        {
            judgmentManager.combo = 0;
            judgmentManager.errors++;
            CompleteHoldNote(note, state, "Too Late");
            toRemove.Add(note);
        }
    }

    void CheckHoldInput()
    {
        foreach (KeyCode key in fixKeys)
        {
            if (Input.GetKeyDown(key))
            {
                if (!TryStartHold(key) && !TryResumeHold(key))
                    CheckWrongKeyPress(key);
            }

            if (Input.GetKeyUp(key))
                HandleKeyRelease(key);
        }
    }

    bool TryStartHold(KeyCode key)
    {
        foreach (var kvp in activeHolds)
        {
            var note = kvp.Key;
            var state = kvp.Value;

            if (state.isJudged || state.isHolding || state.inProtection) continue;

            float timeDiff = note.triggerTime - cursor.cursorTime;
            if (Mathf.Abs(timeDiff) <= JudgmentManager.JUDGMENT_WINDOW_EXIT)
            {
                StartHoldNote(note, state, timeDiff, key);
                judgmentManager.MarkInputProcessed();
                return true;
            }
        }
        return false;
    }

    bool TryResumeHold(KeyCode key)
    {
        foreach (var kvp in activeHolds)
        {
            var state = kvp.Value;
            if (state.IsInProtection(cursor.cursorTime) && key == state.heldKey)
            {
                state.isHolding = true;
                state.inProtection = false;
                state.releaseTime = -1f;
                judgmentManager.MarkInputProcessed();
                return true;
            }
        }
        return false;
    }

    void CheckWrongKeyPress(KeyCode key)
    {
        foreach (var kvp in activeHolds)
        {
            var state = kvp.Value;
            if (state.IsInProtection(cursor.cursorTime) && key != state.heldKey)
            {
                var note = kvp.Key;
                note.isJudged = true;
                CompleteHoldNote(note, state, "Too Late");
                return;
            }
        }
    }

    void HandleKeyRelease(KeyCode key)
    {
        foreach (var kvp in activeHolds)
        {
            var state = kvp.Value;
            if (state.isHolding && !state.isJudged && state.heldKey == key)
            {
                state.isHolding = false;
                state.inProtection = true;
                state.releaseTime = cursor.cursorTime;
            }
        }
    }

    void StartHoldNote(NoteData note, HoldState state, float timeDiff, KeyCode key)
    {
        state.isHolding = true;
        state.heldKey = key;
        state.hasStarted = true;

        string judgment = judgmentManager.GetJudgmentResult(timeDiff);
        state.judgmentResult = judgment;

        if (judgment == "Too Fast" || judgment == "Too Late")
        {
            judgmentManager.combo = 0;
            judgmentManager.errors++;
        }
        else
        {
            judgmentManager.combo++;
        }

        judgmentManager.totalNotesJudged++;
    }

    void CompleteHoldNote(NoteData note, HoldState state, string finalJudgment)
    {
        state.isJudged = true;
        note.isJudged = true;
        note.judgmentResult = finalJudgment;

    }

    void UpdateHoldVisuals()
    {
        foreach (var kvp in activeHolds)
        {
            var note = kvp.Key;
            var state = kvp.Value;

            if (note.noteObject == null) continue;

            var renderer = note.noteObject.GetComponent<SpriteRenderer>();
            if (renderer == null) continue;

            renderer.color = state.isHolding ? Color.green :
                           state.inProtection ? Color.yellow : Color.white;
        }
    }

    public void ResetHoldStates()
    {
        activeHolds.Clear();
    }

    public Dictionary<NoteData, HoldState> GetActiveHolds()
    {
        return activeHolds;
    }
}