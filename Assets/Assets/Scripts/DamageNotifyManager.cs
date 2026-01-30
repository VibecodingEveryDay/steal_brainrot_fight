using UnityEngine;
using System.Collections;

/// <summary>
/// Менеджер уведомления об уроне по боссу.
/// Показывает весь урон, нанесённый боссу за всё время (double).
/// Если урон не наносился заданное время (по умолчанию 2 с), уведомление скрывается.
/// </summary>
public class DamageNotifyManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Через сколько секунд без урона скрывать уведомление")]
    [SerializeField] private float hideAfterNoDamageSeconds = 2f;
    
    [Header("References")]
    [Tooltip("Объект DamageNotify (если не назначен — ищется по имени в сцене)")]
    [SerializeField] private GameObject damageNotify;
    
    [Tooltip("Объект для запуска корутин, если этот скрипт висит на неактивном DamageNotify (например BattleManager). Не назначен — подставляется BattleManager автоматически.")]
    [SerializeField] private MonoBehaviour coroutineRunner;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private DamageNotifyAnimation notifyAnimation;
    private double totalDamage;
    private float lastDamageTime;
    private bool notificationHidden;
    private Coroutine damageAnimationCoroutine;
    private MonoBehaviour _effectiveRunner;
    
    private void Awake()
    {
        FindDamageNotify();
    }
    
    private void Start()
    {
        if (damageNotify == null || notifyAnimation == null)
            FindDamageNotify();
        
        if (damageNotify != null && !damageNotify.activeInHierarchy)
            damageNotify.SetActive(true);
        
        totalDamage = 0;
        lastDamageTime = -999f;
        notificationHidden = true;
    }
    
    private void Update()
    {
        if (notifyAnimation == null) return;
        if (notificationHidden || totalDamage <= 0) return;
        
        if (Time.time - lastDamageTime >= hideAfterNoDamageSeconds)
        {
            notifyAnimation.Hide();
            notificationHidden = true;
        }
    }
    
    private IEnumerator RunDamageAnimationCoroutine(double totalDamage)
    {
        if (notifyAnimation != null)
            yield return notifyAnimation.AnimateToAmountCoroutine(totalDamage);
        damageAnimationCoroutine = null;
    }
    
    private void FindDamageNotify()
    {
        if (damageNotify == null)
        {
            damageNotify = GameObject.Find("DamageNotify");
            if (damageNotify == null)
                damageNotify = FindGameObjectInScene("DamageNotify");
        }
        
        if (damageNotify != null)
        {
            if (!damageNotify.activeInHierarchy)
                damageNotify.SetActive(true);
            
            notifyAnimation = damageNotify.GetComponent<DamageNotifyAnimation>();
            if (notifyAnimation == null)
                notifyAnimation = damageNotify.GetComponentInChildren<DamageNotifyAnimation>(true);
            // Если скрипт не висит в сцене — добавляем в рантайме, чтобы текст показывался
            if (notifyAnimation == null)
            {
                notifyAnimation = damageNotify.AddComponent<DamageNotifyAnimation>();
                if (debug)
                    Debug.Log("[DamageNotifyManager] DamageNotifyAnimation добавлен на DamageNotify в рантайме.");
            }
            
            if (notifyAnimation == null && debug)
                Debug.LogWarning("[DamageNotifyManager] DamageNotifyAnimation не найден на DamageNotify.");
            else if (debug)
                Debug.Log($"[DamageNotifyManager] DamageNotify найден: {damageNotify.name}");
        }
        else if (debug)
        {
            Debug.LogWarning("[DamageNotifyManager] DamageNotify не найден в сцене (MainCanvas->OverflowUiContainer->DamageNotify).");
        }
    }
    
    /// <summary>
    /// Добавляет урон к общей сумме за всё время и сразу обновляет/показывает уведомление.
    /// Если 2 с не было урона — уведомление скроется автоматически.
    /// </summary>
    public void AddDamage(double amount)
    {
        if (amount <= 0) return;
        
        totalDamage += amount;
        lastDamageTime = Time.time;
        notificationHidden = false;
        
        if (notifyAnimation == null)
            FindDamageNotify();
        if (notifyAnimation == null) return;
        
        MonoBehaviour runner = GetEffectiveCoroutineRunner();
        if (runner == null)
        {
            if (debug)
                Debug.LogWarning("[DamageNotifyManager] Нет активного объекта для запуска корутины.");
            return;
        }
        
        try
        {
            if (damageAnimationCoroutine != null && _effectiveRunner != null)
                _effectiveRunner.StopCoroutine(damageAnimationCoroutine);
            _effectiveRunner = runner;
            damageAnimationCoroutine = runner.StartCoroutine(RunDamageAnimationCoroutine(totalDamage));
            if (debug)
                Debug.Log($"[DamageNotifyManager] Урон: +{amount}, всего за бой: {totalDamage}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DamageNotifyManager] Ошибка анимации урона: {e.Message}");
        }
    }
    
    /// <summary>
    /// Возвращает MonoBehaviour, на котором можно запускать корутины.
    /// Если этот объект неактивен (например менеджер висит на DamageNotify) — используем coroutineRunner или BattleManager.
    /// </summary>
    private MonoBehaviour GetEffectiveCoroutineRunner()
    {
        if (gameObject.activeInHierarchy)
            return this;
        if (coroutineRunner != null && coroutineRunner.gameObject.activeInHierarchy)
            return coroutineRunner;
        var bm = BattleManager.Instance;
        if (bm != null && bm.gameObject.activeInHierarchy)
            return bm;
        return null;
    }
    
    /// <summary>
    /// Сбросить накопленный урон (например, при начале нового боя).
    /// </summary>
    public void ResetTotalDamage()
    {
        totalDamage = 0;
        lastDamageTime = -999f;
        notificationHidden = true;
        if (notifyAnimation != null)
            notifyAnimation.Hide();
    }
    
    private GameObject FindGameObjectInScene(string name)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var found = FindGameObjectInHierarchy(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }
    
    private GameObject FindGameObjectInHierarchy(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        foreach (Transform child in parent)
        {
            var found = FindGameObjectInHierarchy(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
