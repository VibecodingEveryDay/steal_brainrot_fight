using UnityEngine;
using System.Collections;

/// <summary>
/// Контроллер для управления поведением бота
/// Бот движется вперед, прыгает каждые 2 секунды и удаляется при столкновении со стеной
/// Использует CharacterController для физики, аналогично игроку
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BotController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Базовая скорость движения (аналогично ThirdPersonController)")]
    [SerializeField] private float baseMoveSpeed = 5f;
    
    [Tooltip("Множитель для расчета скорости на основе уровня (аналогично ThirdPersonController)")]
    [SerializeField] private float speedLevelScaler = 1f;
    
    [Tooltip("Использовать уровень скорости из GameStorage")]
    [SerializeField] private bool useSpeedLevel = true;
    
    [Header("Jump Settings")]
    [Tooltip("Сила прыжка")]
    [SerializeField] private float jumpForce = 8f;
    
    [Tooltip("Минимальный интервал между прыжками (в секундах)")]
    [SerializeField] private float jumpIntervalMin = 1.5f;
    
    [Tooltip("Максимальный интервал между прыжками (в секундах)")]
    [SerializeField] private float jumpIntervalMax = 2.5f;
    
    private float currentJumpInterval; // Текущий случайный интервал
    
    [Header("Physics Settings")]
    [Tooltip("Гравитация (аналогично ThirdPersonController)")]
    [SerializeField] private float gravity = -9.81f;
    
    [Header("Ground Check")]
    [Tooltip("Расстояние для проверки земли")]
    [SerializeField] private float groundCheckDistance = 0.2f;
    
    [Tooltip("LayerMask для определения земли (укажите слои, на которых находятся коллайдеры земли)")]
    [SerializeField] private LayerMask groundLayerMask = -1; // По умолчанию все слои
    
    [Tooltip("Максимальная вертикальная скорость для определения, что бот на земле (если скорость меньше этого значения, бот считается на земле)")]
    [SerializeField] private float maxGroundedVelocity = 0.5f;
    
    [Header("Wall Collision")]
    [Tooltip("Допуск для обнаружения столкновения со стеной по Z")]
    [SerializeField] private float zTolerance = 3f;
    
    [Tooltip("Минимальная координата X для проверки столкновения")]
    [SerializeField] private float minX = -142f;
    
    [Tooltip("Максимальная координата X для проверки столкновения")]
    [SerializeField] private float maxX = 50.41f;
    
    [Tooltip("Минимальная координата Y для проверки столкновения")]
    [SerializeField] private float minY = -1f;
    
    [Header("References")]
    [Tooltip("Трансформ модели (для анимации)")]
    [SerializeField] private Transform modelTransform;
    
    [Tooltip("Аниматор")]
    [SerializeField] private Animator animator;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private CharacterController characterController;
    private Vector3 velocity = Vector3.zero; // Вектор скорости для движения и гравитации (инициализируем нулем)
    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private float moveSpeed;
    private float lastJumpTime = -1f;
    private float currentSpeed;
    private GameStorage gameStorage;
    private ThirdPersonController playerController;
    
    // Простой флаг прыжка для анимации
    private bool isJumping = false; // Флаг прыжка
    
    // Кэш для стен (обновляется периодически)
    private WallMovement[] cachedWalls;
    private float lastWallCacheUpdate = 0f;
    private const float wallCacheUpdateInterval = 0.5f; // Обновляем кэш каждые 0.5 секунд
    
    // Защита от удаления сразу после спавна
    private float spawnTime;
    private const float spawnProtectionTime = 1f; // Бот не удаляется первые 1 секунду после спавна
    
    // Время жизни бота
    private float lifetime = 0f; // 0 = бесконечное время жизни
    
    // Отслеживание застревания
    private float lastZPosition = 0f; // Последняя позиция по Z
    private float stuckTimer = 0f; // Таймер застревания
    private const float stuckCheckInterval = 0.2f; // Проверяем застревание каждые 0.2 секунды
    private const float stuckThreshold = 0.01f; // Порог для определения застревания (изменение Z меньше этого значения)
    private const float stuckTimeLimit = 1f; // Время, после которого считаем бота застрявшим (в секундах)
    private int unstuckAttempts = 0; // Количество попыток распутывания
    private const int maxUnstuckAttempts = 3; // Максимальное количество попыток распутывания
    
    // Параметры аниматора
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    
    private void Awake()
    {
        // Получаем или добавляем CharacterController
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            // Удаляем Rigidbody перед добавлением CharacterController (конфликтует)
            Rigidbody existingRb = GetComponent<Rigidbody>();
            if (existingRb != null)
            {
                // Используем Destroy вместо DestroyImmediate
                Destroy(existingRb);
                if (debug)
                {
                    Debug.LogWarning($"[BotController] Удален Rigidbody у бота {gameObject.name} (конфликтует с CharacterController)");
                }
            }
            
            characterController = gameObject.AddComponent<CharacterController>();
            
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
                Debug.Log($"[BotController] CharacterController добавлен к боту {gameObject.name}");
            }
        }
        else
        {
            // Если CharacterController уже есть, проверяем Rigidbody
            Rigidbody existingRb = GetComponent<Rigidbody>();
            if (existingRb != null)
            {
                Destroy(existingRb);
                if (debug)
                {
                    Debug.LogWarning($"[BotController] Удален Rigidbody у бота {gameObject.name} (конфликтует с CharacterController)");
                }
            }
        }
        
        // Автоматически находим дочерний объект с моделью
        if (modelTransform == null)
        {
            Animator childAnimator = GetComponentInChildren<Animator>();
            if (childAnimator != null)
            {
                modelTransform = childAnimator.transform;
            }
        }
        
        // Автоматически находим Animator
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        // ВАЖНО: Отключаем Apply Root Motion в Animator, чтобы анимации не влияли на позицию модели
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
    }
    
    private void Start()
    {
        // Получаем ссылки на GameStorage и ThirdPersonController
        gameStorage = GameStorage.Instance;
        playerController = FindFirstObjectByType<ThirdPersonController>();
        
        // Обновляем скорость на основе уровня
        UpdateSpeedFromLevel();
        
        // Инициализируем кэш стен
        UpdateWallCache();
        
        // Устанавливаем начальное время для первого прыжка и случайный интервал
        lastJumpTime = Time.time;
        UpdateJumpInterval();
        
        // Инициализируем состояние земли
        wasGroundedLastFrame = false;
        isJumping = false;
        
        // Защита от удаления сразу после спавна
        spawnTime = Time.time;
        
        // Инициализируем velocity нулем при старте
        velocity = Vector3.zero;
        
        // Принудительно проверяем землю при старте
        HandleGroundCheck();
        wasGroundedLastFrame = isGrounded;
        
        // Сбрасываем velocity при старте, если бот на земле
        if (isGrounded)
        {
            velocity.y = -2f;
        }
        
        // Инициализируем отслеживание позиции для обнаружения застревания
        lastZPosition = transform.position.z;
        stuckTimer = 0f;
        unstuckAttempts = 0;
    }
    
    private void Update()
    {
        // Проверяем время жизни бота
        if (lifetime > 0f && Time.time - spawnTime >= lifetime)
        {
            if (debug)
            {
                Debug.Log($"[BotController] Бот {gameObject.name} удален по истечении времени жизни ({lifetime} сек)");
            }
            Destroy(gameObject);
            return;
        }
        
        HandleGroundCheck();
        HandleJump();
        ApplyGravity();
        
        // Проверяем застревание перед движением
        CheckStuck();
        
        HandleMovement();
        CheckWallCollisions();
        
        // ВАЖНО: Обновляем аниматор ПОСЛЕ всех проверок, чтобы параметры были актуальными
        UpdateAnimator();
        
        // Сохраняем текущее состояние для следующего кадра (ПОСЛЕ всех обновлений)
        wasGroundedLastFrame = isGrounded;
    }
    
    /// <summary>
    /// Обновляет скорость на основе уровня из GameStorage
    /// ВАЖНО: Убеждаемся, что moveSpeed всегда больше 0
    /// </summary>
    private void UpdateSpeedFromLevel()
    {
        if (!useSpeedLevel)
        {
            moveSpeed = baseMoveSpeed;
            // Убеждаемся, что скорость всегда больше 0
            if (moveSpeed <= 0f)
            {
                moveSpeed = 5f; // Минимум 5 единиц в секунду
                if (debug)
                {
                    Debug.LogWarning($"[BotController] baseMoveSpeed <= 0, используем минимум {moveSpeed}");
                }
            }
            return;
        }
        
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        if (gameStorage == null)
        {
            moveSpeed = baseMoveSpeed;
            // Убеждаемся, что скорость всегда больше 0
            if (moveSpeed <= 0f)
            {
                moveSpeed = 5f; // Минимум 5 единиц в секунду
            }
            if (debug)
            {
                Debug.LogWarning("[BotController] GameStorage не найден, используем базовую скорость");
            }
            return;
        }
        
        int speedLevel = gameStorage.GetPlayerSpeedLevel();
        
        // Пытаемся использовать ThirdPersonController для расчета скорости
        if (playerController != null)
        {
            moveSpeed = playerController.CalculateSpeedFromLevel(speedLevel);
        }
        else
        {
            // Если ThirdPersonController не найден, вычисляем самостоятельно
            moveSpeed = baseMoveSpeed + (speedLevel * speedLevelScaler);
        }
        
        // ВАЖНО: Убеждаемся, что скорость всегда больше 0
        if (moveSpeed <= 0f)
        {
            moveSpeed = Mathf.Max(baseMoveSpeed, 5f); // Используем baseMoveSpeed или минимум 5
            if (debug)
            {
                Debug.LogWarning($"[BotController] Рассчитанная скорость <= 0, используем {moveSpeed}");
            }
        }
        
        if (debug)
        {
            Debug.Log($"[BotController] Скорость обновлена: level={speedLevel}, moveSpeed={moveSpeed}");
        }
    }
    
    /// <summary>
    /// Обновляет кэш активных стен в сцене
    /// </summary>
    private void UpdateWallCache()
    {
        cachedWalls = FindObjectsByType<WallMovement>(FindObjectsSortMode.None);
        lastWallCacheUpdate = Time.time;
        
        if (debug && cachedWalls != null)
        {
            Debug.Log($"[BotController] Обновлен кэш стен: найдено {cachedWalls.Length} активных стен");
        }
    }
    
    /// <summary>
    /// Проверяет, находится ли бот на земле
    /// Использует комбинацию методов для более надежного определения земли:
    /// 1. CharacterController.isGrounded
    /// 2. Raycast с нескольких точек (центр и края)
    /// 3. Проверка вертикальной скорости
    /// </summary>
    private void HandleGroundCheck()
    {
        if (characterController == null)
        {
            isGrounded = false;
            return;
        }
        
        // Метод 1: Проверка через CharacterController (основной метод)
        bool controllerGrounded = characterController.isGrounded;
        
        // Метод 2: Проверка через Raycast с нескольких точек для надежности
        bool raycastGrounded = false;
        
        // Используем несколько точек для Raycast (центр и по краям)
        Vector3[] raycastPoints = new Vector3[]
        {
            transform.position + characterController.center, // Центр CharacterController
            transform.position + characterController.center + Vector3.forward * characterController.radius * 0.7f, // Вперед
            transform.position + characterController.center + Vector3.back * characterController.radius * 0.7f, // Назад
            transform.position + characterController.center + Vector3.right * characterController.radius * 0.7f, // Вправо
            transform.position + characterController.center + Vector3.left * characterController.radius * 0.7f // Влево
        };
        
        float checkDistance = groundCheckDistance + characterController.skinWidth + 0.1f;
        int groundHits = 0;
        
        foreach (Vector3 rayStart in raycastPoints)
        {
            RaycastHit hit;
            // Raycast вниз от каждой точки
            if (Physics.Raycast(rayStart, Vector3.down, out hit, checkDistance, groundLayerMask))
            {
                // Проверяем, что это действительно земля (не триггер и не сам бот)
                if (!hit.collider.isTrigger && hit.collider.gameObject != gameObject)
                {
                    float distanceToGround = hit.distance;
                    // Если расстояние до земли меньше порога, считаем что на земле
                    if (distanceToGround <= checkDistance)
                    {
                        groundHits++;
                    }
                }
            }
        }
        
        // Если хотя бы 2 из 5 лучей нашли землю, считаем что бот на земле
        raycastGrounded = groundHits >= 2;
        
        // Метод 3: Проверка вертикальной скорости
        // Если вертикальная скорость близка к нулю или отрицательная и небольшая, вероятно бот на земле
        bool velocityGrounded = velocity.y <= maxGroundedVelocity && velocity.y >= -maxGroundedVelocity * 2f;
        
        // Комбинируем все методы: бот на земле, если хотя бы один метод говорит что он на земле
        // И вертикальная скорость не слишком большая (не прыгает)
        isGrounded = (controllerGrounded || raycastGrounded) && velocity.y <= maxGroundedVelocity * 3f;
        
        // Дополнительная проверка: если вертикальная скорость очень маленькая и бот не прыгает,
        // считаем что он на земле (даже если CharacterController не определил)
        if (!isGrounded && velocityGrounded && !isJumping && velocity.y <= 0.1f)
        {
            isGrounded = true;
        }
        
        // Сброс вертикальной скорости при приземлении
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Небольшая отрицательная скорость для удержания на земле
        }
        
        // Сбрасываем флаг прыжка при приземлении
        // ВАЖНО: сбрасываем флаг только когда бот действительно приземлился
        if (isGrounded && isJumping)
        {
            // Сбрасываем флаг прыжка при приземлении
            isJumping = false;
            if (debug)
            {
                Debug.Log($"[BotController] Бот {gameObject.name} приземлился, сбрасываем флаг прыжка. velocity.y={velocity.y:F2}, controller={controllerGrounded}, raycast={raycastGrounded} (hits={groundHits})");
            }
        }
        
        if (debug && !isGrounded && controllerGrounded)
        {
            Debug.LogWarning($"[BotController] Бот {gameObject.name} CharacterController говорит что на земле, но общая проверка говорит что нет. velocity.y={velocity.y:F2}, raycastHits={groundHits}");
        }
        
        if (debug && isGrounded != wasGroundedLastFrame)
        {
            Debug.Log($"[BotController] Бот {gameObject.name} изменил состояние земли: {wasGroundedLastFrame} -> {isGrounded}, velocity.y={velocity.y:F2}");
        }
    }
    
    /// <summary>
    /// Обновляет случайный интервал между прыжками
    /// </summary>
    private void UpdateJumpInterval()
    {
        currentJumpInterval = Random.Range(jumpIntervalMin, jumpIntervalMax);
        
        if (debug)
        {
            Debug.Log($"[BotController] Новый интервал прыжка для бота {gameObject.name}: {currentJumpInterval:F2} сек");
        }
    }
    
    /// <summary>
    /// Обрабатывает автоматические прыжки
    /// Использует velocity.y для прыжка (аналогично ThirdPersonController)
    /// </summary>
    private void HandleJump()
    {
        // Проверяем, прошло ли достаточно времени с последнего прыжка
        // ВАЖНО: прыгаем только если бот на земле И не прыгает
        if (Time.time - lastJumpTime >= currentJumpInterval && isGrounded && !isJumping)
        {
            // Выполняем прыжок через velocity (аналогично ThirdPersonController)
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            isJumping = true; // Устанавливаем флаг прыжка
            lastJumpTime = Time.time;
            
            // Выбираем новый случайный интервал для следующего прыжка
            UpdateJumpInterval();
            
            if (debug)
            {
                Debug.Log($"[BotController] Бот {gameObject.name} выполнил прыжок, следующий через {currentJumpInterval:F2} сек. velocity.y={velocity.y:F2}");
            }
        }
    }
    
    /// <summary>
    /// Применяет гравитацию к velocity
    /// Использует velocity для гравитации (аналогично ThirdPersonController)
    /// </summary>
    private void ApplyGravity()
    {
        if (characterController == null)
        {
            return;
        }
        
        // Применяем гравитацию к velocity
        velocity.y += gravity * Time.deltaTime;
    }
    
    /// <summary>
    /// Проверяет, застрял ли бот (не двигается по Z)
    /// </summary>
    private void CheckStuck()
    {
        float currentZ = transform.position.z;
        float zDelta = Mathf.Abs(currentZ - lastZPosition);
        
        // Проверяем застревание только если бот на земле (не в прыжке)
        if (isGrounded && !isJumping)
        {
            // Если изменение Z очень маленькое, увеличиваем таймер застревания
            if (zDelta < stuckThreshold)
            {
                stuckTimer += Time.deltaTime;
                
                // Если бот застрял дольше лимита, пытаемся распутать
                if (stuckTimer >= stuckCheckInterval)
                {
                    if (stuckTimer >= stuckTimeLimit && unstuckAttempts < maxUnstuckAttempts)
                    {
                        TryUnstuck();
                        stuckTimer = 0f; // Сбрасываем таймер после попытки распутывания
                    }
                }
            }
            else
            {
                // Бот двигается - сбрасываем таймер и счетчик попыток
                stuckTimer = 0f;
                unstuckAttempts = 0;
            }
        }
        else
        {
            // Бот в воздухе - сбрасываем таймер (не проверяем застревание в прыжке)
            stuckTimer = 0f;
        }
        
        // Обновляем последнюю позицию
        lastZPosition = currentZ;
    }
    
    /// <summary>
    /// Пытается распутать застрявшего бота
    /// </summary>
    private void TryUnstuck()
    {
        unstuckAttempts++;
        
        if (debug)
        {
            Debug.LogWarning($"[BotController] Бот {gameObject.name} застрял! Попытка распутывания #{unstuckAttempts}. Позиция Z: {transform.position.z:F2}");
        }
        
        // Метод 1: Попытка движения вбок (влево или вправо) для обхода препятствия
        if (unstuckAttempts <= 2)
        {
            // Чередуем направление: первая попытка - влево, вторая - вправо
            Vector3 sideDirection = (unstuckAttempts == 1) ? Vector3.left : Vector3.right;
            Vector3 unstuckMovement = sideDirection * moveSpeed * 0.5f * Time.deltaTime; // Двигаемся медленнее вбок
            
            // Применяем движение вбок
            if (characterController != null)
            {
                characterController.Move(unstuckMovement);
            }
            
            if (debug)
            {
                Debug.Log($"[BotController] Попытка распутывания #{unstuckAttempts}: движение вбок {sideDirection}");
            }
        }
        else
        {
            // Метод 2: Принудительное перемещение вперед (если предыдущие попытки не помогли)
            Vector3 currentPos = transform.position;
            currentPos.z += 0.5f; // Принудительно перемещаем на 0.5 единицы вперед
            
            // Используем CharacterController для перемещения (с проверкой коллизий)
            if (characterController != null)
            {
                Vector3 forcedMovement = Vector3.forward * 0.5f;
                characterController.Move(forcedMovement);
            }
            else
            {
                // Если CharacterController недоступен, используем прямой перевод
                transform.position = currentPos;
            }
            
            if (debug)
            {
                Debug.Log($"[BotController] Попытка распутывания #{unstuckAttempts}: принудительное перемещение вперед");
            }
        }
        
        // Если все попытки исчерпаны, сбрасываем счетчик (чтобы не спамить логи)
        if (unstuckAttempts >= maxUnstuckAttempts)
        {
            if (debug)
            {
                Debug.LogError($"[BotController] Бот {gameObject.name} не удалось распутать после {maxUnstuckAttempts} попыток!");
            }
            // Сбрасываем счетчик, чтобы через некоторое время попробовать снова
            unstuckAttempts = 0;
            stuckTimer = 0f;
        }
    }
    
    /// <summary>
    /// Обрабатывает движение бота вперед и применяет гравитацию
    /// Использует CharacterController.Move() один раз (аналогично ThirdPersonController)
    /// ВАЖНО: Бот ВСЕГДА должен двигаться по +Z, даже если движение блокируется
    /// </summary>
    private void HandleMovement()
    {
        if (characterController == null)
        {
            return;
        }
        
        // ВАЖНО: Убеждаемся, что moveSpeed всегда больше 0
        // Если moveSpeed стал 0 или отрицательным, используем базовую скорость
        if (moveSpeed <= 0f)
        {
            if (debug)
            {
                Debug.LogWarning($"[BotController] Бот {gameObject.name} имеет moveSpeed={moveSpeed}, используем базовую скорость {baseMoveSpeed}");
            }
            moveSpeed = baseMoveSpeed > 0f ? baseMoveSpeed : 5f; // Минимум 5 единиц в секунду
            UpdateSpeedFromLevel(); // Пытаемся обновить скорость
        }
        
        // Движение вперед (в направлении увеличения Z)
        // Бот ВСЕГДА движется вперед по +Z
        Vector3 moveDirection = Vector3.forward;
        
        // Бот всегда движется с максимальной скоростью (пока он активен)
        // ВАЖНО: currentSpeed используется для анимации, но движение всегда применяется
        currentSpeed = Mathf.Max(moveSpeed, 0.1f); // Минимум 0.1 для анимации
        
        // Комбинируем горизонтальное движение и вертикальное (гравитация/прыжок)
        Vector3 movement = Vector3.zero;
        
        // Горизонтальное движение (бот ВСЕГДА движется вперед по +Z)
        // ВАЖНО: Даже если движение блокируется, мы все равно пытаемся двигаться
        movement += moveDirection * moveSpeed * Time.deltaTime;
        
        // Поворачиваем бота в направлении движения
        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        
        // Плавный поворот модели (только если не в прыжке)
        if (modelTransform != null && !isJumping)
        {
            modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, 10f * Time.deltaTime);
        }
        
        // Вертикальное движение (гравитация и прыжок) - применяем вместе с горизонтальным
        movement += velocity * Time.deltaTime;
        
        // ВАЖНО: Сохраняем позицию до движения для проверки реального движения
        Vector3 positionBeforeMove = transform.position;
        
        // Применяем движение через CharacterController (ОДИН РАЗ за кадр)
        // ВАЖНО: CharacterController.Move() может вернуть CollisionFlags
        CollisionFlags collisionFlags = characterController.Move(movement);
        
        // Проверяем реальное движение по Z
        Vector3 positionAfterMove = transform.position;
        float actualZMovement = positionAfterMove.z - positionBeforeMove.z;
        
        // Если движение по Z было заблокировано (движение вперед < 0.001), пытаемся обойти препятствие
        if (isGrounded && !isJumping && actualZMovement < 0.001f && moveSpeed > 0.1f)
        {
            // Движение заблокировано - пытаемся обойти препятствие
            // Пробуем небольшое движение вбок, чтобы обойти препятствие
            Vector3 sideMovement = Vector3.right * moveSpeed * 0.3f * Time.deltaTime;
            characterController.Move(sideMovement);
            
            if (debug && Random.Range(0f, 1f) < 0.05f) // Логируем только 5% кадров
            {
                Debug.LogWarning($"[BotController] Бот {gameObject.name} движение по Z заблокировано (actualZMovement={actualZMovement:F4}), пытаемся обойти препятствие");
            }
        }
        
        // Проверяем, было ли движение заблокировано сбоку
        if ((collisionFlags & CollisionFlags.Sides) != 0)
        {
            // Движение было заблокировано сбоку - это может быть препятствие
            // Не логируем каждый кадр, чтобы не спамить
            if (debug && Random.Range(0f, 1f) < 0.01f) // Логируем только 1% кадров
            {
                Debug.Log($"[BotController] Бот {gameObject.name} движение заблокировано сбоку. CollisionFlags: {collisionFlags}, actualZMovement: {actualZMovement:F4}");
            }
        }
    }
    
    /// <summary>
    /// Проверяет столкновения со всеми активными стенами
    /// </summary>
    private void CheckWallCollisions()
    {
        // Защита от удаления сразу после спавна
        if (Time.time - spawnTime < spawnProtectionTime)
        {
            return; // Не проверяем столкновения первые spawnProtectionTime секунд
        }
        
        if (cachedWalls == null || cachedWalls.Length == 0)
        {
            return;
        }
        
        // Периодически обновляем кэш стен
        if (Time.time - lastWallCacheUpdate >= wallCacheUpdateInterval)
        {
            UpdateWallCache();
        }
        
        Vector3 botPos = transform.position;
        
        foreach (WallMovement wall in cachedWalls)
        {
            if (wall == null)
            {
                continue;
            }
            
            Vector3 wallPos = wall.transform.position;
            
            // Проверка X: бот должен быть в диапазоне от minX до maxX
            bool checkX = botPos.x >= minX && botPos.x <= maxX;
            
            if (!checkX)
            {
                continue; // Если X не подходит, пропускаем эту стену
            }
            
            // Проверка Y: бот должен быть выше minY (но не проверяем это строго, так как бот может прыгать)
            // Убираем строгую проверку Y, чтобы боты удалялись при столкновении даже в прыжке
            // bool checkY = botPos.y > minY;
            // if (!checkY) continue;
            
            // Проверка Z: бот должен быть впереди стены (zDifference < 0) в пределах zTolerance
            float zDifference = botPos.z - wallPos.z;
            bool checkZ = zDifference < 0 && zDifference >= -zTolerance;
            
            if (checkZ)
            {
                // Столкновение обнаружено - удаляем бота (независимо от состояния прыжка)
                OnWallCollision();
                return;
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает столкновение со стеной - уничтожает бота
    /// </summary>
    private void OnWallCollision()
    {
        if (debug)
        {
            Debug.Log($"[BotController] Бот {gameObject.name} столкнулся со стеной, удаляем");
        }
        
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Обновляет параметры аниматора
    /// ВАЖНО: Анимация должна соответствовать реальному движению бота
    /// Если бот не двигается по Z, анимация должна быть idle, а не run
    /// </summary>
    private void UpdateAnimator()
    {
        if (animator == null)
        {
            return;
        }
        
        if (characterController == null)
        {
            return;
        }
        
        // ВАЖНО: Проверяем реальное движение по Z для определения анимации
        // Если бот не двигается по Z (застрял), анимация должна быть idle
        float zDelta = Mathf.Abs(transform.position.z - lastZPosition);
        bool isActuallyMoving = zDelta > 0.001f; // Бот действительно двигается по Z
        
        // Бот бежит: на земле И не прыгает И действительно двигается
        bool isRunning = isGrounded && !isJumping && isActuallyMoving && moveSpeed > 0.1f;
        
        if (isRunning)
        {
            // Бот бежит - устанавливаем Speed для анимации бега
            animator.SetBool(IsGroundedHash, true);
            
            // Убеждаемся, что Speed >= 0.1 для запуска анимации бега
            // Используем реальную скорость движения для более точной анимации
            float speedValue = Mathf.Max(currentSpeed, 0.1f);
            animator.SetFloat(SpeedHash, speedValue);
            
            if (debug && !wasGroundedLastFrame)
            {
                Debug.Log($"[BotController] Бот {gameObject.name} приземлился и бежит. Speed={speedValue}, isGrounded={isGrounded}, isJumping={isJumping}, velocity.y={velocity.y:F2}, zDelta={zDelta:F4}");
            }
        }
        else if (isGrounded && !isJumping && !isActuallyMoving)
        {
            // Бот на земле, но не двигается - устанавливаем Speed = 0 (idle анимация)
            animator.SetBool(IsGroundedHash, true);
            animator.SetFloat(SpeedHash, 0f);
            
            if (debug && Random.Range(0f, 1f) < 0.01f) // Логируем только 1% кадров
            {
                Debug.LogWarning($"[BotController] Бот {gameObject.name} на земле, но не двигается! zDelta={zDelta:F4}, moveSpeed={moveSpeed}, currentSpeed={currentSpeed}");
            }
        }
        else
        {
            // Бот прыгает или в воздухе - устанавливаем Speed = 0 и isGrounded = false
            animator.SetBool(IsGroundedHash, false);
            animator.SetFloat(SpeedHash, 0f);
            
            if (debug && isJumping && velocity.y > 0.2f)
            {
                Debug.Log($"[BotController] Бот {gameObject.name} прыгает. isGrounded={isGrounded}, isJumping={isJumping}, velocity.y={velocity.y:F2}");
            }
        }
    }
    
    /// <summary>
    /// Принудительно обновить скорость на основе уровня (можно вызвать извне при изменении уровня)
    /// </summary>
    public void RefreshSpeed()
    {
        UpdateSpeedFromLevel();
    }
    
    /// <summary>
    /// Установить базовую скорость движения
    /// </summary>
    public void SetBaseMoveSpeed(float speed)
    {
        baseMoveSpeed = speed;
        UpdateSpeedFromLevel();
    }
    
    /// <summary>
    /// Установить множитель уровня скорости
    /// </summary>
    public void SetSpeedLevelScaler(float scaler)
    {
        speedLevelScaler = scaler;
        UpdateSpeedFromLevel();
    }
    
    /// <summary>
    /// Установить использование уровня скорости из GameStorage
    /// </summary>
    public void SetUseSpeedLevel(bool use)
    {
        useSpeedLevel = use;
        UpdateSpeedFromLevel();
    }
    
    /// <summary>
    /// Установить силу прыжка
    /// </summary>
    public void SetJumpForce(float force)
    {
        jumpForce = force;
    }
    
    /// <summary>
    /// Установить минимальный интервал между прыжками
    /// </summary>
    public void SetJumpIntervalMin(float minInterval)
    {
        jumpIntervalMin = minInterval;
        // Обновляем текущий интервал, если нужно
        if (currentJumpInterval < jumpIntervalMin)
        {
            UpdateJumpInterval();
        }
    }
    
    /// <summary>
    /// Установить максимальный интервал между прыжками
    /// </summary>
    public void SetJumpIntervalMax(float maxInterval)
    {
        jumpIntervalMax = maxInterval;
        // Обновляем текущий интервал, если нужно
        if (currentJumpInterval > jumpIntervalMax)
        {
            UpdateJumpInterval();
        }
    }
    
    /// <summary>
    /// Установить интервалы между прыжками (для обратной совместимости)
    /// </summary>
    public void SetJumpInterval(float minInterval, float maxInterval)
    {
        jumpIntervalMin = minInterval;
        jumpIntervalMax = maxInterval;
        UpdateJumpInterval();
    }
    
    /// <summary>
    /// Установить гравитацию
    /// </summary>
    public void SetGravity(float newGravity)
    {
        gravity = newGravity;
    }
    
    /// <summary>
    /// Установить расстояние проверки земли
    /// </summary>
    public void SetGroundCheckDistance(float distance)
    {
        groundCheckDistance = distance;
    }
    
    /// <summary>
    /// Устанавливает LayerMask для определения земли
    /// </summary>
    public void SetGroundLayerMask(LayerMask layerMask)
    {
        groundLayerMask = layerMask;
    }
    
    /// <summary>
    /// Устанавливает время жизни бота в секундах (0 = бесконечное время жизни)
    /// </summary>
    public void SetLifetime(float newLifetime)
    {
        lifetime = newLifetime;
    }
}
