using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Модальное окно магазина ультимейтов.
/// Позволяет покупать различные ультимейты (IsStrongBeat1, IsStrongBeat2 и т.д.).
/// </summary>
public class UltimateShop : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Панель магазина (модальное окно)")]
    [SerializeField] private GameObject shopPanel;
    
    [Tooltip("Кнопка закрытия магазина")]
    [SerializeField] private Button closeButton;
    
    [Tooltip("Контейнер для списка ультимейтов")]
    [SerializeField] private Transform ultimatesContainer;
    
    [Tooltip("Префаб элемента ультимейта в списке")]
    [SerializeField] private GameObject ultimateItemPrefab;
    
    [Header("Ultimate Settings")]
    [Tooltip("Список доступных ультимейтов")]
    [SerializeField] private List<UltimateData> availableUltimates = new List<UltimateData>();
    
    [Header("References")]
    [Tooltip("Ссылка на GameStorage")]
    [SerializeField] private GameStorage gameStorage;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private bool isShopOpen = false;
    
    [System.Serializable]
    public class UltimateData
    {
        public string ultimateName; // Название триггера анимации (IsStrongBeat1, IsStrongBeat2 и т.д.)
        public string displayName; // Отображаемое имя
        public long cost; // Стоимость покупки
        public bool isPurchased; // Куплен ли ультимейт
    }
    
    private void Awake()
    {
        // Находим GameStorage если не назначен
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        // Подписываемся на кнопку закрытия
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseShop);
        }
        
        // Скрываем магазин по умолчанию
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }
    
    private void Start()
    {
        // Загружаем данные о купленных ультимейтах
        LoadPurchasedUltimates();
        
        // Обновляем список ультимейтов
        UpdateUltimatesList();
    }
    
    /// <summary>
    /// Открывает магазин ультимейтов
    /// </summary>
    public void OpenShop()
    {
        if (isShopOpen) return;
        
        isShopOpen = true;
        
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }
        
        // Обновляем список ультимейтов
        UpdateUltimatesList();
        
        if (debug)
        {
            Debug.Log("[UltimateShop] Магазин открыт");
        }
    }
    
    /// <summary>
    /// Закрывает магазин ультимейтов
    /// </summary>
    public void CloseShop()
    {
        if (!isShopOpen) return;
        
        isShopOpen = false;
        
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
        
        if (debug)
        {
            Debug.Log("[UltimateShop] Магазин закрыт");
        }
    }
    
    /// <summary>
    /// Переключает состояние магазина (открыть/закрыть)
    /// </summary>
    public void ToggleShop()
    {
        if (isShopOpen)
        {
            CloseShop();
        }
        else
        {
            OpenShop();
        }
    }
    
    /// <summary>
    /// Загружает данные о купленных ультимейтах из GameStorage
    /// </summary>
    private void LoadPurchasedUltimates()
    {
        if (gameStorage == null) return;
        
        string currentUltimate = gameStorage.GetCurrentUltimate();
        
        // Помечаем текущий ультимейт как купленный
        foreach (UltimateData ultimate in availableUltimates)
        {
            if (ultimate.ultimateName == currentUltimate)
            {
                ultimate.isPurchased = true;
            }
        }
    }
    
    /// <summary>
    /// Обновляет список ультимейтов в UI
    /// </summary>
    private void UpdateUltimatesList()
    {
        if (ultimatesContainer == null) return;
        
        // Очищаем контейнер
        foreach (Transform child in ultimatesContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Создаем элементы для каждого ультимейта
        foreach (UltimateData ultimate in availableUltimates)
        {
            CreateUltimateItem(ultimate);
        }
    }
    
    /// <summary>
    /// Создает элемент ультимейта в списке
    /// </summary>
    private void CreateUltimateItem(UltimateData ultimate)
    {
        if (ultimateItemPrefab == null)
        {
            Debug.LogWarning("[UltimateShop] Префаб элемента ультимейта не назначен!");
            return;
        }
        
        GameObject item = Instantiate(ultimateItemPrefab, ultimatesContainer);
        
        // Находим компоненты UI
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        Button buyButton = item.GetComponentInChildren<Button>();
        TextMeshProUGUI costText = null;
        
        // Ищем текст стоимости
        TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        foreach (TextMeshProUGUI text in texts)
        {
            if (text != nameText)
            {
                costText = text;
                break;
            }
        }
        
        // Устанавливаем имя
        if (nameText != null)
        {
            nameText.text = ultimate.displayName;
        }
        
        // Устанавливаем стоимость
        if (costText != null)
        {
            if (ultimate.isPurchased)
            {
                costText.text = "Куплено";
            }
            else
            {
                costText.text = FormatCost(ultimate.cost);
            }
        }
        
        // Настраиваем кнопку покупки
        if (buyButton != null)
        {
            if (ultimate.isPurchased)
            {
                // Если ультимейт куплен, кнопка становится кнопкой выбора
                buyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Выбрать";
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => SelectUltimate(ultimate));
            }
            else
            {
                // Если не куплен, кнопка для покупки
                buyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Купить";
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => BuyUltimate(ultimate));
            }
        }
    }
    
    /// <summary>
    /// Покупает ультимейт
    /// </summary>
    private void BuyUltimate(UltimateData ultimate)
    {
        if (gameStorage == null)
        {
            Debug.LogError("[UltimateShop] GameStorage не найден!");
            return;
        }
        
        // Проверяем, достаточно ли средств
        double currentBalance = gameStorage.GetBalanceDouble();
        if (currentBalance < ultimate.cost)
        {
            if (debug)
            {
                Debug.Log($"[UltimateShop] Недостаточно средств для покупки {ultimate.displayName}. Нужно: {ultimate.cost}, есть: {currentBalance}");
            }
            return;
        }
        
        // Вычитаем стоимость
        bool success = gameStorage.SubtractBalanceLong(ultimate.cost);
        
        if (success)
        {
            // Помечаем как купленный
            ultimate.isPurchased = true;
            
            // Выбираем ультимейт
            SelectUltimate(ultimate);
            
            // Обновляем список
            UpdateUltimatesList();
            
            if (debug)
            {
                Debug.Log($"[UltimateShop] Ультимейт {ultimate.displayName} куплен и выбран");
            }
        }
    }
    
    /// <summary>
    /// Выбирает ультимейт
    /// </summary>
    private void SelectUltimate(UltimateData ultimate)
    {
        if (gameStorage == null)
        {
            Debug.LogError("[UltimateShop] GameStorage не найден!");
            return;
        }
        
        // Устанавливаем текущий ультимейт
        gameStorage.SetCurrentUltimate(ultimate.ultimateName);
        
        if (debug)
        {
            Debug.Log($"[UltimateShop] Выбран ультимейт: {ultimate.displayName}");
        }
    }
    
    /// <summary>
    /// Форматирует стоимость для отображения
    /// </summary>
    private string FormatCost(long cost)
    {
        if (gameStorage != null)
        {
            return gameStorage.FormatBalance(cost);
        }
        
        return cost.ToString();
    }
}
