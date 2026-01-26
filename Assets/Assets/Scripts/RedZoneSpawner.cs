using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Спавнит красные зоны на земле с динамической сложностью.
/// Частота появления и урон зависят от HP босса.
/// </summary>
public class RedZoneSpawner : MonoBehaviour
{
    [Header("Red Zone Prefab")]
    [Tooltip("Префаб красной зоны (круг на земле)")]
    [SerializeField] private GameObject redZonePrefab;
    
    [Header("Spawn Settings")]
    [Tooltip("Базовая частота появления зон (интервал в секундах)")]
    [SerializeField] private float baseSpawnInterval = 3f;
    
    [Tooltip("Минимальный интервал между спавнами (в секундах)")]
    [SerializeField] private float minSpawnInterval = 0.5f;
    
    [Tooltip("Множитель для расчета частоты на основе HP босса")]
    [SerializeField] private float hpFrequencyMultiplier = 0.001f;
    
    [Header("Damage Settings")]
    [Tooltip("Базовый урон от красной зоны")]
    [SerializeField] private float baseDamage = 10f;
    
    [Tooltip("Множитель урона на основе HP босса")]
    [SerializeField] private float hpDamageMultiplier = 0.01f;
    
    [Header("Zone Settings")]
    [Tooltip("Радиус красной зоны")]
    [SerializeField] private float zoneRadius = 2f;
    
    [Tooltip("Время жизни зоны (в секундах)")]
    [SerializeField] private float zoneLifetime = 3f;
    
    [Tooltip("Время предупреждения перед активацией (в секундах)")]
    [SerializeField] private float warningTime = 1f;
    
    [Header("Spawn Area")]
    [Tooltip("Границы области спавна (если не назначен, используется BattleZone)")]
    [SerializeField] private Collider spawnArea;
    
    [Header("References")]
    [Tooltip("Ссылка на BattleZone")]
    [SerializeField] private BattleZone battleZone;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private bool isSpawning = false;
    private float currentBossHP = 0f;
    private float maxBossHP = 0f;
    private Coroutine spawnCoroutine;
    private List<GameObject> activeZones = new List<GameObject>();
    
    private void Awake()
    {
        // Автоматически находим BattleZone если не назначена
        if (battleZone == null)
        {
            battleZone = FindFirstObjectByType<BattleZone>();
        }
    }
    
    /// <summary>
    /// Начинает спавн красных зон
    /// </summary>
    public void StartSpawning(float bossHP)
    {
        if (isSpawning)
        {
            Debug.LogWarning("[RedZoneSpawner] Спавн уже активен!");
            return;
        }
        
        isSpawning = true;
        currentBossHP = bossHP;
        maxBossHP = bossHP;
        
        // Очищаем старые зоны
        ClearAllZones();
        
        // Запускаем корутину спавна
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
        spawnCoroutine = StartCoroutine(SpawnZonesCoroutine());
        
        if (debug)
        {
            Debug.Log($"[RedZoneSpawner] Начат спавн красных зон. HP босса: {bossHP}");
        }
    }
    
    /// <summary>
    /// Останавливает спавн красных зон
    /// </summary>
    public void StopSpawning()
    {
        if (!isSpawning) return;
        
        isSpawning = false;
        
        // Останавливаем корутину
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
        
        // Очищаем все зоны
        ClearAllZones();
        
        if (debug)
        {
            Debug.Log("[RedZoneSpawner] Спавн красных зон остановлен");
        }
    }
    
    /// <summary>
    /// Обновляет HP босса (для динамической сложности)
    /// </summary>
    public void UpdateBossHP(float hp)
    {
        currentBossHP = hp;
    }
    
    /// <summary>
    /// Корутина для спавна красных зон
    /// </summary>
    private IEnumerator SpawnZonesCoroutine()
    {
        while (isSpawning)
        {
            // Вычисляем интервал спавна на основе HP босса
            float spawnInterval = CalculateSpawnInterval();
            
            // Ждем интервал
            yield return new WaitForSeconds(spawnInterval);
            
            // Спавним красную зону
            SpawnRedZone();
        }
    }
    
    /// <summary>
    /// Вычисляет интервал спавна на основе HP босса
    /// </summary>
    private float CalculateSpawnInterval()
    {
        // Формула: интервал = базовый_интервал / (1 + HP_босса * множитель)
        // Чем больше HP, тем меньше интервал (чаще появляются зоны)
        float interval = baseSpawnInterval / (1f + currentBossHP * hpFrequencyMultiplier);
        
        // Ограничиваем минимальным интервалом
        interval = Mathf.Max(interval, minSpawnInterval);
        
        return interval;
    }
    
    /// <summary>
    /// Вычисляет урон на основе HP босса
    /// </summary>
    private float CalculateDamage()
    {
        // Формула: урон = базовый_урон * (1 + HP_босса * множитель)
        // Чем больше HP, тем больше урон
        float damage = baseDamage * (1f + currentBossHP * hpDamageMultiplier);
        
        return damage;
    }
    
    /// <summary>
    /// Спавнит красную зону
    /// </summary>
    private void SpawnRedZone()
    {
        if (redZonePrefab == null)
        {
            Debug.LogWarning("[RedZoneSpawner] Префаб красной зоны не назначен!");
            return;
        }
        
        // Получаем случайную позицию для спавна
        Vector3 spawnPosition = GetRandomSpawnPosition();
        
        // Создаем зону
        GameObject zone = Instantiate(redZonePrefab, spawnPosition, Quaternion.identity);
        
        // Настраиваем зону
        RedZone redZoneComponent = zone.GetComponent<RedZone>();
        if (redZoneComponent == null)
        {
            redZoneComponent = zone.AddComponent<RedZone>();
        }
        
        float damage = CalculateDamage();
        redZoneComponent.Initialize(zoneRadius, damage, zoneLifetime, warningTime);
        
        // Добавляем в список активных зон
        activeZones.Add(zone);
        
        // Удаляем зону из списка после истечения времени жизни
        StartCoroutine(RemoveZoneAfterLifetime(zone, zoneLifetime));
        
        if (debug)
        {
            Debug.Log($"[RedZoneSpawner] Заспавнена красная зона. Позиция: {spawnPosition}, урон: {damage}, интервал: {CalculateSpawnInterval():F2}s");
        }
    }
    
