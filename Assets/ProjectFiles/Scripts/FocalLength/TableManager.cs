using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    // =========================================================
    // TABLE CELL
    // =========================================================

    [Serializable]
    public class TableCell
    {
        [Header("Identification")]
        [Tooltip("Inspector name only. Example: Trial 1 - Row 1")]
        public string cellName;

        [Header("Input")]
        public TMP_InputField inputField;

        [Header("Answer")]
        [Tooltip("Exact required answer. Example: 25.0 or 0.250")]
        public string correctAnswer;

        [Header("Feedback")]
        [Tooltip("Color the input field flashes briefly on a correct answer, before reverting.")]
        public Color correctFlashColor = Color.green;

        [Tooltip("Color the input field flashes briefly on a wrong answer, before reverting.")]
        public Color incorrectFlashColor = Color.red;

        // Runtime only - the field's original color, captured once so
        // flashes always revert to the true starting color.
        [NonSerialized] public Color originalColor;
        [NonSerialized] public bool originalColorCaptured;

        [Header("Page Flow")]
        [Tooltip("0-based page index from PageNavigationController.")]
        public int pageIndex;

        [Tooltip("0 = first input on this page, 1 = second, etc.")]
        public int orderOnPage;

        [Header("Auto Fill (Optional)")]
        [Tooltip("Turn ON to skip the numpad entirely for this cell. It " +
                 "auto-fills with correctAnswer as soon as the user " +
                 "reaches this cell's own pageIndex - no typing needed.")]
        public bool isAutoFillCell = false;

        [Header("Events")]
        [Tooltip("Called once when this cell is completed correctly.")]
        public UnityEvent onCorrectAnswer;

        // Runtime state only.
        [NonSerialized] public bool completed;
        [NonSerialized] public int wrongAttempts;
    }

    // =========================================================
    // TABLE
    // =========================================================

    [Header("Table Cells")]
    [Tooltip("Configure every editable table cell here.")]
    [SerializeField]
    private List<TableCell> tableCells = new List<TableCell>();

    // =========================================================
    // NUMPAD
    // =========================================================

    [Header("Numpad - Digits")]
    [SerializeField] private Button button0;
    [SerializeField] private Button button1;
    [SerializeField] private Button button2;
    [SerializeField] private Button button3;
    [SerializeField] private Button button4;
    [SerializeField] private Button button5;
    [SerializeField] private Button button6;
    [SerializeField] private Button button7;
    [SerializeField] private Button button8;
    [SerializeField] private Button button9;

    [Header("Numpad - Controls")]
    [SerializeField] private Button decimalButton;
    [SerializeField] private Button plusMinusButton;
    [SerializeField] private Button backspaceButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button submitButton;

    // =========================================================
    // AUTO FILL (wrong-attempt bailout, unchanged from original)
    // =========================================================

    [Header("Auto Fill")]
    [Tooltip("One shared Auto Fill button for the currently active cell.")]
    [SerializeField] private Button autoFillButton;

    [Tooltip("Number of wrong submissions required before Auto Fill appears.")]
    [Min(1)]
    [SerializeField] private int wrongAttemptsForAutoFill = 3;

    // =========================================================
    // FEEDBACK
    // =========================================================

    [Header("Feedback Settings")]
    [Tooltip("How long Correct / Incorrect images remain visible.")]
    [Min(0f)]
    [SerializeField] private float feedbackDuration = 1.5f;

    // =========================================================
    // RUNTIME
    // =========================================================

    private TableCell activeCell;
    private int currentPageIndex = -1;

    // Keeps feedback timers separate for every table cell.
    private readonly Dictionary<TableCell, Coroutine> feedbackRoutines =
        new Dictionary<TableCell, Coroutine>();

    // =========================================================
    // UNITY
    // =========================================================

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void Start()
    {
        PrepareTableCells();
        SetupButtons();

        // Auto Fill must start hidden.
        SetAutoFillVisible(false);

        HandlePageChanged(PageNavigationController.CurrentIndex);
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void PrepareTableCells()
    {
        foreach (TableCell cell in tableCells)
        {
            if (cell == null)
                continue;

            if (cell.inputField != null)
            {
                // User cannot type directly using keyboard/mobile keyboard.
                // Input is controlled by the UI numpad.
                cell.inputField.readOnly = true;

                // All cells begin locked.
                cell.inputField.interactable = false;

                // Capture the field's real starting color once, so
                // flashes always have a true color to revert to.
                if (!cell.originalColorCaptured &&
                    cell.inputField.targetGraphic != null)
                {
                    cell.originalColor = cell.inputField.targetGraphic.color;
                    cell.originalColorCaptured = true;
                }
            }

            // Every cell starts fully hidden (opacity 0) until it
            // becomes the active cell for its page.
            SetCellOpacity(cell, 0f);
        }
    }

    // =========================================================
    // CELL VISIBILITY (OPACITY)
    // =========================================================

    /// <summary>
    /// Fades the input field's own Graphic (background/box) to the given
    /// alpha, without touching the field's text component. Used to:
    /// - hide a cell entirely (alpha 0) while it's not the active cell
    /// - show it fully (alpha 1) while the user is filling it in
    /// - fade the box away (alpha 0) once completed, leaving just the
    ///   text visible on top of it
    /// </summary>
    private void SetCellOpacity(TableCell cell, float alpha)
    {
        if (cell == null || cell.inputField == null)
            return;

        Graphic bg = cell.inputField.targetGraphic;

        if (bg != null)
        {
            Color c = bg.color;
            c.a = alpha;
            bg.color = c;
        }
    }

    private void SetupButtons()
    {
        if (button0)
            button0.onClick.AddListener(() => AddDigit("0"));

        if (button1)
            button1.onClick.AddListener(() => AddDigit("1"));

        if (button2)
            button2.onClick.AddListener(() => AddDigit("2"));

        if (button3)
            button3.onClick.AddListener(() => AddDigit("3"));

        if (button4)
            button4.onClick.AddListener(() => AddDigit("4"));

        if (button5)
            button5.onClick.AddListener(() => AddDigit("5"));

        if (button6)
            button6.onClick.AddListener(() => AddDigit("6"));

        if (button7)
            button7.onClick.AddListener(() => AddDigit("7"));

        if (button8)
            button8.onClick.AddListener(() => AddDigit("8"));

        if (button9)
            button9.onClick.AddListener(() => AddDigit("9"));

        if (decimalButton)
            decimalButton.onClick.AddListener(AddDecimal);

        if (plusMinusButton)
            plusMinusButton.onClick.AddListener(ToggleSign);

        if (backspaceButton)
            backspaceButton.onClick.AddListener(Backspace);

        if (clearButton)
            clearButton.onClick.AddListener(Clear);

        if (submitButton)
            submitButton.onClick.AddListener(Submit);

        if (autoFillButton)
            autoFillButton.onClick.AddListener(AutoFill);
    }

    // =========================================================
    // PAGE CHANGE
    // =========================================================

    private void HandlePageChanged(int pageIndex)
    {
        currentPageIndex = pageIndex;

        // Lock everything before evaluating the new page.
        LockAllCells();

        // Feedback from the previous page should not remain visible.
        HideAllFeedback();

        // Determine which cell should now be active.
        RefreshCurrentPage();
    }

    // =========================================================
    // PAGE FLOW
    // =========================================================

    private void RefreshCurrentPage()
    {
        LockAllCells();

        // Reveal any linked cells on this page whose source cell is
        // already completed, before deciding what the user needs to type.
        RevealLinkedCellsForPage(currentPageIndex);

        List<TableCell> pageCells = GetCellsForPage(currentPageIndex);

        // No table interaction configured for this page.
        if (pageCells.Count == 0)
        {
            SetAutoFillVisible(false);
            return;
        }

        // Sequence fields according to Order On Page.
        pageCells.Sort(
            (a, b) => a.orderOnPage.CompareTo(b.orderOnPage)
        );

        // Find the first field that has not been completed.
        foreach (TableCell cell in pageCells)
        {
            if (cell == null)
                continue;

            if (cell.completed)
                continue;

            // Linked cells are never unlocked for numpad entry - they
            // only get filled by RevealLinkedCellsForPage above, once
            // their source cell is done. Skip past them here.
            if (cell.isAutoFillCell)
                continue;

            UnlockCell(cell);
            return;
        }

        // Reaching this point means every table cell
        // assigned to this page has been completed (or is still
        // waiting on a source cell elsewhere - see note below).
        activeCell = null;

        SetAutoFillVisible(false);

        // Only unlock navigation once every cell on this page - including
        // linked ones - is actually completed.
        if (AllCellsOnPageCompleted(pageCells))
            PageNavigationController.RequestNavigationUnlock();
    }

    /// <summary>
    /// Fills in any TableCell on this page that has Is Auto Fill Cell
    /// turned on. No dependency on any other cell - it simply reveals
    /// itself with correctAnswer the moment the user reaches its page.
    /// </summary>
    private void RevealLinkedCellsForPage(int pageIndex)
    {
        foreach (TableCell cell in tableCells)
        {
            if (cell == null || cell.completed)
                continue;

            if (cell.pageIndex != pageIndex)
                continue;

            if (!cell.isAutoFillCell)
                continue;

            if (cell.inputField != null)
                cell.inputField.text = cell.correctAnswer;

            cell.completed = true;

            if (cell.inputField != null)
                cell.inputField.interactable = false;

            // Same rule as a normally-completed cell: box fades away,
            // only the text remains visible.
            SetCellOpacity(cell, 0f);

            cell.onCorrectAnswer?.Invoke();
        }
    }

    private bool AllCellsOnPageCompleted(List<TableCell> pageCells)
    {
        foreach (TableCell cell in pageCells)
        {
            if (cell != null && !cell.completed)
                return false;
        }

        return true;
    }

    private List<TableCell> GetCellsForPage(int pageIndex)
    {
        List<TableCell> result = new List<TableCell>();

        foreach (TableCell cell in tableCells)
        {
            if (cell == null)
                continue;

            if (cell.pageIndex == pageIndex)
                result.Add(cell);
        }

        return result;
    }

    // =========================================================
    // LOCK / UNLOCK
    // =========================================================

    private void LockAllCells()
    {
        foreach (TableCell cell in tableCells)
        {
            if (cell == null || cell.inputField == null)
                continue;

            cell.inputField.interactable = false;

            // Hide every cell that isn't completed - only the active
            // cell (set right after this, in UnlockCell) should be
            // visible for entry. Completed cells stay at 0 (box hidden,
            // their text remains visible on its own).
            if (!cell.completed)
                SetCellOpacity(cell, 0f);
        }

        activeCell = null;

        // No active cell = Auto Fill cannot be used.
        SetAutoFillVisible(false);
    }

    private void UnlockCell(TableCell cell)
    {
        if (cell == null)
            return;

        if (cell.completed)
            return;

        if (cell.inputField == null)
        {
            Debug.LogWarning(
                $"TableManager: '{cell.cellName}' has no Input Field assigned."
            );

            return;
        }

        cell.inputField.interactable = true;

        // The active cell is the only one visible for numpad entry.
        SetCellOpacity(cell, 1f);

        activeCell = cell;

        // If the user previously made 3 mistakes on this field,
        // Auto Fill should still be available when returning.
        UpdateAutoFillVisibility();
    }

    // =========================================================
    // NUMPAD
    // =========================================================

    public void AddDigit(string digit)
    {
        if (!CanEditActiveCell())
            return;

        activeCell.inputField.text += digit;
    }

    public void AddDecimal()
    {
        if (!CanEditActiveCell())
            return;

        string value = activeCell.inputField.text;

        // Only one decimal point is allowed.
        if (value.Contains("."))
            return;

        if (string.IsNullOrEmpty(value))
        {
            activeCell.inputField.text = "0.";
        }
        else
        {
            activeCell.inputField.text += ".";
        }
    }

    /// <summary>
    /// Toggles the leading minus sign on the active cell's current value.
    /// Empty field + toggle just inserts "-", ready for digits after it.
    /// </summary>
    public void ToggleSign()
    {
        if (!CanEditActiveCell())
            return;

        string value = activeCell.inputField.text;

        if (string.IsNullOrEmpty(value))
        {
            activeCell.inputField.text = "-";
        }
        else if (value.StartsWith("-"))
        {
            activeCell.inputField.text = value.Substring(1);
        }
        else
        {
            activeCell.inputField.text = "-" + value;
        }
    }

    public void Backspace()
    {
        if (!CanEditActiveCell())
            return;

        string value = activeCell.inputField.text;

        if (string.IsNullOrEmpty(value))
            return;

        activeCell.inputField.text =
            value.Substring(0, value.Length - 1);
    }

    public void Clear()
    {
        if (!CanEditActiveCell())
            return;

        activeCell.inputField.text = "";
    }

    private bool CanEditActiveCell()
    {
        if (activeCell == null)
            return false;

        if (activeCell.completed)
            return false;

        if (activeCell.inputField == null)
            return false;

        return activeCell.inputField.interactable;
    }

    // =========================================================
    // SUBMIT / CHECK
    // =========================================================

    public void Submit()
    {
        if (!CanEditActiveCell())
            return;

        string entered =
            activeCell.inputField.text.Trim();

        string expected =
            (activeCell.correctAnswer ?? "").Trim();

        if (entered == expected)
        {
            CompleteActiveCell();
        }
        else
        {
            HandleIncorrectAnswer();
        }
    }

    // =========================================================
    // INCORRECT ANSWER
    // =========================================================

    private void HandleIncorrectAnswer()
    {
        if (activeCell == null)
            return;

        TableCell wrongCell = activeCell;

        // Count ONLY actual failed Check/Submit attempts.
        wrongCell.wrongAttempts++;

        Debug.Log(
            $"TableManager: '{wrongCell.cellName}' wrong attempt " +
            $"{wrongCell.wrongAttempts}/{wrongAttemptsForAutoFill}"
        );

        // Flash this field red briefly, then it reverts to normal.
        FlashCellColor(wrongCell, correct: false);

        // Automatically clear incorrect input.
        wrongCell.inputField.text = "";

        // Check whether Auto Fill has now become available.
        UpdateAutoFillVisibility();

        // The same field remains active and interactable.
    }

    // =========================================================
    // CORRECT ANSWER
    // =========================================================

    private void CompleteActiveCell()
    {
        if (activeCell == null)
            return;

        TableCell completedCell = activeCell;

        // Display the answer using the configured format.
        completedCell.inputField.text =
            completedCell.correctAnswer;

        // Permanently complete this field.
        completedCell.completed = true;

        // Lock it immediately.
        completedCell.inputField.interactable = false;

        // Flash the field green briefly - RevertFlashAfterDelay fades
        // the box to alpha 0 afterward, leaving just the answer text.
        FlashCellColor(completedCell, correct: true);

        // Hide Auto Fill because this field is finished.
        SetAutoFillVisible(false);

        // -----------------------------------------------------
        // PER-CELL UNITY EVENT
        // -----------------------------------------------------
        //
        // This can trigger:
        // Animation
        // Audio
        // Timeline
        // GameObject
        // Another script
        // etc.
        //
        completedCell.onCorrectAnswer?.Invoke();

        // -----------------------------------------------------

        activeCell = null;

        // Either:
        // - unlock the next field on this page
        // - or unlock navigation if the page is complete
        RefreshCurrentPage();
    }

    // =========================================================
    // AUTO FILL
    // =========================================================

    public void AutoFill()
    {
        if (!CanEditActiveCell())
            return;

        // Safety check.
        // Even if something externally enables the button,
        // Auto Fill cannot be used before enough wrong attempts.
        if (activeCell.wrongAttempts < wrongAttemptsForAutoFill)
            return;

        activeCell.inputField.text =
            activeCell.correctAnswer;

        // Auto Fill counts exactly like a correct answer.
        CompleteActiveCell();
    }

    private void UpdateAutoFillVisibility()
    {
        bool shouldShow =
            activeCell != null &&
            !activeCell.completed &&
            activeCell.wrongAttempts >= wrongAttemptsForAutoFill;

        SetAutoFillVisible(shouldShow);
    }

    private void SetAutoFillVisible(bool visible)
    {
        if (autoFillButton == null)
            return;

        // The whole button appears/disappears.
        if (autoFillButton.gameObject.activeSelf != visible)
            autoFillButton.gameObject.SetActive(visible);
    }

    // =========================================================
    // FEEDBACK (COLOR FLASH)
    // =========================================================

    /// <summary>
    /// Flashes the input field's background to green (correct) or red
    /// (incorrect), then reverts it to its original color after
    /// feedbackDuration. For a correct answer, the field is also faded
    /// to alpha 0 right after the flash, since the field gets replaced
    /// by plain answer text at that point (see CompleteActiveCell).
    /// </summary>
    private void FlashCellColor(TableCell cell, bool correct)
    {
        if (cell == null || cell.inputField == null)
            return;

        Graphic bg = cell.inputField.targetGraphic;

        if (bg == null)
            return;

        // If this cell already has a flash running, stop it first so
        // rapid wrong-answers don't stack coroutines on the same cell.
        if (feedbackRoutines.TryGetValue(cell, out Coroutine existing))
        {
            if (existing != null)
                StopCoroutine(existing);

            feedbackRoutines.Remove(cell);
        }

        Color flashColor = correct
            ? cell.correctFlashColor
            : cell.incorrectFlashColor;

        // Flashes only ever happen while a cell is the active, fully
        // visible cell - always flash at full alpha rather than reading
        // whatever the graphic's alpha happens to report this frame.
        flashColor.a = 1f;
        bg.color = flashColor;

        Coroutine routine = StartCoroutine(RevertFlashAfterDelay(cell, correct));
        feedbackRoutines[cell] = routine;
    }

    private IEnumerator RevertFlashAfterDelay(TableCell cell, bool correct)
    {
        yield return new WaitForSeconds(feedbackDuration);

        if (cell != null && cell.inputField != null &&
            cell.inputField.targetGraphic != null)
        {
            if (correct)
            {
                // Correct + completed cells end up as plain text -
                // fade the box away instead of reverting its color.
                SetCellOpacity(cell, 0f);
            }
            else
            {
                cell.inputField.targetGraphic.color = cell.originalColor;
            }
        }

        feedbackRoutines.Remove(cell);
    }

    private void HideAllFeedback()
    {
        // Stop every currently running flash timer and snap each
        // affected cell straight back to its resting state.
        foreach (KeyValuePair<TableCell, Coroutine> kvp in feedbackRoutines)
        {
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);

            TableCell cell = kvp.Key;

            if (cell == null || cell.inputField == null ||
                cell.inputField.targetGraphic == null)
                continue;

            if (cell.completed)
                SetCellOpacity(cell, 0f);
            else
                cell.inputField.targetGraphic.color = cell.originalColor;
        }

        feedbackRoutines.Clear();
    }
}