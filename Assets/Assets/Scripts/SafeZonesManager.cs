using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Менеджер для управления покупками безопасных зон в магазине
/// Управляет отображением цен и покупками через Bar1, Bar2, Bar3, Bar4
/// </summary>
public class SafeZonesManager : MonoBehaviour
{
    [Header("Price Settings")]
    [Tooltip("Цены за безопасные зоны (1-4). По умолчанию: 20K, 100K, 2M, 2B")]
    [SerializeField] private long[] zonePrices = new long[] { 20000, 100000, 2000000, 2000000000 };
    
    [Header("References")]
    [Tooltip("Transform, содержащий все Bar объекты (Bar1, Bar2, Bar3, Bar4)")]
    [SerializeField] private Transform safeZonesContainer;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    [System.Serializable]
    public class SafeZoneBar
    {
        public string barName;
        public Transform barTransform;
        public TextMeshProUGUI buttonText; // Текст на кнопке ("КУПИТЬ" / "КУПЛЕНО")
        public TextMeshProUGUI priceText; // Цена
        public Button button;
        public int zoneNumber; // Номер зоны (1-4)
    }
    
    // Список баров (всегда 4: Bar1, Bar2, Bar3, Bar4)
    private List<SafeZoneBar> safeZoneBars = new List<SafeZoneBar>();
    
    private GameStorage gameStorage;
    
    private void Awake()
    {
        // Автоматически находим контейнер с барами, если не назначен
        if (safeZonesContainer == null)
        {
            // Сначала пытаемся найти SafeZonesModalContainer в сцене
            GameObject safeZonesModalContainer = GameObject.Find("SafeZonesModalContainer");
            if (safeZonesModalContainer != null)
            {
                safeZonesContainer = safeZonesModalContainer.transform;
                if (debug)
                {
                    Debug.Log($"[SafeZonesManager] SafeZonesModalContainer найден через GameObject.Find: {safeZonesModalContainer.name}");
                }
            }
            else
            {
                // Если не найден в сцене, ищем в родительском объекте
                Transform parent = transform.parent;
                if (parent != null && (parent.name == "SafeZonesModalContainer" || parent.name.Contains("SafeZone")))
                {
                    safeZonesContainer = parent;
                    if (debug)
                    {
                        Debug.Log($"[SafeZonesManager] Контейнер найден через родительский объект: {parent.name}");
                    }
                }
                else
                {
                    // Если ничего не найдено, используем текущий transform
                    safeZonesContainer = transform;
                    if (debug)
                    {
                        Debug.Log($"[SafeZonesManager] Используется текущий transform: {transform.name}");
                    }
                }
            }
        }
        
        if (debug && safeZonesContainer != null)
        {
            Debug.Log($"[SafeZonesManager] Контейнер для поиска баров: {safeZonesContainer.name}");
            // Выводим список дочерних объектов для отладки
            foreach (Transform child in safeZonesContainer)
            {
                Debug.Log($"[SafeZonesManager] Дочерний объект найден: {child.name}");
            }
        }
        
        // Инициализируем массив цен, если он не инициализирован
        if (zonePrices == null || zonePrices.Length != 4)
        {
            zonePrices = new long[] { 20000, 100000, 2000000, 2000000000 };
            if (debug)
            {
                Debug.Log("[SafeZonesManager] Массив zonePrices инициализирован значениями по умолчанию");
            }
        }
        
        // Автоматически находим и настраиваем Bar объекты (всегда 4)
        if (safeZoneBars.Count == 0)
        {
            SetupSafeZones();
        }
    }
    
    private void Start()
    {
        // Получаем ссылки на необходимые компоненты
        gameStorage = GameStorage.Instance;
        
        if (gameStorage == null)
        {
            Debug.LogError("[SafeZonesManager] GameStorage.Instance не найден!");
        }
        
        // Обновляем UI
        UpdateAllSafeZones();
    }
    
