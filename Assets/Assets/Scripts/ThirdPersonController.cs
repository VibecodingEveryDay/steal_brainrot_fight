using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if EnvirData_yg
using YG;
#endif

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float baseMoveSpeed = 5f; // Базовая скорость при уровне 0
    [SerializeField] private float speedLevelScaler = 1f; // Множитель для расчета скорости на основе уровня
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float gravity = -9.81f;
    
    [Header("Speed Level Settings")]
    [Tooltip("Использовать уровень скорости из GameStorage (если true, moveSpeed будет вычисляться на основе уровня)")]
    [SerializeField] private bool useSpeedLevel = true;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения о скорости")]
    [SerializeField] private bool debugSpeed = false;
    
    private float moveSpeed; // Вычисляемая скорость (может изменяться на основе уровня)
    
    [Header("References")]
    [SerializeField] private Transform modelTransform; // Дочерний объект с моделью
    [SerializeField] private Animator animator;
    [SerializeField] private ThirdPersonCamera cameraController;
    
    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    
    [Header("Jump Rotation")]
    [SerializeField] private float jumpRotationAngle = 10f; // Угол поворота модели при прыжке
    
    [Header("Jump Animation Threshold")]
    [SerializeField] private float minJumpVelocity = 2.0f; // Минимальная вертикальная скорость для запуска анимации прыжка
    [SerializeField] private float minJumpYDelta = 0.25f; // Минимальное изменение Y для запуска анимации прыжка
    
    private CharacterController characterController;
    private Vector3 velocity;
    private bool isGrounded;
    private float currentSpeed;
    private bool jumpRequested = false; // Запрос на прыжок от кнопки
    private float jumpRequestTime = -1f; // Время запроса прыжка (для обработки с небольшой задержкой)
    private const float jumpRequestWindow = 0.3f; // Окно времени для обработки запроса прыжка (в секундах) - увеличено для надежности
    private bool isJumping = false; // Флаг прыжка для поворота модели
    private Quaternion savedModelRotation; // Сохраненный поворот модели перед прыжком
    private float previousYPosition; // Предыдущая Y позиция для отслеживания изменения
    private bool wasJumpingLastFrame = false; // Флаг прыжка в предыдущем кадре
    
    // Ввод от джойстика (для мобильных устройств)
    private Vector2 joystickInput = Vector2.zero;
    
    // GameStorage для получения уровня скорости
    private GameStorage gameStorage;
    
    // Флаг готовности игры (блокирует управление до инициализации GameReady)
    private bool isGameReady = false;
    
    // Параметры аниматора
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsTakingHash = Animator.StringToHash("IsTaking");
    private static readonly int IsJabHash = Animator.StringToHash("IsJab");
    private static readonly int IsUpperCutJabHash = Animator.StringToHash("IsUpperCutJab");
    private static readonly int IsStrongBeat1Hash = Animator.StringToHash("IsStrongBeat1");
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        previousYPosition = transform.position.y;
        
        // Автоматически найти дочерний объект с моделью, если не назначен
        if (modelTransform == null)
        {
            // Ищем дочерний объект с Animator
            Animator childAnimator = GetComponentInChildren<Animator>();
            if (childAnimator != null)
            {
                modelTransform = childAnimator.transform;
            }
        }
        
        // Автоматически найти Animator, если не назначен
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        // ВАЖНО: Отключаем Apply Root Motion в Animator, чтобы анимации не влияли на позицию модели
        // Это предотвращает смещение дочерней модели из-за анимаций
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
        
        // Автоматически найти камеру, если не назначена
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<ThirdPersonCamera>();
        }
        
        // Инициализируем скорость на основе базовой скорости
        if (!useSpeedLevel)
        {
            moveSpeed = baseMoveSpeed;
        }
    }
    
    private void Start()
    {
        // Получаем ссылку на GameStorage
        gameStorage = GameStorage.Instance;
        
        // Обновляем скорость на основе уровня при старте
        if (useSpeedLevel && gameStorage != null)
        {
            UpdateSpeedFromLevel();
        }
        else if (!useSpeedLevel)
        {
            moveSpeed = baseMoveSpeed;
        }
        
        // Проверяем готовность игры
        CheckGameReady();
        
        // Подписываемся на событие получения данных SDK (если используется YG2)
#if EnvirData_yg
        if (YG2.onGetSDKData != null)
        {
            YG2.onGetSDKData += OnSDKDataReceived;
        }
#endif
    }
    
    private void OnEnable()
    {
        // Обновляем скорость при включении объекта (на случай если GameStorage был инициализирован после Start)
        if (useSpeedLevel)
        {
            if (gameStorage == null)
            {
                gameStorage = GameStorage.Instance;
            }
            if (gameStorage != null)
            {
                UpdateSpeedFromLevel();
            }
        }
        
        // Проверяем готовность игры при включении
        CheckGameReady();
    }
    
    private void OnDisable()
    {
        // Отписываемся от событий
#if EnvirData_yg
        if (YG2.onGetSDKData != null)
        {
            YG2.onGetSDKData -= OnSDKDataReceived;
        }
#endif
    }
    
    /// <summary>
    /// Проверяет, готов ли GameReady (используя рефлексию для доступа к приватному полю)
    /// </summary>
    private void CheckGameReady()
    {
#if EnvirData_yg
        // Используем рефлексию для проверки gameReadyDone
        var gameReadyType = typeof(YG2);
        var gameReadyDoneField = gameReadyType.GetField("gameReadyDone", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        if (gameReadyDoneField != null)
        {
            bool gameReadyDone = (bool)gameReadyDoneField.GetValue(null);
            if (gameReadyDone && !isGameReady)
            {
                isGameReady = true;
                Debug.Log("[ThirdPersonController] GameReady инициализирован, управление разблокировано");
            }
        }
        else
        {
            // Если рефлексия не работает, проверяем через задержку (fallback)
            StartCoroutine(CheckGameReadyDelayed());
        }
#else
        // Если YG2 не используется, сразу разблокируем управление
        isGameReady = true;
#endif
    }
    
    /// <summary>
    /// Проверяет GameReady с задержкой (fallback метод)
    /// </summary>
    private System.Collections.IEnumerator CheckGameReadyDelayed()
    {
        // Ждем немного и проверяем снова
        yield return new WaitForSeconds(0.5f);
        CheckGameReady();
        
        // Если все еще не готово, разблокируем через 3 секунды (на случай проблем)
        if (!isGameReady)
        {
            yield return new WaitForSeconds(2.5f);
            if (!isGameReady)
            {
                isGameReady = true;
                Debug.LogWarning("[ThirdPersonController] GameReady не обнаружен, управление разблокировано по таймауту");
            }
        }
    }
    
    /// <summary>
    /// Вызывается при получении данных SDK
    /// </summary>
    private void OnSDKDataReceived()
    {
        CheckGameReady();
    }
    
    private void Update()
    {
        // Проверяем, что CharacterController активен и не null перед выполнением обновлений
        if (characterController == null || !characterController.enabled || !gameObject.activeInHierarchy)
        {
            return;
        }
        
        // Периодически проверяем готовность игры, пока она не готова
        if (!isGameReady)
        {
            CheckGameReady();
        }
        
        HandleGroundCheck();
        HandleJump();
        ApplyGravity();
        HandleMovement();
        UpdateAnimator();
        
        // Сохраняем состояние прыжка для следующего кадра
        wasJumpingLastFrame = isJumping;
    }
    
    private void LateUpdate()
    {
        // Применяем компенсацию поворота после обновления анимации
        HandleJumpRotation();
    }
    
    private void HandleGroundCheck()
    {
        // Проверка земли через CharacterController (основной метод)
        isGrounded = characterController.isGrounded;
        
        // Дополнительная проверка через Raycast только если CharacterController говорит что не на земле
        // Это помогает определить, действительно ли персонаж в воздухе или просто небольшой зазор
        if (!isGrounded)
        {
            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 0.1f;
            // Если Raycast находит землю близко, считаем что персонаж на земле
            if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance + 0.2f))
            {
                // Проверяем расстояние до земли
                float distanceToGround = hit.distance;
                if (distanceToGround <= groundCheckDistance + 0.1f)
                {
                    isGrounded = true;
                }
            }
        }
        
        // Сброс вертикальной скорости при приземлении
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Небольшая отрицательная скорость для удержания на земле
            // Сбрасываем флаг прыжка при приземлении
            isJumping = false;
        }
    }
    
    private void HandleMovement()
    {
        // Блокируем управление, если игра не готова
        if (!isGameReady)
        {
            return;
        }
        
        // Получаем ввод с клавиатуры или джойстика
        float horizontal = 0f; // A/D
        float vertical = 0f; // W/S
        
        // Приоритет джойстику на мобильных устройствах
        if (joystickInput.magnitude > 0.1f)
        {
            horizontal = joystickInput.x;
            vertical = joystickInput.y;
        }
        else
        {
            // Используем клавиатуру, если джойстик не активен
#if ENABLE_INPUT_SYSTEM
            // Новый Input System
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    horizontal -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    horizontal += 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    vertical += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    vertical -= 1f;
            }
#else
            // Старый Input System
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
#endif
        }
        
        // Вычисляем направление движения относительно камеры
        Vector3 moveDirection = Vector3.zero;
        
        if (cameraController != null)
        {
            // Получаем направление камеры (только горизонтальное вращение)
            Vector3 cameraForward = cameraController.GetCameraForward();
            Vector3 cameraRight = cameraController.GetCameraRight();
            
            // Нормализуем векторы камеры и убираем вертикальную составляющую
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            // Вычисляем направление движения относительно камеры
            moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;
        }
        else
        {
            // Если камера не найдена, используем мировые оси
            moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        }
        
        // Вычисляем скорость движения
        currentSpeed = moveDirection.magnitude * moveSpeed;
        
        // Применяем движение через CharacterController
        if (moveDirection.magnitude > 0.1f)
        {
            // Движение - проверяем, что CharacterController активен перед вызовом Move
            if (characterController != null && characterController.enabled)
            {
            characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
            }
            
            // Плавный поворот корневого объекта в сторону движения
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // Плавный поворот модели для визуального эффекта (только если не в прыжке)
            if (modelTransform != null && !isJumping)
            {
                modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
    
    private void HandleJump()
    {
        // Блокируем прыжок, если игра не готова
        if (!isGameReady)
        {
            return;
        }
        
        // Проверяем нажатие Space или кнопки прыжка
        bool jumpPressedThisFrame = false;
        
#if ENABLE_INPUT_SYSTEM
        // Новый Input System
        if (Keyboard.current != null)
        {
            jumpPressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#else
        // Старый Input System
        jumpPressedThisFrame = Input.GetKeyDown(KeyCode.Space);
#endif
        
        // ВАЖНО: Сохраняем ВСЕ нажатия Space как запросы, даже если персонаж не на земле в момент нажатия
        // Это исправляет баг, когда 30% прыжков не срабатывают из-за неточной проверки isGrounded
        if (jumpPressedThisFrame)
        {
            jumpRequested = true;
            jumpRequestTime = Time.time;
        }
        
        // Также проверяем запрос от кнопки прыжка (для мобильных устройств)
        // Запрос уже установлен через метод Jump(), просто обновляем время если нужно
        
        // Проверяем, есть ли активный запрос прыжка (в пределах окна времени)
        bool hasActiveJumpRequest = jumpRequested && (jumpRequestTime >= 0f && Time.time - jumpRequestTime <= jumpRequestWindow);
        
        // Выполняем прыжок, если есть активный запрос И персонаж на земле
        if (hasActiveJumpRequest && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            isJumping = true; // Устанавливаем флаг прыжка для поворота модели
            // Сохраняем текущий поворот модели перед прыжком для компенсации
            if (modelTransform != null)
            {
                savedModelRotation = modelTransform.rotation;
            }
            
            // Сбрасываем запрос после успешного прыжка
            jumpRequested = false;
            jumpRequestTime = -1f;
        }
        
        // Сбрасываем устаревшие запросы (если прошло слишком много времени)
        if (jumpRequested && jumpRequestTime >= 0f && Time.time - jumpRequestTime > jumpRequestWindow)
        {
            jumpRequested = false;
            jumpRequestTime = -1f;
        }
        
        // Сбрасываем флаг прыжка при приземлении
        if (isGrounded && isJumping && velocity.y <= 0)
        {
            isJumping = false;
        }
    }
    
    /// <summary>
    /// Обработка поворота модели во время прыжка
    /// Компенсирует возможный поворот анимации прыжка на -10 градусов по Y
    /// </summary>
    private void HandleJumpRotation()
    {
        if (modelTransform == null || !isJumping) return;
        
        // Анимация прыжка поворачивает модель на -10 градусов по Y каждый кадр
        // Компенсируем это, устанавливая поворот модели равным базовому повороту + компенсация
        // Это перезаписывает поворот анимации и предотвращает накопление ошибки
        Quaternion baseRotation = transform.rotation;
        Quaternion compensationRotation = Quaternion.Euler(0f, jumpRotationAngle, 0f);
        
        // Устанавливаем поворот модели напрямую, игнорируя поворот анимации
        // LateUpdate гарантирует, что это применяется после обновления анимации
        modelTransform.rotation = baseRotation * compensationRotation;
    }
    
    /// <summary>
    /// Публичный метод для прыжка (вызывается из UI кнопки)
    /// </summary>
    public void Jump()
    {
        // Устанавливаем запрос на прыжок, который будет обработан в HandleJump()
        // Сохраняем запрос даже если персонаж не на земле в момент вызова
        // Это позволяет обработать прыжок в следующем кадре, когда персонаж уже на земле
            jumpRequested = true;
        jumpRequestTime = Time.time;
    }
    
    private void ApplyGravity()
    {
        // Проверяем, что CharacterController активен и не null
        if (characterController == null || !characterController.enabled || !gameObject.activeInHierarchy)
        {
            return;
        }
        
        // Применяем гравитацию
        velocity.y += gravity * Time.deltaTime;
        
        // Применяем вертикальное движение
        characterController.Move(velocity * Time.deltaTime);
    }
    
    private void UpdateAnimator()
    {
        if (animator != null)
        {
            // ВАЖНО: Проверяем, находится ли игрок в области дома
            // Если да, не обновляем аниматор (отключаем анимацию)
            if (IsPlayerInHouseArea())
            {
                // Игрок в области дома - не обновляем аниматор
                return;
            }
            
            // Обновляем параметр Speed
            animator.SetFloat(SpeedHash, currentSpeed);
            
            // Определяем, нужно ли считать игрока на земле для аниматора
            bool animatorIsGrounded = isGrounded;
            
            // Если игрок не на земле, проверяем, был ли это реальный прыжок
            if (!isGrounded)
            {
                // Вычисляем изменение Y позиции
                float currentYPosition = transform.position.y;
                float yDelta = currentYPosition - previousYPosition;
                
                // Проверяем несколько условий для определения реального прыжка:
                // 1. Флаг isJumping установлен (реальный прыжок был инициирован) - самый надежный индикатор
                // 2. Или была вертикальная скорость выше порога (для случаев, когда флаг не успел установиться)
                // 3. Или изменение Y позиции вверх больше порога (дополнительная проверка)
                // Используем флаг прыжка как основной критерий, остальные - только как дополнение
                bool isRealJump = isJumping || wasJumpingLastFrame;
                
                // Дополнительные проверки только если флаг прыжка не установлен
                if (!isRealJump)
                {
                    // Проверяем вертикальную скорость (должна быть достаточно большой)
                    bool hasHighVelocity = Mathf.Abs(velocity.y) >= minJumpVelocity;
                    
                    // Проверяем изменение Y позиции (должно быть достаточно большим)
                    bool hasSignificantYChange = yDelta >= minJumpYDelta;
                    
                    // Оба условия должны выполняться одновременно для дополнительной проверки
                    isRealJump = hasHighVelocity && hasSignificantYChange;
                }
                
                // Если это не реальный прыжок, считаем что персонаж на земле (для анимации)
                if (!isRealJump)
                {
                    animatorIsGrounded = true;
                }
                
                // Сохраняем текущую Y позицию для следующего кадра
                previousYPosition = currentYPosition;
            }
            else
            {
                // Если на земле, обновляем предыдущую позицию
                previousYPosition = transform.position.y;
            }
            
            // Обновляем параметр isGrounded в аниматоре
            animator.SetBool(IsGroundedHash, animatorIsGrounded);
        }
    }
    
    /// <summary>
    /// Установить параметр IsTaking в аниматоре
    /// </summary>
    public void SetIsTaking(bool value)
    {
        if (animator != null)
        {
            animator.SetBool(IsTakingHash, value);
        }
    }
    
    // Публичные методы для получения состояния (могут быть полезны для других скриптов)
    public bool IsGrounded()
    {
        return isGrounded;
    }
    
    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }
    
    public Vector3 GetVelocity()
    {
        return characterController.velocity;
    }
    
    /// <summary>
    /// Установить ввод от джойстика (вызывается из JoystickManager)
    /// </summary>
    public void SetJoystickInput(Vector2 input)
    {
        joystickInput = input;
    }
    
    /// <summary>
    /// Обновляет скорость на основе уровня из GameStorage
    /// </summary>
    private void UpdateSpeedFromLevel()
    {
        if (!useSpeedLevel)
        {
            moveSpeed = baseMoveSpeed;
            return;
        }
        
        // Если GameStorage еще не инициализирован, пытаемся получить его
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        if (gameStorage == null)
        {
            moveSpeed = baseMoveSpeed;
            return;
        }
        
        int speedLevel = gameStorage.GetPlayerSpeedLevel();
        moveSpeed = baseMoveSpeed + (speedLevel * speedLevelScaler);
        
        if (debugSpeed)
        {
            Debug.Log($"[ThirdPersonController] Скорость обновлена: baseMoveSpeed={baseMoveSpeed}, speedLevel={speedLevel}, speedLevelScaler={speedLevelScaler}, moveSpeed={moveSpeed}");
        }
    }
    
    /// <summary>
    /// Установить скорость движения вручную (вызывается из ShopSpeedManager)
    /// </summary>
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }
    
    /// <summary>
    /// Получить текущую скорость движения
    /// </summary>
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
    
    /// <summary>
    /// Принудительно обновить скорость на основе уровня (можно вызвать из ShopSpeedManager после покупки)
    /// </summary>
    public void RefreshSpeedFromLevel()
    {
        UpdateSpeedFromLevel();
    }
    
    /// <summary>
    /// Получить базовую скорость движения
    /// </summary>
    public float GetBaseMoveSpeed()
    {
        return baseMoveSpeed;
    }
    
    /// <summary>
    /// Получить множитель уровня скорости
    /// </summary>
    public float GetSpeedLevelScaler()
    {
        return speedLevelScaler;
    }
    
    /// <summary>
    /// Вычислить скорость на основе уровня (для отображения в UI)
    /// </summary>
    public float CalculateSpeedFromLevel(int level)
    {
        return baseMoveSpeed + (level * speedLevelScaler);
    }
    
    /// <summary>
    /// Проверяет, находится ли игрок в области дома
    /// </summary>
    private bool IsPlayerInHouseArea()
    {
        // Получаем TeleportManager для доступа к housePos
        TeleportManager teleportManager = TeleportManager.Instance;
        if (teleportManager == null)
        {
            return false;
        }
        
        // Получаем housePos через рефлексию (так как поле приватное)
        System.Reflection.FieldInfo housePosField = typeof(TeleportManager).GetField("housePos", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (housePosField == null)
        {
            return false;
        }
        
        Transform housePos = housePosField.GetValue(teleportManager) as Transform;
        if (housePos == null)
        {
            return false;
        }
        
        // Получаем позицию и масштаб области дома
        Vector3 housePosition = housePos.position;
        Vector3 houseScale = housePos.localScale;
        
        // Вычисляем границы области дома (используем масштаб как размер области)
        float halfWidth = houseScale.x / 2f;
        float halfHeight = houseScale.y / 2f;
        float halfDepth = houseScale.z / 2f;
        
        // Получаем позицию игрока
        Vector3 playerPosition = transform.position;
        
        // Проверяем, находится ли игрок в пределах области дома
        bool inXRange = Mathf.Abs(playerPosition.x - housePosition.x) <= halfWidth;
        bool inYRange = Mathf.Abs(playerPosition.y - housePosition.y) <= halfHeight;
        bool inZRange = Mathf.Abs(playerPosition.z - housePosition.z) <= halfDepth;
        
        return inXRange && inYRange && inZRange;
    }
}
