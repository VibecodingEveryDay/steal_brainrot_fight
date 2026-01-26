using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Спавнер ботов, которые периодически появляются на случайных позициях
/// </summary>
public class BotSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Количество ботов для спавна за один раз")]
    [SerializeField] private int botCount = 5;
    
    [Tooltip("Интервал между спавнами (в секундах)")]
    [SerializeField] private float spawnNewTimer = 10f;
    
    [Tooltip("Количество ботов для единоразового спавна при старте игры и при столкновении игрока со стеной")]
    [SerializeField] private int startCount = 0;
    
    [Header("Spawn Area")]
    [Tooltip("Минимальная координата X для спавна")]
    [SerializeField] private float minX = -144f;
    
    [Tooltip("Максимальная координата X для спавна")]
    [SerializeField] private float maxX = 50f;
    
    [Tooltip("Минимальная координата Z для спавна")]
    [SerializeField] private float minZ = 100f;
    
    [Tooltip("Максимальная координата Z для спавна")]
    [SerializeField] private float maxZ = 1200f;
    
    [Tooltip("Высота спавна (Y координата)")]
    [SerializeField] private float spawnY = 0f;
    
    
    [Header("Bot Prefab")]
    [Tooltip("Префаб бота (если не назначен, загружается из Resources/game/Bots/Guy_Rig_idle)")]
    [SerializeField] private GameObject botPrefab;
    
    [Header("Bot Scale")]
    [Tooltip("Масштаб модели бота (1.0 = оригинальный размер)")]
    [SerializeField] private float botScale = 1f;
    
    [Tooltip("Смещение модели бота по Y (компенсирует локальное смещение модели в префабе). Если модель находится ниже корня GameObject, значение должно быть положительным. Уменьшите это значение, если боты слишком высоко над землей, или увеличьте, если они под землей")]
    [SerializeField] private float modelYOffset = 1.0f;
    
    [Header("Bot Behavior Settings")]
    [Tooltip("Базовая скорость движения ботов")]
    [SerializeField] private float botBaseMoveSpeed = 5f;
    
    [Tooltip("Множитель для расчета скорости на основе уровня")]
    [SerializeField] private float botSpeedLevelScaler = 1f;
    
    [Tooltip("Использовать уровень скорости из GameStorage")]
    [SerializeField] private bool botUseSpeedLevel = true;
    
    [Tooltip("Сила прыжка ботов")]
    [SerializeField] private float botJumpForce = 8f;
    
    [Tooltip("Минимальный интервал между прыжками ботов (в секундах)")]
    [SerializeField] private float botJumpIntervalMin = 1.5f;
    
    [Tooltip("Максимальный интервал между прыжками ботов (в секундах)")]
    [SerializeField] private float botJumpIntervalMax = 2.5f;
    
    [Tooltip("Гравитация для ботов")]
    [SerializeField] private float botGravity = -9.81f;
    
    [Tooltip("Расстояние для проверки земли")]
    [SerializeField] private float botGroundCheckDistance = 0.1f;
    
    [Tooltip("LayerMask для определения земли (укажите слои, на которых находятся коллайдеры земли)")]
    [SerializeField] private LayerMask groundLayerMask = -1; // По умолчанию все слои
    
    [Tooltip("Время жизни ботов в секундах (боты будут удалены через это время после создания). 0 = бесконечное время жизни")]
    [SerializeField] private float botLifetime = 12f;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private List<GameObject> spawnedBots = new List<GameObject>();
    private Coroutine spawnCoroutine;
    
    // Для определения позиции игрока
    private Transform playerTransform;
    
    private void Start()
    {
        // Загружаем префаб, если не назначен
        if (botPrefab == null)
        {
            LoadBotPrefab();
        }
        
        // Находим игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            ThirdPersonController playerController = FindFirstObjectByType<ThirdPersonController>();
            if (playerController != null)
            {
                playerTransform = playerController.transform;
            }
        }
        
        // Спавним начальное количество ботов
        if (startCount > 0)
        {
            SpawnBotsWithCount(startCount);
        }
        
        // Запускаем корутину спавна
        if (spawnCoroutine == null)
        {
            spawnCoroutine = StartCoroutine(SpawnBotsCoroutine());
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
    /// Загружает префаб бота из Resources
    /// </summary>
    private void LoadBotPrefab()
    {
#if UNITY_EDITOR
        // В редакторе используем AssetDatabase
        string prefabPath = "Assets/Assets/Resources/game/Bots/Guy_Rig_idle.prefab";
        botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
#else
        // В билде используем Resources
        botPrefab = Resources.Load<GameObject>("game/Bots/Guy_Rig_idle");
#endif
        
        if (botPrefab == null)
        {
            Debug.LogError("[BotSpawner] Не удалось загрузить префаб бота! Проверьте путь: Resources/game/Bots/Guy_Rig_idle");
        }
        else if (debug)
        {
            Debug.Log($"[BotSpawner] Префаб бота загружен: {botPrefab.name}");
        }
    }
    
    /// <summary>
    /// Корутина для периодического спавна ботов
    /// </summary>
    private IEnumerator SpawnBotsCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnNewTimer);
            
            // Обновляем ссылку на игрока (на случай, если он появился позже)
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
                else
                {
                    ThirdPersonController playerController = FindFirstObjectByType<ThirdPersonController>();
                    if (playerController != null)
                    {
                        playerTransform = playerController.transform;
                    }
                }
            }
            
            SpawnBots();
        }
    }
    
    /// <summary>
    /// Спавнит указанное количество ботов на случайных позициях (использует botCount)
    /// </summary>
    public void SpawnBots()
    {
        SpawnBotsWithCount(botCount);
    }
    
    /// <summary>
    /// Спавнит указанное количество ботов на случайных позициях
    /// </summary>
    public void SpawnBotsWithCount(int count)
    {
        if (botPrefab == null)
        {
            Debug.LogError("[BotSpawner] Префаб бота не назначен!");
            return;
        }
        
        int spawnedCount = 0;
        
        for (int i = 0; i < count; i++)
        {
            // Генерируем случайную позицию
            Vector3 spawnPosition = GetRandomSpawnPosition();
            
            // Создаем экземпляр префаба
            GameObject bot = Instantiate(botPrefab, spawnPosition, Quaternion.identity);
            
            if (bot == null)
            {
                Debug.LogWarning($"[BotSpawner] Не удалось создать бота {i + 1}/{botCount}");
                continue;
            }
            
            // Добавляем BotController, если его нет
            BotController botController = bot.GetComponent<BotController>();
            if (botController == null)
            {
                botController = bot.AddComponent<BotController>();
                
                if (debug)
                {
                    Debug.Log($"[BotSpawner] BotController добавлен к боту {bot.name}");
                }
            }
            
            // Удаляем Rigidbody, если он есть (конфликтует с CharacterController)
            Rigidbody existingRb = bot.GetComponent<Rigidbody>();
            if (existingRb != null)
            {
                // Используем Destroy вместо DestroyImmediate, чтобы избежать проблем
                Destroy(existingRb);
                if (debug)
                {
                    Debug.Log($"[BotSpawner] Удален Rigidbody у бота {bot.name}");
                }
            }
            
            // Убеждаемся, что есть CharacterController
            CharacterController characterController = bot.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = bot.AddComponent<CharacterController>();
                
                // Настраиваем параметры CharacterController по умолчанию
                characterController.height = 2f;
                characterController.radius = 0.5f;
                characterController.center = new Vector3(0, 1, 0);
                characterController.slopeLimit = 45f;
                characterController.stepOffset = 0.3f;
                characterController.skinWidth = 0.08f;
                characterController.minMoveDistance = 0f;
                
                if (debug)
                {
                    Debug.Log($"[BotSpawner] CharacterController добавлен к боту {bot.name}");
                }
            }
            else
            {
                // Убеждаемся, что CharacterController правильно настроен
                if (characterController.center.y < 0.5f)
                {
                    characterController.center = new Vector3(0, 1, 0);
                }
            }
            
            // CapsuleCollider не нужен - CharacterController имеет свой коллайдер
            // Но если он есть как отдельный компонент, его можно оставить (не конфликтует)
            
            // Применяем масштаб к боту
            if (botScale != 1f)
            {
                bot.transform.localScale = Vector3.one * botScale;
                
                // Масштабируем CharacterController (его коллайдер), если он есть
                if (characterController != null)
                {
                    characterController.height *= botScale;
                    characterController.radius *= botScale;
                    characterController.center = new Vector3(
                        characterController.center.x,
                        characterController.center.y * botScale,
                        characterController.center.z
                    );
                }
                
                if (debug)
                {
                    Debug.Log($"[BotSpawner] Применен масштаб {botScale} к боту {bot.name}");
                }
            }
            
            // Корректируем позицию бота с учетом смещения модели
            if (modelYOffset != 0f && characterController != null)
            {
                // Текущая позиция бота (CharacterController должен быть на земле после GetRandomSpawnPosition)
                Vector3 currentPos = bot.transform.position;
                
                // Модель имеет локальное смещение в префабе (обычно отрицательное, например -2.53)
                // Чтобы визуально модель была на земле, нужно поднять GameObject на modelYOffset
                // Но тогда CharacterController тоже поднимется, поэтому корректируем center
                
                // Поднимаем GameObject на modelYOffset, чтобы модель была на земле
                currentPos.y += modelYOffset;
                bot.transform.position = currentPos;
                
                // Корректируем CharacterController.center вниз на modelYOffset,
                // чтобы нижняя часть коллайдера осталась на земле
                // Нижняя часть коллайдера = pos.y + center.y - (height/2)
                // Если pos.y увеличили на modelYOffset, а center.y уменьшили на modelYOffset,
                // то нижняя часть коллайдера остается на том же месте
                Vector3 adjustedCenter = characterController.center;
                adjustedCenter.y -= modelYOffset;
                characterController.center = adjustedCenter;
                
                if (debug)
                {
                    Debug.Log($"[BotSpawner] Скорректирована позиция бота {bot.name}: pos.y={currentPos.y:F2}, center.y={adjustedCenter.y:F2}, modelYOffset={modelYOffset:F2}");
                }
            }
            
            // Применяем параметры поведения к боту
            ApplyBotBehaviorSettings(botController);
            
            // Добавляем в список активных ботов
            spawnedBots.Add(bot);
            spawnedCount++;
            
            if (debug)
            {
                Debug.Log($"[BotSpawner] Спавнен бот {i + 1}/{botCount} на позиции {bot.transform.position}");
            }
        }
        
        // Очищаем список от уничтоженных ботов
        CleanupDestroyedBots();
        
            if (debug)
            {
                Debug.Log($"[BotSpawner] Спавнено {spawnedCount} из {count} ботов. Всего активных ботов: {spawnedBots.Count}");
            }
        }
    
    /// <summary>
    /// Генерирует случайную позицию в области спавна
    /// Боты спавнятся в области X:minX-maxX, Z:minZ-maxZ
    /// </summary>
    private Vector3 GetRandomSpawnPosition()
    {
        float randomX = Random.Range(minX, maxX);
        float spawnZ = Random.Range(minZ, maxZ);
        
        // Пытаемся определить высоту через Raycast
        float yPosition = spawnY;
        RaycastHit hit;
        Vector3 rayStart = new Vector3(randomX, spawnY + 10f, spawnZ);
        // Используем LayerMask для правильного определения коллайдеров земли
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 20f, groundLayerMask))
        {
            // CharacterController.center = (0, 1, 0), height = 2f
            // Нижняя часть коллайдера = transform.position.y + center.y - (height/2) = transform.position.y + 1 - 1 = transform.position.y
            // Чтобы CharacterController был на земле, нижняя часть коллайдера должна быть на hit.point.y
            // Значит: transform.position.y = hit.point.y
            
            // Модель бота имеет локальное смещение по Y в префабе (обычно отрицательное, например -2.53)
            // Визуально нижняя часть модели = transform.position.y + modelLocalOffset
            // Чтобы модель была на земле: transform.position.y + modelLocalOffset = hit.point.y
            // Значит: transform.position.y = hit.point.y - modelLocalOffset
            // Но т.к. modelLocalOffset отрицательное, используем положительный modelYOffset для компенсации
            // transform.position.y = hit.point.y + modelYOffset
            
            // Однако, если мы установим transform.position.y = hit.point.y + modelYOffset,
            // то CharacterController будет выше земли на modelYOffset, что неправильно.
            // Правильнее: установить transform.position.y так, чтобы CharacterController был на земле,
            // а затем скорректировать позицию модели или CharacterController.center
            
            // Устанавливаем позицию так, чтобы нижняя часть CharacterController была на земле
            yPosition = hit.point.y;
            
            // Если модель имеет смещение, нужно скорректировать CharacterController.center
            // Но это делается после создания бота, поэтому здесь просто устанавливаем базовую позицию
            // Дополнительная коррекция будет применена в InitializeBotAfterSpawn
        }
        else
        {
            // Если Raycast не попал, используем spawnY
            yPosition = spawnY;
        }
        
        return new Vector3(randomX, yPosition, spawnZ);
    }
    
    /// <summary>
    /// Удаляет уничтоженных ботов из списка
    /// </summary>
    private void CleanupDestroyedBots()
    {
        spawnedBots.RemoveAll(bot => bot == null);
    }
    
    /// <summary>
    /// Удаляет всех активных ботов
    /// </summary>
    public void ClearAllBots()
    {
        foreach (GameObject bot in spawnedBots)
        {
            if (bot != null)
            {
                Destroy(bot);
            }
        }
        spawnedBots.Clear();
        
        if (debug)
        {
            Debug.Log("[BotSpawner] Все боты удалены");
        }
    }
    
    /// <summary>
    /// Получить количество активных ботов
    /// </summary>
    public int GetActiveBotsCount()
    {
        CleanupDestroyedBots();
        return spawnedBots.Count;
    }
    
    /// <summary>
    /// Спавнит начальное количество ботов (startCount)
    /// </summary>
    public void SpawnStartBots()
    {
        if (startCount > 0)
        {
            SpawnBotsWithCount(startCount);
        }
    }
    
    /// <summary>
    /// Получить значение startCount
    /// </summary>
    public int GetStartCount()
    {
        return startCount;
    }
    
    /// <summary>
    /// Применяет настройки поведения к боту
    /// </summary>
    private void ApplyBotBehaviorSettings(BotController botController)
    {
        if (botController == null)
        {
            return;
        }
        
        botController.SetBaseMoveSpeed(botBaseMoveSpeed);
        botController.SetSpeedLevelScaler(botSpeedLevelScaler);
        botController.SetUseSpeedLevel(botUseSpeedLevel);
        botController.SetJumpForce(botJumpForce);
        botController.SetJumpInterval(botJumpIntervalMin, botJumpIntervalMax);
        botController.SetGravity(botGravity);
        botController.SetGroundCheckDistance(botGroundCheckDistance);
        botController.SetGroundLayerMask(groundLayerMask);
        botController.SetLifetime(botLifetime);
        
        if (debug)
        {
            Debug.Log($"[BotSpawner] Применены настройки поведения к боту: speed={botBaseMoveSpeed}, jumpForce={botJumpForce}, jumpInterval={botJumpIntervalMin}-{botJumpIntervalMax}");
        }
    }
    
    /// <summary>
    /// Инициализирует бота после спавна (дает время для инициализации физики)
    /// </summary>
    private IEnumerator InitializeBotAfterSpawn(GameObject bot, BotController botController)
    {
        // Ждем один кадр для инициализации компонентов
        yield return null;
        
        if (bot == null || botController == null)
        {
            yield break;
        }
        
        // Применяем параметры поведения к боту после инициализации
        ApplyBotBehaviorSettings(botController);
        
        // Даем еще немного времени для стабилизации
        yield return new WaitForSeconds(0.1f);
        
        if (debug && bot != null)
        {
            Debug.Log($"[BotSpawner] Бот {bot.name} инициализирован после спавна");
        }
    }
}
