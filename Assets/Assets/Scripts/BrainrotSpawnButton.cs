using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Кнопка для получения брейнрота.
/// При взаимодействии спавнит случайный брейнрот рядом с кнопкой.
/// </summary>
public class BrainrotSpawnButton : InteractableObject
{
    [Header("Настройки спавна")]
    [Tooltip("Смещение позиции спавна относительно кнопки")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 2f);
    
    [Tooltip("Радиус для поиска свободного места для спавна")]
    [SerializeField] private float spawnRadius = 3f;
    
    [Header("Шансы редкостей (в процентах, 0-100)")]
    [Tooltip("Шанс появления Common брейнрота (0-100)")]
    [SerializeField] private float commonChance = 50f;
    
    [Tooltip("Шанс появления Rare брейнрота (0-100)")]
    [SerializeField] private float rareChance = 25f;
    
    [Tooltip("Шанс появления Exclusive брейнрота (0-100)")]
    [SerializeField] private float exclusiveChance = 15f;
    
    [Tooltip("Шанс появления Epic брейнрота (0-100)")]
    [SerializeField] private float epicChance = 7f;
    
    [Tooltip("Шанс появления Mythic брейнрота (0-100)")]
    [SerializeField] private float mythicChance = 2f;
    
    [Tooltip("Шанс появления Legendary брейнрота (0-100)")]
    [SerializeField] private float legendaryChance = 0.8f;
    
    [Tooltip("Шанс появления Secret брейнрота (0-100)")]
    [SerializeField] private float secretChance = 0.2f;
    
    [Header("BaseIncome")]
    [Tooltip("Минимальный базовый доход")]
    [SerializeField] private long baseIncomeMin = 100;
    
    [Tooltip("Максимальный базовый доход")]
    [SerializeField] private long baseIncomeMax = 1000;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    // Загруженные префабы брейнротов
    private GameObject[] brainrotPrefabs;
    
    // Список всех редкостей в порядке приоритета
    private readonly string[] allRarities = { "Common", "Rare", "Exclusive", "Epic", "Mythic", "Legendary", "Secret" };
    
    // Нормализованные шансы редкостей
    private System.Collections.Generic.Dictionary<string, float> normalizedRarityChances = new System.Collections.Generic.Dictionary<string, float>();
    
    private void Awake()
    {
        // Загружаем префабы брейнротов
        LoadBrainrotPrefabs();
        
        // Нормализуем шансы редкостей
        NormalizeRarityChances();
    }
    
