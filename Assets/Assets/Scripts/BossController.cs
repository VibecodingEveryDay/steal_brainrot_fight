using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AI контроллер для босса.
/// Босс выбирает случайную цель (игрок или союзники) и движется к ней.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BossController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Скорость движения босса")]
    [SerializeField] private float moveSpeed = 3f;
    
    [Tooltip("Скорость поворота босса")]
    [SerializeField] private float rotationSpeed = 5f;
    
    [Tooltip("Расстояние атаки (когда босс близко к цели, он атакует)")]
    [SerializeField] private float attackDistance = 2f;
    
    [Header("Target Selection")]
    [Tooltip("Интервал перевыбора цели (в секундах)")]
    [SerializeField] private float targetReselectInterval = 3f;
    
    [Header("Attack Settings")]
    [Tooltip("Урон от атаки босса")]
    [SerializeField] private float attackDamage = 10f;
    
    [Tooltip("Интервал между атаками (в секундах)")]
    [SerializeField] private float attackCooldown = 2f;
    
    [Header("References")]
    [Tooltip("Модель босса (для поворота)")]
    [SerializeField] private Transform modelTransform;
    
    [Tooltip("Аниматор босса")]
    [SerializeField] private Animator animator;
    
    [Header("Boss Visual Settings")]
    [Tooltip("Смещение по Y для спавна босса (высота над точкой спавна)")]
    [SerializeField] private float offsetY = 1f;
    
    [Tooltip("Высота модели босса (localPosition Y для bossVisual)")]
    [SerializeField] private float bossVisualHeight = 70f;
    
    [Tooltip("Целевая Y позиция босса (по умолчанию -369)")]
    [SerializeField] private float targetBossY = -369f;
    
    [Header("Test Settings")]
    [Tooltip("Если включено, спавнить всех брейнротов из Resources/game/Brainrots в зоне битвы для тестирования")]
    [SerializeField] private bool prefabTestY = false;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private CharacterController characterController;
    private BattleManager battleManager;
    private BattleZone battleZone;
    
    // Текущая цель
    private Transform currentTarget;
    private float lastTargetReselectTime = 0f;
    private float lastAttackTime = 0f;
    
    // Состояние
    private bool isInitialized = false;
    private BrainrotObject brainrotObject;
    private float currentHP;
    
    // ВАЖНО: Флаг для отслеживания, что позиция уже установлена
    private bool positionFinalized = false;
    private float targetYPosition = -367.0966f;
    
    // Список всех возможных целей
    private List<Transform> availableTargets = new List<Transform>();
    
    // Аниматор параметры
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        
        // Автоматически находим модель если не назначена
        if (modelTransform == null)
        {
            Animator childAnimator = GetComponentInChildren<Animator>();
            if (childAnimator != null)
            {
                modelTransform = childAnimator.transform;
            }
        }
        
        // Автоматически находим аниматор если не назначен
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }
    
    private void Start()
    {
        // Находим BattleManager
        if (battleManager == null)
        {
            battleManager = BattleManager.Instance;
        }
        
        if (battleManager != null)
        {
            battleManager.SetBossController(this);
        }
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        if (battleManager == null || !battleManager.IsBattleActive()) return;
        
        // ВАЖНО: Всегда поворачиваемся к игроку, даже если цель не выбрана
        RotateTowardsPlayer();
        
        // Обновляем список доступных целей
        UpdateAvailableTargets();
        
        // Перевыбираем цель если нужно
        if (Time.time - lastTargetReselectTime >= targetReselectInterval)
        {
            SelectRandomTarget();
            lastTargetReselectTime = Time.time;
        }
        
        // Если цель не назначена, пытаемся выбрать новую
        if (currentTarget == null)
        {
            SelectRandomTarget();
        }
        
        // Если цель все еще не назначена, ничего не делаем
        if (currentTarget == null) return;
        
        // Движемся к цели
        MoveTowardsTarget();
        
        // Проверяем возможность атаки
        CheckAttack();
    }
    
    private void LateUpdate()
    {
        // ВАЖНО: Защищаем позицию босса, если она уже финализирована
        if (positionFinalized && Mathf.Abs(transform.position.y - targetYPosition) > 0.1f)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\",\"location\":\"BossController.cs:148\",\"message\":\"LateUpdate restoring position\",\"data\":{{\"currentY\":{transform.position.y},\"targetYPosition\":{targetYPosition},\"difference\":{Mathf.Abs(transform.position.y - targetYPosition)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            // Восстанавливаем Y позицию
            Vector3 pos = transform.position;
            float oldY = pos.y;
            pos.y = targetYPosition;
            transform.position = pos;
            
            if (debug)
            {
                Debug.LogWarning($"[BossController] Позиция была изменена в LateUpdate, восстанавливаем: было Y={oldY}, восстанавливаем Y={targetYPosition}");
            }
        }
    }
    
    /// <summary>
    /// Инициализирует босса
    /// </summary>
    public void InitializeBoss(BrainrotObject brainrot, float hp, BattleZone zone)
    {
        if (brainrot == null)
        {
            Debug.LogError("[BossController] BrainrotObject равен null при инициализации!");
            return;
        }
        
        brainrotObject = brainrot;
        currentHP = hp;
        battleZone = zone;
        isInitialized = true;
        
        // ВАЖНО: Отключаем CharacterController перед всеми операциями
        bool wasControllerEnabled = false;
        if (characterController != null)
        {
            wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
        }
        
        // Активируем босса (делаем видимым) ПЕРЕД созданием визуальной модели
        gameObject.SetActive(true);
        
        // Создаем визуальную модель босса на основе брейнрота
        CreateBossVisual(brainrot);
        
        // ВАЖНО: Устанавливаем позицию спавна ПОСЛЕ создания модели
        if (battleZone != null)
        {
            Vector3 spawnPosition = battleZone.GetBossSpawnPosition();
            transform.rotation = battleZone.GetBossSpawnRotation();
            
            // ВАЖНО: Устанавливаем X и Z из точки спавна, Y временно = 0
            // Финальный Y будет установлен в корутине на основе позиции игрока
            transform.position = new Vector3(spawnPosition.x, 0f, spawnPosition.z);
            
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"F\",\"location\":\"BossController.cs:200\",\"message\":\"Initial position set in InitializeBoss\",\"data\":{{\"position\":{{\"x\":{transform.position.x},\"y\":{transform.position.y},\"z\":{transform.position.z}}},\"spawnPosition\":{{\"x\":{spawnPosition.x},\"y\":{spawnPosition.y},\"z\":{spawnPosition.z}}}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            if (debug)
            {
                Debug.Log($"[BossController] Временная позиция установлена: {transform.position}");
            }
        }
        
        // ВАЖНО: Корректируем позицию после создания модели (получаем позицию игрока с задержкой)
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"BossController.cs:209\",\"message\":\"Starting FinalizeBossPosition coroutine\",\"data\":{{\"currentPosition\":{{\"x\":{transform.position.x},\"y\":{transform.position.y},\"z\":{transform.position.z}}}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        StartCoroutine(FinalizeBossPosition(wasControllerEnabled));

        // Если включен тестовый режим, спавним всех брейнротов
        if (prefabTestY)
        {
            SpawnAllBrainrotsForTest();
        }
        
        // Выбираем первую цель
        SelectRandomTarget();
        
        Debug.Log($"[BossController] Босс инициализирован: {brainrot.GetObjectName()}, HP: {hp}, позиция: {transform.position}, активен: {gameObject.activeSelf}, визуальная модель: {(modelTransform != null ? "создана" : "НЕ СОЗДАНА")}");
    }
    
    /// <summary>
    /// Создает визуальную модель босса на основе брейнрота
    /// </summary>
    private void CreateBossVisual(BrainrotObject brainrot)
    {
        if (brainrot == null)
        {
            Debug.LogError("[BossController] BrainrotObject равен null при создании визуальной модели!");
            return;
        }
        
        if (brainrot.gameObject == null)
        {
            Debug.LogError("[BossController] GameObject брейнрота равен null!");
            return;
        }
        
        // ВАЖНО: Удаляем старую модель если есть (включая все дочерние объекты)
        // Это гарантирует, что не будет нескольких моделей босса
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.gameObject != null)
            {
                // Удаляем все дочерние объекты, включая старые модели
                DestroyImmediate(child.gameObject);
            }
        }
        
        // Сбрасываем ссылку на modelTransform, так как старая модель удалена
        modelTransform = null;
        
        // Создаем копию всего брейнрота как дочерний объект
        // ВАЖНО: Создаем без родителя сначала, чтобы масштаб не искажался
        GameObject bossVisual = Instantiate(brainrot.gameObject);
        if (bossVisual == null)
        {
            Debug.LogError("[BossController] Не удалось создать копию брейнрота!");
            return;
        }
        
        bossVisual.name = "BossVisual";
        
        // ВАЖНО: Сохраняем оригинальный масштаб созданной копии ДО любых изменений
        // Instantiate создает копию с тем же масштабом, что и оригинал
        Vector3 originalScale = bossVisual.transform.localScale;
        
        Debug.Log($"[BossController] Оригинальный масштаб созданной копии: {originalScale}");
        
        // Удаляем компоненты, которые не нужны для босса
        BrainrotObject brainrotComponent = bossVisual.GetComponent<BrainrotObject>();
        if (brainrotComponent != null)
        {
            Destroy(brainrotComponent);
        }
        
        InteractableObject interactable = bossVisual.GetComponent<InteractableObject>();
        if (interactable != null)
        {
            Destroy(interactable);
        }
        
        // ВАЖНО: Отключаем или удаляем Rigidbody, чтобы физика не изменяла позицию
        Rigidbody[] rigidbodies = bossVisual.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                // ВАЖНО: Можно удалить Rigidbody полностью, чтобы он точно не влиял
                Destroy(rb);
            }
        }
        
        // ВАЖНО: Отключаем коллайдеры или делаем их триггерами, чтобы они не влияли на позицию
        Collider[] colliders = bossVisual.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (col != null)
            {
                // Делаем коллайдеры триггерами, чтобы они не влияли на физику
                col.isTrigger = true;
            }
        }
        
        // Удаляем UI элементы
        Transform[] allChildren = bossVisual.GetComponentsInChildren<Transform>();
        foreach (Transform child in allChildren)
        {
            if (child != null && child.gameObject != null)
            {
                if (child.name.Contains("InfoPrefab") || child.name.Contains("UI"))
                {
                    Destroy(child.gameObject);
                }
            }
        }
        
        // ВАЖНО: Устанавливаем родителя ПОСЛЕ удаления компонентов
        // Используем SetParent с worldPositionStays=false, чтобы сохранить локальный масштаб
        bossVisual.transform.SetParent(transform, false);
        
        // Устанавливаем локальную позицию
        // Поднимаем модель босса на заданную высоту (по умолчанию 0, можно настроить в инспекторе)
        bossVisual.transform.localPosition = new Vector3(0f, bossVisualHeight, 0f);
        
        // ВАЖНО: Определяем, нужно ли поворачивать модель на 180Y
        // Поворачиваем все модели на 180Y, кроме тех, что в списке исключений
        bool shouldRotate180Y = true;
        if (brainrot != null)
        {
            string objectName = brainrot.GetObjectName();
            
            // Список исключений - эти модели НЕ нужно поворачивать
            // DragonCannelloni убран из списка - теперь поворачивается на 180Y при битве
            // ballerinacaputchina убран из списка - теперь поворачивается на 180Y при битве
            string[] excludedNames = { "67", "BisonteGiuppitere", "KetupatKepat", "BlackholeGoat", "AdminLuckyBlock" };
            
            // Проверяем, есть ли имя в списке исключений
            foreach (string excludedName in excludedNames)
            {
                if (objectName == excludedName)
                {
                    shouldRotate180Y = false;
                    break;
                }
            }
            
            if (debug)
            {
                Debug.Log($"[BossController] Объект '{objectName}': shouldRotate180Y = {shouldRotate180Y}");
            }
        }
        else
        {
            Debug.LogWarning("[BossController] BrainrotObject равен null, используем поворот по умолчанию (180Y)");
        }
        
        // ВАЖНО: Устанавливаем поворот по Y
        // Поворот применяется ПОСЛЕ установки родителя, чтобы быть локальным
        if (shouldRotate180Y)
        {
            bossVisual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            if (debug)
            {
                Debug.Log("[BossController] Босс повернут на 180 градусов по Y");
            }
        }
        else
        {
            bossVisual.transform.localRotation = Quaternion.identity;
            if (debug)
            {
                Debug.Log("[BossController] Босс без поворота по Y (в списке исключений)");
            }
        }
        
        // ВАЖНО: Устанавливаем фиксированный масштаб босса
        // X: 0.057, Y: 2.5, Z: 0.05
        Vector3 bossScale = new Vector3(0.057f, 2.5f, 0.05f);
        bossVisual.transform.localScale = bossScale;
        
        Debug.Log($"[BossController] Финальный масштаб босса: {bossVisual.transform.localScale}, поворот Y: {(shouldRotate180Y ? 180f : 0f)}, shouldRotate180Y: {shouldRotate180Y}");
        
        // ВАЖНО: Активируем визуальную модель и все её дочерние объекты
        bossVisual.SetActive(true);
        
        // Активируем все дочерние объекты (на случай если они были деактивированы)
        foreach (Transform child in bossVisual.GetComponentsInChildren<Transform>())
        {
            if (child != null && child.gameObject != null)
            {
                child.gameObject.SetActive(true);
            }
        }
        
        // Используем визуальную модель как modelTransform
        modelTransform = bossVisual.transform;
        
        // Также ищем аниматор в визуальной модели
        if (animator == null)
        {
            animator = bossVisual.GetComponentInChildren<Animator>();
        }
        
        // Проверяем, что визуальная модель создана правильно
        Renderer[] renderers = bossVisual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[BossController] Визуальная модель босса не имеет рендереров! Босс может быть невидим.");
        }
        else
        {
            // ВАЖНО: Убеждаемся, что все рендереры включены
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }
    }

    /// <summary>
    /// Финализирует позицию босса после создания модели
    /// </summary>
    private System.Collections.IEnumerator FinalizeBossPosition(bool wasControllerEnabled)
    {
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"BossController.cs:409\",\"message\":\"FinalizeBossPosition coroutine started\",\"data\":{{\"currentPosition\":{{\"x\":{transform.position.x},\"y\":{transform.position.y},\"z\":{transform.position.z}}}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        Debug.Log("[BossController] Корутина FinalizeBossPosition запущена");
        
        // Ждем несколько кадров, чтобы игрок успел телепортироваться и bounds обновились
        yield return null;
        yield return null;
        yield return null;
        yield return null;
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"BossController.cs:417\",\"message\":\"After delay frames\",\"data\":{{\"currentPosition\":{{\"x\":{transform.position.x},\"y\":{transform.position.y},\"z\":{transform.position.z}}}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        Debug.Log($"[BossController] После задержки, текущая позиция: {transform.position}");
        
        // ВАЖНО: CharacterController должен быть отключен
        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
            Debug.Log("[BossController] CharacterController отключен");
        }
        
        // Получаем целевую высоту (позиция игрока) - теперь игрок уже телепортирован
        float targetGroundY = targetBossY; // Используем настраиваемое значение по умолчанию
        if (battleManager != null)
        {
            Transform playerTransform = battleManager.GetPlayerTransform();
            if (playerTransform != null)
            {
                float playerY = playerTransform.position.y;
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:435\",\"message\":\"Got player Y position\",\"data\":{{\"playerY\":{playerY},\"targetBossY\":{targetBossY},\"playerPosition\":{{\"x\":{playerTransform.position.x},\"y\":{playerTransform.position.y},\"z\":{playerTransform.position.z}}}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
                
                // ВАЖНО: Проверяем, что игрок действительно телепортирован
                // Если позиция игрока слишком высокая (больше 0), значит телепортация не произошла
                if (playerY > 0f)
                {
                    // #region agent log
                    try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                        $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:442\",\"message\":\"WARNING: Player not teleported! Using targetBossY\",\"data\":{{\"playerY\":{playerY},\"usingDefault\":true,\"targetBossY\":{targetBossY}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                    // #endregion
                    
                    Debug.LogWarning($"[BossController] Игрок не телепортирован! Y={playerY}, используем значение по умолчанию {targetBossY}");
                    targetGroundY = targetBossY;
                }
                else
                {
                    // Игрок телепортирован, используем его позицию как базу
                    // Но применяем offset, чтобы босс был на нужной высоте
                    targetGroundY = targetBossY;
                    
                    // #region agent log
                    try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                        $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:451\",\"message\":\"Player teleported, using targetBossY\",\"data\":{{\"playerY\":{playerY},\"targetBossY\":{targetBossY},\"targetGroundY\":{targetGroundY}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                    // #endregion
                }
                
                Debug.Log($"[BossController] Получена позиция игрока: Y={playerY}, используем targetGroundY={targetGroundY}");
            }
            else
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:460\",\"message\":\"PlayerTransform is null, using targetBossY\",\"data\":{{\"targetGroundY\":{targetGroundY}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
                
                Debug.LogWarning($"[BossController] PlayerTransform равен null, используем значение по умолчанию {targetBossY}");
            }
        }
        else
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:467\",\"message\":\"BattleManager is null, using targetBossY\",\"data\":{{\"targetGroundY\":{targetGroundY}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            Debug.LogWarning($"[BossController] BattleManager равен null, используем значение по умолчанию {targetBossY}");
        }
        
        // ВАЖНО: Получаем нижнюю точку модели босса
        float modelBottomY = 0f;
        if (modelTransform != null)
        {
            Renderer[] renderers = modelTransform.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                }
                modelBottomY = bounds.min.y;
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:464\",\"message\":\"Calculated model bottom Y\",\"data\":{{\"modelBottomY\":{modelBottomY},\"boundsMin\":{bounds.min.y},\"boundsMax\":{bounds.max.y}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
                
                Debug.Log($"[BossController] Нижняя точка модели: Y={modelBottomY}");
            }
            else
            {
                Debug.LogError("[BossController] Не найдено рендереров!");
            }
        }
        else
        {
            Debug.LogError("[BossController] modelTransform равен null!");
        }
        
        // ВАЖНО: Вычисляем offset
        float offset = targetGroundY - modelBottomY;
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:477\",\"message\":\"Calculated offset\",\"data\":{{\"offset\":{offset},\"targetGroundY\":{targetGroundY},\"modelBottomY\":{modelBottomY}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        Debug.Log($"[BossController] Вычислен offset: {offset} (targetGroundY={targetGroundY} - modelBottomY={modelBottomY})");
        
        // ВАЖНО: Отключаем все, что может изменять позицию
        // 1. CharacterController
        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
        }
        
        // 2. Rigidbody на модели босса
        if (modelTransform != null)
        {
            Rigidbody[] rigidbodies = modelTransform.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    // ВАЖНО: Можно удалить Rigidbody полностью
                    Destroy(rb);
                }
            }
        }
        
        // ВАЖНО: Устанавливаем позицию напрямую
        Vector3 pos = transform.position;
        float oldY = pos.y;
        pos.y = oldY + offset;
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BossController.cs:504\",\"message\":\"Setting position before assignment\",\"data\":{{\"oldY\":{oldY},\"offset\":{offset},\"newY\":{pos.y},\"currentPosition\":{{\"x\":{transform.position.x},\"y\":{transform.position.y},\"z\":{transform.position.z}}}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        Debug.Log($"[BossController] Устанавливаем позицию: старая Y={oldY}, offset={offset}, новая Y={pos.y}");
        
        transform.position = pos;
        
        // ВАЖНО: Проверяем, что позиция установлена
        yield return null;
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\",\"location\":\"BossController.cs:514\",\"message\":\"After setting position, checking actual Y\",\"data\":{{\"expectedY\":{pos.y},\"actualY\":{transform.position.y},\"difference\":{Mathf.Abs(transform.position.y - pos.y)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        Debug.Log($"[BossController] После установки позиции, фактическая Y={transform.position.y}");
        
        // Если позиция не установилась, пробуем еще раз
        if (Mathf.Abs(transform.position.y - pos.y) > 0.1f)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\",\"location\":\"BossController.cs:517\",\"message\":\"Position not set correctly, retrying\",\"data\":{{\"expectedY\":{pos.y},\"actualY\":{transform.position.y}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            Debug.LogWarning($"[BossController] Позиция не установилась! Было Y={transform.position.y}, должно быть Y={pos.y}, устанавливаем еще раз");
            transform.position = pos;
            yield return null;
            Debug.Log($"[BossController] После повторной установки, фактическая Y={transform.position.y}");
        }
        
        // Сохраняем целевую позицию
        targetYPosition = pos.y;
        positionFinalized = true;
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"BossController.cs:527\",\"message\":\"Position finalized\",\"data\":{{\"targetYPosition\":{targetYPosition},\"positionFinalized\":{positionFinalized.ToString().ToLower()},\"actualY\":{transform.position.y}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        Debug.Log($"[BossController] ФИНАЛЬНАЯ ПОЗИЦИЯ УСТАНОВЛЕНА: Y={transform.position.y}, targetYPosition={targetYPosition}, positionFinalized={positionFinalized}");
        
        // ВАЖНО: Включаем CharacterController обратно
        if (characterController != null && wasControllerEnabled)
        {
            characterController.enabled = true;
            Debug.Log("[BossController] CharacterController включен обратно");
        }
    }

    
    /// <summary>
    /// Обновляет список доступных целей
    /// </summary>
    private void UpdateAvailableTargets()
    {
        availableTargets.Clear();
        
        // Добавляем игрока
        if (battleManager != null)
        {
            Transform player = battleManager.GetPlayerTransform();
            if (player != null)
            {
                availableTargets.Add(player);
            }
        }
        
        // Добавляем союзников (ботов)
        // Находим все AllyBotController в сцене
        AllyBotController[] allies = FindObjectsByType<AllyBotController>(FindObjectsSortMode.None);
        foreach (AllyBotController ally in allies)
        {
            if (ally != null && ally.gameObject != null && ally.gameObject.activeInHierarchy)
            {
                availableTargets.Add(ally.transform);
            }
        }
    }
    
    /// <summary>
    /// Выбирает случайную цель из доступных
    /// </summary>
    private void SelectRandomTarget()
    {
        if (availableTargets.Count == 0)
        {
            currentTarget = null;
            return;
        }
        
        // Выбираем случайную цель
        int randomIndex = Random.Range(0, availableTargets.Count);
        currentTarget = availableTargets[randomIndex];
        
        // Проверяем, что цель все еще валидна
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;
            // Пытаемся выбрать другую цель
            if (availableTargets.Count > 1)
            {
                SelectRandomTarget();
            }
        }
        
        if (debug && currentTarget != null)
        {
            Debug.Log($"[BossController] Выбрана новая цель: {currentTarget.name}");
        }
    }
    
    /// <summary>
    /// Поворачивается к игроку (автоповорот)
    /// </summary>
    private void RotateTowardsPlayer()
    {
        // Находим игрока
        Transform playerTransform = null;
        if (battleManager != null)
        {
            playerTransform = battleManager.GetPlayerTransform();
        }
        
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        if (playerTransform == null) return;
        
        // Вычисляем направление к игроку
        Vector3 directionToPlayer = (playerTransform.position - transform.position);
        directionToPlayer.y = 0f; // Игнорируем вертикальную составляющую
        
        if (directionToPlayer.magnitude > 0.1f)
        {
            directionToPlayer.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // Поворачиваем модель если она назначена
            if (modelTransform != null)
            {
                modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
    
    /// <summary>
    /// Движется к текущей цели
    /// </summary>
    private void MoveTowardsTarget()
    {
        if (currentTarget == null) return;
        
        // ВАЖНО: Проверяем, что CharacterController активен перед использованием
        if (characterController == null || !characterController.enabled)
        {
            return;
        }
        
        // Вычисляем направление к цели
        Vector3 direction = (currentTarget.position - transform.position);
        direction.y = 0f; // Игнорируем вертикальную составляющую
        direction.Normalize();
        
        // Движемся к цели
        // ВАЖНО: Сохраняем текущую Y позицию, чтобы CharacterController не изменял её
        // Если позиция уже финализирована, используем сохраненную целевую позицию
        float targetY = positionFinalized ? targetYPosition : transform.position.y;
        Vector3 movement = direction * moveSpeed * Time.deltaTime;
        characterController.Move(movement);
        // ВАЖНО: Восстанавливаем Y позицию после движения
        Vector3 pos = transform.position;
        pos.y = targetY;
        transform.position = pos;
        
        // Поворачиваемся к цели
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // Поворачиваем модель если она назначена
            if (modelTransform != null)
            {
                modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        
        // Обновляем аниматор
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, moveSpeed);
        }
    }
    
    /// <summary>
    /// Проверяет возможность атаки
    /// </summary>
    private void CheckAttack()
    {
        if (currentTarget == null) return;
        if (Time.time - lastAttackTime < attackCooldown) return;
        
        // Проверяем расстояние до цели
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);
        if (distanceToTarget <= attackDistance)
        {
            // Атакуем цель
            AttackTarget();
            lastAttackTime = Time.time;
        }
    }
    
    /// <summary>
    /// Атакует текущую цель
    /// </summary>
    private void AttackTarget()
    {
        if (currentTarget == null) return;
        
        // Запускаем анимацию атаки
        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
        
        // Наносим урон цели
        // Проверяем, является ли цель игроком
        if (currentTarget.CompareTag("Player"))
        {
            // Наносим урон игроку
            if (battleManager != null)
            {
                battleManager.DamagePlayer(attackDamage);
            }
        }
        else
        {
            // Наносим урон союзнику
            AllyBotController ally = currentTarget.GetComponent<AllyBotController>();
            if (ally != null && battleManager != null)
            {
                battleManager.DamageAlly(ally.gameObject, attackDamage);
            }
        }
        
        if (debug)
        {
            Debug.Log($"[BossController] Атака по цели: {currentTarget.name}, урон: {attackDamage}");
        }
    }
    
    /// <summary>
    /// Наносит урон боссу (вызывается из BattleManager)
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (battleManager != null)
        {
            battleManager.DamageBoss(damage);
        }
    }
    
    /// <summary>
    /// Получает текущую цель
    /// </summary>
    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }
    
    /// <summary>
    /// Сбрасывает состояние босса (вызывается при окончании боя)
    /// </summary>
    public void ResetBoss()
    {
        isInitialized = false;
        currentTarget = null;
        availableTargets.Clear();
        
        // ВАЖНО: Удаляем все дочерние объекты (визуальные модели босса)
        // Это гарантирует, что не будет накопления старых моделей
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.gameObject != null)
            {
                DestroyImmediate(child.gameObject);
            }
        }
        
        // Сбрасываем ссылку на modelTransform
        modelTransform = null;
        
        // Скрываем босса
        gameObject.SetActive(false);
        
        if (debug)
        {
            Debug.Log("[BossController] Босс сброшен, все модели удалены и скрыт");
        }
    }
    
    /// <summary>
    /// Спавнит всех брейнротов из Resources/game/Brainrots в зоне битвы для тестирования
    /// </summary>
    private void SpawnAllBrainrotsForTest()
    {
        if (battleZone == null)
        {
            Debug.LogWarning("[BossController] BattleZone равен null, не могу спавнить тестовые брейнроты");
            return;
        }
        
        // Загружаем все префабы брейнротов из Resources
        GameObject[] brainrotPrefabs = Resources.LoadAll<GameObject>("game/Brainrots");
        
        if (brainrotPrefabs == null || brainrotPrefabs.Length == 0)
        {
            Debug.LogWarning("[BossController] Не найдено префабов брейнротов в Resources/game/Brainrots");
            return;
        }
        
        // Фильтруем только те, у которых есть компонент BrainrotObject
        List<GameObject> validPrefabs = new List<GameObject>();
        foreach (GameObject prefab in brainrotPrefabs)
        {
            if (prefab != null && prefab.GetComponent<BrainrotObject>() != null)
            {
                validPrefabs.Add(prefab);
            }
        }
        
        if (validPrefabs.Count == 0)
        {
            Debug.LogWarning("[BossController] Не найдено валидных префабов брейнротов с компонентом BrainrotObject");
            return;
        }
        
        Debug.Log($"[BossController] Найдено {validPrefabs.Count} префабов брейнротов для тестирования");
        
        // Получаем позицию спавна босса
        Vector3 bossSpawnPos = battleZone.GetBossSpawnPosition();
        
        // Спавним брейнротов в круге вокруг позиции босса
        float radius = 5f; // Радиус круга
        int count = validPrefabs.Count;
        
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = validPrefabs[i];
            
            // Вычисляем позицию в круге
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            Vector3 spawnPos = bossSpawnPos + new Vector3(x, 0f, z);
            
            // ВАЖНО: Всегда устанавливаем Y позицию в -370
            spawnPos.y = -370f;
            
            // Создаем экземпляр
            GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);
            
            if (instance != null)
            {
                // Получаем компонент BrainrotObject
                BrainrotObject brainrotObj = instance.GetComponent<BrainrotObject>();
                if (brainrotObj != null)
                {
                    // Устанавливаем unfought = true, чтобы можно было сразиться
                    brainrotObj.SetUnfought(true);
                    
                    // ВАЖНО: Устанавливаем placementScale в (2, 2, 2) перед размещением
                    // Это гарантирует, что PutAtPosition установит правильный масштаб
                    brainrotObj.SetPlacementScale(new Vector3(2f, 2f, 2f));
                    
                    // Размещаем объект (чтобы он был в состоянии isPlaced)
                    // Y позиция уже установлена в -370
                    brainrotObj.PutAtPosition(spawnPos, Quaternion.identity);
                    
                    // ВАЖНО: Убеждаемся, что масштаб установлен правильно после PutAtPosition
                    if (instance.transform.localScale != new Vector3(2f, 2f, 2f))
                    {
                        instance.transform.localScale = new Vector3(2f, 2f, 2f);
                    }
                }
                else
                {
                    // Если нет BrainrotObject, устанавливаем масштаб напрямую
                    instance.transform.localScale = new Vector3(2f, 2f, 2f);
                }
                
                Debug.Log($"[BossController] Заспавнен тестовый брейнрот: {prefab.name} в позиции {spawnPos}");
            }
        }
        
        Debug.Log($"[BossController] Заспавнено {count} тестовых брейнротов в зоне битвы");
    }
}
