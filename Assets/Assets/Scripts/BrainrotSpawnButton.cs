using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Набор шансов редкостей и диапазон baseIncome для одной области уровней (в процентах 0-100).
/// </summary>
[System.Serializable]
public class RarityChanceSet
{
    [Tooltip("Common 0-100")]
    public float commonChance = 50f;
    [Tooltip("Rare 0-100")]
    public float rareChance = 25f;
    [Tooltip("Exclusive 0-100")]
    public float exclusiveChance = 15f;
    [Tooltip("Epic 0-100")]
    public float epicChance = 7f;
    [Tooltip("Mythic 0-100")]
    public float mythicChance = 2f;
    [Tooltip("Legendary 0-100")]
    public float legendaryChance = 0.8f;
    [Tooltip("Secret 0-100")]
    public float secretChance = 0.2f;
    [Tooltip("Минимальный базовый доход для этой области")]
    public long baseIncomeMin = 100;
    [Tooltip("Максимальный базовый доход для этой области")]
    public long baseIncomeMax = 1000;
}

/// <summary>
/// Кнопка для получения брейнрота.
/// При взаимодействии спавнит случайный брейнрот рядом с кнопкой.
/// Шансы редкостей зависят от уровня силы персонажа (5 областей: 10-20, 20-30, 30-40, 40-50, 50-60).
/// </summary>
public class BrainrotSpawnButton : InteractableObject
{
    [Header("Настройки спавна")]
    [Tooltip("Смещение позиции спавна относительно кнопки")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0f, 2f);
    
    [Tooltip("Дополнительное смещение позиции спавна брейнрота (X, Y, Z) в мировых осях")]
    [SerializeField] private Vector3 brainrotSpawnOffset = Vector3.zero;
    
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
    
    [Header("Области по уровню силы (10-60). Если заданы — используются вместо шансов выше.")]
    [Tooltip("Область 1: уровни 10–20")]
    [SerializeField] private RarityChanceSet area1_10_20;
    [Tooltip("Область 2: уровни 20–30")]
    [SerializeField] private RarityChanceSet area2_20_30;
    [Tooltip("Область 3: уровни 30–40")]
    [SerializeField] private RarityChanceSet area3_30_40;
    [Tooltip("Область 4: уровни 40–50")]
    [SerializeField] private RarityChanceSet area4_40_50;
    [Tooltip("Область 5: уровни 50–60")]
    [SerializeField] private RarityChanceSet area5_50_60;
    
    [Header("Дверь")]
    [Tooltip("Родительский объект двери (Door parent с Part... внутри). Если не назначен, анимация двери не выполняется")]
    [SerializeField] private Transform doorTransform;
    
    [Tooltip("Расстояние по Y, на которое дверь уезжает вверх при открытии")]
    [SerializeField] private float doorOpenOffsetY = 3f;
    
    [Tooltip("Время открытия и время закрытия двери (в секундах)")]
    [SerializeField] private float doorOpenCloseDuration = 1f;
    
    [Header("Boss VFX")]
    [Tooltip("VFX-эффект, который появляется над заспавненным брейнротом, когда дверь начала открываться")]
    [SerializeField] private GameObject bossVfxPrefab;
    
    [Tooltip("Смещение VFX относительно позиции брейнрота (X, Y, Z)")]
    [SerializeField] private Vector3 bossVfxOffset = new Vector3(0f, 2f, 0f);
    
    [Tooltip("Масштаб VFX (один параметр для всех осей)")]
    [SerializeField] private float bossVfxScale = 1f;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    // Дверь: мировая Y в закрытом состоянии (запоминается в Start)
    private float doorClosedY;
    private bool isDoorOpen = false;
    private Coroutine doorCoroutine;
    
    // Последний заспавненный брейнрот (для позиции Boss VFX)
    private Transform lastSpawnedBrainrotTransform;
    
    // Загруженные префабы брейнротов
    private GameObject[] brainrotPrefabs;
    
    // Список всех редкостей в порядке приоритета
    private readonly string[] allRarities = { "Common", "Rare", "Exclusive", "Epic", "Mythic", "Legendary", "Secret" };
    
    // Нормализованные шансы редкостей
    private System.Collections.Generic.Dictionary<string, float> normalizedRarityChances = new System.Collections.Generic.Dictionary<string, float>();
    
    private void Awake()
    {
        LoadBrainrotPrefabs();
        NormalizeRarityChances();
    }
    
    /// <summary>Индекс области по уровню силы: 0 = 10–20, 1 = 20–30, 2 = 30–40, 3 = 40–50, 4 = 50–60.</summary>
    private static int GetAreaIndexForLevel(int level)
    {
        level = Mathf.Clamp(level, 10, 60);
        int index = (level - 10) / 10;
        return Mathf.Min(index, 4);
    }
    
