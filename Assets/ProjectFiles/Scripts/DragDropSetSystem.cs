using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A single Page inside a Set. References a Transform (position + rotation) that the
/// Draggable Object and Snap Object should be moved to when this page is applied.
/// </summary>
[Serializable]
public class DragDropPage
{
    [Tooltip("Index of this page within its set (informational only)")]
    public int pageIndex;

    [Header("Draggable Object Target")]
    [Tooltip("The Draggable Object will be moved to this Transform's position/rotation")]
    public Transform draggableTransform;

    [Header("Snap Object Target")]
    [Tooltip("The Snap Object will be moved to this Transform's position/rotation")]
    public Transform snapObjectTransform;
}

/// <summary>
/// A single drag-and-drop Set. Contains the draggable object, its correct snap point collider,
/// the snap object that gets highlighted, and an optional list of Pages.
/// </summary>
[Serializable]
public class DragDropSet
{
    [Tooltip("Index of this set (auto-assigned, matches its position in the Sets list)")]
    public int setIndex;

    [Header("Core References")]
    [Tooltip("The GameObject the user drags")]
    public GameObject draggableObject;

    [Tooltip("The collider that defines the correct drop zone for this set")]
    public Collider snapPointCollider;

    [Tooltip("The GameObject that visually represents the target/snap location")]
    public GameObject snapObject;

    [Tooltip("Material applied to the Snap Object's renderers while this set is active")]
    public Material highlightMaterial;

    [Header("Pages")]
    [Tooltip("Optional list of saved positions/rotations for this set")]
    public List<DragDropPage> pages = new List<DragDropPage>();

    // ---------------- Runtime-only state (not shown/edited in Inspector) ----------------
    [NonSerialized] public bool isCompleted;
    [NonSerialized] public bool isUnlocked;
    [NonSerialized] public Vector3 draggableOriginalPosition;
    [NonSerialized] public Quaternion draggableOriginalRotation;
    [NonSerialized] public Renderer[] snapObjectRenderers;
    [NonSerialized] public Material[][] originalMaterialsPerRenderer;
}

/// <summary>
/// Complete, self-contained Drag and Drop Set System.
/// Handles sequential sets, mouse + touch dragging, raycast-based pickup,
/// collider-based snap detection, highlight materials and a page system.
/// No other manager script is required.
/// </summary>
public class DragDropSetSystem : MonoBehaviour
{
    // ------------------------------------------------------------------------------------
    // Concrete UnityEvent subclasses (required so Unity can serialize/show them in Inspector)
    // ------------------------------------------------------------------------------------
    [Serializable] public class IntEvent : UnityEvent<int> { }
    [Serializable] public class IntIntEvent : UnityEvent<int, int> { }

    [Header("TOTAL SETS")]
    [Tooltip("How many drag-and-drop sets this system should manage. Sets list below auto-resizes to match.")]
    [SerializeField] private int totalSets = 1;

    [Tooltip("Configure each set here. Size is controlled by Total Sets above.")]
    [SerializeField] private List<DragDropSet> sets = new List<DragDropSet>();