    private void OnEnable()
    {
        // Обновляем UI при активации
        if (gameStorage != null)
        {
            UpdateAllSafeZones();
        }
    }
    
    /// <summary>
    /// Автоматически настраивает SafeZoneBar объекты из иерархии
    /// </summary>
    private void SetupSafeZones()
    {
        // Ищем Bar1, Bar2, Bar3, Bar4 в дочерних объектах (поиск нечувствителен к регистру)
        Transform bar1 = FindChildByName(safeZonesContainer, "Bar1") ?? FindChildByName(safeZonesContainer, "bar1");
        Transform bar2 = FindChildByName(safeZonesContainer, "Bar2") ?? FindChildByName(safeZonesContainer, "bar2");
        Transform bar3 = FindChildByName(safeZonesContainer, "Bar3") ?? FindChildByName(safeZonesContainer, "bar3");
        Transform bar4 = FindChildByName(safeZonesContainer, "Bar4") ?? FindChildByName(safeZonesContainer, "bar4");
        
        safeZoneBars.Clear();
        
        // Настраиваем Bar1 (зона 1)
        if (bar1 != null)
        {
            SafeZoneBar bar = CreateSafeZoneBar(bar1, "Bar1", 1);
            safeZoneBars.Add(bar);
        }
        
        // Настраиваем Bar2 (зона 2)
        if (bar2 != null)
        {
            SafeZoneBar bar = CreateSafeZoneBar(bar2, "Bar2", 2);
            safeZoneBars.Add(bar);
        }
        
        // Настраиваем Bar3 (зона 3)
        if (bar3 != null)
        {
            SafeZoneBar bar = CreateSafeZoneBar(bar3, "Bar3", 3);
            safeZoneBars.Add(bar);
        }
        
        // Настраиваем Bar4 (зона 4)
        if (bar4 != null)
        {
            SafeZoneBar bar = CreateSafeZoneBar(bar4, "Bar4", 4);
            safeZoneBars.Add(bar);
        }
        
        if (safeZoneBars.Count == 0 && debug)
        {
            Debug.LogWarning("[SafeZonesManager] Не найдено ни одного Bar объекта (Bar1, Bar2, Bar3, Bar4)!");
        }
    }
    
    /// <summary>
    /// Создает SafeZoneBar из Transform
    /// </summary>
    private SafeZoneBar CreateSafeZoneBar(Transform barTransform, string name, int zoneNumber)
    {
        SafeZoneBar bar = new SafeZoneBar
        {
            barName = name,
            barTransform = barTransform,
            zoneNumber = zoneNumber
        };
        
        // Ищем Button и Text внутри него, а также Price
        bar.button = FindChildComponent<Button>(barTransform, "Button");
        if (bar.button != null)
        {
            // Ищем TextMeshProUGUI внутри Button
            bar.buttonText = bar.button.GetComponentInChildren<TextMeshProUGUI>();
            if (bar.buttonText == null)
            {
                // Если не найден, ищем по имени "Text"
                bar.buttonText = FindChildComponent<TextMeshProUGUI>(bar.button.transform, "Text");
            }
        }
        
        bar.priceText = FindChildComponent<TextMeshProUGUI>(barTransform, "Price") ?? 
                        FindChildComponent<TextMeshProUGUI>(barTransform, "price");
        
        // Отладочная информация о найденных компонентах
        if (debug)
        {
            Debug.Log($"[SafeZonesManager] Bar '{name}' (Зона {zoneNumber}) - " +
                      $"ButtonText: {(bar.buttonText != null ? bar.buttonText.name : "НЕ НАЙДЕН")}, " +
                      $"PriceText: {(bar.priceText != null ? bar.priceText.name : "НЕ НАЙДЕН")}, " +
                      $"Button: {(bar.button != null ? bar.button.name : "НЕ НАЙДЕН")}");
        }
        
        // Настраиваем обработчик клика на кнопке
        if (bar.button != null)
        {
            bar.button.onClick.RemoveAllListeners();
            int zoneNumberCopy = zoneNumber; // Копируем для замыкания
            bar.button.onClick.AddListener(() => OnBuyZoneClicked(zoneNumberCopy));
        }
        
        return bar;
    }
    