    /// <summary>Возвращает набор шансов для области (0–4). Если область не задана — null.</summary>
    private RarityChanceSet GetChanceSetForArea(int areaIndex)
    {
        switch (areaIndex)
        {
            case 0: return area1_10_20;
            case 1: return area2_20_30;
            case 2: return area3_30_40;
            case 3: return area4_40_50;
            case 4: return area5_50_60;
            default: return null;
        }
    }
    
    private static bool HasAnyChance(RarityChanceSet set)
    {
        if (set == null) return false;
        return set.commonChance > 0f || set.rareChance > 0f || set.exclusiveChance > 0f
            || set.epicChance > 0f || set.mythicChance > 0f || set.legendaryChance > 0f || set.secretChance > 0f;
    }
    
    private static Dictionary<string, float> BuildChancesFromSet(RarityChanceSet set)
    {
        var d = new Dictionary<string, float>();
        if (set == null) return d;
        d["Common"] = set.commonChance;
        d["Rare"] = set.rareChance;
        d["Exclusive"] = set.exclusiveChance;
        d["Epic"] = set.epicChance;
        d["Mythic"] = set.mythicChance;
        d["Legendary"] = set.legendaryChance;
        d["Secret"] = set.secretChance;
        return d;
    }
    
    /// <summary>
    /// Нормализует шансы так, чтобы сумма была 100%. Редкости с явным 0 остаются 0 (не раздаём им остаток).
    /// </summary>
    private static void NormalizeChanceDictionary(Dictionary<string, float> chances)
    {
        float sum = 0f;
        foreach (string r in new[] { "Common", "Rare", "Exclusive", "Epic", "Mythic", "Legendary", "Secret" })
        {
            float c = chances.ContainsKey(r) ? chances[r] : 0f;
            if (c > 0f) sum += c;
        }
        if (sum <= 0f) return;
        float scale = 100f / sum;
        foreach (string r in new[] { "Common", "Rare", "Exclusive", "Epic", "Mythic", "Legendary", "Secret" })
        {
            if (!chances.ContainsKey(r)) chances[r] = 0f;
            if (chances[r] > 0f)
                chances[r] = chances[r] * scale;
        }
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
        
        if (doorTransform != null)
        {
            doorClosedY = doorTransform.position.y;
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
        
        if (doorTransform != null)
        {
            if (doorCoroutine != null)
                StopCoroutine(doorCoroutine);
            doorCoroutine = StartCoroutine(RunDoorSequence());
        }
        else
        {
            RemoveAllUnfoughtBrainrots();
            SpawnBrainrot();
        }
        
        ResetInteraction();
        
        if (debug)
        {
            Debug.Log($"[BrainrotSpawnButton] {gameObject.name}: Состояние взаимодействия сброшено, UI должен остаться видимым");
        }
    }
    
    /// <summary>
    /// Удаляет все unfought брейнроты на сцене (на карте остаётся только новый заспавненный).
    /// </summary>
    private void RemoveAllUnfoughtBrainrots()
    {
        BrainrotObject[] all = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        foreach (BrainrotObject brainrot in all)
        {
            if (brainrot != null && brainrot.gameObject != null && brainrot.IsUnfought())
            {
                Destroy(brainrot.gameObject);
                if (debug) Debug.Log($"[BrainrotSpawnButton] Удалён unfought брейнрот: {brainrot.GetObjectName()}");
            }
        }
    }
    
    /// <summary>
    /// Последовательность: при повторной активации — закрыть дверь, после закрытия удалить unfought и спавнить нового, затем открыть дверь.
    /// </summary>
    private IEnumerator RunDoorSequence()
    {
        if (doorTransform == null) yield break;
        
        // При повторной активации — сначала закрыть дверь
        if (isDoorOpen)
        {
            yield return AnimateDoorY(doorClosedY, doorOpenCloseDuration);
            // После закрытия: удалить unfought брейнротов и спавнить нового (появится, когда дверь начнёт открываться)
            RemoveAllUnfoughtBrainrots();
            SpawnBrainrot();
        }
        else
        {
            // Первая активация: удалить (пусто) и спавнить до открытия
            RemoveAllUnfoughtBrainrots();
            SpawnBrainrot();
        }
        
        // Показать Boss VFX над заспавненным брейнротом, когда дверь начала открываться
        if (bossVfxPrefab != null && lastSpawnedBrainrotTransform != null)
        {
            Vector3 vfxPosition = lastSpawnedBrainrotTransform.position + bossVfxOffset;
            GameObject vfxInstance = Instantiate(bossVfxPrefab, vfxPosition, Quaternion.identity);
            if (vfxInstance != null)
                vfxInstance.transform.localScale = Vector3.one * bossVfxScale;
        }
        
        // Открыть дверь
        yield return AnimateDoorY(doorClosedY + doorOpenOffsetY, doorOpenCloseDuration);
        isDoorOpen = true;
        doorCoroutine = null;
    }
    
    /// <summary>
    /// Закрыть дверь (вызывается при начале боя с боссом). Анимация до закрытого положения.
    /// </summary>
    public void CloseDoor()
    {
        if (doorTransform == null) return;
        if (doorCoroutine != null)
        {
            StopCoroutine(doorCoroutine);
            doorCoroutine = null;
        }
        doorCoroutine = StartCoroutine(CloseDoorCoroutine());
    }
    
    private IEnumerator CloseDoorCoroutine()
    {
        if (doorTransform == null) { doorCoroutine = null; yield break; }
        yield return AnimateDoorY(doorClosedY, doorOpenCloseDuration);
        isDoorOpen = false;
        doorCoroutine = null;
    }
    
    /// <summary>
    /// Анимирует позицию двери по Y до целевого значения за заданное время.
    /// </summary>
    private IEnumerator AnimateDoorY(float targetY, float duration)
    {
        if (doorTransform == null || duration <= 0f) yield break;
        
        Vector3 pos = doorTransform.position;
        float startY = pos.y;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            pos.y = Mathf.Lerp(startY, targetY, t);
            doorTransform.position = pos;
            yield return null;
        }
        
        pos.y = targetY;
        doorTransform.position = pos;
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
    /// Выбирает случайную редкость по шансам области, соответствующей уровню силы (10–60).
    /// </summary>
    private string GetRandomRarity()
    {
        int level = 10;
        if (GameStorage.Instance != null)
            level = Mathf.Clamp(GameStorage.Instance.GetAttackPowerLevel(), 10, 60);
        
        int areaIndex = GetAreaIndexForLevel(level);
        RarityChanceSet set = GetChanceSetForArea(areaIndex);
        Dictionary<string, float> chances;
        
        if (HasAnyChance(set))
        {
            chances = BuildChancesFromSet(set);
            NormalizeChanceDictionary(chances);
        }
        else
        {
            chances = new Dictionary<string, float>();
            foreach (string r in allRarities)
                chances[r] = normalizedRarityChances.ContainsKey(r) ? normalizedRarityChances[r] : 0f;
        }
        
        float randomValue = Random.Range(0f, 100f);
        float currentSum = 0f;
        foreach (string rarity in allRarities)
        {
            float chance = chances.ContainsKey(rarity) ? chances[rarity] : 0f;
            if (randomValue >= currentSum && randomValue < currentSum + chance)
                return rarity;
            currentSum += chance;
        }
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
        
        // Создаём экземпляр; поворот по Y выставим после спавна из данных префаба (у экземпляра значения префаба уже подставлены)
        GameObject spawnedObject = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        
        if (spawnedObject == null)
        {
            Debug.LogWarning("[BrainrotSpawnButton] Не удалось создать экземпляр префаба");
            return;
        }
        
        BrainrotObject brainrotObject = spawnedObject.GetComponentInChildren<BrainrotObject>();
        if (brainrotObject == null)
        {
            Debug.LogWarning($"[BrainrotSpawnButton] У спавненного объекта {spawnedObject.name} нет компонента BrainrotObject!");
            Destroy(spawnedObject);
            return;
        }
        
        // Поворот по Y берём из заспавненного объекта (там уже корректные значения из префаба); если 0 — случайный
        float rotationY = brainrotObject.GetSpawnRotationY();
        if (Mathf.Abs(rotationY) < 0.01f)
            rotationY = Random.Range(0f, 360f);
        spawnedObject.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        
        // Устанавливаем редкость
        brainrotObject.SetRarity(selectedRarity);
        
        // Генерируем случайный baseIncome из диапазона текущей области (по уровню силы)
        int level = 10;
        if (GameStorage.Instance != null)
            level = Mathf.Clamp(GameStorage.Instance.GetAttackPowerLevel(), 10, 60);
        int areaIndex = GetAreaIndexForLevel(level);
        RarityChanceSet set = GetChanceSetForArea(areaIndex);
        long minInc = 100, maxInc = 1000;
        if (set != null)
        {
            minInc = set.baseIncomeMin;
            maxInc = set.baseIncomeMax;
        }
        if (maxInc < minInc) maxInc = minInc;
        long randomBaseIncome = Random.Range((int)minInc, (int)maxInc + 1);
        brainrotObject.SetBaseIncome(randomBaseIncome);
        
        lastSpawnedBrainrotTransform = spawnedObject.transform;
        
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
        
        spawnPosition += brainrotSpawnOffset;
        return spawnPosition;
    }
}