    [Header("Drag Settings")]
    [Tooltip("Camera used for raycasting/drag calculations. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera interactionCamera;

    [Tooltip("How smoothly the draggable object follows the pointer. Higher = snappier / less lag.")]
    [SerializeField] private float dragSmoothSpeed = 15f;

    [Tooltip("Max raycast distance when detecting the draggable object under the pointer/finger")]
    [SerializeField] private float raycastMaxDistance = 1000f;

    [Tooltip("Layer mask used when raycasting to detect the draggable object")]
    [SerializeField] private LayerMask draggableLayerMask = ~0;

    [Header("Snap Settings")]
    [Tooltip("If true, snaps the draggable object exactly to the Snap Point Collider's bounds center on a correct drop")]
    [SerializeField] private bool snapToColliderCenter = true;

    [Header("Events")]
    public IntEvent OnSetStarted = new IntEvent();
    public IntEvent OnSetCompleted = new IntEvent();
    public UnityEvent OnAllSetsCompleted = new UnityEvent();
    public IntIntEvent OnPageChanged = new IntIntEvent();

    // ------------------------------------------------------------------------------------
    // Runtime state
    // ------------------------------------------------------------------------------------
    private int currentSetIndex = 0;
    private int currentPageIndex = -1;

    private bool isDragging = false;
    private GameObject currentDraggedObject;
    private Collider currentDraggableCollider;
    private Vector3 dragOffset;
    private Plane dragPlane;
    private Vector3 pointerScreenPosition;
    private Vector3 dragStartPosition;
    private Quaternion dragStartRotation;

    // ------------------------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------------------------

    private void OnValidate()
    {
        totalSets = Mathf.Max(0, totalSets);

        while (sets.Count < totalSets) sets.Add(new DragDropSet());
        while (sets.Count > totalSets) sets.RemoveAt(sets.Count - 1);

        for (int i = 0; i < sets.Count; i++)
        {
            sets[i].setIndex = i;
        }
    }

    private void Awake()
    {
        if (interactionCamera == null) interactionCamera = Camera.main;
        if (interactionCamera == null)
        {
            Debug.LogWarning("[DragDropSetSystem] No Interaction Camera assigned and no Camera.main found in scene.");
        }

        for (int i = 0; i < sets.Count; i++)
        {
            DragDropSet set = sets[i];
            set.setIndex = i;
            set.isCompleted = false;
            set.isUnlocked = (i == 0);

            if (set.draggableObject != null)
            {
                set.draggableOriginalPosition = set.draggableObject.transform.position;
                set.draggableOriginalRotation = set.draggableObject.transform.rotation;
                set.draggableObject.SetActive(false);
            }

            CacheSnapObjectMaterials(set);
        }
    }

    private void Start()
    {
        StartCurrentSet();
    }

    private void Update()
    {
        HandleInput();

        if (isDragging)
        {
            UpdateDragPosition();
        }
    }

    // ------------------------------------------------------------------------------------
    // Input handling (Mouse + Touch, unified)
    // ------------------------------------------------------------------------------------

    private void HandleInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            ProcessPointer(touch.phase, touch.position);
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                ProcessPointer(TouchPhase.Began, Input.mousePosition);
            }
            else if (Input.GetMouseButton(0))
            {
                ProcessPointer(TouchPhase.Moved, Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                ProcessPointer(TouchPhase.Ended, Input.mousePosition);
            }
        }
    }

    private void ProcessPointer(TouchPhase phase, Vector3 screenPosition)
    {
        switch (phase)
        {
            case TouchPhase.Began:
                TryBeginDrag(screenPosition);
                break;

            case TouchPhase.Moved:
            case TouchPhase.Stationary:
                if (isDragging) pointerScreenPosition = screenPosition;
                break;

            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                EndDrag();
                break;
        }
    }

    // ------------------------------------------------------------------------------------
    // Drag lifecycle
    // ------------------------------------------------------------------------------------

