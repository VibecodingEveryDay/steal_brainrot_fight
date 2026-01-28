using UnityEngine;
using System.Collections;

/// <summary>
/// Менеджер для управления уведомлением о балансе
/// Раз в 3 секунды суммирует доходы со всех EarnPanel и обновляет уведомление
/// </summary>
public class BalanceNotifyManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Интервал обновления суммы со всех EarnPanel (в секундах)")]
    [SerializeField] private float updateInterval = 3f;
    
    [Header("References")]
    [Tooltip("BalanceNotify GameObject (если не назначен, будет найден автоматически)")]
    [SerializeField] private GameObject balanceNotify;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private BalanceNotifyAnimation notifyAnimation;
    private Coroutine updateCoroutine;
    private float lastUpdateTime = 0f;
    
    // ВАЖНО: Накопленная сумма заработанных денег за последние 3 секунды
    private double accumulatedEarnedAmount = 0.0;
    
    private void Awake()
    {
        if (debug)
        {
            Debug.Log($"[BalanceNotifyManager] Awake() вызван на объекте: {gameObject.name}");
        }
        FindBalanceNotify();
    }
    
    private void Start()
    {
        // Убеждаемся, что BalanceNotify найден
        if (balanceNotify == null || notifyAnimation == null)
        {
            if (debug)
            {
                Debug.LogWarning("[BalanceNotifyManager] BalanceNotify или notifyAnimation не найдены, пытаемся найти снова...");
            }
            FindBalanceNotify();
        }
        
        // ВАЖНО: Убеждаемся, что BalanceNotify активен
        if (balanceNotify != null && !balanceNotify.activeInHierarchy)
        {
            balanceNotify.SetActive(true);
        }
        
        // ВАЖНО: При старте накопленная сумма = 0
        accumulatedEarnedAmount = 0.0;
        
        // Запускаем корутину обновления при старте
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        updateCoroutine = StartCoroutine(UpdateBalanceNotificationCoroutine());
    }
    
    private void OnEnable()
    {
        // Запускаем корутину обновления при включении
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        updateCoroutine = StartCoroutine(UpdateBalanceNotificationCoroutine());
    }
    
    private void OnDisable()
    {
        // Останавливаем корутину при выключении
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
            updateCoroutine = null;
        }
    }
    
    /// <summary>
    /// Находит BalanceNotify в сцене
    /// </summary>
    private void FindBalanceNotify()
    {
        if (balanceNotify == null)
        {
            balanceNotify = GameObject.Find("BalanceNotify");
            if (balanceNotify == null)
            {
                // Пытаемся найти через поиск по имени с учетом вложенности
                balanceNotify = FindGameObjectInScene("BalanceNotify");
            }
        }
        
        if (balanceNotify != null)
        {
            // ВАЖНО: Убеждаемся, что объект активен для поиска компонентов
            if (!balanceNotify.activeInHierarchy)
            {
                balanceNotify.SetActive(true);
            }
            
            // Находим компонент BalanceNotifyAnimation
            notifyAnimation = balanceNotify.GetComponent<BalanceNotifyAnimation>();
            if (notifyAnimation == null)
            {
                notifyAnimation = balanceNotify.GetComponentInChildren<BalanceNotifyAnimation>(true); // true = включая неактивные
            }
            
            if (notifyAnimation == null)
            {
                Debug.LogError("[BalanceNotifyManager] BalanceNotifyAnimation не найден на объекте BalanceNotify! Проверьте, что компонент BalanceNotifyAnimation добавлен на объект BalanceNotify или его дочерний объект.");
            }
            else if (debug)
            {
                Debug.Log($"[BalanceNotifyManager] BalanceNotify найден: {balanceNotify.name}");
            }
        }
        else
        {
            Debug.LogError("[BalanceNotifyManager] BalanceNotify не найден в сцене! Убедитесь, что объект с именем 'BalanceNotify' существует в иерархии.");
        }
    }
    
    /// <summary>
    /// Корутина для периодического обновления уведомления
    /// </summary>
    private IEnumerator UpdateBalanceNotificationCoroutine()
    {
        if (debug)
        {
            Debug.Log("[BalanceNotifyManager] UpdateBalanceNotificationCoroutine запущена");
        }
        
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            
            // ВАЖНО: Проверяем только один раз при первом запуске, затем используем кэш
            if (notifyAnimation == null)
            {
                if (debug)
                {
                    Debug.LogWarning("[BalanceNotifyManager] notifyAnimation равен null, пытаемся найти...");
                }
                FindBalanceNotify();
                // Если не найден, пропускаем этот цикл
                if (notifyAnimation == null)
                {
                    continue;
                }
            }
            
            // ВАЖНО: Показываем только накопленную сумму за последние 3 секунды
            double earnedAmount = accumulatedEarnedAmount;
            
            // ВАЖНО: Показываем уведомление только если есть заработанные деньги
            if (earnedAmount > 0)
            {
                try
                {
                    notifyAnimation.AnimateToAmount(0.0, earnedAmount);
                    if (debug)
                    {
                        Debug.Log($"[BalanceNotifyManager] AnimateToAmount вызван для суммы: {earnedAmount}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[BalanceNotifyManager] Ошибка при вызове AnimateToAmount: {e.Message}\n{e.StackTrace}");
                }
            }
            
            // ВАЖНО: Обнуляем накопленную сумму после показа
            accumulatedEarnedAmount = 0.0;
            lastUpdateTime = Time.time;
        }
    }
    
    /// <summary>
    /// Добавляет заработанную сумму в накопитель (вызывается из EarnPanel при сборе)
    /// Уведомление будет показано через 3 секунды с накопленной суммой
    /// </summary>
    public void UpdateNotificationImmediately(double amount)
    {
        // ВАЖНО: Проверяем, что сумма валидна
        if (amount <= 0.0)
        {
            if (debug)
            {
                Debug.LogWarning($"[BalanceNotifyManager] UpdateNotificationImmediately вызван с невалидной суммой: {amount}, пропускаем");
            }
            return;
        }
        
        // ВАЖНО: Добавляем сумму в накопитель, а не показываем сразу
        // Уведомление будет показано через 3 секунды с накопленной суммой
        double oldAccumulated = accumulatedEarnedAmount;
        accumulatedEarnedAmount += amount;
        
        if (debug)
        {
            Debug.Log($"[BalanceNotifyManager] Сумма добавлена: {amount}, было: {oldAccumulated}, стало: {accumulatedEarnedAmount}");
        }
    }
    
    
    /// <summary>
    /// Рекурсивно ищет GameObject в сцене по имени
    /// </summary>
    private GameObject FindGameObjectInScene(string name)
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            GameObject found = FindGameObjectInHierarchy(root.transform, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Рекурсивно ищет GameObject в иерархии
    /// </summary>
    private GameObject FindGameObjectInHierarchy(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent.gameObject;
        }
        
        foreach (Transform child in parent)
        {
            GameObject found = FindGameObjectInHierarchy(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
}
