using UnityEngine;
using System.Collections;

/// <summary>
/// Скрипт для 3D кнопки открытия магазина силы
/// При наступлении игрока на кнопку показывает PowerModalContainer
/// При уходе игрока скрывает модальное окно
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShopPowerButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PowerModalContainer GameObject, который будет показан при наступлении на кнопку")]
    [SerializeField] private GameObject powerModalContainer;
    
    [Header("Settings")]
    [Tooltip("Скрывать PowerModalContainer при старте (если true, контейнер будет скрыт в Start)")]
    [SerializeField] private bool hideOnStart = true;
    
    [Header("Player Detection")]
    [Tooltip("Тег игрока (по умолчанию 'Player')")]
    [SerializeField] private string playerTag = "Player";
    
    // Флаг для отслеживания, находится ли игрок на кнопке
    private bool isPlayerOnButton = false;
    
    private void Awake()
    {
        // Проверяем наличие триггера
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Убеждаемся, что коллайдер настроен как триггер
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }
    }
    
    private void Start()
    {
        // Автоматически находим PowerModalContainer, если не назначен
        if (powerModalContainer == null)
        {
            // Метод 1: Ищем в сцене объект с именем "PowerModalContainer" (только активные объекты)
            GameObject foundContainer = GameObject.Find("PowerModalContainer");
            
            // Метод 2: Если не найден, ищем через рекурсивный поиск (включая неактивные)
            if (foundContainer == null)
            {
                // Используем более надежный метод поиска через все объекты в сцене
                UnityEngine.SceneManagement.Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                GameObject[] rootObjects = currentScene.GetRootGameObjects();
                
                // Рекурсивно ищем во всех объектах сцены
                foundContainer = FindGameObjectInHierarchy(rootObjects, "PowerModalContainer");
                
                if (foundContainer != null)
                {
                    Debug.Log($"[ShopPowerButton] PowerModalContainer найден через рекурсивный поиск: {foundContainer.name}, activeSelf={foundContainer.activeSelf}, activeInHierarchy={foundContainer.activeInHierarchy}");
                }
            }
            
            // Метод 3: Ищем через поиск по компоненту ShopPowerManager
            if (foundContainer == null)
            {
                ShopPowerManager powerManager = FindFirstObjectByType<ShopPowerManager>();
                if (powerManager != null)
                {
                    // Ищем родителя с именем, содержащим "Power" или "Modal"
                    Transform parent = powerManager.transform;
                    while (parent != null)
                    {
                        if (parent.name.Contains("Power") || parent.name.Contains("Modal") || parent.name.Contains("Container"))
                        {
                            foundContainer = parent.gameObject;
                            Debug.Log($"[ShopPowerButton] PowerModalContainer найден через ShopPowerManager: {parent.name}");
                            break;
                        }
                        parent = parent.parent;
                    }
                    
                    // Если не нашли по имени, используем прямой родитель ShopPowerManager
                    if (foundContainer == null && powerManager.transform.parent != null)
                    {
                        foundContainer = powerManager.transform.parent.gameObject;
                        Debug.Log($"[ShopPowerButton] PowerModalContainer найден как родитель ShopPowerManager: {foundContainer.name}");
                    }
                }
            }
            
            if (foundContainer != null)
            {
                powerModalContainer = foundContainer;
                Debug.Log($"[ShopPowerButton] PowerModalContainer найден автоматически: {foundContainer.name}, activeSelf={foundContainer.activeSelf}, activeInHierarchy={foundContainer.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("[ShopPowerButton] PowerModalContainer не найден! Убедитесь, что объект существует в сцене с именем 'PowerModalContainer' или назначьте его вручную в инспекторе.");
            }
        }
        else
        {
            Debug.Log($"[ShopPowerButton] PowerModalContainer назначен вручную: {powerModalContainer.name}");
        }
        
        // Проверяем коллайдер
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Debug.Log($"[ShopPowerButton] Collider найден: isTrigger={col.isTrigger}, enabled={col.enabled}");
        }
        else
        {
            Debug.LogError("[ShopPowerButton] Collider не найден! Добавьте Collider к объекту.");
        }
        
        // Скрываем контейнер при старте, если требуется
        if (powerModalContainer != null && hideOnStart)
        {
            powerModalContainer.SetActive(false);
            Debug.Log("[ShopPowerButton] PowerModalContainer скрыт при старте");
        }
    }
    
    /// <summary>
    /// Вызывается когда объект входит в триггер
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[ShopPowerButton] OnTriggerEnter вызван: other={other.name}, tag={other.tag}, playerTag={playerTag}");
        
        // Проверяем, что это игрок
        if (other.CompareTag(playerTag))
        {
            Debug.Log($"[ShopPowerButton] Игрок вошел в триггер, открываем магазин");
            isPlayerOnButton = true;
            OpenShop();
        }
        else
        {
            Debug.LogWarning($"[ShopPowerButton] Объект с тегом '{other.tag}' вошел в триггер, но ожидался тег '{playerTag}'");
        }
    }
    
    /// <summary>
    /// Вызывается когда объект выходит из триггера
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        // Проверяем, что это игрок
        if (other.CompareTag(playerTag))
        {
            isPlayerOnButton = false;
            
            // Скрываем модальное окно при уходе игрока
            CloseShop();
        }
    }
    
    /// <summary>
    /// Публичный метод для открытия магазина (можно вызвать извне)
    /// </summary>
    public void OpenShop()
    {
        if (powerModalContainer != null)
        {
            Debug.Log($"[ShopPowerButton] Открываем магазин: powerModalContainer={powerModalContainer.name}, activeSelf={powerModalContainer.activeSelf}, activeInHierarchy={powerModalContainer.activeInHierarchy}");
            
            // ВАЖНО: Проверяем, не находится ли модальное окно в неактивном родителе
            // Если родитель неактивен, активируем его тоже
            Transform parent = powerModalContainer.transform.parent;
            if (parent != null && !parent.gameObject.activeSelf)
            {
                Debug.LogWarning($"[ShopPowerButton] Родитель модального окна неактивен: {parent.name}, активируем его");
                parent.gameObject.SetActive(true);
            }
            
            // Активируем модальное окно
            powerModalContainer.SetActive(true);
            
            Debug.Log($"[ShopPowerButton] Модальное окно активировано: activeSelf={powerModalContainer.activeSelf}, activeInHierarchy={powerModalContainer.activeInHierarchy}");
            
            // Обновляем локализацию после активации с небольшой задержкой
            // Это необходимо, чтобы YG2 успел инициализироваться
            StartCoroutine(UpdateLocalizationDelayed());
        }
        else
        {
            Debug.LogError("[ShopPowerButton] Не удалось открыть магазин: powerModalContainer == null");
        }
    }
    
    /// <summary>
    /// Обновляет все LocalizedText компоненты в модальном окне с небольшой задержкой
    /// Это необходимо, чтобы YG2 успел инициализироваться перед обновлением текстов
    /// Также обновляет цвета цен в зависимости от баланса
    /// </summary>
    private IEnumerator UpdateLocalizationDelayed()
    {
        // Ждем конец кадра, чтобы YG2 успел инициализироваться
        yield return new WaitForEndOfFrame();
        
        if (powerModalContainer != null && powerModalContainer.activeSelf)
        {
            // Находим все LocalizedText компоненты в модальном окне (включая дочерние объекты)
            LocalizedText[] localizedTexts = powerModalContainer.GetComponentsInChildren<LocalizedText>(true);
            
            foreach (LocalizedText localizedText in localizedTexts)
            {
                if (localizedText != null)
                {
                    // Обновляем текст с правильным языком
                    localizedText.UpdateText();
                }
            }
            
            // Обновляем цвета цен в ShopPowerManager
            ShopPowerManager powerManager = powerModalContainer.GetComponentInChildren<ShopPowerManager>();
            if (powerManager != null)
            {
                // Вызываем метод обновления всех баров для обновления цветов цен
                powerManager.UpdateAllPowerBars();
            }
        }
    }
    
    /// <summary>
    /// Публичный метод для закрытия магазина
    /// </summary>
    public void CloseShop()
    {
        if (powerModalContainer != null)
        {
            powerModalContainer.SetActive(false);
        }
    }
    
    /// <summary>
    /// Рекурсивно ищет GameObject по имени в иерархии
    /// </summary>
    private GameObject FindGameObjectInHierarchy(GameObject[] rootObjects, string name)
    {
        foreach (GameObject root in rootObjects)
        {
            GameObject found = FindGameObjectInTransform(root.transform, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Рекурсивно ищет GameObject по имени в Transform и его дочерних объектах
    /// </summary>
    private GameObject FindGameObjectInTransform(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent.gameObject;
        }
        
        foreach (Transform child in parent)
        {
            GameObject found = FindGameObjectInTransform(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
}
