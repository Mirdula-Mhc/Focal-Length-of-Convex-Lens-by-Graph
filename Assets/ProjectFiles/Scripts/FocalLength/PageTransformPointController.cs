using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Defines which GameObject should be placed at which Transform point (e.g. empty GameObject)
/// on a given page index.
/// 
/// KEY BEHAVIOR:
/// - Only applies the transform point the FIRST TIME the user enters that page.
/// - If the user interacts with the object (e.g. drag-and-drop, sliders, animations),
///   the new position/rotation is preserved.
/// - When returning to previously visited pages, the object remains wherever it was left.
/// </summary>
public class PageTransformPointController : MonoBehaviour
{
    [System.Serializable]
    public class PageTransformPoint
    {
        [Tooltip("Page index (0-based: Page 1 = index 0, Page 2 = index 1, etc.)")]
        public int pageIndex;

        [Tooltip("Target object to position. If left empty, uses the Default Target on this controller (or this GameObject).")]
        public Transform targetObject;

        [Tooltip("Empty GameObject / Transform point in the scene representing the initial position and rotation.")]
        public Transform transformPoint;

        [Header("Options")]
        [Tooltip("If true, updates position from the transform point.")]
        public bool applyPosition = true;

        [Tooltip("If true, updates rotation from the transform point.")]
        public bool applyRotation = true;

        [Tooltip("If true, updates scale from the transform point.")]
        public bool applyScale = false;

        [Tooltip("If true, uses local transform instead of world transform.")]
        public bool useLocalSpace = false;

        // Runtime state: whether this point has already been applied on a first-time visit
        [NonSerialized] public bool hasBeenApplied = false;
    }

    [Header("Default Target Object")]
    [Tooltip("Default object to move if not specified per item. Defaults to this GameObject if null.")]
    [SerializeField] private Transform defaultTarget;

    [Header("Page Transform Points")]
    [SerializeField] private List<PageTransformPoint> pagePoints = new List<PageTransformPoint>();

    private void Awake()
    {
        if (defaultTarget == null)
            defaultTarget = transform;
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;

        // Sync immediately for the current active page
        ApplyForPage(PageNavigationController.CurrentIndex);
    }

    private void Start()
    {
        // Re-check on Start in case PageNavigationController initialized in Start
        ApplyForPage(PageNavigationController.CurrentIndex);
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
    }

    private void HandlePageChanged(int pageIndex)
    {
        ApplyForPage(pageIndex);
    }

    /// <summary>
    /// Applies transform points configured for this page if they have not been applied yet.
    /// </summary>
    public void ApplyForPage(int pageIndex)
    {
        if (pagePoints == null || pagePoints.Count == 0)
            return;

        foreach (var item in pagePoints)
        {
            if (item == null || item.pageIndex != pageIndex)
                continue;

            // 🚀 First time visit only: skip if already applied
            if (item.hasBeenApplied)
                continue;

            Transform target = item.targetObject != null ? item.targetObject : defaultTarget;
            if (target == null || item.transformPoint == null)
                continue;

            ApplyTransform(target, item.transformPoint, item.applyPosition, item.applyRotation, item.applyScale, item.useLocalSpace);
            item.hasBeenApplied = true;
        }
    }

    private static void ApplyTransform(Transform target, Transform point, bool pos, bool rot, bool scale, bool local)
    {
        if (local)
        {
            if (pos) target.localPosition = point.localPosition;
            if (rot) target.localRotation = point.localRotation;
            if (scale) target.localScale = point.localScale;
        }
        else
        {
            if (pos) target.position = point.position;
            if (rot) target.rotation = point.rotation;
            if (scale) target.localScale = point.localScale;
        }
    }

    /// <summary>
    /// Force applies transform point for a page regardless of previous visit history.
    /// </summary>
    public void ForceApplyForPage(int pageIndex)
    {
        if (pagePoints == null || pagePoints.Count == 0)
            return;

        foreach (var item in pagePoints)
        {
            if (item == null || item.pageIndex != pageIndex)
                continue;

            Transform target = item.targetObject != null ? item.targetObject : defaultTarget;
            if (target == null || item.transformPoint == null)
                continue;

            ApplyTransform(target, item.transformPoint, item.applyPosition, item.applyRotation, item.applyScale, item.useLocalSpace);
            item.hasBeenApplied = true;
        }
    }

    /// <summary>
    /// Resets all first-time visit flags so points can be re-applied upon page entry (e.g. when restarting).
    /// </summary>
    public void ResetAllAppliedFlags()
    {
        if (pagePoints == null) return;
        foreach (var item in pagePoints)
        {
            if (item != null)
                item.hasBeenApplied = false;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PageTransformPointController))]
[CanEditMultipleObjects]
public class PageTransformPointControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PageTransformPointController controller = (PageTransformPointController)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Helper Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Create Empty Transform Point at Selected Object Position"))
        {
            CreateEmptyPointHelper(controller);
        }

        if (Application.isPlaying)
        {
            if (GUILayout.Button("Reset First-Time Visit Flags"))
            {
                controller.ResetAllAppliedFlags();
            }
        }
    }

    private void CreateEmptyPointHelper(PageTransformPointController controller)
    {
        Transform source = controller.transform;
        GameObject emptyPoint = new GameObject($"Point_P{PageNavigationController.CurrentIndex + 1}_{source.name}");
        Undo.RegisterCreatedObjectUndo(emptyPoint, "Create Empty Transform Point");

        if (source.parent != null)
            emptyPoint.transform.SetParent(source.parent, false);

        emptyPoint.transform.position = source.position;
        emptyPoint.transform.rotation = source.rotation;
        emptyPoint.transform.localScale = source.localScale;

        Selection.activeGameObject = emptyPoint;
        Debug.Log($"[PageTransformPointController] Created empty transform point: {emptyPoint.name}", emptyPoint);
    }
}
#endif
