using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Single-axis needle slider (e.g. object needle height alignment).
/// Dragging the UI slider smoothly moves a real 3D needle along an axis
/// (e.g. positive Y from 1.12283 to 1.15). Releasing near target value 1.14
/// completes the step, triggers onCorrectPosition, and locks the slider.
/// </summary>
public class NeedleAxisSlider : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Slider")]
    [SerializeField] private Slider axisSlider;
    [Tooltip("Ensures slider wholeNumbers is disabled for smooth continuous motion.")]
    [SerializeField] private bool forceSmoothSlider = true;

    [Header("Needle")]
    [SerializeField] private Transform needle;
    [SerializeField] private Axis moveAxis = Axis.Y;
    [SerializeField] private bool useLocalSpace = false;

    [Header("Position Target & Range")]
    [Tooltip("If true, startValue is automatically captured from the needle's starting coordinate in the scene.")]
    [SerializeField] private bool autoDetectStartValue = true;

    [Tooltip("Coordinate value along moveAxis when slider = minValue. Stays here on play (e.g. 1.12283).")]
    [SerializeField] private float startValue = 1.12283f;

    [Tooltip("Target coordinate value where the needle aligns (e.g. 1.14).")]
    [SerializeField] private float targetValue = 1.14f;

    [Tooltip("Maximum coordinate value along moveAxis when slider = maxValue (e.g. 1.15).")]
    [SerializeField] private float maxValue = 1.15f;

    [Tooltip("Tolerance around targetValue to consider alignment correct.")]
    [SerializeField] private float tolerance = 0.003f;

    [Header("Trigger Behavior")]
    [Tooltip("If true, alignment is verified when the user releases the slider drag.")]
    [SerializeField] private bool triggerOnReleaseOnly = true;

    [Header("Completion")]
    [SerializeField] private bool lockOnceCompleted = true;
    public UnityEvent onCorrectPosition;

    private bool completed;
    private Vector3 initialPosition;
    private Vector3 initialLocalPosition;
    private bool initialPositionCaptured;
    private UnityEngine.EventSystems.EventTrigger sliderEventTrigger;
    private UnityEngine.EventSystems.EventTrigger.Entry pointerUpEntry;

    private void Awake()
    {
        if (needle == null)
            needle = transform;

        CaptureInitialPosition();
        ConfigureSliderSmoothness();
    }

    private void CaptureInitialPosition()
    {
        if (needle != null && !initialPositionCaptured)
        {
            initialPosition = needle.position;
            initialLocalPosition = needle.localPosition;
            initialPositionCaptured = true;

            if (autoDetectStartValue)
            {
                Vector3 basePos = useLocalSpace ? initialLocalPosition : initialPosition;
                switch (moveAxis)
                {
                    case Axis.X: startValue = basePos.x; break;
                    case Axis.Y: startValue = basePos.y; break;
                    case Axis.Z: startValue = basePos.z; break;
                }
            }
        }
    }

    private void ConfigureSliderSmoothness()
    {
        if (axisSlider != null && forceSmoothSlider)
        {
            axisSlider.wholeNumbers = false;
        }
    }

    private void OnEnable()
    {
        CaptureInitialPosition();
        ConfigureSliderSmoothness();

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

        sliderEventTrigger = axisSlider.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (sliderEventTrigger == null)
        {
            sliderEventTrigger = axisSlider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        }

        if (pointerUpEntry == null)
        {
            pointerUpEntry = new UnityEngine.EventSystems.EventTrigger.Entry
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

        float currentVal = GetCurrentAxisValue();
        CheckAlignment(currentVal);
    }

    private float GetCurrentAxisValue()
    {
        if (axisSlider == null)
            return startValue;

        float range = Mathf.Max(0.0001f, axisSlider.maxValue - axisSlider.minValue);
        float t = Mathf.Clamp01((axisSlider.value - axisSlider.minValue) / range);
        return Mathf.Lerp(startValue, maxValue, t);
    }

    private void CheckAlignment(float currentAxisVal)
    {
        if (Mathf.Abs(currentAxisVal - targetValue) <= tolerance)
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
        float currentAxisVal = Mathf.Lerp(startValue, maxValue, t);

        Vector3 pos = useLocalSpace ? initialLocalPosition : initialPosition;

        switch (moveAxis)
        {
            case Axis.X: pos.x = currentAxisVal; break;
            case Axis.Y: pos.y = currentAxisVal; break;
            case Axis.Z: pos.z = currentAxisVal; break;
        }

        if (useLocalSpace)
            needle.localPosition = pos;
        else
            needle.position = pos;

        if (!triggerOnReleaseOnly && !completed)
        {
            CheckAlignment(currentAxisVal);
        }
    }

    private void Complete()
    {
        if (completed)
            return;

        completed = true;

        // Snap exactly to target value (1.14)
        SetNeedleToTargetValue();

        if (lockOnceCompleted && axisSlider != null)
            axisSlider.interactable = false;

        onCorrectPosition?.Invoke();
        PageNavigationController.RequestNavigationUnlock();
    }

    private void SetNeedleToTargetValue()
    {
        if (needle == null)
            return;

        Vector3 pos = useLocalSpace ? initialLocalPosition : initialPosition;
        switch (moveAxis)
        {
            case Axis.X: pos.x = targetValue; break;
            case Axis.Y: pos.y = targetValue; break;
            case Axis.Z: pos.z = targetValue; break;
        }

        if (useLocalSpace)
            needle.localPosition = pos;
        else
            needle.position = pos;

        // Sync slider visual handle to the exact target position
        if (axisSlider != null)
        {
            float valRange = Mathf.Max(0.0001f, maxValue - startValue);
            float targetT = Mathf.Clamp01((targetValue - startValue) / valRange);
            float sliderRange = axisSlider.maxValue - axisSlider.minValue;
            axisSlider.SetValueWithoutNotify(axisSlider.minValue + targetT * sliderRange);
        }
    }
}