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
    
    [Tooltip("Вероятность (0–1), что зона заспавнится под игроком, а не в случайном месте. По умолчанию 30%")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnUnderPlayerChance = 0.3f;
    
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
    
    [Tooltip("Земля зоны боя — Y всех зон берётся отсюда. Если не назначен, ищется по имени FightZoneGround")]
    [SerializeField] private Transform fightZoneGround;
    
    [Header("VFX")]
    [Tooltip("Префаб VFX эффекта зоны (удаляется вместе с зоной)")]
    [SerializeField] private GameObject vfxPrefab;
    
    [Tooltip("Смещение VFX относительно центра зоны (локальные XYZ)")]
    [SerializeField] private Vector3 vfxOffset = Vector3.zero;
    
    [Tooltip("Масштаб VFX (один множитель для XYZ)")]
    [SerializeField] private float vfxScale = 1f;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private bool isSpawning = false;
    private float currentBossHP = 0f;
    private float maxBossHP = 0f;
    private Coroutine spawnCoroutine;
    private List<GameObject> activeZones = new List<GameObject>();
    private Transform playerTransform;
    
    private void Awake()
    {
        if (battleZone == null)
            battleZone = FindFirstObjectByType<BattleZone>();
        if (fightZoneGround == null)
        {
            GameObject go = GameObject.Find("FightZoneGround");
            if (go != null)
                fightZoneGround = go.transform;
        }
        FindPlayer();
    }
    
    private void FindPlayer()
    {
        if (playerTransform != null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
        else
        {
            var controller = FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
                playerTransform = controller.transform;
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
        
        // С вероятностью spawnUnderPlayerChance спавним под игроком, иначе — в случайном месте
        Vector3 spawnPosition;
        if (spawnUnderPlayerChance > 0f && Random.value < spawnUnderPlayerChance)
        {
            FindPlayer();
            spawnPosition = playerTransform != null ? GetPositionUnderPlayer() : GetRandomSpawnPosition();
        }
        else
        {
            spawnPosition = GetRandomSpawnPosition();
        }
        
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
        
        // VFX: спавним как дочерний объект зоны — удалится вместе с зоной; Y задаётся один раз — в сторону игрока
        if (vfxPrefab != null)
        {
            FindPlayer();
            float angleY = 0f;
            if (playerTransform != null)
            {
                Vector3 toPlayer = playerTransform.position - spawnPosition;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                    angleY = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            }
            GameObject vfx = Instantiate(vfxPrefab, zone.transform);
            vfx.transform.localPosition = vfxOffset;
            vfx.transform.localScale = Vector3.one * vfxScale;
            // Мировая ротация, затем на следующий кадр — чтобы префаб/ParticleSystem не перезаписали
            StartCoroutine(SetVfxRotationNextFrame(vfx.transform, 90f, angleY, 0f));
        }
        
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
    /// Y плоскости земли зоны боя (FightZoneGround). Все зоны спавнятся на этой высоте.
    /// </summary>
    private float GetGroundY()
    {
        if (fightZoneGround != null)
            return fightZoneGround.position.y;
        if (battleZone != null)
            return battleZone.transform.position.y;
        return transform.position.y;
    }
    
    /// <summary>
    /// Возвращает позицию под игроком на высоте FightZoneGround (X,Z от игрока, Y от земли зоны).
    /// </summary>
    private Vector3 GetPositionUnderPlayer()
    {
        Vector3 p = playerTransform.position;
        return new Vector3(p.x, GetGroundY(), p.z);
    }
    
    /// <summary>
    /// Получает случайную позицию для спавна зоны. X,Z — из области боя, Y — от FightZoneGround.
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        float groundY = GetGroundY();
        Vector3 position;
        
        if (battleZone != null)
        {
            Collider zoneCollider = battleZone.GetComponent<Collider>();
            if (zoneCollider != null)
            {
                Bounds bounds = zoneCollider.bounds;
                float randomX = Random.Range(bounds.min.x, bounds.max.x);
                float randomZ = Random.Range(bounds.min.z, bounds.max.z);
                position = new Vector3(randomX, groundY, randomZ);
            }
            else
            {
                Vector3 p = battleZone.transform.position;
                position = new Vector3(p.x, groundY, p.z);
            }
        }
        else if (spawnArea != null)
        {
            Bounds bounds = spawnArea.bounds;
            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);
            position = new Vector3(randomX, groundY, randomZ);
        }
        else
        {
            Vector3 p = transform.position;
            position = new Vector3(p.x, groundY, p.z);
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
    /// Устанавливает мировую ротацию VFX на следующем кадре, чтобы префаб/частицы не перезаписали.
    /// </summary>
    private IEnumerator SetVfxRotationNextFrame(Transform vfxTransform, float eulerX, float eulerY, float eulerZ)
    {
        yield return null;
        if (vfxTransform != null)
        {
            vfxTransform.rotation = Quaternion.Euler(eulerX, eulerY, eulerZ);
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
    /// <summary> Игрок находится внутри триггера зоны (CapsuleCollider isTrigger на префабе). </summary>
    private bool playerInZone = false;
    
    /// <summary> Вызывается из RedZoneTriggerForwarder или при триггере на этом объекте. </summary>
    public void SetPlayerInZone(bool inZone)
    {
        playerInZone = inZone;
    }
    
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
        CreateZoneVisual();
        EnsureTriggerEventsReceived();
    }
    
    /// <summary>
    /// Если триггер (CapsuleCollider) на дочернем объекте — добавляем пересылку событий в этот RedZone.
    /// </summary>
    private void EnsureTriggerEventsReceived()
    {
        Collider selfTrigger = GetComponent<Collider>();
        if (selfTrigger != null && selfTrigger.isTrigger)
            return;
        Collider[] childColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in childColliders)
        {
            if (col == null || !col.isTrigger || col.gameObject == gameObject) continue;
            if (col.GetComponent<RedZoneTriggerForwarder>() != null) continue;
            var forwarder = col.gameObject.AddComponent<RedZoneTriggerForwarder>();
            forwarder.SetRedZone(this);
        }
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
            // Если игрок на зоне (в триггере CapsuleCollider префаба) — телепортируем на базу и заканчиваем бой (удаляем босса)
            if (playerInZone)
            {
                TeleportManager tm = TeleportManager.Instance;
                if (tm != null)
                    tm.TeleportToHouse();
                if (battleManager != null)
                    battleManager.EndBattle();
            }
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Player") || other.GetComponent<ThirdPersonController>() != null)
            playerInZone = true;
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Player") || other.GetComponent<ThirdPersonController>() != null)
            playerInZone = false;
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
    /// Создает визуальное представление зоны.
    /// Если у префаба уже есть Renderer (цилиндр и т.д.), только применяем материал — не создаём лишний белый цилиндр.
    /// </summary>
    private void CreateZoneVisual()
    {
        Renderer existingRenderer = GetComponentInChildren<Renderer>();
        if (existingRenderer != null)
        {
            // Префаб уже содержит визуал (цилиндр) — только применяем материал предупреждения
            if (warningMaterial != null)
            {
                existingRenderer.material = warningMaterial;
            }
            return;
        }
        
        // Визуала нет — создаём цилиндр для визуализации зоны
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "RedZoneVisual";
        visual.transform.SetParent(transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(radius * 2f, 0.1f, radius * 2f);
        
        Collider col = visual.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null && warningMaterial != null)
            renderer.material = warningMaterial;
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

/// <summary>
/// Пересылает OnTriggerEnter/Exit с дочернего объекта (CapsuleCollider) в родительский RedZone.
/// </summary>
public class RedZoneTriggerForwarder : MonoBehaviour
{
    private RedZone redZone;
    
    public void SetRedZone(RedZone zone)
    {
        redZone = zone;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (redZone == null) return;
        if (other != null && (other.CompareTag("Player") || other.GetComponent<ThirdPersonController>() != null))
            redZone.SetPlayerInZone(true);
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (redZone == null) return;
        if (other != null && (other.CompareTag("Player") || other.GetComponent<ThirdPersonController>() != null))
            redZone.SetPlayerInZone(false);
    }
}
