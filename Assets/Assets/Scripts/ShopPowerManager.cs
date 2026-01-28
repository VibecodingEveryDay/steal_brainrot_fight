using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

/// <summary>
/// Менеджер для управления покупками улучшений силы в магазине
/// Управляет отображением силы и покупками через Bar1, Bar2, Bar3
/// </summary>
public class ShopPowerManager : MonoBehaviour
{
    private const int MAX_LEVEL = 60; // Максимальный уровень силы
    
    [Header("Power Upgrade Settings")]
    [Tooltip("Стартовый уровень силы игрока (устанавливается при первом запуске, если текущий уровень = 0)")]
    [SerializeField] private int startingPowerLevel = 10;
    
    [Header("Price Settings")]
    [Tooltip("Цены за уровни от 0 до 60. Если цена не указана (-1), будет интерполирована между ближайшими указанными ценами")]
    [SerializeField] private long[] levelPrices = new long[61];
    
    [Header("References")]
    [Tooltip("Transform, содержащий все Bar объекты (Bar1, Bar2, Bar3)")]
    [SerializeField] private Transform powerBarsContainer;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    [System.Serializable]
    public class PowerBar
    {
        public string barName;
        public Transform barTransform;
        public TextMeshProUGUI powerText1; // Текущий уровень
        public TextMeshProUGUI powerText2; // Будущий уровень
        public TextMeshProUGUI priceText; // Цена
        public Button button;
        public int powerAmount; // Количество уровней, добавляемых при покупке (1, 5 или 10)
    }
    
    // Список баров (всегда 3: Bar1, Bar2, Bar3)
    private List<PowerBar> powerBars = new List<PowerBar>();
    
    private GameStorage gameStorage;
    
    // Для отслеживания изменений баланса
    private double lastBalance = -1;
    private float balanceCheckInterval = 0.1f; // Проверяем баланс каждые 0.1 секунды
    private float balanceCheckTimer = 0f;
    
