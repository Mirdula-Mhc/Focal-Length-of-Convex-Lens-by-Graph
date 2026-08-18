using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Eye-position parallax slider (Left/Right style, see Img4/5).
/// Dragging the slider moves two UI needle images in opposite directions
/// to simulate parallax separation/alignment. Releasing near the target
/// value (center) marks the step complete and unlocks navigation.
/// </summary>
public class ParallaxEyeSlider : MonoBehaviour
{
    [Header("Slider")]
    [SerializeField] private Slider eyeSlider;

    [Tooltip("Slider value considered 'aligned' (e.g. center of the range).")]
    [SerializeField] private float targetValue = 2f;

    [Tooltip("How close to targetValue counts as aligned.")]
    [SerializeField] private float tolerance = 0.2f;

    [Header("Needle Images (UI)")]
    [SerializeField] private RectTransform objectNeedleImage;
    [SerializeField] private RectTransform imageNeedleImage;

    [Tooltip("How far each needle drifts (in local X) at max slider offset from target.")]
    [SerializeField] private float maxOffset = 40f;

    [Header("Completion")]
    [SerializeField] private bool lockOnceCompleted = true;
    public UnityEvent onAligned;

    private bool completed;
    private Vector2 objectNeedleStartPos;
    private Vector2 imageNeedleStartPos;

    private void Awake()
    {
        if (objectNeedleImage != null)
            objectNeedleStartPos = objectNeedleImage.anchoredPosition;

        if (imageNeedleImage != null)
            imageNeedleStartPos = imageNeedleImage.anchoredPosition;
    }

    private void OnEnable()
    {
        if (eyeSlider != null)
            eyeSlider.onValueChanged.AddListener(HandleSliderChanged);

        ResetState();
    }

    private void OnDisable()
    {
        if (eyeSlider != null)
            eyeSlider.onValueChanged.RemoveListener(HandleSliderChanged);
    }

    /// <summary>
    /// Call this whenever the page becomes active, so the slider
    /// and needles start from a clean, unlocked state.
    /// </summary>
    public void ResetState()
    {

        completed = false;

        if (eyeSlider != null)
            eyeSlider.interactable = true;

        UpdateNeedleVisuals(eyeSlider != null ? eyeSlider.value : targetValue);
    }

    private void HandleSliderChanged(float value)
    {
        Debug.Log(" HandleSliderChanged ");
        if (completed)
            return;

        UpdateNeedleVisuals(value);
    }

    private void UpdateNeedleVisuals(float value)
    {
        Debug.Log(" UpdateNeedleVisuals ");
        float offsetFromTarget = value - targetValue;

        // Normalize offset against slider range so drift scales with the
        // slider's actual min/max, not just raw value units.
        float range = eyeSlider != null
            ? Mathf.Max(0.0001f, eyeSlider.maxValue - eyeSlider.minValue)
            : 1f;

        float normalized = Mathf.Clamp(offsetFromTarget / (range * 0.5f), -1f, 1f);
        float drift = normalized * maxOffset;

        if (objectNeedleImage != null)
            objectNeedleImage.anchoredPosition = objectNeedleStartPos + new Vector2(-drift, 0f);

        if (imageNeedleImage != null)
            imageNeedleImage.anchoredPosition = imageNeedleStartPos + new Vector2(drift, 0f);
    }

    /// <summary>
    /// Hook this to the slider's "On Pointer Up" (via an EventTrigger)
    /// or call manually when the user releases the drag.
    /// </summary>
    public void OnReleased()
    {
        Debug.Log(" On Released ");
        if (completed || eyeSlider == null)
            return;

        if (Mathf.Abs(eyeSlider.value - targetValue) <= tolerance)
        {
            completed = true;
            

            // Snap visuals to perfectly aligned for a clean look.
            UpdateNeedleVisuals(targetValue);

            if (lockOnceCompleted)
                eyeSlider.interactable = false;

            onAligned?.Invoke();
            PageNavigationController.RequestNavigationUnlock();
        }
    }
}