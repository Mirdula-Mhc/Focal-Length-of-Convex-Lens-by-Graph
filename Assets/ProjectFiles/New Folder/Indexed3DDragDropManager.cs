using UnityEngine;
using System;
using System.Collections.Generic;

public class Indexed3DDragDropManager : MonoBehaviour
{
    [Serializable]
    public class DragDropElement
    {
        [Header("Element Settings")]
        [Tooltip("Page index where this element should work.")]
        public int index;

        [Tooltip("3D object that can be dragged. Same object can be used in multiple indexes.")]
        public GameObject draggableObject;

        [Tooltip("Highlight object shown while this element is being dragged.")]
        public GameObject highlightObject;

        [Header("Snap Settings")]
        [Tooltip("Collider that defines the valid drop area for this index.")]
        public Collider snapCollider;

        [Tooltip("Transform where the object will snap for this index.")]
        public Transform snapTransform;

        [Header("Options")]
        [Tooltip("After successful drop, lock this element for this index.")]
        public bool lockAfterSuccessfulSnap = true;

        [Tooltip("Hide highlight after successful snap.")]
        public bool hideHighlightAfterSnap = true;

        [Tooltip("Hide highlight after invalid drop.")]
        public bool hideHighlightAfterInvalidDrop = true;

        // ------------------------------------------------------------
        // Runtime state
        // ------------------------------------------------------------

        [NonSerialized]
        public bool isCompleted;

        [NonSerialized]
        public bool isDragging;

        [NonSerialized]
        public bool initialized;

        // These are kept only for compatibility/reset purposes.
        [NonSerialized]
        public Vector3 originalPosition;

        [NonSerialized]
        public Quaternion originalRotation;

        [NonSerialized]
        public Vector3 originalScale;
    }

    // ================================================================
    // INSPECTOR
    // ================================================================

    [Header("Drag & Drop Elements")]
    [SerializeField]
    private List<DragDropElement> elements =
        new List<DragDropElement>();

    [Header("Drag Settings")]
    [Tooltip("Default drag distance from camera.")]
    [SerializeField]
    private float dragDistance = 5f;

    [Tooltip("Drag smoothing speed.")]
    [SerializeField]
    private float dragSmoothSpeed = 20f;

    [Tooltip("If enabled, object follows smoothly.")]
    [SerializeField]
    private bool smoothDragging = true;

    [Header("Raycast Settings")]
    [Tooltip("Layers on which draggable objects exist.")]
    [SerializeField]
    private LayerMask draggableLayerMask = ~0;

    [Tooltip("Maximum raycast distance.")]
    [SerializeField]
    private float maxRaycastDistance = 1000f;

    [Header("Page Navigation")]
    [Tooltip("Only the element whose Index matches the current page can be dragged.")]
    [SerializeField]
    private bool usePageIndex = true;

    [Tooltip("Unlock PageNavigationController after successful drop.")]
    [SerializeField]
    private bool unlockPageAfterSuccess = true;

    [Header("Input")]
    [SerializeField]
    private bool enableMouse = true;

    [SerializeField]
    private bool enableTouch = true;

    [Header("General")]
    [Tooltip("Hide all highlights when the scene starts.")]
    [SerializeField]
    private bool hideHighlightsOnStart = true;

    [Tooltip("Only one object can be dragged at a time.")]
    [SerializeField]
    private bool singleDragAtATime = true;

    [Header("Debug")]
    [SerializeField]
    private bool debugLogs = false;

    // ================================================================
    // RUNTIME VARIABLES
    // ================================================================

    private Camera mainCamera;

    private DragDropElement currentElement;

    private Transform currentDraggedTransform;

    private Vector3 currentDragTargetPosition;

    private Vector3 dragOffset;

    private float currentDragDepth;

    private bool isDragging;

    private int activePointerId = -1;

    // ------------------------------------------------------------
    // IMPORTANT:
    // Position where the current drag STARTED.
    //
    // This is different from the object's original scene position.
    //
    // Example:
    // Page 1 -> Cube snapped to Point 1
    // Page 7 -> Cube starts dragging from Point 1
    // Invalid drop -> Cube returns to Point 1
    // ------------------------------------------------------------