    private void TryBeginDrag(Vector3 screenPosition)
    {
        if (interactionCamera == null) return;
        if (!IsValidSetIndex(currentSetIndex)) return;

        DragDropSet set = sets[currentSetIndex];
        if (set.isCompleted) return;
        if (set.draggableObject == null || !set.draggableObject.activeSelf) return;

        Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, raycastMaxDistance, draggableLayerMask))
        {
            bool hitIsDraggable = hit.transform == set.draggableObject.transform ||
                                   hit.transform.IsChildOf(set.draggableObject.transform);

            if (hitIsDraggable)
            {
                isDragging = true;
                currentDraggedObject = set.draggableObject;
                currentDraggableCollider = set.draggableObject.GetComponentInChildren<Collider>();

                dragStartPosition = currentDraggedObject.transform.position;
                dragStartRotation = currentDraggedObject.transform.rotation;

                // Build a drag plane facing the camera, passing through the draggable object's current position
                dragPlane = new Plane(interactionCamera.transform.forward, currentDraggedObject.transform.position);

                Vector3 worldPoint;
                dragOffset = GetPlanePoint(screenPosition, out worldPoint)
                    ? currentDraggedObject.transform.position - worldPoint
                    : Vector3.zero;

                pointerScreenPosition = screenPosition;
            }
        }
    }

    private void UpdateDragPosition()
    {
        if (currentDraggedObject == null)
        {
            isDragging = false;
            return;
        }

        Vector3 worldPoint;
        if (GetPlanePoint(pointerScreenPosition, out worldPoint))
        {
            Vector3 desiredPosition = worldPoint + dragOffset;
            currentDraggedObject.transform.position = Vector3.Lerp(
                currentDraggedObject.transform.position,
                desiredPosition,
                Time.deltaTime * dragSmoothSpeed);
        }

        CheckSnap();
    }

    private void EndDrag()
    {
        if (!isDragging) return;

        if (currentDraggedObject != null)
        {
            currentDraggedObject.transform.position = dragStartPosition;
            currentDraggedObject.transform.rotation = dragStartRotation;
        }

        isDragging = false;
        currentDraggedObject = null;
        currentDraggableCollider = null;
    }

    private bool GetPlanePoint(Vector3 screenPosition, out Vector3 point)
    {
        if (interactionCamera == null)
        {
            point = Vector3.zero;
            return false;
        }

        Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
        float distance;

        if (dragPlane.Raycast(ray, out distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    // ------------------------------------------------------------------------------------
    // Snap detection
    // ------------------------------------------------------------------------------------

    private void CheckSnap()
    {
        if (!IsValidSetIndex(currentSetIndex)) return;

        DragDropSet set = sets[currentSetIndex];
        if (set.isCompleted) return;
        if (currentDraggableCollider == null || set.snapPointCollider == null) return;

        if (currentDraggableCollider.bounds.Intersects(set.snapPointCollider.bounds))
        {
            HandleCorrectSnap(set);
        }
    }

    private void HandleCorrectSnap(DragDropSet set)
    {
        isDragging = false;
        currentDraggedObject = null;
        currentDraggableCollider = null;

        if (snapToColliderCenter && set.snapPointCollider != null && set.draggableObject != null)
        {
            set.draggableObject.transform.position = set.snapPointCollider.bounds.center;
        }

        if (set.draggableObject != null)
        {
            set.draggableObject.SetActive(false);
        }

        RestoreSnapObjectMaterials(set);
        CompleteCurrentSet();
    }

    // ------------------------------------------------------------------------------------
    // Highlight material handling (supports multiple renderers / material slots)
    // ------------------------------------------------------------------------------------

    private void CacheSnapObjectMaterials(DragDropSet set)
    {
        if (set.snapObject == null)
        {
            set.snapObjectRenderers = new Renderer[0];
            set.originalMaterialsPerRenderer = new Material[0][];
            return;
        }

        set.snapObjectRenderers = set.snapObject.GetComponentsInChildren<Renderer>(true);
        set.originalMaterialsPerRenderer = new Material[set.snapObjectRenderers.Length][];

        for (int i = 0; i < set.snapObjectRenderers.Length; i++)
        {
            set.originalMaterialsPerRenderer[i] = set.snapObjectRenderers[i].sharedMaterials;
        }
    }

    private void ApplyHighlightMaterial(DragDropSet set)
    {
        if (set.snapObjectRenderers == null || set.highlightMaterial == null) return;

        foreach (Renderer renderer in set.snapObjectRenderers)
        {
            if (renderer == null) continue;

            int slotCount = renderer.sharedMaterials.Length;
            Material[] highlighted = new Material[slotCount];
            for (int i = 0; i < slotCount; i++) highlighted[i] = set.highlightMaterial;

            renderer.sharedMaterials = highlighted;
        }
    }

    private void RestoreSnapObjectMaterials(DragDropSet set)
    {
        if (set.snapObjectRenderers == null || set.originalMaterialsPerRenderer == null) return;

        for (int i = 0; i < set.snapObjectRenderers.Length; i++)
        {
            Renderer renderer = set.snapObjectRenderers[i];
            if (renderer == null) continue;
            if (i >= set.originalMaterialsPerRenderer.Length) continue;

            renderer.sharedMaterials = set.originalMaterialsPerRenderer[i];
        }
    }

    // ------------------------------------------------------------------------------------
    // Public API — Set flow
    // ------------------------------------------------------------------------------------

    /// <summary>Activates the current set: enables its draggable object, applies the highlight material, and loads Page 0 if any pages exist.</summary>
    public void StartCurrentSet()
    {
        if (!IsValidSetIndex(currentSetIndex)) return;

        DragDropSet set = sets[currentSetIndex];
        set.isUnlocked = true;

        if (set.draggableObject != null)
        {
            set.draggableObject.SetActive(true);
        }

        ApplyHighlightMaterial(set);

        currentPageIndex = -1;
        if (set.pages != null && set.pages.Count > 0)
        {
            SetPage(0);
        }

        OnSetStarted?.Invoke(currentSetIndex);
    }

    /// <summary>Marks the current set as completed and advances to the next set (or fires OnAllSetsCompleted if this was the last one).</summary>
    public void CompleteCurrentSet()
    {
        if (!IsValidSetIndex(currentSetIndex)) return;

        DragDropSet set = sets[currentSetIndex];
        set.isCompleted = true;

        OnSetCompleted?.Invoke(currentSetIndex);

        if (currentSetIndex >= sets.Count - 1)
        {
            OnAllSetsCompleted?.Invoke();
        }
        else
        {
            GoToNextSet();
        }
    }

    /// <summary>Unlocks and starts the next set in sequence, if any.</summary>
    public void GoToNextSet()
    {
        if (currentSetIndex + 1 >= sets.Count) return;

        currentSetIndex++;
        sets[currentSetIndex].isUnlocked = true;
        StartCurrentSet();
    }

    /// <summary>Jumps directly to a specific set index (disables the previous set's draggable object first).</summary>
    public void SetCurrentSet(int index)
    {
        if (!IsValidSetIndex(index)) return;

        if (IsValidSetIndex(currentSetIndex) && currentSetIndex != index)
        {
            DragDropSet previous = sets[currentSetIndex];
            if (previous.draggableObject != null) previous.draggableObject.SetActive(false);
        }

        isDragging = false;
        currentDraggedObject = null;
        currentDraggableCollider = null;

        currentSetIndex = index;
        StartCurrentSet();
    }

    // ------------------------------------------------------------------------------------
    // Public API — Page system
    // ------------------------------------------------------------------------------------

    /// <summary>Moves the draggable and snap objects of the current set to the saved position/rotation of the given page.</summary>
    public void SetPage(int pageIndex)
    {
        ApplyPage(pageIndex);
    }

    /// <summary>Applies a page's saved transform data to the current set's draggable and snap objects.</summary>
    public void ApplyPage(int pageIndex)
    {
        if (!IsValidSetIndex(currentSetIndex)) return;

        DragDropSet set = sets[currentSetIndex];
        if (set.pages == null || pageIndex < 0 || pageIndex >= set.pages.Count) return;

        DragDropPage page = set.pages[pageIndex];

        if (set.draggableObject != null && page.draggableTransform != null)
        {
            set.draggableObject.transform.position = page.draggableTransform.position;
            set.draggableObject.transform.rotation = page.draggableTransform.rotation;
        }

        if (set.snapObject != null && page.snapObjectTransform != null)
        {
            set.snapObject.transform.position = page.snapObjectTransform.position;
            set.snapObject.transform.rotation = page.snapObjectTransform.rotation;
        }

        currentPageIndex = pageIndex;
        OnPageChanged?.Invoke(currentSetIndex, pageIndex);
    }

    // ------------------------------------------------------------------------------------
    // Public API — Reset
    // ------------------------------------------------------------------------------------

    /// <summary>Resets only the current set back to its original state and restarts it.</summary>
    public void ResetCurrentSet()
    {
        if (!IsValidSetIndex(currentSetIndex)) return;

        ResetSetInternal(currentSetIndex);
        StartCurrentSet();
    }

    /// <summary>Resets every set back to its original state, re-locks all but the first, and restarts Set 0.</summary>
    public void ResetAllSets()
    {
        for (int i = 0; i < sets.Count; i++)
        {
            ResetSetInternal(i);
            sets[i].isUnlocked = (i == 0);
        }

        isDragging = false;
        currentDraggedObject = null;
        currentDraggableCollider = null;

        currentSetIndex = 0;
        StartCurrentSet();
    }

    private void ResetSetInternal(int index)
    {
        if (!IsValidSetIndex(index)) return;

        DragDropSet set = sets[index];
        set.isCompleted = false;

        if (set.draggableObject != null)
        {
            set.draggableObject.transform.position = set.draggableOriginalPosition;
            set.draggableObject.transform.rotation = set.draggableOriginalRotation;
            set.draggableObject.SetActive(false);
        }

        RestoreSnapObjectMaterials(set);
    }

    // ------------------------------------------------------------------------------------
    // Public API — Queries
    // ------------------------------------------------------------------------------------

    /// <summary>Returns the index of the currently active set.</summary>
    public int GetCurrentSetIndex()
    {
        return currentSetIndex;
    }

    /// <summary>Returns true only if every configured set has been completed.</summary>
    public bool AreAllSetsCompleted()
    {
        if (sets.Count == 0) return false;

        for (int i = 0; i < sets.Count; i++)
        {
            if (!sets[i].isCompleted) return false;
        }

        return true;
    }

    // ------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------

    private bool IsValidSetIndex(int index)
    {
        return sets != null && index >= 0 && index < sets.Count;
    }
}