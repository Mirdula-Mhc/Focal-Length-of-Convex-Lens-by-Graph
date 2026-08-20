using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Single-script Drag & Drop Manager for Screen Space UI (Overlay or Camera).
/// Assign Draggable Image / Snap Point pairs in the Inspector — no per-object
/// scripts required. The manager auto-attaches the runtime drag handling.
/// </summary>
[DisallowMultipleComponent]
public class UIDragDropManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------

    [Serializable]
    public class IntUnityEvent : UnityEvent<int> { }

    // ------------------------------------------------------------------
    // Element data
    // ------------------------------------------------------------------

    [Serializable]
    public class DragDropElement
    {
        [Tooltip("The UI Image RectTransform that will be dragged.")]
        public RectTransform DraggableImage;

        [Tooltip("The RectTransform this element must be dropped on.")]
        public RectTransform SnapPoint;

        [Tooltip("Maximum distance (canvas units) considered a correct drop.")]
        public float SnapDistance = 60f;

        // --- runtime state (hidden from inspector) ---
        [HideInInspector] public bool IsCompleted;
        [HideInInspector] public RectTransform ParentRect;
        [HideInInspector] public Vector3 HomePosition;          // world position
        [HideInInspector] public Vector2 DragStartAnchoredPos;
        [HideInInspector] public Vector2 DragStartLocalPointer;
        [HideInInspector] public CanvasGroup DragCanvasGroup;
        [HideInInspector] public Coroutine ActiveRoutine;
        [HideInInspector] public DragHandler Handler;
    }

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("Setup")]
    [Tooltip("How many drag-and-drop elements to configure.")]
    [SerializeField] private int numberOfElements = 1;

    [Tooltip("Optional. If left empty, the manager will search for a parent/scene Canvas automatically.")]
    [SerializeField] private Canvas canvas;

    [Header("Elements")]
    [SerializeField] private List<DragDropElement> elements = new List<DragDropElement>();

    [Header("Snap / Return Animation")]
    [SerializeField] private float snapAnimationDuration = 0.2f;
    [SerializeField] private float returnAnimationDuration = 0.2f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events")]
    public IntUnityEvent OnElementCompleted;
    public UnityEvent OnAllElementsCompleted;

    // ------------------------------------------------------------------
    // Internal state
    // ------------------------------------------------------------------

    private int completedCount;
    private bool allCompletedFired;

    public int ElementCount => elements.Count;
    public int CompletedCount => completedCount;
    public bool IsElementCompleted(int index) =>
        index >= 0 && index < elements.Count && elements[index].IsCompleted;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void OnValidate()
    {
        if (numberOfElements < 0) numberOfElements = 0;

        while (elements.Count < numberOfElements) elements.Add(new DragDropElement());
        while (elements.Count > numberOfElements) elements.RemoveAt(elements.Count - 1);
    }

    private void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
    }

    private void Start()
    {
        for (int i = 0; i < elements.Count; i++)
            SetupElement(i);
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void SetupElement(int index)
    {
        DragDropElement element = elements[index];

        if (element.DraggableImage == null)
        {
            Debug.LogWarning($"UIDragDropManager: Element {index} has no Draggable Image assigned.", this);
            return;
        }
        if (element.SnapPoint == null)
        {
            Debug.LogWarning($"UIDragDropManager: Element {index} has no Snap Point assigned.", this);
        }

        element.ParentRect = element.DraggableImage.parent as RectTransform;
        element.HomePosition = element.DraggableImage.position;

        // Make sure the image can receive pointer/touch events.
        Graphic graphic = element.DraggableImage.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = true;

        // CanvasGroup lets us lock a completed element without touching its hierarchy.
        element.DragCanvasGroup = element.DraggableImage.GetComponent<CanvasGroup>();
        if (element.DragCanvasGroup == null)
            element.DragCanvasGroup = element.DraggableImage.gameObject.AddComponent<CanvasGroup>();

        // Auto-attach the runtime drag handler — this is the only component
        // ever added to the draggable object, and it is added by code, not by hand.
        element.Handler = element.DraggableImage.GetComponent<DragHandler>();
        if (element.Handler == null)
            element.Handler = element.DraggableImage.gameObject.AddComponent<DragHandler>();
        element.Handler.Init(this, index);
    }

    /// <summary>
    /// Call this if your draggable images move (e.g. after a layout group rebuild)
    /// and you want their "home" / wrong-drop-return position re-cached.
    /// </summary>
    public void CacheHomePositions()
    {
        foreach (DragDropElement element in elements)
        {
            if (element.DraggableImage != null)
                element.HomePosition = element.DraggableImage.position;
        }
    }

    // ------------------------------------------------------------------
    // Drag callbacks (invoked by DragHandler)
    // ------------------------------------------------------------------

    internal void HandleBeginDrag(int index, PointerEventData eventData)
    {
        DragDropElement element = elements[index];
        if (element.IsCompleted || element.DraggableImage == null) return;

        if (element.ActiveRoutine != null)
        {
            StopCoroutine(element.ActiveRoutine);
            element.ActiveRoutine = null;
        }

        element.DraggableImage.SetAsLastSibling();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            element.ParentRect, eventData.position, eventData.pressEventCamera, out element.DragStartLocalPointer);

        element.DragStartAnchoredPos = element.DraggableImage.anchoredPosition;
    }

    internal void HandleDrag(int index, PointerEventData eventData)
    {
        DragDropElement element = elements[index];
        if (element.IsCompleted || element.DraggableImage == null) return;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            element.ParentRect, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            Vector2 delta = localPoint - element.DragStartLocalPointer;
            element.DraggableImage.anchoredPosition = element.DragStartAnchoredPos + delta;
        }
    }

    internal void HandleEndDrag(int index, PointerEventData eventData)
    {
        DragDropElement element = elements[index];
        if (element.IsCompleted || element.DraggableImage == null) return;

        if (element.SnapPoint != null)
        {
            float distance = Vector3.Distance(element.DraggableImage.position, element.SnapPoint.position);
            if (distance <= element.SnapDistance)
            {
                element.ActiveRoutine = StartCoroutine(SnapToTarget(index));
                return;
            }
        }

        element.ActiveRoutine = StartCoroutine(ReturnToHome(index));
    }

    // ------------------------------------------------------------------
    // Animations
    // ------------------------------------------------------------------

    private IEnumerator SnapToTarget(int index)
    {
        DragDropElement element = elements[index];
        Vector3 startPos = element.DraggableImage.position;
        Vector3 endPos = element.SnapPoint.position;

        yield return AnimatePosition(element.DraggableImage, startPos, endPos, snapAnimationDuration);

        element.DraggableImage.position = endPos;
        element.IsCompleted = true;
        element.ActiveRoutine = null;

        if (element.DragCanvasGroup != null)
        {
            element.DragCanvasGroup.blocksRaycasts = false;
            element.DragCanvasGroup.interactable = false;
        }

        completedCount++;
        OnElementCompleted?.Invoke(index);

        if (completedCount >= elements.Count && !allCompletedFired)
        {
            allCompletedFired = true;
            OnAllElementsCompleted?.Invoke();
        }
    }

    private IEnumerator ReturnToHome(int index)
    {
        DragDropElement element = elements[index];
        Vector3 startPos = element.DraggableImage.position;
        Vector3 endPos = element.HomePosition;

        yield return AnimatePosition(element.DraggableImage, startPos, endPos, returnAnimationDuration);

        element.DraggableImage.position = endPos;
        element.ActiveRoutine = null;
    }

    private IEnumerator AnimatePosition(RectTransform target, Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = animationCurve.Evaluate(Mathf.Clamp01(t / duration));
            target.position = Vector3.LerpUnclamped(from, to, p);
            yield return null;
        }
    }

    // ------------------------------------------------------------------
    // Reset API
    // ------------------------------------------------------------------

    public void ResetElement(int index)
    {
        if (index < 0 || index >= elements.Count) return;
        DragDropElement element = elements[index];
        if (element.DraggableImage == null) return;

        if (element.ActiveRoutine != null)
        {
            StopCoroutine(element.ActiveRoutine);
            element.ActiveRoutine = null;
        }

        if (element.IsCompleted)
            completedCount = Mathf.Max(0, completedCount - 1);

        element.IsCompleted = false;
        element.DraggableImage.position = element.HomePosition;

        if (element.DragCanvasGroup != null)
        {
            element.DragCanvasGroup.blocksRaycasts = true;
            element.DragCanvasGroup.interactable = true;
        }

        allCompletedFired = false;
    }

    public void ResetAll()
    {
        for (int i = 0; i < elements.Count; i++)
            ResetElement(i);

        completedCount = 0;
        allCompletedFired = false;
    }

    // ------------------------------------------------------------------
    // Runtime-only drag handler.
    // Added automatically by the manager via AddComponent<DragHandler>() —
    // never add this manually in the Inspector.
    // ------------------------------------------------------------------

    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private UIDragDropManager manager;
        private int index;

        public void Init(UIDragDropManager owner, int elementIndex)
        {
            manager = owner;
            index = elementIndex;
        }

        public void OnBeginDrag(PointerEventData eventData) => manager?.HandleBeginDrag(index, eventData);
        public void OnDrag(PointerEventData eventData) => manager?.HandleDrag(index, eventData);
        public void OnEndDrag(PointerEventData eventData) => manager?.HandleEndDrag(index, eventData);
    }
}