    private void Awake()
    {
        // Автоматически находим контейнер с барами, если не назначен
        if (powerBarsContainer == null)
        {
            // Сначала пытаемся найти PowerModalContainer в сцене
            GameObject powerModalContainer = GameObject.Find("PowerModalContainer");
            if (powerModalContainer != null)
            {
                powerBarsContainer = powerModalContainer.transform;
                if (debug)
                {
                    Debug.Log($"[ShopPowerManager] PowerModalContainer найден через GameObject.Find: {powerModalContainer.name}");
                }
            }
            else
            {
                // Если не найден в сцене, ищем в родительском объекте
                Transform parent = transform.parent;
                if (parent != null && (parent.name == "PowerModalContainer" || parent.name.Contains("Power")))
                {
                    powerBarsContainer = parent;
                    if (debug)
                    {
                        Debug.Log($"[ShopPowerManager] Контейнер найден через родительский объект: {parent.name}");
                    }
                }
                else
                {
                    // Если ничего не найдено, используем текущий transform
                    powerBarsContainer = transform;
                    if (debug)
                    {
                        Debug.Log($"[ShopPowerManager] Используется текущий transform: {transform.name}");
                    }
                }
            }
        }
        
        if (debug && powerBarsContainer != null)
        {
            Debug.Log($"[ShopPowerManager] Контейнер для поиска баров: {powerBarsContainer.name}");
            // Выводим список дочерних объектов для отладки
            foreach (Transform child in powerBarsContainer)
            {
                Debug.Log($"[ShopPowerManager] Дочерний объект найден: {child.name}");
            }
        }
        
        // Инициализируем массив цен, если он не инициализирован
        if (levelPrices == null)
        {
            levelPrices = new long[61];
            // Инициализируем все цены как -1 (не указано)
            for (int i = 0; i < levelPrices.Length; i++)
            {
                levelPrices[i] = -1;
            }
            if (debug)
            {
                Debug.Log("[ShopPowerManager] Массив levelPrices инициализирован как null");
            }
        }
        else if (levelPrices.Length != 61)
        {
            // Если длина не 61, создаем новый массив и копируем существующие значения
            long[] oldPrices = levelPrices;
            levelPrices = new long[61];
            for (int i = 0; i < levelPrices.Length; i++)
            {
                if (i < oldPrices.Length)
                {
                    levelPrices[i] = oldPrices[i];
                }
                else
                {
                    levelPrices[i] = -1;
                }
            }
            if (debug)
            {
                Debug.Log($"[ShopPowerManager] Массив levelPrices изменен с длины {oldPrices.Length} на 61");
            }
        }
        
        // ВАЖНО: Проверяем, что массив не пуст (все значения = -1)
        // Если массив пуст, устанавливаем временные значения
        // НО НЕ перезаписываем значения, которые уже установлены в инспекторе!
        int specifiedPrices = 0;
        for (int i = 0; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] >= 0)
            {
                specifiedPrices++;
            }
        }
        
        Debug.Log($"[ShopPowerManager] В Awake(): Всего указано цен: {specifiedPrices} из {levelPrices.Length}");
        
        // Выводим все указанные цены для отладки
        for (int i = 0; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] >= 0)
            {
                Debug.Log($"[ShopPowerManager] В Awake(): Уровень {i}: цена = {levelPrices[i]}");
            }
        }
        
        if (specifiedPrices == 0)
        {
            Debug.LogWarning("[ShopPowerManager] ВНИМАНИЕ: В массиве Level Prices не указано ни одной цены! Все значения = -1. Устанавливаю временные значения по умолчанию для тестирования.");
            
            // ВАЖНО: Устанавливаем временные значения по умолчанию для тестирования
            // В реальной игре эти значения должны быть заполнены в инспекторе Unity
            // Устанавливаем цены для уровней 10, 20, 30, 40, 50, 60 для интерполяции
            levelPrices[10] = 1000;
            levelPrices[20] = 5000;
            levelPrices[30] = 15000;
            levelPrices[40] = 50000;
            levelPrices[50] = 150000;
            levelPrices[60] = 500000;
            
            Debug.LogWarning("[ShopPowerManager] Установлены временные значения по умолчанию. НЕ ЗАБУДЬТЕ заполнить массив Level Prices в инспекторе Unity!");
        }
        
        // Автоматически находим и настраиваем Bar объекты (всегда 3)
        if (powerBars.Count == 0)
        {
            SetupPowerBars();
        }
    }
    
    private void Start()
    {
        // Получаем ссылки на необходимые компоненты
        gameStorage = GameStorage.Instance;
        
        if (gameStorage == null)
        {
            Debug.LogError("[ShopPowerManager] GameStorage.Instance не найден!");
        }
        
        // ВАЖНО: Проверяем, что временные значения установлены, если массив пуст
        int specifiedPrices = 0;
        for (int i = 0; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] >= 0)
            {
                specifiedPrices++;
            }
        }
        
        Debug.Log($"[ShopPowerManager] В Start(): Всего указано цен: {specifiedPrices} из {levelPrices.Length}");
        
        // Выводим все указанные цены для отладки
        for (int i = 0; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] >= 0)
            {
                Debug.Log($"[ShopPowerManager] В Start(): Уровень {i}: цена = {levelPrices[i]}");
            }
        }
        
        // ВАЖНО: Если массив пуст или нет цен для уровней 10-60, устанавливаем временные значения
        bool hasPricesForLevels10To60 = false;
        for (int i = 10; i <= 60; i++)
        {
            if (levelPrices[i] >= 0)
            {
                hasPricesForLevels10To60 = true;
                break;
            }
        }
        
        if (specifiedPrices == 0 || !hasPricesForLevels10To60)
        {
            Debug.LogWarning($"[ShopPowerManager] В Start(): Массив цен пуст (specifiedPrices={specifiedPrices}) или нет цен для уровней 10-60 (hasPricesForLevels10To60={hasPricesForLevels10To60})! Устанавливаю временные значения.");
            levelPrices[10] = 1000;
            levelPrices[20] = 5000;
            levelPrices[30] = 15000;
            levelPrices[40] = 50000;
            levelPrices[50] = 150000;
            levelPrices[60] = 500000;
            Debug.LogWarning("[ShopPowerManager] Временные значения установлены в Start(). Проверяю установку:");
            for (int i = 10; i <= 60; i += 10)
            {
                Debug.Log($"[ShopPowerManager] Уровень {i}: цена = {levelPrices[i]}");
            }
        }
        
        // Устанавливаем стартовый уровень силы, если текущий уровень = 0
        if (gameStorage != null)
        {
            int currentLevel = gameStorage.GetAttackPowerLevel();
            if (currentLevel == 0 && startingPowerLevel > 0)
            {
                gameStorage.SetAttackPowerLevel(startingPowerLevel);
                gameStorage.Save();
                if (debug)
                {
                    Debug.Log($"[ShopPowerManager] Установлен стартовый уровень силы: {startingPowerLevel} (текущий уровень был: 0)");
                }
            }
        }
        
        // Обновляем UI
        UpdateAllPowerBars();
        
        // Инициализируем lastBalance для отслеживания изменений
        if (gameStorage != null)
        {
            lastBalance = gameStorage.GetBalanceDouble();
        }
    }
    
    private void OnEnable()
    {
        // Обновляем UI при активации
        if (gameStorage != null)
        {
            UpdateAllPowerBars();
        }
    }
    
    private void Update()
    {
        // Обновляем UI только если модальное окно активно
        if (powerBarsContainer != null && powerBarsContainer.gameObject.activeInHierarchy)
        {
            // Проверяем баланс с интервалом для оптимизации
            balanceCheckTimer += Time.deltaTime;
            if (balanceCheckTimer >= balanceCheckInterval)
            {
                balanceCheckTimer = 0f;
                
                if (gameStorage == null)
                {
                    gameStorage = GameStorage.Instance;
                }
                
                if (gameStorage != null)
                {
                    double currentBalance = gameStorage.GetBalanceDouble();
                    
                    // Если баланс изменился, обновляем UI
                    if (lastBalance < 0 || Math.Abs(currentBalance - lastBalance) > 0.0001)
                    {
                        lastBalance = currentBalance;
                        UpdateAllPowerBars();
                        
                        if (debug)
                        {
                            Debug.Log($"[ShopPowerManager] Баланс изменился до {currentBalance}, обновляю UI");
                        }
                    }
                }
            }
        }
        else
        {
            // Если модальное окно неактивно, сбрасываем таймер
            balanceCheckTimer = 0f;
        }
    }
    
    /// <summary>
    /// Автоматически настраивает PowerBar объекты из иерархии
    /// </summary>
    private void SetupPowerBars()
    {
        // Ищем Bar1, Bar2, Bar3 в дочерних объектах (поиск нечувствителен к регистру)
        Transform bar1 = FindChildByName(powerBarsContainer, "Bar1") ?? FindChildByName(powerBarsContainer, "bar1");
        Transform bar2 = FindChildByName(powerBarsContainer, "Bar2") ?? FindChildByName(powerBarsContainer, "bar2");
        Transform bar3 = FindChildByName(powerBarsContainer, "Bar3") ?? FindChildByName(powerBarsContainer, "bar3");
        
        powerBars.Clear();
        
        // Настраиваем Bar1 (всегда добавляет +1 к уровню)
        if (bar1 != null)
        {
            PowerBar bar = CreatePowerBar(bar1, "Bar1", 1);
            powerBars.Add(bar);
        }
        
        // Настраиваем Bar2 (всегда добавляет +5 к уровню)
        if (bar2 != null)
        {
            PowerBar bar = CreatePowerBar(bar2, "Bar2", 5);
            powerBars.Add(bar);
        }
        
        // Настраиваем Bar3 (всегда добавляет +10 к уровню)
        if (bar3 != null)
        {
            PowerBar bar = CreatePowerBar(bar3, "Bar3", 10);
            powerBars.Add(bar);
        }
        
        if (powerBars.Count == 0 && debug)
        {
            Debug.LogWarning("[ShopPowerManager] Не найдено ни одного Bar объекта (Bar1, Bar2, Bar3)!");
        }
    }
    
    /// <summary>
    /// Создает PowerBar из Transform
    /// </summary>
    private PowerBar CreatePowerBar(Transform barTransform, string name, int powerAmount)
    {
        PowerBar bar = new PowerBar
        {
            barName = name,
            barTransform = barTransform,
            powerAmount = powerAmount
        };
        
        // Ищем PowerText1, PowerText2 и Price
        bar.powerText1 = FindChildComponent<TextMeshProUGUI>(barTransform, "PowerText1");
        bar.powerText2 = FindChildComponent<TextMeshProUGUI>(barTransform, "PowerText2");
        bar.priceText = FindChildComponent<TextMeshProUGUI>(barTransform, "Price") ?? 
                        FindChildComponent<TextMeshProUGUI>(barTransform, "price");
        
        // Отладочная информация о найденных компонентах
        if (debug)
        {
            Debug.Log($"[ShopPowerManager] Bar '{name}' - PowerText1: {(bar.powerText1 != null ? bar.powerText1.name : "НЕ НАЙДЕН")}, " +
                      $"PowerText2: {(bar.powerText2 != null ? bar.powerText2.name : "НЕ НАЙДЕН")}, " +
                      $"Price: {(bar.priceText != null ? bar.priceText.name : "НЕ НАЙДЕН")}");
        }
        
        // Ищем Button
        bar.button = FindChildComponent<Button>(barTransform, "Button");
        if (bar.button != null)
        {
            // Добавляем обработчик клика
            bar.button.onClick.RemoveAllListeners();
            int powerAmountCopy = powerAmount; // Копируем для замыкания
            bar.button.onClick.AddListener(() => OnBuyPowerButtonClicked(powerAmountCopy));
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
    /// Обновляет все PowerBar UI
    /// </summary>
    public void UpdateAllPowerBars()
    {
        // Убеждаемся, что gameStorage инициализирован
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        if (gameStorage == null)
        {
            if (debug)
            {
                Debug.LogWarning("[ShopPowerManager] GameStorage недоступен, не могу обновить PowerBars");
            }
            return;
        }
        
        int currentLevel = gameStorage.GetAttackPowerLevel();
        
        if (debug)
        {
            Debug.Log($"[ShopPowerManager] Обновление всех PowerBars - Уровень игрока: {currentLevel}");
        }
        
        foreach (PowerBar bar in powerBars)
        {
            UpdatePowerBar(bar, currentLevel);
        }
    }
    
    /// <summary>
    /// Обновляет UI конкретного PowerBar
    /// </summary>
    private void UpdatePowerBar(PowerBar bar, int currentLevel)
    {
        // Проверяем, достигнут ли максимальный уровень
        bool isMaxLevel = currentLevel >= MAX_LEVEL;
        
        // Вычисляем будущий уровень с учетом ограничения в 60
        int futureLevel = currentLevel + bar.powerAmount;
        int actualLevelsToAdd = bar.powerAmount;
        
        // Если будущий уровень превышает максимум, ограничиваем до 60
        if (futureLevel > MAX_LEVEL)
        {
            futureLevel = MAX_LEVEL;
            actualLevelsToAdd = MAX_LEVEL - currentLevel;
        }
        
        // Вычисляем цену покупки (только за реально доступные уровни)
        long price = 0;
        if (!isMaxLevel && actualLevelsToAdd > 0)
        {
            price = CalculatePriceForLevels(currentLevel, actualLevelsToAdd);
            
            // ВАЖНО: Если цена = 0, это означает, что массив цен не заполнен
            if (price == 0 && actualLevelsToAdd > 0)
            {
                Debug.LogError($"[ShopPowerManager] ВНИМАНИЕ: Цена = 0 для уровня {currentLevel}! Проверьте, что массив Level Prices заполнен в инспекторе Unity. Укажите цены хотя бы для уровней 10, 20, 30, 40, 50, 60.");
            }
            
            if (debug)
            {
                Debug.Log($"[ShopPowerManager] UpdatePowerBar для '{bar.barName}': currentLevel={currentLevel}, actualLevelsToAdd={actualLevelsToAdd}, calculated price={price}");
            }
        }
        
        // Обновляем PowerText1 (текущий уровень)
        if (bar.powerText1 != null)
        {
            bar.powerText1.text = currentLevel.ToString();
            if (debug)
            {
                Debug.Log($"[ShopPowerManager] Bar '{bar.barName}' - Уровень: {currentLevel}, PowerText1 обновлен: {bar.powerText1.text}");
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[ShopPowerManager] PowerText1 не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем PowerText2 (будущий уровень)
        if (bar.powerText2 != null)
        {
            bar.powerText2.text = futureLevel.ToString();
            if (debug)
            {
                Debug.Log($"[ShopPowerManager] Bar '{bar.barName}' - Уровень: {currentLevel}, PowerText2 обновлен: {bar.powerText2.text} (будущий уровень: {futureLevel})");
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[ShopPowerManager] PowerText2 не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем PriceText (цена покупки) - форматируем через GameStorage
        if (bar.priceText != null)
        {
            if (isMaxLevel)
            {
                bar.priceText.text = "Макс. ур";
                if (debug)
                {
                    Debug.Log($"[ShopPowerManager] Bar '{bar.barName}' - Максимальный уровень достигнут, PriceText: 'Макс. ур'");
                }
            }
            else
            {
                string formattedPrice = gameStorage != null ? gameStorage.FormatBalance((double)price) : price.ToString();
                bar.priceText.text = formattedPrice;
                
                // Проверяем баланс и меняем цвет цены
                if (gameStorage != null && price > 0)
                {
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
                else if (price == 0)
                {
                    // Если цена 0, устанавливаем белый цвет
                    bar.priceText.color = Color.white;
                }
                
                if (debug)
                {
                    double balance = gameStorage != null ? gameStorage.GetBalanceDouble() : 0;
                    Debug.Log($"[ShopPowerManager] Bar '{bar.barName}' - Уровень: {currentLevel}, PriceText обновлен: {bar.priceText.text} (raw price: {price}, balance: {balance}, color: {bar.priceText.color})");
                }
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[ShopPowerManager] PriceText не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем активность кнопки (достаточно ли денег и не достигнут ли максимум)
        if (bar.button != null)
        {
            if (isMaxLevel)
            {
                bar.button.interactable = false;
            }
            else if (gameStorage != null && price > 0)
            {
                double balance = gameStorage.GetBalanceDouble();
                bool canAfford = balance >= (double)price;
                bar.button.interactable = canAfford && actualLevelsToAdd > 0;
                
                if (debug)
                {
                    Debug.Log($"[ShopPowerManager] Bar '{bar.barName}' - Кнопка: interactable={bar.button.interactable}, balance={balance}, price={price}, canAfford={canAfford}, actualLevelsToAdd={actualLevelsToAdd}");
                }
            }
            else
            {
                bar.button.interactable = false;
            }
        }
    }
    
    /// <summary>
    /// Получает цену за уровень из массива, с интерполяцией если цена не указана
    /// </summary>
    private long GetPriceForLevel(int level)
    {
        // Проверяем, что массив инициализирован
        if (levelPrices == null || levelPrices.Length == 0)
        {
            Debug.LogError($"[ShopPowerManager] Массив levelPrices не инициализирован!");
            return 0;
        }
        
        // ВАЖНО: Проверяем, есть ли цены для уровней 10-60 (опорные точки для интерполяции)
        // Если нет, устанавливаем временные значения
        // ВАЖНО: Проверяем только цены > 0 (0 считается как "не указано")
        bool hasPricesForLevels10To60 = false;
        for (int i = 10; i <= 60; i += 10)
        {
            if (levelPrices[i] > 0)
            {
                hasPricesForLevels10To60 = true;
                break;
            }
        }
        
        // Если нет опорных точек для интерполяции, устанавливаем временные значения
        if (!hasPricesForLevels10To60)
        {
            Debug.LogWarning($"[ShopPowerManager] GetPriceForLevel: Нет цен для опорных уровней (10-60)! Устанавливаю временные значения по умолчанию.");
            // Устанавливаем только если значения еще не установлены (не перезаписываем существующие)
            if (levelPrices[10] < 0) levelPrices[10] = 1000;
            if (levelPrices[20] < 0) levelPrices[20] = 5000;
            if (levelPrices[30] < 0) levelPrices[30] = 15000;
            if (levelPrices[40] < 0) levelPrices[40] = 50000;
            if (levelPrices[50] < 0) levelPrices[50] = 150000;
            if (levelPrices[60] < 0) levelPrices[60] = 500000;
            
            // Проверяем, что значения установлены
            Debug.Log($"[ShopPowerManager] Временные значения установлены: уровень 10={levelPrices[10]}, 20={levelPrices[20]}, 30={levelPrices[30]}, 40={levelPrices[40]}, 50={levelPrices[50]}, 60={levelPrices[60]}");
        }
        
        // Подсчитываем количество указанных цен для логирования (только > 0)
        int specifiedPricesCount = 0;
        for (int i = 0; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] > 0)
            {
                specifiedPricesCount++;
            }
        }
        
        // Проверяем границы массива
        if (level < 0) level = 0;
        if (level >= levelPrices.Length) level = levelPrices.Length - 1;
        
        // ВАЖНО: Всегда логируем для диагностики проблемы
        Debug.Log($"[ShopPowerManager] GetPriceForLevel: запрос уровня {level}, массив длины {levelPrices.Length}, значение в массиве: {levelPrices[level]}, всего указано цен: {specifiedPricesCount}, hasPricesForLevels10To60: {hasPricesForLevels10To60}");
        
        // Если цена указана и больше 0 (не -1 и не 0), возвращаем её
        // ВАЖНО: 0 считается как "не указано", поэтому для него тоже нужна интерполяция
        if (levelPrices[level] > 0)
        {
            Debug.Log($"[ShopPowerManager] Цена для уровня {level} указана напрямую: {levelPrices[level]}");
            return levelPrices[level];
        }
        
        // Если цена не указана (0 или -1), ищем ближайшие указанные цены (больше 0) для интерполяции
        int lowerLevel = -1;
        int upperLevel = -1;
        
        // Ищем ближайший нижний уровень с указанной ценой (больше 0)
        for (int i = level - 1; i >= 0; i--)
        {
            if (levelPrices[i] > 0)
            {
                lowerLevel = i;
                Debug.Log($"[ShopPowerManager] Найден нижний уровень {i} с ценой {levelPrices[i]}");
                break;
            }
        }
        
        // Ищем ближайший верхний уровень с указанной ценой (больше 0)
        for (int i = level + 1; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] > 0)
            {
                upperLevel = i;
                Debug.Log($"[ShopPowerManager] Найден верхний уровень {i} с ценой {levelPrices[i]}");
                break;
            }
        }
        
        // ВАЖНО: Если не нашли опорные точки, но мы только что установили временные значения, 
        // это означает, что массив был сброшен или значения не сохранились
        if (lowerLevel < 0 && upperLevel < 0)
        {
            Debug.LogError($"[ShopPowerManager] КРИТИЧЕСКАЯ ОШИБКА: Не найдены опорные точки для уровня {level} даже после установки временных значений! Проверяю массив:");
            for (int i = 10; i <= 60; i += 10)
            {
                Debug.LogError($"[ShopPowerManager] Уровень {i}: значение в массиве = {levelPrices[i]}");
            }
        }
        
        Debug.Log($"[ShopPowerManager] GetPriceForLevel: для уровня {level} найдены: lowerLevel={lowerLevel}, upperLevel={upperLevel}");
        
        // Если нашли оба уровня, интерполируем линейно
        if (lowerLevel >= 0 && upperLevel >= 0)
        {
            long lowerPrice = levelPrices[lowerLevel];
            long upperPrice = levelPrices[upperLevel];
            
            // Линейная интерполяция с использованием double для избежания переполнения
            double t = (double)(level - lowerLevel) / (double)(upperLevel - lowerLevel);
            double priceDiff = (double)upperPrice - (double)lowerPrice;
            double interpolatedPriceDouble = (double)lowerPrice + priceDiff * t;
            long interpolatedPrice = (long)System.Math.Round(interpolatedPriceDouble);
            
            Debug.Log($"[ShopPowerManager] Интерполяция для уровня {level}: между уровнем {lowerLevel} (цена {lowerPrice}) и уровнем {upperLevel} (цена {upperPrice}), t={t}, результат: {interpolatedPrice}");
            
            return interpolatedPrice;
        }
        // Если нашли только нижний уровень
        else if (lowerLevel >= 0)
        {
            Debug.Log($"[ShopPowerManager] Используем цену нижнего уровня {lowerLevel}: {levelPrices[lowerLevel]}");
            return levelPrices[lowerLevel];
        }
        // Если нашли только верхний уровень
        else if (upperLevel >= 0)
        {
            Debug.Log($"[ShopPowerManager] Используем цену верхнего уровня {upperLevel}: {levelPrices[upperLevel]}");
            return levelPrices[upperLevel];
        }
        
        // Если ничего не найдено, проверяем, есть ли вообще указанные цены в массиве
        int totalSpecifiedPrices = 0;
        for (int i = 0; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] > 0)
            {
                totalSpecifiedPrices++;
                Debug.Log($"[ShopPowerManager] Найдена указанная цена для уровня {i}: {levelPrices[i]}");
            }
        }
        
        Debug.LogError($"[ShopPowerManager] Не найдено цен для уровня {level}. Всего указано цен в массиве: {totalSpecifiedPrices} из {levelPrices.Length}. Проверьте, что в массиве Level Prices указаны цены хотя бы для некоторых уровней (например, уровень 10, 20, 30, 40, 50, 60).");
        return 0;
    }
    
    /// <summary>
    /// Вычисляет общую цену покупки за несколько уровней
    /// Суммирует цену за каждый уровень от currentLevel до currentLevel + levelsToAdd - 1
    /// ВАЖНО: Суммируем цены за уровни currentLevel, currentLevel+1, ..., currentLevel+levelsToAdd-1
    /// </summary>
    private long CalculatePriceForLevels(int currentLevel, int levelsToAdd)
    {
        long totalPrice = 0;
        
        if (debug)
        {
            Debug.Log($"[ShopPowerManager] CalculatePriceForLevels: currentLevel={currentLevel}, levelsToAdd={levelsToAdd}");
        }
        
        // Проверяем, что массив цен инициализирован
        if (levelPrices == null || levelPrices.Length == 0)
        {
            Debug.LogError("[ShopPowerManager] Массив levelPrices не инициализирован в CalculatePriceForLevels!");
            return 0;
        }
        
        // Суммируем цену за каждый уровень
        for (int i = 0; i < levelsToAdd; i++)
        {
            int levelToCalculate = currentLevel + i;
            long priceForLevel = GetPriceForLevel(levelToCalculate);
            totalPrice += priceForLevel;
            
            if (debug)
            {
                Debug.Log($"[ShopPowerManager] Уровень {levelToCalculate}: цена за уровень = {priceForLevel}, сумма = {totalPrice}");
            }
        }
        
        if (debug)
        {
            Debug.Log($"[ShopPowerManager] CalculatePriceForLevels: итоговая цена = {totalPrice}");
        }
        
        return totalPrice;
    }
    
    /// <summary>
    /// Обработчик клика по кнопке покупки силы
    /// </summary>
    private void OnBuyPowerButtonClicked(int powerAmount)
    {
        if (gameStorage == null)
        {
            Debug.LogError("[ShopPowerManager] GameStorage недоступен!");
            return;
        }
        
        int currentLevel = gameStorage.GetAttackPowerLevel();
        
        // Проверяем, не достигнут ли максимальный уровень
        if (currentLevel >= MAX_LEVEL)
        {
            if (debug)
            {
                Debug.LogWarning($"[ShopPowerManager] Максимальный уровень ({MAX_LEVEL}) уже достигнут!");
            }
            return;
        }
        
        // Вычисляем реальное количество уровней для покупки (с учетом максимума)
        int actualLevelsToAdd = powerAmount;
        int futureLevel = currentLevel + powerAmount;
        if (futureLevel > MAX_LEVEL)
        {
            actualLevelsToAdd = MAX_LEVEL - currentLevel;
            if (debug)
            {
                Debug.Log($"[ShopPowerManager] Будущий уровень ({futureLevel}) превышает максимум ({MAX_LEVEL}). Покупаем только до {MAX_LEVEL} уровня ({actualLevelsToAdd} уровней)");
            }
        }
        
        // Вычисляем цену только за реально доступные уровни
        long price = CalculatePriceForLevels(currentLevel, actualLevelsToAdd);
        
        // Проверяем баланс
        double balance = gameStorage.GetBalanceDouble();
        if (balance < (double)price)
        {
            if (debug)
            {
                Debug.LogWarning($"[ShopPowerManager] Недостаточно средств для покупки! Требуется: {price}, есть: {balance}");
            }
            return;
        }
        
        // Вычитаем деньги - используем метод GameStorage для корректной конвертации
        // Используем SubtractBalanceLong для работы с long значениями
        bool purchaseSuccess = gameStorage.SubtractBalanceLong(price);
        
        if (purchaseSuccess)
        {
            // Увеличиваем уровень силы только на доступное количество
            gameStorage.IncreaseAttackPowerLevel(actualLevelsToAdd);
            
            // Сохраняем прогресс
            gameStorage.Save();
            
            // Обновляем UI
            UpdateAllPowerBars();
            
            if (debug)
            {
                int newLevel = gameStorage.GetAttackPowerLevel();
                int oldLevel = newLevel - actualLevelsToAdd;
                Debug.Log($"[ShopPowerManager] Сила увеличена на {actualLevelsToAdd}! Уровень изменен с {oldLevel} на {newLevel}");
            }
        }
        else
        {
            Debug.LogError($"[ShopPowerManager] Не удалось вычесть деньги из баланса!");
        }
    }
}
