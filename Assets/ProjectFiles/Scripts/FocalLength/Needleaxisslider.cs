using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Single-axis needle slider (see Img2 - object needle height alignment).
/// Dragging the UI slider moves a real 3D needle along one axis
/// (e.g. Y). Releasing near the target value (within tolerance) completes
/// the step and locks the slider.
/// </summary>
public class NeedleAxisSlider : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Slider")]
    [SerializeField] private Slider axisSlider;

    [Header("Needle")]
    [SerializeField] private Transform needle;
    [SerializeField] private Axis moveAxis = Axis.Y;

    [Header("Offset Movement (Relative to Initial Position)")]
    [Tooltip("If true, moves relative to the needle's starting position using minOffset and maxOffset. If false, uses absolute coordinates.")]
    [SerializeField] private bool useRelativeOffset = true;

    [Tooltip("Whether offsets are applied in local space or world space.")]
    [SerializeField] private bool useLocalSpace = false;

    [Tooltip("Offset applied to needle along moveAxis when slider = minValue.")]
    [SerializeField] private float minOffset = 0f;

    [Tooltip("Offset applied to needle along moveAxis when slider = maxValue.")]
    [SerializeField] private float maxOffset = -0.21f;

    [Header("Absolute Movement (Legacy / Optional)")]
    [Tooltip("World/Local position value when slider = minValue (used when useRelativeOffset = false).")]
    [SerializeField] private float axisMin = 0f;

    [Tooltip("World/Local position value when slider = maxValue (used when useRelativeOffset = false).")]
    [SerializeField] private float axisMax = 1f;

    [Header("Target")]
    [SerializeField] private float targetSliderValue = 2f;
    [SerializeField] private float tolerance = 0.05f;

    [Header("Completion")]
    [SerializeField] private bool lockOnceCompleted = true;
    public UnityEvent onCorrectPosition;

    private bool completed;
    private Vector3 initialPosition;
    private Vector3 initialLocalPosition;
    private bool initialPositionCaptured;
    private EventTrigger sliderEventTrigger;
    private EventTrigger.Entry pointerUpEntry;

    private void Awake()
    {
        if (needle == null)
            needle = transform;

        CaptureInitialPosition();
    }

    private void CaptureInitialPosition()
    {
        if (needle != null && !initialPositionCaptured)
        {
            initialPosition = needle.position;
            initialLocalPosition = needle.localPosition;
            initialPositionCaptured = true;
        }
    }

    private void OnEnable()
    {
        CaptureInitialPosition();

        if (axisSlider != null)
        {
            axisSlider.onValueChanged.AddListener(HandleSliderChanged);
            AddPointerListener();
        }

        ResetState();
    }

    private void OnDisable()
    {
        if (axisSlider != null)
        {
            axisSlider.onValueChanged.RemoveListener(HandleSliderChanged);
            RemovePointerListener();
        }
    }

    private void AddPointerListener()
    {
        if (axisSlider == null)
            return;

        sliderEventTrigger = axisSlider.GetComponent<EventTrigger>();
        if (sliderEventTrigger == null)
        {
            sliderEventTrigger = axisSlider.gameObject.AddComponent<EventTrigger>();
        }

        if (pointerUpEntry == null)
        {
            pointerUpEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerUp
            };
            pointerUpEntry.callback.AddListener((data) => OnReleased());
        }

        if (!sliderEventTrigger.triggers.Contains(pointerUpEntry))
        {
            sliderEventTrigger.triggers.Add(pointerUpEntry);
        }
    }

    private void RemovePointerListener()
    {
        if (sliderEventTrigger != null && pointerUpEntry != null)
        {
            sliderEventTrigger.triggers.Remove(pointerUpEntry);
        }
    }

    /// <summary>
    /// Call when the page becomes active so the needle/slider start clean.
    /// </summary>
    public void ResetState()
    {
        completed = false;

        if (axisSlider != null)
        {
            axisSlider.interactable = true;
            ApplyNeedlePosition(axisSlider.value);
        }
    }

    private void HandleSliderChanged(float value)
    {
        if (completed)
            return;

        ApplyNeedlePosition(value);
    }

    /// <summary>
    /// Called when the user releases the slider drag.
    /// </summary>
    public void OnReleased()
    {
        Debug.Log(" NeedleAxisSlider OnReleased ");
        if (completed || axisSlider == null)
            return;

        if (Mathf.Abs(axisSlider.value - targetSliderValue) <= tolerance)
        {
            Complete();
        }
    }

    private void ApplyNeedlePosition(float sliderValue)
    {
        if (needle == null || axisSlider == null)
            return;

        CaptureInitialPosition();

        float range = Mathf.Max(0.0001f, axisSlider.maxValue - axisSlider.minValue);
        float t = Mathf.Clamp01((sliderValue - axisSlider.minValue) / range);

        if (useRelativeOffset)
        {
            float currentOffset = Mathf.Lerp(minOffset, maxOffset, t);
            Vector3 pos = useLocalSpace ? initialLocalPosition : initialPosition;

            switch (moveAxis)
            {
                case Axis.X: pos.x += currentOffset; break;
                case Axis.Y: pos.y += currentOffset; break;
                case Axis.Z: pos.z += currentOffset; break;
            }

            if (useLocalSpace)
                needle.localPosition = pos;
            else
                needle.position = pos;
        }
        else
        {
            float targetVal = Mathf.Lerp(axisMin, axisMax, t);
            Vector3 pos = useLocalSpace ? needle.localPosition : needle.position;

            switch (moveAxis)
            {
                case Axis.X: pos.x = targetVal; break;
                case Axis.Y: pos.y = targetVal; break;
                case Axis.Z: pos.z = targetVal; break;
            }

            if (useLocalSpace)
                needle.localPosition = pos;
            else
                needle.position = pos;
        }
    }

    private void Complete()
    {
        completed = true;

        // Snap exactly to target so the needle lands precisely.
        ApplyNeedlePosition(targetSliderValue);

        if (lockOnceCompleted && axisSlider != null)
            axisSlider.interactable = false;

        onCorrectPosition?.Invoke();
        PageNavigationController.RequestNavigationUnlock();
    }
}