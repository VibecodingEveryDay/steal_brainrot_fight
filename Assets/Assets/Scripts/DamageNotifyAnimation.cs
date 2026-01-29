using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Анимация текста уведомления об уроне по боссу.
/// Отображает урон как double (целое без нулей/точки или с дробной частью при необходимости).
/// </summary>
public class DamageNotifyAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TextMeshProUGUI для отображения урона (если не назначен — ищется дочерний DamageText)")]
    [SerializeField] private TextMeshProUGUI damageText;
    
    [Tooltip("CanvasGroup для fade (если не назначен — ищется на себе или родителе)")]
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Animation Settings")]
    [Tooltip("Длительность анимации числа (сек)")]
    [SerializeField] private float animationDuration = 0.4f;
    
    [Tooltip("Кривая анимации")]
    [SerializeField] private AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
    
    [Header("Fade Settings")]
    [Tooltip("Длительность fade in (сек)")]
    [SerializeField] private float fadeInDuration = 0.2f;
    
    [Tooltip("Длительность fade out (сек)")]
    [SerializeField] private float fadeOutDuration = 0.4f;
    
    private Coroutine currentAnimation;
    private Coroutine fadeCoroutine;
    private double currentDisplayedValue;
    
    private void Awake()
    {
        Initialize();
    }
    
    private void OnEnable()
    {
        if (damageText == null)
            Initialize();
    }
    
    private void Initialize()
    {
        if (damageText == null)
        {
            Transform child = transform.Find("DamageText");
            if (child == null)
                child = FindChildByName(transform, "DamageText");
            if (child != null)
                damageText = child.GetComponent<TextMeshProUGUI>();
            if (damageText == null)
                damageText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (damageText == null)
                damageText = GetComponent<TextMeshProUGUI>();
        }
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInParent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        if (animationCurve == null || animationCurve.keys.Length == 0)
        {
            animationCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(1f, 1f, 0f, 0f)
            );
        }
    }
    
    /// <summary>
    /// Анимация от текущего отображаемого значения до целевого урона (double).
    /// </summary>
    public void AnimateToAmount(double targetDamage)
    {
        AnimateToAmount(currentDisplayedValue, targetDamage);
    }
    
    /// <summary>
    /// Анимация от fromDamage до targetDamage (double).
    /// </summary>
    public void AnimateToAmount(double fromDamage, double targetDamage)
    {
        if (damageText == null || canvasGroup == null)
        {
            Initialize();
            if (damageText == null)
            {
                Debug.LogWarning("[DamageNotifyAnimation] damageText не найден.");
                return;
            }
        }
        
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);
        
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        
        if (canvasGroup != null)
        {
            if (canvasGroup.alpha < 0.9f)
                fadeCoroutine = StartCoroutine(FadeInCoroutine());
            else
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
        
        double actualFrom = fromDamage;
        if (targetDamage > 0 && currentDisplayedValue > 0 && fromDamage == 0)
            actualFrom = currentDisplayedValue;
        
        SetText(actualFrom);
        currentDisplayedValue = actualFrom;
        
        if (targetDamage > 0)
        {
            if (currentAnimation != null)
                StopCoroutine(currentAnimation);
            currentAnimation = StartCoroutine(AnimateValueCoroutine(actualFrom, targetDamage));
        }
        else
        {
            SetText(0);
            currentDisplayedValue = 0;
        }
    }
    
    /// <summary>
    /// Скрыть уведомление (fade out). Вызывается менеджером после 2 с без урона.
    /// </summary>
    public void Hide()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        if (canvasGroup != null && canvasGroup.alpha > 0.01f)
            fadeCoroutine = StartCoroutine(FadeOutCoroutine());
    }
    
    private void SetText(double value)
    {
        if (damageText == null) return;
        damageText.text = FormatDamage(value);
        damageText.ForceMeshUpdate();
    }
    
    private static string FormatDamage(double value)
    {
        if (value == (long)value && value >= 0)
            return ((long)value).ToString();
        if (value == (long)value && value < 0)
            return ((long)value).ToString();
        return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
    
    private IEnumerator AnimateValueCoroutine(double fromValue, double toValue)
    {
        if (damageText == null) yield break;
        
        float dur = animationDuration > 0f ? animationDuration : 0.4f;
        float elapsed = 0f;
        double diff = toValue - fromValue;
        
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float curveT = animationCurve != null ? animationCurve.Evaluate(t) : t;
            double current = fromValue + diff * curveT;
            if (damageText != null)
            {
                SetText(current);
                currentDisplayedValue = current;
            }
            yield return null;
        }
        
        SetText(toValue);
        currentDisplayedValue = toValue;
        currentAnimation = null;
    }
    
    private IEnumerator FadeInCoroutine()
    {
        if (canvasGroup == null) yield break;
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / fadeInDuration));
            yield return null;
        }
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        if (damageText != null)
            damageText.ForceMeshUpdate();
        fadeCoroutine = null;
    }
    
    private IEnumerator FadeOutCoroutine()
    {
        if (canvasGroup == null) yield break;
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / fadeOutDuration));
            yield return null;
        }
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        fadeCoroutine = null;
    }
    
    private static Transform FindChildByName(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var found = FindChildByName(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