    private void Start()
    {
        // Включаем debugMode для диагностики, если включен debug в BrainrotSpawnButton
        if (debug)
        {
            // Используем рефлексию для установки debugMode в базовом классе
            System.Reflection.FieldInfo debugModeField = typeof(InteractableObject).GetField("debugMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (debugModeField != null)
            {
                debugModeField.SetValue(this, true);
                Debug.Log($"[BrainrotSpawnButton] {gameObject.name}: Debug mode включен для InteractableObject");
            }
            
            // Проверяем наличие UI Prefab
            System.Reflection.FieldInfo uiPrefabField = typeof(InteractableObject).GetField("uiPrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (uiPrefabField != null)
            {
                GameObject uiPrefab = (GameObject)uiPrefabField.GetValue(this);
                if (uiPrefab == null)
                {
                    Debug.LogError($"[BrainrotSpawnButton] {gameObject.name}: UI Prefab не назначен! Назначьте UI Prefab в инспекторе InteractableObject.");
                }
                else
                {
                    Debug.Log($"[BrainrotSpawnButton] {gameObject.name}: UI Prefab назначен: {uiPrefab.name}");
                }
            }
        }
    }
    
    /// <summary>
    /// Переопределяем CompleteInteraction для спавна брейнрота
    /// </summary>
    protected override void OnInteractionComplete()
    {
        SpawnBrainrot();
    }
    
    /// <summary>
    /// Переопределяем CompleteInteraction, чтобы не уничтожать UI и сбрасывать состояние для повторного использования
    /// </summary>
    protected override void CompleteInteraction()
    {
        if (debug)
        {
            Debug.Log($"[BrainrotSpawnButton] {gameObject.name}: CompleteInteraction вызван");
        }
        
        // Вызываем виртуальный метод для спавна брейнрота
        OnInteractionComplete();
        
        // ПРИМЕЧАНИЕ: Событие onInteractionComplete не вызывается здесь, так как оно приватное в базовом классе.
        // Если нужно вызвать события, настройте их в инспекторе через UnityEvent или используйте другой подход.
        
        // НЕ уничтожаем UI - оставляем его для повторного использования
        // НЕ устанавливаем interactionCompleted = true - сбрасываем его для повторного использования
        
        // Сбрасываем состояние взаимодействия, чтобы можно было взаимодействовать снова
        // Это сбросит прогресс и interactionCompleted
        ResetInteraction();
        
        if (debug)
        {
            Debug.Log($"[BrainrotSpawnButton] {gameObject.name}: Состояние взаимодействия сброшено, UI должен остаться видимым");
        }
    }
    
    /// <summary>
    /// Загружает префабы брейнротов из Resources
    /// </summary>
    private void LoadBrainrotPrefabs()
    {
#if UNITY_EDITOR
        // В редакторе используем AssetDatabase для загрузки из папки
        string folderPath = "Assets/Assets/Resources/game/Brainrots";
        System.Collections.Generic.List<GameObject> prefabsList = new System.Collections.Generic.List<GameObject>();
        
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<BrainrotObject>() != null)
            {
                prefabsList.Add(prefab);
            }
        }
        
        brainrotPrefabs = prefabsList.ToArray();
#else
        // В билде используем Resources (путь относительно папки Resources)
        brainrotPrefabs = Resources.LoadAll<GameObject>("game/Brainrots");
        
        // Фильтруем только те, у которых есть компонент BrainrotObject
        System.Collections.Generic.List<GameObject> filteredPrefabs = new System.Collections.Generic.List<GameObject>();
        foreach (GameObject prefab in brainrotPrefabs)
        {
            if (prefab != null && prefab.GetComponent<BrainrotObject>() != null)
            {
                filteredPrefabs.Add(prefab);
            }
        }
        brainrotPrefabs = filteredPrefabs.ToArray();
#endif
        
        if (brainrotPrefabs == null || brainrotPrefabs.Length == 0)
        {
            Debug.LogError("[BrainrotSpawnButton] Не удалось загрузить префабы брейнротов! Проверьте путь к папке.");
        }
        else if (debug)
        {
            Debug.Log($"[BrainrotSpawnButton] Загружено {brainrotPrefabs.Length} префабов брейнротов");
        }
    }
    
    /// <summary>
    /// Нормализует шансы редкостей (сумма = 100%)
    /// </summary>
    private void NormalizeRarityChances()
    {
        // Создаем словарь для исходных шансов
        System.Collections.Generic.Dictionary<string, float> originalChances = new System.Collections.Generic.Dictionary<string, float>
        {
            { "Common", commonChance },
            { "Rare", rareChance },
            { "Exclusive", exclusiveChance },
            { "Epic", epicChance },
            { "Mythic", mythicChance },
            { "Legendary", legendaryChance },
            { "Secret", secretChance }
        };
        
        // Вычисляем сумму указанных шансов (не равных 0)
        float sumOfSpecified = 0f;
        int countOfUnspecified = 0;
        
        foreach (string rarity in allRarities)
        {
            float chance = originalChances[rarity];
            if (chance > 0f)
            {
                sumOfSpecified += chance;
            }
            else
            {
                countOfUnspecified++;
            }
        }
        
        // Инициализируем нормализованные шансы
        normalizedRarityChances.Clear();
        
        if (sumOfSpecified > 100f)
        {
            // Если сумма > 100, нормализуем все пропорционально
            float scale = 100f / sumOfSpecified;
            foreach (string rarity in allRarities)
            {
                float chance = originalChances[rarity];
                normalizedRarityChances[rarity] = chance * scale;
            }
        }
        else if (sumOfSpecified < 100f)
        {
            // Если сумма < 100, распределяем остаток поровну между неуказанными
            float remaining = 100f - sumOfSpecified;
            float perUnspecified = countOfUnspecified > 0 ? remaining / countOfUnspecified : 0f;
            
            foreach (string rarity in allRarities)
            {
                float chance = originalChances[rarity];
                if (chance > 0f)
                {
                    normalizedRarityChances[rarity] = chance;
                }
                else
                {
                    normalizedRarityChances[rarity] = perUnspecified;
                }
            }
        }
        else
        {
            // Если сумма = 100, используем как есть
            foreach (string rarity in allRarities)
            {
                normalizedRarityChances[rarity] = originalChances[rarity];
            }
        }
    }
    
