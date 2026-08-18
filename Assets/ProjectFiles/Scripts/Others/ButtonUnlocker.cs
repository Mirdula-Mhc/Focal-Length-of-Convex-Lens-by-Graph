using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ButtonUnlocker : MonoBehaviour
{
    [Header("Buttons To Track")]
    public List<Button> targetButtons = new List<Button>();

    [Header("Dropdowns To Track")]
    [SerializeField] private List<DropdownMainButton> targetDropdowns = new List<DropdownMainButton>();

    [Header("Page Flow")]
    //public CellPotentialEvPageFlow slideController;

    [Header("Optional")]
    public bool disableButtonAfterPress = false;
    public bool countOnlyOnce = true;

    private HashSet<Button> pressedButtons = new HashSet<Button>();
    private HashSet<DropdownMainButton> correctDropdowns = new HashSet<DropdownMainButton>();

    void Start()
    {
        foreach (Button btn in targetButtons)
        {
            if (btn == null) continue;
            Button capturedButton = btn;
            capturedButton.onClick.AddListener(() => OnButtonPressed(capturedButton));
        }

        foreach (DropdownMainButton dd in targetDropdowns)
        {
            if (dd == null) continue;
            DropdownMainButton capturedDropdown = dd;
            capturedDropdown.OnCorrectAnswer += () => OnDropdownCorrect(capturedDropdown);
        }
    }

    void OnButtonPressed(Button btn)
    {
        if (btn == null) return;

        if (countOnlyOnce)
        {
            if (!pressedButtons.Contains(btn))
                pressedButtons.Add(btn);
        }
        else
        {
            pressedButtons.Add(btn);
        }

        if (disableButtonAfterPress)
            btn.interactable = false;

        CheckCompletion();
    }

    void OnDropdownCorrect(DropdownMainButton dd)
    {
        correctDropdowns.Add(dd);
        CheckCompletion();
    }

    void CheckCompletion()
    {
        bool allButtonsDone = pressedButtons.Count >= targetButtons.Count;
        bool allDropdownsDone = correctDropdowns.Count >= targetDropdowns.Count;

        if (allButtonsDone && allDropdownsDone)
        {
            PageNavigationController.RequestNavigationUnlock();
        }
    }

    public void ResetProgress()
    {
        pressedButtons.Clear();
        correctDropdowns.Clear();

        foreach (Button btn in targetButtons)
        {
            if (btn != null)
                btn.interactable = true;
        }
    }
}