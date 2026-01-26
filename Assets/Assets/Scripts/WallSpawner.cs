using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Спавнер стен, которые движутся по оси Z и при столкновении с игроком телепортируют его в начало координат
/// </summary>
public class WallSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Интервал между спавнами стен (в секундах)")]
    [SerializeField] private float spawnTime = 3f;
    
    [Header("Wall Speeds")]
    [Tooltip("Скорость движения стены 1")]
    [SerializeField] private float wall1Speed = 5f;
    [Tooltip("Скорость движения стены 2")]
    [SerializeField] private float wall2Speed = 5f;
    [Tooltip("Скорость движения стены 3")]
    [SerializeField] private float wall3Speed = 5f;
    [Tooltip("Скорость движения стены 4")]
    [SerializeField] private float wall4Speed = 5f;
    
    [Header("Speed Boost Settings")]
    [Tooltip("Множитель ускорения волн в зависимости от уровня скорости игрока. Уровень 1 = 100% (1.0), уровень 60 = 150% (1.5)")]
    [SerializeField] private float speedBoostScaler = 1f;
    [Tooltip("Минимальный уровень скорости игрока для расчета ускорения (по умолчанию 1)")]
    [SerializeField] private int minSpeedLevel = 1;
    [Tooltip("Максимальный уровень скорости игрока для расчета ускорения (по умолчанию 60)")]
    [SerializeField] private int maxSpeedLevel = 60;
    [Tooltip("Минимальный множитель скорости волн (для уровня 1, по умолчанию 1.0 = 100%)")]
    [SerializeField] private float minSpeedMultiplier = 1.0f;
    [Tooltip("Максимальный множитель скорости волн (для уровня 60, по умолчанию 1.5 = 150%)")]
    [SerializeField] private float maxSpeedMultiplier = 1.5f;
    
    [Header("Spawn Positions")]
    [Tooltip("Позиция по X для спавна стен")]
    [SerializeField] private float spawnPosX = 0f;
    [Tooltip("Начальная позиция по Z для спавна стен")]
    [SerializeField] private float startPosZ = 50f;
    [Tooltip("Конечная позиция по Z (стена уничтожается при достижении)")]
    [SerializeField] private float endPosZ = -50f;
    
    [Header("Wall Prefabs")]
    [Tooltip("Префаб стены 1")]
    [SerializeField] private GameObject wall1Prefab;
    [Tooltip("Префаб стены 2")]
    [SerializeField] private GameObject wall2Prefab;
    [Tooltip("Префаб стены 3")]
    [SerializeField] private GameObject wall3Prefab;
    [Tooltip("Префаб стены 4")]
    [SerializeField] private GameObject wall4Prefab;
    
    [Header("Player Reference")]
    [Tooltip("Ссылка на игрока (если не назначена, будет искаться по тегу 'Player')")]
    [SerializeField] private Transform playerTransform;
    
    [Header("Collision Settings")]
    [Tooltip("Допустимая разница по Z для обнаружения коллизии (чем больше, тем проще обнаружить). Рекомендуется 5-10 для быстрых стен")]
    [SerializeField] private float zTolerance = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private PlayerCarryController playerCarryController;
    private List<GameObject> activeWalls = new List<GameObject>();
    private Coroutine spawnCoroutine;
    private GameStorage gameStorage;
    
    private void Awake()
    {
        // Получаем ссылку на GameStorage
        gameStorage = GameStorage.Instance;
        if (gameStorage == null && debug)
        {
            Debug.LogWarning("[WallSpawner] GameStorage.Instance не найден! Ускорение волн не будет работать.");
        }
        
        // Ищем игрока, если ссылка не назначена
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                if (debug)
                {
                    Debug.Log($"[WallSpawner] Игрок найден по тегу: {player.name}");
                }
            }
            else
            {
                Debug.LogWarning("[WallSpawner] Игрок не найден по тегу 'Player'! Назначьте playerTransform в инспекторе.");
            }
        }
        
        // Ищем PlayerCarryController
        if (playerTransform != null)
        {
            playerCarryController = playerTransform.GetComponent<PlayerCarryController>();
            if (playerCarryController == null)
            {
                Debug.LogWarning("[WallSpawner] PlayerCarryController не найден на игроке!");
            }
        }
    }
    
    private void Start()
    {
        // Запускаем корутину спавна
        if (spawnCoroutine == null)
        {
            spawnCoroutine = StartCoroutine(SpawnWallsCoroutine());
        }
    }
    
    private void OnDestroy()
    {
        // Останавливаем корутину при уничтожении
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
    }
    
    /// <summary>
    /// Корутина для периодического спавна стен
    /// </summary>
    private IEnumerator SpawnWallsCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnTime);
            SpawnRandomWall();
        }
    }
    
    /// <summary>
    /// Спавнит случайную стену
    /// </summary>
    private void SpawnRandomWall()
    {
        // Выбираем случайный префаб
        GameObject prefabToSpawn = null;
        float baseSpeed = 0f;
        int randomIndex = Random.Range(0, 4);
        
        switch (randomIndex)
        {
            case 0:
                prefabToSpawn = wall1Prefab;
                baseSpeed = wall1Speed;
                break;
            case 1:
                prefabToSpawn = wall2Prefab;
                baseSpeed = wall2Speed;
                break;
            case 2:
                prefabToSpawn = wall3Prefab;
                baseSpeed = wall3Speed;
                break;
            case 3:
                prefabToSpawn = wall4Prefab;
                baseSpeed = wall4Speed;
                break;
        }
        
        // Применяем ускорение в зависимости от уровня скорости игрока
        float speed = ApplySpeedBoost(baseSpeed);
        
        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[WallSpawner] Префаб стены {randomIndex + 1} не назначен!");
            return;
        }
        
        // Вычисляем стартовую позицию по Z в зависимости от позиции игрока
        float actualStartPosZ = GetActualStartPosZ();
        
        // Создаем стену
        Vector3 spawnPosition = new Vector3(spawnPosX, 0f, actualStartPosZ);
        GameObject wall = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        
        if (wall == null)
        {
            Debug.LogError($"[WallSpawner] Не удалось создать стену из префаба {randomIndex + 1}!");
            return;
        }
        
        // Настраиваем стену
        SetupWall(wall, speed);
        
        // Добавляем в список активных стен
        activeWalls.Add(wall);
        
        if (debug)
        {
            Debug.Log($"[WallSpawner] Создана стена {randomIndex + 1} на позиции {spawnPosition}, скорость: {speed}, Z игрока: {(playerTransform != null ? playerTransform.position.z : 0f)}");
        }
    }
    
    /// <summary>
    /// Получает актуальную стартовую позицию по Z в зависимости от позиции игрока
    /// Если Z игрока < 50, возвращает 5/8 от startPosZ
    /// Если Z игрока >= 50, возвращает полное значение startPosZ (8/8)
    /// </summary>
    private float GetActualStartPosZ()
    {
        if (playerTransform == null)
        {
            // Если игрок не найден, используем полное значение
            return startPosZ;
        }
        
        float playerZ = playerTransform.position.z;
        
        if (playerZ < 250f)
        {
            // Если игрок на Z < 50, используем 5/8 от заданного значения
            return startPosZ * (5f / 8f);
        }
        else
        {
            // Если игрок на Z >= 50, используем полное значение (8/8)
            return startPosZ;
        }
    }
    
    /// <summary>
    /// Настраивает стену: добавляет компонент движения и коллайдер
    /// </summary>
    private void SetupWall(GameObject wall, float speed)
    {
        if (wall == null)
        {
            Debug.LogError("[WallSpawner] SetupWall: wall == null!");
            return;
        }
        
        // Убеждаемся, что объект активен (для добавления компонентов)
        bool wasActive = wall.activeSelf;
        if (!wasActive)
        {
            wall.SetActive(true);
        }
        
        // Добавляем компонент движения стены
        WallMovement wallMovement = wall.GetComponent<WallMovement>();
        if (wallMovement == null)
        {
            try
            {
                wallMovement = wall.AddComponent<WallMovement>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[WallSpawner] Ошибка при добавлении компонента WallMovement: {e.Message}");
                if (!wasActive)
                {
                    wall.SetActive(false);
                }
                return;
            }
        }
        
        if (wallMovement == null)
        {
            Debug.LogError($"[WallSpawner] Не удалось добавить компонент WallMovement к стене! Активен: {wall.activeSelf}, Имя: {wall.name}");
            if (!wasActive)
            {
                wall.SetActive(false);
            }
            return;
        }
        
        // Инициализируем компонент движения
        wallMovement.Initialize(speed, endPosZ, this, zTolerance);
        
        // Возвращаем исходное состояние активности, если нужно
        if (!wasActive)
        {
            wall.SetActive(false);
        }
    }
    
    /// <summary>
    /// Вызывается компонентом WallMovement при коллизии с игроком
    /// </summary>
    /// <param name="collidingWall">Стена, которая вызвала коллизию</param>
    public void OnWallCollisionWithPlayer(GameObject collidingWall)
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[WallSpawner] playerTransform == null, не могу телепортировать игрока!");
            return;
        }
        
        // ФИНАЛЬНАЯ ПРОВЕРКА: проверяем конкретную стену, которая вызвала коллизию
        // Если игрок за этой стеной (Z игрока > Z стены), не телепортируем
        if (collidingWall != null)
        {
            float playerZ = playerTransform.position.z;
            float wallZ = collidingWall.transform.position.z;
            
            // Если игрок за стеной (Z игрока > Z стены), не телепортируем
            if (playerZ > wallZ)
            {
                if (debug)
                {
                    Debug.LogWarning($"[WallSpawner] ФИНАЛЬНАЯ ЗАЩИТА: Игрок за стеной {collidingWall.name}! Z игрока: {playerZ:F2} > Z стены: {wallZ:F2}, телепорт ОТМЕНЁН");
                }
                return;
            }
        }
        
        // Получаем CharacterController для правильной телепортации
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool wasEnabled = false;
        
        if (characterController != null)
        {
            // Временно отключаем CharacterController, чтобы можно было изменить позицию
            wasEnabled = characterController.enabled;
            characterController.enabled = false;
        }
        
        // Сохраняем текущую Y координату игрока, чтобы не изменить высоту
        float currentY = playerTransform.position.y;
        
        // Телепортируем игрока на точку респавна (x: -32.85563, z: 51.19408, y: текущая высота)
        Vector3 respawnPosition = new Vector3(-32.85563f, currentY, 51.19408f);
        playerTransform.position = respawnPosition;
        
        // Включаем CharacterController обратно, если он был включен
        if (characterController != null && wasEnabled)
        {
            characterController.enabled = true;
        }
        
        if (debug)
        {
            Debug.Log($"[WallSpawner] Игрок телепортирован на позицию (0, 0, 0). CharacterController был {(wasEnabled ? "включен" : "выключен")}");
        }
        
        // Уничтожаем брейнрот в руках игрока
        if (playerCarryController != null)
        {
            BrainrotObject carriedObject = playerCarryController.GetCurrentCarriedObject();
            if (carriedObject != null)
            {
                // Уничтожаем объект
                Destroy(carriedObject.gameObject);
                
                // Освобождаем руки
                playerCarryController.DropObject();
                
                if (debug)
                {
                    Debug.Log("[WallSpawner] Брейнрот в руках игрока уничтожен");
                }
            }
        }
        
        // Очищаем все активные стены
        ClearAllWalls();
        
        // Респавним брейнроты на всех ZoneSpawner
        RespawnAllZoneSpawners();
        
        // Спавним ботов при столкновении
        SpawnBotsOnWallCollision();
    }
    
    /// <summary>
    /// Спавнит ботов при столкновении игрока со стеной
    /// </summary>
    private void SpawnBotsOnWallCollision()
    {
        // Находим BotSpawner в сцене
        BotSpawner botSpawner = FindFirstObjectByType<BotSpawner>();
        if (botSpawner != null)
        {
            botSpawner.SpawnStartBots();
            
            if (debug)
            {
                int startCount = botSpawner.GetStartCount();
                if (startCount > 0)
                {
                    Debug.Log($"[WallSpawner] Спавнено {startCount} ботов после столкновения со стеной");
                }
            }
        }
    }
    
    /// <summary>
    /// Респавнит брейнроты на всех ZoneSpawner в сцене
    /// </summary>
    private void RespawnAllZoneSpawners()
    {
        ZoneSpawner[] allZoneSpawners = FindObjectsByType<ZoneSpawner>(FindObjectsSortMode.None);
        
        foreach (ZoneSpawner zoneSpawner in allZoneSpawners)
        {
            if (zoneSpawner != null)
            {
                zoneSpawner.RespawnBrainrots();
            }
        }
        
        if (debug)
        {
            Debug.Log($"[WallSpawner] Респавнены брейнроты на {allZoneSpawners.Length} ZoneSpawner");
        }
    }
    
    /// <summary>
    /// Уничтожает все активные стены
    /// </summary>
    private void ClearAllWalls()
    {
        // Создаем копию списка, чтобы избежать проблем при итерации и уничтожении
        List<GameObject> wallsToDestroy = new List<GameObject>(activeWalls);
        
        // Уничтожаем все стены
        foreach (GameObject wall in wallsToDestroy)
        {
            if (wall != null)
            {
                Destroy(wall);
            }
        }
        
        // Очищаем список
        activeWalls.Clear();
        
        if (debug)
        {
            Debug.Log($"[WallSpawner] Все активные стены уничтожены ({wallsToDestroy.Count} шт.)");
        }
    }
    
    /// <summary>
    /// Удаляет стену из списка активных (вызывается при уничтожении)
    /// </summary>
    public void RemoveWall(GameObject wall)
    {
        if (activeWalls.Contains(wall))
        {
            activeWalls.Remove(wall);
        }
    }
    
    /// <summary>
    /// Получить список активных стен (для BotSpawner)
    /// </summary>
    public List<GameObject> GetActiveWalls()
    {
        // Удаляем null объекты перед возвратом
        activeWalls.RemoveAll(wall => wall == null);
        return new List<GameObject>(activeWalls); // Возвращаем копию списка
    }
    
    /// <summary>
    /// Применяет ускорение к скорости волны на основе уровня скорости игрока
    /// Интерполяция: уровень 1 = 100% (minSpeedMultiplier), уровень 60 = 150% (maxSpeedMultiplier)
    /// </summary>
    private float ApplySpeedBoost(float baseSpeed)
    {
        // Если ускорение отключено, возвращаем базовую скорость
        if (speedBoostScaler <= 0f)
        {
            return baseSpeed;
        }
        
        // Если GameStorage недоступен, возвращаем базовую скорость
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
            if (gameStorage == null)
            {
                return baseSpeed;
            }
        }
        
        // Получаем текущий уровень скорости игрока
        int playerSpeedLevel = gameStorage.GetPlayerSpeedLevel();
        
        // Ограничиваем уровень между minSpeedLevel и maxSpeedLevel
        playerSpeedLevel = Mathf.Clamp(playerSpeedLevel, minSpeedLevel, maxSpeedLevel);
        
        // Вычисляем интерполяцию множителя скорости
        // Формула: multiplier = minSpeedMultiplier + (level - minSpeedLevel) * (maxSpeedMultiplier - minSpeedMultiplier) / (maxSpeedLevel - minSpeedLevel)
        float levelRange = maxSpeedLevel - minSpeedLevel;
        float multiplierRange = maxSpeedMultiplier - minSpeedMultiplier;
        
        float t = 0f;
        if (levelRange > 0f)
        {
            t = (float)(playerSpeedLevel - minSpeedLevel) / levelRange;
        }
        
        float speedMultiplier = minSpeedMultiplier + (multiplierRange * t);
        
        // Применяем speedBoostScaler к множителю
        speedMultiplier = 1f + (speedMultiplier - 1f) * speedBoostScaler;
        
        // Вычисляем финальную скорость
        float finalSpeed = baseSpeed * speedMultiplier;
        
        if (debug)
        {
            Debug.Log($"[WallSpawner] Уровень скорости игрока: {playerSpeedLevel}, базовая скорость: {baseSpeed}, множитель: {speedMultiplier:F3}, финальная скорость: {finalSpeed:F2}");
        }
        
        return finalSpeed;
    }
}