    private Vector3 dragStartPosition;

    private Quaternion dragStartRotation;

    private Vector3 dragStartScale;

    // ================================================================
    // UNITY
    // ================================================================

    private void Awake()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            mainCamera =
                FindFirstObjectByType<Camera>();
        }

        InitializeElements();
    }

    private void Start()
    {
        if (hideHighlightsOnStart)
        {
            HideAllHighlights();
        }
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
                return;
        }

        if (!isDragging)
        {
            HandleInput();
        }
        else
        {
            HandleDragInput();
            UpdateDragging();
        }
    }

    // ================================================================
    // INITIALIZE
    // ================================================================

    private void InitializeElements()
    {
        for (int i = 0; i < elements.Count; i++)
        {
            DragDropElement element =
                elements[i];

            if (element == null)
                continue;

            if (element.draggableObject == null)
            {
                Debug.LogWarning(
                    $"[{name}] Element {i} has no Draggable Object.",
                    this
                );

                continue;
            }

            element.originalPosition =
                element.draggableObject.transform.position;

            element.originalRotation =
                element.draggableObject.transform.rotation;

            element.originalScale =
                element.draggableObject.transform.localScale;

            element.isCompleted = false;
            element.isDragging = false;
            element.initialized = true;

            if (element.highlightObject != null)
            {
                element.highlightObject.SetActive(false);
            }
        }
    }

    // ================================================================
    // INPUT
    // ================================================================

    private void HandleInput()
    {
        // ------------------------------------------------------------
        // TOUCH
        // ------------------------------------------------------------

        if (enableTouch &&
            Input.touchCount > 0)
        {
            Touch touch =
                Input.GetTouch(0);

            if (touch.phase ==
                TouchPhase.Began)
            {
                activePointerId =
                    touch.fingerId;

                TryStartDrag(
                    touch.position
                );

                return;
            }
        }

        // ------------------------------------------------------------
        // MOUSE
        // ------------------------------------------------------------

        if (enableMouse)
        {
            if (Input.GetMouseButtonDown(0))
            {
                activePointerId = -1;

                TryStartDrag(
                    Input.mousePosition
                );
            }
        }
    }

    // ================================================================
    // DRAG INPUT
    // ================================================================

    private void HandleDragInput()
    {
        // ------------------------------------------------------------
        // TOUCH
        // ------------------------------------------------------------

        if (enableTouch &&
            activePointerId >= 0)
        {
            if (Input.touchCount > 0)
            {
                Touch touch =
                    FindTouch(
                        activePointerId
                    );

                if (touch.fingerId ==
                    activePointerId)
                {
                    if (touch.phase ==
                            TouchPhase.Moved ||
                        touch.phase ==
                            TouchPhase.Stationary)
                    {
                        UpdatePointerPosition(
                            touch.position
                        );
                    }

                    if (touch.phase ==
                            TouchPhase.Ended ||
                        touch.phase ==
                            TouchPhase.Canceled)
                    {
                        UpdatePointerPosition(
                            touch.position
                        );

                        FinishDrag();
                    }
                }
            }

            return;
        }

        // ------------------------------------------------------------
        // MOUSE
        // ------------------------------------------------------------

        if (enableMouse)
        {
            if (Input.GetMouseButton(0))
            {
                UpdatePointerPosition(
                    Input.mousePosition
                );
            }

            if (Input.GetMouseButtonUp(0))
            {
                UpdatePointerPosition(
                    Input.mousePosition
                );

                FinishDrag();
            }
        }
    }

    // ================================================================
    // FIND TOUCH
    // ================================================================

    private Touch FindTouch(int fingerId)
    {
        for (int i = 0;
             i < Input.touchCount;
             i++)
        {
            Touch touch =
                Input.GetTouch(i);

            if (touch.fingerId ==
                fingerId)
            {
                return touch;
            }
        }

        return default;
    }

    // ================================================================
    // START DRAG
    // ================================================================

    private void TryStartDrag(
        Vector3 screenPosition)
    {
        if (singleDragAtATime &&
            isDragging)
        {
            return;
        }

        Ray ray =
            mainCamera.ScreenPointToRay(
                screenPosition
            );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxRaycastDistance,
                draggableLayerMask,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        // ============================================================
        // IMPORTANT FIX
        //
        // DO NOT simply find the first element using the object.
        //
        // Same object can exist multiple times:
        //
        // Element 0:
        // Cube + Index 1
        //
        // Element 1:
        // Cube + Index 7
        //
        // We MUST find the element matching BOTH:
        //
        // 1. The clicked object
        // 2. Current Page Index
        //
        // ============================================================

        int currentPage =
            GetCurrentPageIndex();

        DragDropElement element =
            FindMatchingElement(
                hit,
                currentPage
            );

        if (element == null)
        {
            Log(
                $"No matching element found for " +
                $"current page {currentPage}."
            );

            return;
        }

        // ------------------------------------------------------------
        // Check completion ONLY for this index
        // ------------------------------------------------------------

        if (element.isCompleted &&
            element.lockAfterSuccessfulSnap)
        {
            Log(
                $"Element Index {element.index} " +
                "is already completed."
            );

            return;
        }

        StartDrag(
            element,
            hit,
            screenPosition
        );
    }

    // ================================================================
    // FIND MATCHING ELEMENT
    // ================================================================

    private DragDropElement FindMatchingElement(
        RaycastHit hit,
        int currentPage)
    {
        Transform hitTransform =
            hit.collider.transform;

        for (int i = 0;
             i < elements.Count;
             i++)
        {
            DragDropElement element =
                elements[i];

            if (element == null)
                continue;

            if (element.draggableObject == null)
                continue;

            // --------------------------------------------------------
            // PAGE MATCH
            // --------------------------------------------------------

            if (usePageIndex &&
                element.index != currentPage)
            {
                continue;
            }

            // --------------------------------------------------------
            // OBJECT MATCH
            // --------------------------------------------------------

            Transform draggableTransform =
                element.draggableObject.transform;

            bool objectMatched = false;

            // Direct collider
            if (hitTransform ==
                draggableTransform)
            {
                objectMatched = true;
            }

            // Child collider
            else if (hitTransform.IsChildOf(
                        draggableTransform))
            {
                objectMatched = true;
            }

            if (!objectMatched)
                continue;

            // --------------------------------------------------------
            // FOUND CORRECT OBJECT + CORRECT PAGE
            // --------------------------------------------------------

            Log(
                $"MATCH FOUND | Object = " +
                $"{element.draggableObject.name} | " +
                $"Index = {element.index}"
            );

            return element;
        }

        return null;
    }

    // ================================================================
    // CURRENT PAGE
    // ================================================================

    private int GetCurrentPageIndex()
    {
        if (PageNavigationController.Instance != null)
        {
            return PageNavigationController.CurrentIndex;
        }

        return 0;
    }

    // ================================================================
    // START DRAG
    // ================================================================

    private void StartDrag(
        DragDropElement element,
        RaycastHit hit,
        Vector3 screenPosition)
    {
        currentElement =
            element;

        currentDraggedTransform =
            element.draggableObject.transform;

        isDragging = true;

        element.isDragging = true;

        // ------------------------------------------------------------
        // STORE CURRENT POSITION
        //
        // IMPORTANT FOR SAME OBJECT / MULTIPLE PAGES
        // ------------------------------------------------------------

        dragStartPosition =
            currentDraggedTransform.position;

        dragStartRotation =
            currentDraggedTransform.rotation;

        dragStartScale =
            currentDraggedTransform.localScale;

        // ------------------------------------------------------------
        // Calculate camera depth
        // ------------------------------------------------------------

        currentDragDepth =
            Vector3.Distance(
                mainCamera.transform.position,
                currentDraggedTransform.position
            );

        if (currentDragDepth <= 0.01f)
        {
            currentDragDepth =
                dragDistance;
        }

        // ------------------------------------------------------------
        // Calculate drag offset
        // ------------------------------------------------------------

        Vector3 mouseWorldPosition =
            GetWorldPositionFromScreen(
                screenPosition,
                currentDragDepth
            );

        dragOffset =
            currentDraggedTransform.position -
            mouseWorldPosition;

        // ------------------------------------------------------------
        // SHOW CURRENT ELEMENT HIGHLIGHT
        // ------------------------------------------------------------

        HideAllHighlights();

        if (element.highlightObject != null)
        {
            element.highlightObject.SetActive(true);
        }

        Log(
            $"START DRAG | " +
            $"Object = {element.draggableObject.name} | " +
            $"Index = {element.index}"
        );
    }

    // ================================================================
    // UPDATE DRAG
    // ================================================================

    private void UpdateDragging()
    {
        if (!isDragging)
            return;

        if (currentElement == null)
            return;

        if (currentDraggedTransform == null)
            return;

        if (!currentDraggedTransform
            .gameObject
            .activeInHierarchy)
        {
            CancelCurrentDrag();
            return;
        }

        Vector3 targetPosition =
            currentDragTargetPosition +
            dragOffset;

        if (smoothDragging)
        {
            currentDraggedTransform.position =
                Vector3.Lerp(
                    currentDraggedTransform.position,
                    targetPosition,
                    dragSmoothSpeed *
                    Time.deltaTime
                );
        }
        else
        {
            currentDraggedTransform.position =
                targetPosition;
        }
    }

    // ================================================================
    // UPDATE POINTER
    // ================================================================

    private void UpdatePointerPosition(
        Vector3 screenPosition)
    {
        if (!isDragging)
            return;

        currentDragTargetPosition =
            GetWorldPositionFromScreen(
                screenPosition,
                currentDragDepth
            );
    }

    // ================================================================
    // SCREEN TO WORLD
    // ================================================================

    private Vector3 GetWorldPositionFromScreen(
        Vector3 screenPosition,
        float depth)
    {
        Vector3 screenPoint =
            new Vector3(
                screenPosition.x,
                screenPosition.y,
                depth
            );

        return mainCamera.ScreenToWorldPoint(
            screenPoint
        );
    }

    // ================================================================
    // FINISH DRAG
    // ================================================================

    private void FinishDrag()
    {
        if (!isDragging ||
            currentElement == null)
        {
            ResetDragState();
            return;
        }

        DragDropElement element =
            currentElement;

        bool validDrop =
            IsInsideSnapCollider(
                element.draggableObject,
                element.snapCollider
            );

        if (validDrop)
        {
            SuccessfulDrop(
                element
            );
        }
        else
        {
            InvalidDrop(
                element
            );
        }

        ResetDragState();
    }

    // ================================================================
    // SNAP DETECTION
    // ================================================================

    private bool IsInsideSnapCollider(
        GameObject draggable,
        Collider snapCollider)
    {
        if (draggable == null)
            return false;

        if (snapCollider == null)
            return false;

        Collider draggableCollider =
            draggable.GetComponent<Collider>();

        // ------------------------------------------------------------
        // No collider on draggable
        // ------------------------------------------------------------

        if (draggableCollider == null)
        {
            return snapCollider.bounds.Contains(
                draggable.transform.position
            );
        }

        // ------------------------------------------------------------
        // Check object center
        // ------------------------------------------------------------

        Vector3 objectCenter =
            draggableCollider.bounds.center;

        if (snapCollider.bounds.Contains(
                objectCenter))
        {
            return true;
        }

        // ------------------------------------------------------------
        // Closest point test
        // ------------------------------------------------------------

        Vector3 closestPoint =
            snapCollider.ClosestPoint(
                objectCenter
            );

        float distance =
            Vector3.Distance(
                objectCenter,
                closestPoint
            );

        return distance <= 0.05f;
    }

    // ================================================================
    // SUCCESSFUL DROP
    // ================================================================

    private void SuccessfulDrop(
        DragDropElement element)
    {
        Log(
            $"SUCCESS | " +
            $"Object = {element.draggableObject.name} | " +
            $"Index = {element.index}"
        );

        // ------------------------------------------------------------
        // SNAP TO THIS INDEX'S TRANSFORM
        // ------------------------------------------------------------

        if (element.snapTransform != null)
        {
            element.draggableObject.transform.position =
                element.snapTransform.position;

            element.draggableObject.transform.rotation =
                element.snapTransform.rotation;
        }
        else
        {
            Debug.LogWarning(
                $"[{name}] Element Index " +
                $"{element.index} has no Snap Transform.",
                this
            );

            ReturnToDragStartPosition(
                element
            );
        }

        // ------------------------------------------------------------
        // COMPLETE ONLY THIS INDEX ENTRY
        // ------------------------------------------------------------

        element.isCompleted = true;

        element.isDragging = false;

        // ------------------------------------------------------------
        // HIDE HIGHLIGHT
        // ------------------------------------------------------------

        if (element.hideHighlightAfterSnap)
        {
            if (element.highlightObject != null)
            {
                element.highlightObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------
        // UNLOCK PAGE
        // ------------------------------------------------------------

        if (unlockPageAfterSuccess)
        {
            RequestPageNavigationUnlock(
                element
            );
        }
    }

    // ================================================================
    // INVALID DROP
    // ================================================================

    private void InvalidDrop(
        DragDropElement element)
    {
        Log(
            $"INVALID DROP | " +
            $"Object = {element.draggableObject.name} | " +
            $"Index = {element.index}"
        );

        // ------------------------------------------------------------
        // IMPORTANT:
        // Return to where THIS drag started.
        //
        // Not the original scene position.
        // ------------------------------------------------------------

        ReturnToDragStartPosition(
            element
        );

        element.isDragging = false;

        if (element.hideHighlightAfterInvalidDrop)
        {
            if (element.highlightObject != null)
            {
                element.highlightObject.SetActive(false);
            }
        }
    }

    // ================================================================
    // RETURN TO DRAG START
    // ================================================================

    private void ReturnToDragStartPosition(
        DragDropElement element)
    {
        if (element == null)
            return;

        if (element.draggableObject == null)
            return;

        element.draggableObject.transform.position =
            dragStartPosition;

        element.draggableObject.transform.rotation =
            dragStartRotation;

        element.draggableObject.transform.localScale =
            dragStartScale;
    }

    // ================================================================
    // PAGE NAVIGATION UNLOCK
    // ================================================================

    private void RequestPageNavigationUnlock(
        DragDropElement element)
    {
        if (PageNavigationController.Instance == null)
        {
            Log(
                "PageNavigationController not found."
            );

            return;
        }

        int currentPage =
            PageNavigationController.CurrentIndex;

        // ------------------------------------------------------------
        // Only unlock the page belonging to this element.
        // ------------------------------------------------------------

        if (element.index ==
            currentPage)
        {
            PageNavigationController
                .RequestNavigationUnlock();

            Log(
                $"Navigation unlocked | " +
                $"Page = {currentPage}"
            );
        }
    }

    // ================================================================
    // CANCEL CURRENT DRAG
    // ================================================================

    public void CancelCurrentDrag()
    {
        if (currentElement != null)
        {
            ReturnToDragStartPosition(
                currentElement
            );

            currentElement.isDragging = false;

            if (currentElement.highlightObject != null)
            {
                currentElement.highlightObject
                    .SetActive(false);
            }
        }

        ResetDragState();
    }

    // ================================================================
    // RESET DRAG STATE
    // ================================================================

    private void ResetDragState()
    {
        if (currentElement != null)
        {
            currentElement.isDragging = false;
        }

        currentElement = null;

        currentDraggedTransform = null;

        currentDragTargetPosition =
            Vector3.zero;

        dragOffset =
            Vector3.zero;

        isDragging = false;

        activePointerId = -1;
    }

    // ================================================================
    // HIGHLIGHT
    // ================================================================

    public void HideAllHighlights()
    {
        for (int i = 0;
             i < elements.Count;
             i++)
        {
            if (elements[i] == null)
                continue;

            if (elements[i].highlightObject != null)
            {
                elements[i]
                    .highlightObject
                    .SetActive(false);
            }
        }
    }

    public void ShowHighlight(
        int elementListIndex)
    {
        if (elementListIndex < 0 ||
            elementListIndex >= elements.Count)
        {
            return;
        }

        DragDropElement element =
            elements[elementListIndex];

        if (element != null &&
            element.highlightObject != null)
        {
            element.highlightObject
                .SetActive(true);
        }
    }

    public void HideHighlight(
        int elementListIndex)
    {
        if (elementListIndex < 0 ||
            elementListIndex >= elements.Count)
        {
            return;
        }

        DragDropElement element =
            elements[elementListIndex];

        if (element != null &&
            element.highlightObject != null)
        {
            element.highlightObject
                .SetActive(false);
        }
    }

    // ================================================================
    // RESET ONE ELEMENT ENTRY
    // ================================================================

    public void ResetElement(
        int elementListIndex)
    {
        if (elementListIndex < 0 ||
            elementListIndex >= elements.Count)
        {
            return;
        }

        DragDropElement element =
            elements[elementListIndex];

        if (element == null)
            return;

        element.isCompleted = false;

        if (element.draggableObject != null)
        {
            element.draggableObject.transform.position =
                element.originalPosition;

            element.draggableObject.transform.rotation =
                element.originalRotation;

            element.draggableObject.transform.localScale =
                element.originalScale;
        }

        if (element.highlightObject != null)
        {
            element.highlightObject.SetActive(false);
        }

        Log(
            $"Reset element list index " +
            $"{elementListIndex}"
        );
    }

    // ================================================================
    // RESET ALL
    // ================================================================

    public void ResetAllElements()
    {
        CancelCurrentDrag();

        for (int i = 0;
             i < elements.Count;
             i++)
        {
            DragDropElement element =
                elements[i];

            if (element == null)
                continue;

            element.isCompleted = false;

            if (element.draggableObject != null)
            {
                element.draggableObject.transform.position =
                    element.originalPosition;

                element.draggableObject.transform.rotation =
                    element.originalRotation;

                element.draggableObject.transform.localScale =
                    element.originalScale;
            }

            if (element.highlightObject != null)
            {
                element.highlightObject.SetActive(false);
            }
        }

        Log("All elements reset.");
    }

    // ================================================================
    // RESET CURRENT PAGE
    // ================================================================

    public void ResetElementsForPage(
        int pageIndex)
    {
        for (int i = 0;
             i < elements.Count;
             i++)
        {
            DragDropElement element =
                elements[i];

            if (element == null)
                continue;

            if (element.index != pageIndex)
                continue;

            element.isCompleted = false;

            if (element.highlightObject != null)
            {
                element.highlightObject
                    .SetActive(false);
            }
        }
    }

    // ================================================================
    // CHECK COMPLETION
    // ================================================================

    public bool IsElementCompleted(
        int elementListIndex)
    {
        if (elementListIndex < 0 ||
            elementListIndex >= elements.Count)
        {
            return false;
        }

        if (elements[elementListIndex] == null)
            return false;

        return elements[elementListIndex]
            .isCompleted;
    }

    // ================================================================
    // CHECK PAGE COMPLETION
    // ================================================================

    public bool HasCompletedElementForPage(
        int pageIndex)
    {
        for (int i = 0;
             i < elements.Count;
             i++)
        {
            DragDropElement element =
                elements[i];

            if (element == null)
                continue;

            if (element.index ==
                    pageIndex &&
                element.isCompleted)
            {
                return true;
            }
        }

        return false;
    }

    // ================================================================
    // GET ELEMENT COUNT
    // ================================================================

    public int GetElementCount()
    {
        return elements.Count;
    }

    // ================================================================
    // DEBUG
    // ================================================================

    private void Log(string message)
    {
        if (!debugLogs)
            return;

        Debug.Log(
            $"[Indexed3DDragDropManager] {message}",
            this
        );
    }
}