    /// <summary>
    /// Рекурсивно ищет дочерний объект по имени
    /// </summary>
    private Transform FindChildByName(Transform parent, string name)
    {
        if (parent == null) return null;
        
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
            
            Transform found = FindChildByName(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Рекурсивно ищет компонент в дочерних объектах
    /// </summary>
    private T FindChildComponent<T>(Transform parent, string name) where T : Component
    {
        Transform child = FindChildByName(parent, name);
        if (child != null)
        {
            return child.GetComponent<T>();
        }
        return null;
    }
    
    /// <summary>
    /// Обновляет все SafeZoneBar UI
    /// </summary>
    public void UpdateAllSafeZones()
    {
        if (gameStorage == null) return;
        
        if (debug)
        {
            Debug.Log("[SafeZonesManager] Обновление всех SafeZoneBars");
        }
        
        foreach (SafeZoneBar bar in safeZoneBars)
        {
            UpdateSafeZoneBar(bar);
        }
    }
    
    /// <summary>
    /// Обновляет UI конкретного SafeZoneBar
    /// </summary>
    private void UpdateSafeZoneBar(SafeZoneBar bar)
    {
        if (gameStorage == null) return;

        string lang = GetCurrentLanguage();
        
        // Проверяем, куплена ли зона
        bool isPurchased = gameStorage.IsSafeZonePurchased(bar.zoneNumber);
        
        // Получаем цену для этой зоны
        long price = 0;
        if (bar.zoneNumber >= 1 && bar.zoneNumber <= zonePrices.Length)
        {
            price = zonePrices[bar.zoneNumber - 1];
        }
        
        // Обновляем текст кнопки
        if (bar.buttonText != null)
        {
            if (isPurchased)
            {
                bar.buttonText.text = (lang == "ru") ? "КУПЛЕНО" : "PURCHASED";
            }
            else
            {
                bar.buttonText.text = (lang == "ru") ? "КУПИТЬ" : "BUY";
            }
            
            if (debug)
            {
                Debug.Log($"[SafeZonesManager] Bar '{bar.barName}' - ButtonText обновлен: {bar.buttonText.text}");
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[SafeZonesManager] ButtonText не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем цену
        if (bar.priceText != null)
        {
            if (isPurchased)
            {
                // Если зона куплена, можно скрыть цену или оставить пустым
                bar.priceText.text = "";
            }
            else
            {
                string formattedPrice = gameStorage.FormatBalance((double)price);
                bar.priceText.text = formattedPrice;
                
                // Проверяем баланс и меняем цвет цены
                double balance = gameStorage.GetBalanceDouble();
                if (balance < (double)price)
                {
                    // Не хватает баланса - красный цвет
                    bar.priceText.color = Color.red;
                }
                else
                {
                    // Хватает баланса - белый цвет
                    bar.priceText.color = Color.white;
                }
            }
            
            if (debug)
            {
                Debug.Log($"[SafeZonesManager] Bar '{bar.barName}' - PriceText обновлен: {bar.priceText.text} (raw price: {price})");
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[SafeZonesManager] PriceText не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем активность кнопки
        if (bar.button != null)
        {
            if (isPurchased)
            {
                // Если зона куплена, кнопка неактивна
                bar.button.interactable = false;
            }
            else
            {
                // Проверяем баланс
                double balance = gameStorage.GetBalanceDouble();
                bar.button.interactable = balance >= (double)price;
            }
        }
    }

    private string GetCurrentLanguage()
    {
#if Localization_yg
        if (YG2.lang != null)
        {
            return YG2.lang;
        }
#endif
        return LocalizationManager.GetCurrentLanguage();
    }
    
    /// <summary>
    /// Обработчик клика по кнопке покупки зоны
    /// </summary>
    private void OnBuyZoneClicked(int zoneNumber)
    {
        if (gameStorage == null)
        {
            Debug.LogError("[SafeZonesManager] GameStorage недоступен!");
            return;
        }
        
        // Проверяем, не куплена ли зона уже
        if (gameStorage.IsSafeZonePurchased(zoneNumber))
        {
            if (debug)
            {
                Debug.LogWarning($"[SafeZonesManager] Зона {zoneNumber} уже куплена!");
            }
            return;
        }
        
        // Получаем цену
        long price = 0;
        if (zoneNumber >= 1 && zoneNumber <= zonePrices.Length)
        {
            price = zonePrices[zoneNumber - 1];
        }
        else
        {
            Debug.LogError($"[SafeZonesManager] Некорректный номер зоны: {zoneNumber}");
            return;
        }
        
        // Проверяем баланс
        double balance = gameStorage.GetBalanceDouble();
        if (balance < (double)price)
        {
            if (debug)
            {
                Debug.LogWarning($"[SafeZonesManager] Недостаточно средств для покупки зоны {zoneNumber}! Требуется: {price}, есть: {balance}");
            }
            return;
        }
        
        // Вычитаем деньги - используем метод GameStorage для корректной конвертации
        // Используем SubtractBalanceLong для работы с long значениями
        bool purchaseSuccess = gameStorage.SubtractBalanceLong(price);
        
        if (purchaseSuccess)
        {
            // Покупаем зону
            bool zonePurchased = gameStorage.PurchaseSafeZone(zoneNumber);
            
            if (zonePurchased)
            {
                // Сохраняем прогресс
                gameStorage.Save();
                
                // Обновляем UI
                UpdateAllSafeZones();
                
                // Обновляем все SafeZone объекты в сцене, чтобы они обновили статус Collider
                UpdateAllSafeZonesInScene(zoneNumber);
                
                if (debug)
                {
                    Debug.Log($"[SafeZonesManager] Зона {zoneNumber} успешно куплена!");
                }
            }
            else
            {
                Debug.LogError($"[SafeZonesManager] Не удалось купить зону {zoneNumber}!");
            }
        }
        else
        {
            Debug.LogError($"[SafeZonesManager] Не удалось вычесть деньги из баланса!");
        }
    }
    
    /// <summary>
    /// Обновляет все SafeZone объекты в сцене после покупки зоны
    /// </summary>
    private void UpdateAllSafeZonesInScene(int purchasedZoneNumber)
    {
        // Находим все SafeZone компоненты в сцене
        SafeZone[] allSafeZones = FindObjectsByType<SafeZone>(FindObjectsSortMode.None);
        
        foreach (SafeZone safeZone in allSafeZones)
        {
            // Обновляем только зону, которая была куплена
            if (safeZone != null)
            {
                safeZone.UpdatePurchaseStatus();
            }
        }
        
        if (debug)
        {
            Debug.Log($"[SafeZonesManager] Обновлены все SafeZone объекты после покупки зоны {purchasedZoneNumber}");
        }
    }
    
    /// <summary>
    /// Конвертирует double цену в формат value + scaler (аналогично балансу)
    /// </summary>
    private (int value, string scaler) ConvertDoubleToPrice(double price)
    {
        if (price <= 0)
        {
            return (0, "");
        }
        
        // Триллионы (10^12)
        if (price >= 1000000000000.0)
        {
            double trillions = price / 1000000000000.0;
            return ((int)trillions, "T");
        }
        // Миллиарды (10^9)
        else if (price >= 1000000000.0)
        {
            double billions = price / 1000000000.0;
            return ((int)billions, "B");
        }
        // Миллионы (10^6)
        else if (price >= 1000000.0)
        {
            double millions = price / 1000000.0;
            return ((int)millions, "M");
        }
        // Тысячи (10^3)
        else if (price >= 1000.0)
        {
            double thousands = price / 1000.0;
            return ((int)thousands, "K");
        }
        else
        {
            return ((int)price, "");
        }
    }
}