    /// <summary>
    /// Получает случайную позицию для спавна зоны
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        Vector3 position = Vector3.zero;
        
        // Используем BattleZone для определения области спавна
        if (battleZone != null)
        {
            // Генерируем случайную позицию в пределах зоны
            Collider zoneCollider = battleZone.GetComponent<Collider>();
            if (zoneCollider != null)
            {
                Bounds bounds = zoneCollider.bounds;
                
                // Генерируем случайные координаты X и Z
                float randomX = Random.Range(bounds.min.x, bounds.max.x);
                float randomZ = Random.Range(bounds.min.z, bounds.max.z);
                
                // Используем Raycast для определения высоты поверхности
                RaycastHit hit;
                Vector3 rayStart = new Vector3(randomX, bounds.max.y + 5f, randomZ);
                if (Physics.Raycast(rayStart, Vector3.down, out hit, 20f))
                {
                    position = hit.point;
                }
                else
                {
                    position = new Vector3(randomX, bounds.center.y, randomZ);
                }
            }
            else
            {
                // Если коллайдера нет, используем позицию зоны
                position = battleZone.transform.position;
            }
        }
        else if (spawnArea != null)
        {
            // Используем назначенную область спавна
            Bounds bounds = spawnArea.bounds;
            
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);
            
            RaycastHit hit;
            Vector3 rayStart = new Vector3(randomX, bounds.max.y + 5f, randomZ);
            if (Physics.Raycast(rayStart, Vector3.down, out hit, 20f))
            {
                position = hit.point;
            }
            else
            {
                position = new Vector3(randomX, bounds.center.y, randomZ);
            }
        }
        else
        {
            // Если ничего не назначено, используем позицию спавнера
            position = transform.position;
        }
        
        return position;
    }
    
    /// <summary>
    /// Удаляет зону из списка после истечения времени жизни
    /// </summary>
    private IEnumerator RemoveZoneAfterLifetime(GameObject zone, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        
        if (activeZones.Contains(zone))
        {
            activeZones.Remove(zone);
        }
        
        if (zone != null)
        {
            Destroy(zone);
        }
    }
    
    /// <summary>
    /// Очищает все активные зоны
    /// </summary>
    private void ClearAllZones()
    {
        foreach (GameObject zone in activeZones)
        {
            if (zone != null)
            {
                Destroy(zone);
            }
        }
        activeZones.Clear();
    }
}

/// <summary>
/// Компонент красной зоны на земле.
/// Наносит урон игроку при наступлении.
/// </summary>
public class RedZone : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Материал предупреждения (желтый/оранжевый)")]
    [SerializeField] private Material warningMaterial;
    
    [Tooltip("Материал активной зоны (красный)")]
    [SerializeField] private Material activeMaterial;
    
    private float radius;
    private float damage;
    private float lifetime;
    private float warningTime;
    private bool isActive = false;
    private float spawnTime;
    private BattleManager battleManager;
    private Transform playerTransform;
    
    private void Awake()
    {
        spawnTime = Time.time;
        
        // Находим BattleManager
        battleManager = BattleManager.Instance;
        
        // Находим игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            ThirdPersonController controller = FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
            }
        }
    }
    
    private void Start()
    {
        // Создаем визуальное представление зоны
        CreateZoneVisual();
    }
    
    private void Update()
    {
        float elapsed = Time.time - spawnTime;
        
        // Активируем зону после времени предупреждения
        if (!isActive && elapsed >= warningTime)
        {
            ActivateZone();
        }
        
        // Проверяем наступление игрока на зону
        if (isActive && playerTransform != null)
        {
            CheckPlayerInZone();
        }
        
        // Уничтожаем зону после истечения времени жизни
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Инициализирует красную зону
    /// </summary>
    public void Initialize(float zoneRadius, float zoneDamage, float zoneLifetime, float zoneWarningTime)
    {
        radius = zoneRadius;
        damage = zoneDamage;
        lifetime = zoneLifetime;
        warningTime = zoneWarningTime;
    }
    
    /// <summary>
    /// Создает визуальное представление зоны
    /// </summary>
    private void CreateZoneVisual()
    {
        // Создаем цилиндр для визуализации зоны
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "RedZoneVisual";
        visual.transform.SetParent(transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(radius * 2f, 0.1f, radius * 2f);
        
        // Удаляем коллайдер (он не нужен)
        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        
        // Устанавливаем материал предупреждения
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null && warningMaterial != null)
        {
            renderer.material = warningMaterial;
        }
    }
    
    /// <summary>
    /// Активирует зону (после времени предупреждения)
    /// </summary>
    private void ActivateZone()
    {
        isActive = true;
        
        // Меняем материал на активный
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer != null && activeMaterial != null)
        {
            renderer.material = activeMaterial;
        }
    }
    
    /// <summary>
    /// Проверяет, находится ли игрок в зоне
    /// </summary>
    private void CheckPlayerInZone()
    {
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        
        if (distance <= radius)
        {
            // Игрок наступил на зону - наносим урон
            if (battleManager != null)
            {
                battleManager.DamagePlayer(damage);
            }
            
            // Уничтожаем зону после нанесения урона
            Destroy(gameObject);
        }
    }
}