    /// <summary>
    /// Выбирает случайную редкость на основе нормализованных шансов
    /// </summary>
    private string GetRandomRarity()
    {
        // Генерируем случайное число от 0 до 100
        float randomValue = Random.Range(0f, 100f);
        
        // Проходим по редкостям и находим диапазон, в который попало число
        float currentSum = 0f;
        foreach (string rarity in allRarities)
        {
            float chance = normalizedRarityChances[rarity];
            if (randomValue >= currentSum && randomValue < currentSum + chance)
            {
                return rarity;
            }
            currentSum += chance;
        }
        
        // Если не попали ни в один диапазон (из-за ошибок округления), возвращаем последнюю
        return allRarities[allRarities.Length - 1];
    }
    
    /// <summary>
    /// Спавнит случайный брейнрот рядом с кнопкой
    /// </summary>
    private void SpawnBrainrot()
    {
        if (brainrotPrefabs == null || brainrotPrefabs.Length == 0)
        {
            Debug.LogError("[BrainrotSpawnButton] Нет префабов для спавна!");
            return;
        }
        
        // Выбираем случайную редкость
        string selectedRarity = GetRandomRarity();
        
        // Выбираем случайный префаб из загруженных
        GameObject prefabToSpawn = brainrotPrefabs[Random.Range(0, brainrotPrefabs.Length)];
        
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("[BrainrotSpawnButton] Префаб null при попытке спавна");
            return;
        }
        
        // Вычисляем позицию спавна
        Vector3 spawnPosition = GetSpawnPosition();
        
        // Получаем spawnRotationY из префаба (если есть компонент BrainrotObject на префабе)
        float rotationY = 0f;
        BrainrotObject prefabBrainrot = prefabToSpawn.GetComponent<BrainrotObject>();
        if (prefabBrainrot != null)
        {
            // Используем рефлексию для получения spawnRotationY (приватное поле)
            System.Reflection.FieldInfo spawnRotationYField = typeof(BrainrotObject).GetField("spawnRotationY",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (spawnRotationYField != null)
            {
                rotationY = (float)spawnRotationYField.GetValue(prefabBrainrot);
            }
        }
        
        // Если spawnRotationY не задан (0), используем случайный поворот
        if (Mathf.Abs(rotationY) < 0.01f)
        {
            rotationY = Random.Range(0f, 360f);
        }
        
        Quaternion spawnRotation = Quaternion.Euler(0f, rotationY, 0f);
        
        // Создаем экземпляр
        GameObject spawnedObject = Instantiate(prefabToSpawn, spawnPosition, spawnRotation);
        
        if (spawnedObject == null)
        {
            Debug.LogWarning("[BrainrotSpawnButton] Не удалось создать экземпляр префаба");
            return;
        }
        
        // Получаем компонент BrainrotObject
        BrainrotObject brainrotObject = spawnedObject.GetComponent<BrainrotObject>();
        if (brainrotObject == null)
        {
            Debug.LogWarning($"[BrainrotSpawnButton] У спавненного объекта {spawnedObject.name} нет компонента BrainrotObject!");
            Destroy(spawnedObject);
            return;
        }
        
        // Устанавливаем редкость
        brainrotObject.SetRarity(selectedRarity);
        
        // Генерируем случайный baseIncome
        long randomBaseIncome = Random.Range((int)baseIncomeMin, (int)baseIncomeMax + 1);
        brainrotObject.SetBaseIncome(randomBaseIncome);
        
        if (debug)
        {
            Debug.Log($"[BrainrotSpawnButton] Заспавнен брейнрот: {brainrotObject.GetObjectName()}, редкость: {selectedRarity}, доход: {randomBaseIncome}");
        }
    }
    
    /// <summary>
    /// Получает позицию для спавна брейнрота
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        // Вычисляем базовую позицию с учетом смещения
        Vector3 basePosition = transform.position + transform.TransformDirection(spawnOffset);
        
        // Пытаемся найти свободное место в радиусе
        Vector3 spawnPosition = basePosition;
        
        // Используем Raycast для определения высоты поверхности
        RaycastHit hit;
        Vector3 rayStart = new Vector3(basePosition.x, basePosition.y + 5f, basePosition.z);
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f))
        {
            spawnPosition = hit.point;
        }
        else
        {
            // Если Raycast не нашел поверхность, используем базовую позицию
            spawnPosition = basePosition;
        }
        
        return spawnPosition;
    }
}
