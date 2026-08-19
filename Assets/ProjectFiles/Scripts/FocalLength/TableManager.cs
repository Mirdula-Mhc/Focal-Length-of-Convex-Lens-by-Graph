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
        [Tooltip("Correct image belonging to this cell.")]
        public GameObject correctFeedback;

        [Tooltip("Incorrect image belonging to this cell.")]
        public GameObject incorrectFeedback;

        [Header("Page Flow")]
        [Tooltip("0-based page index from PageNavigationController.")]
        public int pageIndex;

        [Tooltip("0 = first input on this page, 1 = second, etc.")]
        public int orderOnPage;

        [Header("Auto Fill On Page Enter")]
        [Tooltip("If true, this cell will automatically fill and complete when entering a specific page.")]
        public bool autoFillOnPageEnter;

        [Tooltip("If true, auto-fills when entering this cell's pageIndex. If false, uses customAutoFillPageIndex.")]
        public bool useCellPageIndexForAutoFill = true;

        [Tooltip("Specific 0-based page index to trigger auto-fill on (used when useCellPageIndexForAutoFill is false).")]
        public int customAutoFillPageIndex;

        [Tooltip("Optional custom text to fill. If left empty, correctAnswer is used.")]
        public string customAutoFillText;

        [Tooltip("If true, invokes onCorrectAnswer event when auto-filled.")]
        public bool triggerEventOnAutoFill = true;

        [Tooltip("If true, briefly displays the correctFeedback image when auto-filled.")]
        public bool showFeedbackOnAutoFill = false;

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
    // PAGE AUTO-FILL GROUPS (OPTIONAL BULK CONFIGURATION)
    // =========================================================

    [Serializable]
    public class PageAutoFillGroup
    {
        [Tooltip("0-based page index that triggers this group auto-fill.")]
        public int pageIndex;

        [Tooltip("List of table cells to automatically fill when entering this page.")]
        public List<TableCell> cellsToFill = new List<TableCell>();

        [Tooltip("If true, invokes onCorrectAnswer events for these cells.")]
        public bool triggerEvents = true;

        [Tooltip("If true, briefly displays correct feedback for these cells.")]
        public bool showFeedback = false;
    }

    [Header("Page Auto Fill Groups (Optional)")]
    [Tooltip("Configure groups of cells that should automatically fill when entering a specific page.")]
    [SerializeField]
    private List<PageAutoFillGroup> pageAutoFillGroups = new List<PageAutoFillGroup>();

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
    [SerializeField] private Button backspaceButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Button submitButton;

    // =========================================================
    // AUTO FILL
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
            }

            // Feedback always begins hidden.
            if (cell.correctFeedback != null)
                cell.correctFeedback.SetActive(false);

            if (cell.incorrectFeedback != null)
                cell.incorrectFeedback.SetActive(false);
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

        // Process any cells configured to auto-fill on this page.
        ProcessAutoFillForPage(pageIndex);

        // Determine which cell should now be active.
        RefreshCurrentPage();
    }

    // =========================================================
    // AUTO FILL ON PAGE ENTER
    // =========================================================

    private void ProcessAutoFillForPage(int pageIndex)
    {
        // 1. Process individual cell settings
        foreach (TableCell cell in tableCells)
        {
            if (cell == null)
                continue;

            if (!cell.autoFillOnPageEnter)
                continue;

            int triggerPage = cell.useCellPageIndexForAutoFill ? cell.pageIndex : cell.customAutoFillPageIndex;
            if (triggerPage == pageIndex && !cell.completed)
            {
                string textToFill = !string.IsNullOrEmpty(cell.customAutoFillText)
                    ? cell.customAutoFillText
                    : cell.correctAnswer;

                FillAndCompleteCell(cell, textToFill, cell.triggerEventOnAutoFill, cell.showFeedbackOnAutoFill);
            }
        }

        // 2. Process page auto-fill groups
        foreach (PageAutoFillGroup group in pageAutoFillGroups)
        {
            if (group == null || group.pageIndex != pageIndex)
                continue;

            if (group.cellsToFill == null)
                continue;

            foreach (TableCell cell in group.cellsToFill)
            {
                if (cell == null || cell.completed)
                    continue;

                FillAndCompleteCell(cell, cell.correctAnswer, group.triggerEvents, group.showFeedback);
            }
        }
    }

    private void FillAndCompleteCell(TableCell cell, string text, bool triggerEvent, bool showFeedback)
    {
        if (cell == null)
            return;

        if (cell.inputField != null)
        {
            cell.inputField.text = text;
            cell.inputField.interactable = false;
        }

        cell.completed = true;

        if (showFeedback)
        {
            ShowTemporaryFeedback(cell, showCorrect: true);
        }

        if (triggerEvent)
        {
            cell.onCorrectAnswer?.Invoke();
        }
    }

    /// <summary>
    /// Manually auto-fills a specific cell and marks it completed.
    /// </summary>
    public void AutoFillSpecificCell(TableCell cell, string customValue = null, bool triggerEvent = true, bool showFeedback = false)
    {
        if (cell == null)
            return;

        string val = string.IsNullOrEmpty(customValue) ? cell.correctAnswer : customValue;
        FillAndCompleteCell(cell, val, triggerEvent, showFeedback);
        RefreshCurrentPage();
    }

    /// <summary>
    /// Auto-fills all cells assigned to a specific page index.
    /// </summary>
    public void AutoFillCellsForPage(int pageIndex, bool triggerEvents = true, bool showFeedback = false)
    {
        foreach (TableCell cell in tableCells)
        {
            if (cell != null && cell.pageIndex == pageIndex && !cell.completed)
            {
                FillAndCompleteCell(cell, cell.correctAnswer, triggerEvents, showFeedback);
            }
        }
        RefreshCurrentPage();
    }

    // =========================================================
    // PAGE FLOW
    // =========================================================

    private void RefreshCurrentPage()
    {
        LockAllCells();

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

            UnlockCell(cell);
            return;
        }

        // Reaching this point means every table cell
        // assigned to this page has been completed.
        activeCell = null;

        SetAutoFillVisible(false);

        PageNavigationController.RequestNavigationUnlock();
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
            activeCell.correctAnswer.Trim();

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

        // Show this specific cell's wrong image.
        ShowTemporaryFeedback(
            wrongCell,
            showCorrect: false
        );

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

        // Show THIS cell's correct feedback.
        ShowTemporaryFeedback(
            completedCell,
            showCorrect: true
        );

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
    // FEEDBACK
    // =========================================================

    private void ShowTemporaryFeedback(
        TableCell cell,
        bool showCorrect)
    {
        if (cell == null)
            return;

        // If this cell already has a feedback timer running,
        // stop it before starting a new one.
        if (feedbackRoutines.TryGetValue(
            cell,
            out Coroutine existingRoutine))
        {
            if (existingRoutine != null)
                StopCoroutine(existingRoutine);

            feedbackRoutines.Remove(cell);
        }

        // Reset both feedback objects first.
        if (cell.correctFeedback != null)
            cell.correctFeedback.SetActive(false);

        if (cell.incorrectFeedback != null)
            cell.incorrectFeedback.SetActive(false);

        // Show requested feedback.
        if (showCorrect)
        {
            if (cell.correctFeedback != null)
                cell.correctFeedback.SetActive(true);
        }
        else
        {
            if (cell.incorrectFeedback != null)
                cell.incorrectFeedback.SetActive(true);
        }

        // Start timer to hide it again.
        Coroutine routine = StartCoroutine(
            HideFeedbackAfterDelay(cell)
        );

        feedbackRoutines[cell] = routine;
    }

    private IEnumerator HideFeedbackAfterDelay(TableCell cell)
    {
        yield return new WaitForSeconds(feedbackDuration);

        if (cell != null)
        {
            if (cell.correctFeedback != null)
                cell.correctFeedback.SetActive(false);

            if (cell.incorrectFeedback != null)
                cell.incorrectFeedback.SetActive(false);
        }

        feedbackRoutines.Remove(cell);
    }

    private void HideAllFeedback()
    {
        // Stop every currently running feedback timer.
        foreach (Coroutine routine in feedbackRoutines.Values)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        feedbackRoutines.Clear();

        // Hide every correct/wrong image.
        foreach (TableCell cell in tableCells)
        {
            if (cell == null)
                continue;

            if (cell.correctFeedback != null)
                cell.correctFeedback.SetActive(false);

            if (cell.incorrectFeedback != null)
                cell.incorrectFeedback.SetActive(false);
        }
    }
}