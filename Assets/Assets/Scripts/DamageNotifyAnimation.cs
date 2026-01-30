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
            {
                foreach (var tmp in GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (tmp != null && !tmp.name.Contains("SubMeshUI"))
                    {
                        damageText = tmp;
                        break;
                    }
                }
            }
            if (damageText == null)
                damageText = GetComponent<TextMeshProUGUI>();
        }
        
        // Только свой CanvasGroup — не родительский, иначе показывается и BalanceNotify (общий родитель).
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 1f;
            }
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
    /// Анимация от fromDamage до targetDamage (double). Вызывается напрямую (корутины запускаются на этом объекте).
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
        EnsureActiveInHierarchy();
        if (damageText != null && !damageText.gameObject.activeSelf)
            damageText.gameObject.SetActive(true);
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
        else
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
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
    /// Корутина анимации — запускается с менеджера (не с DamageNotify), чтобы не требовать activeInHierarchy.
    /// Не вызывает StartCoroutine, только yield return.
    /// </summary>
    public System.Collections.IEnumerator AnimateToAmountCoroutine(double targetDamage)
    {
        if (damageText == null || canvasGroup == null)
        {
            Initialize();
            if (damageText == null)
                yield break;
        }
        EnsureActiveInHierarchy();
        if (damageText != null && !damageText.gameObject.activeSelf)
            damageText.gameObject.SetActive(true);
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 1f;
            }
        }
        // Сразу делаем блок и текст видимыми (до первого yield), иначе текст не успевает отрисоваться
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        ForceTextVisible();
        double actualFrom = currentDisplayedValue;
        if (targetDamage > 0 && currentDisplayedValue > 0)
            actualFrom = currentDisplayedValue;
        if (targetDamage <= 0)
            actualFrom = 0;
        SetText(actualFrom);
        currentDisplayedValue = actualFrom;
        if (targetDamage > 0)
            yield return AnimateValueCoroutine(actualFrom, targetDamage);
        else
        {
            SetText(0);
            currentDisplayedValue = 0;
        }
        currentAnimation = null;
    }
    
    /// <summary>
    /// Скрыть уведомление (мгновенно, без корутины на DamageNotify).
    /// </summary>
    public void Hide()
    {
        if (fadeCoroutine != null)
            fadeCoroutine = null;
        if (canvasGroup != null && canvasGroup.alpha > 0.01f)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
    
    private void SetText(double value)
    {
        if (damageText == null) return;
        if (!damageText.gameObject.activeInHierarchy)
            damageText.gameObject.SetActive(true);
        damageText.enabled = true;
        ForceTextVisible();
        string s = FormatDamage(value);
        damageText.text = s;
        damageText.SetText(s);
        damageText.ForceMeshUpdate(true);
        damageText.UpdateMeshPadding();
    }
    
    /// <summary>Принудительно делает текст видимым (цвет alpha=1, объект активен).</summary>
    private void ForceTextVisible()
    {
        if (damageText == null) return;
        damageText.gameObject.SetActive(true);
        damageText.enabled = true;
        Color c = damageText.color;
        if (c.a < 0.01f)
        {
            c.a = 1f;
            damageText.color = c;
        }
        if (damageText.fontSharedMaterial != null && damageText.fontSharedMaterial.HasProperty("_FaceColor"))
        {
            Color face = damageText.fontSharedMaterial.GetColor("_FaceColor");
            if (face.a < 0.01f)
            {
                face.a = 1f;
                damageText.fontSharedMaterial.SetColor("_FaceColor", face);
            }
        }
    }
    
    /// <summary>Форматирует урон только целым числом (без дробной части).</summary>
    private static string FormatDamage(double value)
    {
        return ((long)System.Math.Round(value)).ToString();
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
    
    /// <summary>
    /// Включает DamageNotify и при необходимости родителя, чтобы уведомление отрисовалось.
    /// Если активируем родителя (OverflowUiContainer), сразу скрываем BalanceNotify, чтобы не показывать уведомление пополнения.
    /// </summary>
    private void EnsureActiveInHierarchy()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        // Если родитель неактивен — мы всё равно не видим. Активируем родителя и скрываем соседа BalanceNotify.
        if (!gameObject.activeInHierarchy && transform.parent != null)
        {
            transform.parent.gameObject.SetActive(true);
            HideSiblingBalanceNotify();
        }
    }
    
    /// <summary>
    /// Скрывает соседний BalanceNotify (общий родитель OverflowUiContainer), чтобы при показе урона не появлялось уведомление пополнения.
    /// </summary>
    private void HideSiblingBalanceNotify()
    {
        if (transform.parent == null) return;
        Transform balance = transform.parent.Find("BalanceNotify");
        if (balance == null) return;
        var cg = balance.GetComponent<CanvasGroup>();
        if (cg == null) cg = balance.GetComponentInChildren<CanvasGroup>(true);
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }
}
