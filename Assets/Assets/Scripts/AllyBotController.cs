using UnityEngine;

/// <summary>
/// Контроллер союзников-ботов.
/// Боты автоматически атакуют босса во время боя.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class AllyBotController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Скорость сближения с боссом")]
    [SerializeField] private float approachSpeed = 4f;
    
    [Tooltip("Скорость возврата на исходную позицию после удара (обычно выше)")]
    [SerializeField] private float returnSpeed = 10f;
    
    [Tooltip("Скорость поворота бота")]
    [SerializeField] private float rotationSpeed = 5f;
    
    [Tooltip("Расстояние атаки по горизонтали XZ (когда бот близко к боссу, он атакует)")]
    [SerializeField] private float attackDistance = 3f;
    
    [Header("Attack Settings")]
    [Tooltip("Урон от атаки бота (если не задан извне через Initialize)")]
    [SerializeField] private float attackDamage = 5f;
    
    [Tooltip("Интервал между атаками (в секундах)")]
    [SerializeField] private float attackCooldown = 1.5f;
    
    [Tooltip("Дистанция до точки спавна, при которой считаем, что бот вернулся")]
    [SerializeField] private float homeReachedDistance = 0.5f;
    
    [Header("References")]
    [Tooltip("Модель бота (для поворота)")]
    [SerializeField] private Transform modelTransform;
    
    [Tooltip("Аниматор бота")]
    [SerializeField] private Animator animator;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private CharacterController characterController;
    private BattleManager battleManager;
    private BossController bossController;
    
    // Состояние
    private bool isInitialized = false;
    private float lastAttackTime = 0f;
    private Transform bossTransform;
    private Vector3 homePosition;
    private float spawnTime;
    private float startDelay;
    
    private enum BotState { GoingToBoss, ReturningHome }
    private BotState state = BotState.GoingToBoss;
    
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
        
        // Регистрируем бота в BattleManager
        if (battleManager != null)
        {
            battleManager.AddAlly(gameObject);
        }
    }
    
    private void Update()
    {
        if (battleManager == null || !battleManager.IsBattleActive())
        {
            return;
        }
        
        if (Time.time < spawnTime + startDelay)
            return;
        
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<BossController>();
            if (bossController != null)
                bossTransform = bossController.transform;
        }
        
        if (state == BotState.GoingToBoss)
        {
            if (bossTransform == null) return;
            MoveTowardsBoss();
            if (CheckAttackAndMaybeReturnHome())
                state = BotState.ReturningHome;
        }
        else
        {
            MoveTowardsHome();
            if (IsHomeReached())
                state = BotState.GoingToBoss;
        }
    }
    
    /// <summary>
    /// Инициализирует бота (место спавна и урон извне, например из BotSpawner).
    /// </summary>
    public void Initialize(Vector3 spawnPosition, float damage)
    {
        transform.position = spawnPosition;
        homePosition = spawnPosition;
        attackDamage = damage;
        spawnTime = Time.time;
        isInitialized = true;
        state = BotState.GoingToBoss;
        
        if (debug)
        {
            Debug.Log($"[AllyBotController] Бот инициализирован на позиции: {spawnPosition}, урон: {damage}");
        }
    }
    
    /// <summary>
    /// Задержка старта с начала битвы (1 раз): бот не движется и не бьёт до spawnTime + delay.
    /// Вызывается из BotSpawner: 1-й бот 0, 2-й n, 3-й n*2 и т.д.
    /// </summary>
    public void SetStartDelay(float delay)
    {
        startDelay = delay;
    }
    
    /// <summary>
    /// Инициализирует бота только позицией (урон остаётся из SerializeField).
    /// </summary>
    public void Initialize(Vector3 spawnPosition)
    {
        Initialize(spawnPosition, attackDamage);
    }
    
    /// <summary>
    /// Задаёт скорости сближения и возврата (вызывается из BotSpawner для настройки из инспектора).
    /// </summary>
    public void SetSpeeds(float approach, float returnSpd)
    {
        approachSpeed = approach;
        returnSpeed = returnSpd;
    }
    
    /// <summary>
    /// Движется к боссу
    /// </summary>
    private void MoveTowardsBoss()
    {
        if (bossTransform == null) return;
        
        // Вычисляем направление к боссу
        Vector3 direction = (bossTransform.position - transform.position);
        direction.y = 0f; // Игнорируем вертикальную составляющую
        direction.Normalize();
        
        // Движемся к боссу (скорость сближения)
        Vector3 movement = direction * approachSpeed * Time.deltaTime;
        characterController.Move(movement);
        
        // Поворачиваемся к боссу
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
    }
    
    /// <summary>
    /// Движется к месту спавна (домой) — быстро на исходную позицию.
    /// </summary>
    private void MoveTowardsHome()
    {
        Vector3 toHome = homePosition - transform.position;
        toHome.y = 0f;
        if (toHome.sqrMagnitude < 0.0001f) return;
        toHome.Normalize();
        Vector3 movement = toHome * returnSpeed * Time.deltaTime;
        characterController.Move(movement);
        if (toHome.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toHome);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            if (modelTransform != null)
                modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    private bool IsHomeReached()
    {
        Vector3 flatPos = transform.position;
        flatPos.y = 0f;
        Vector3 flatHome = homePosition;
        flatHome.y = 0f;
        return Vector3.Distance(flatPos, flatHome) <= homeReachedDistance;
    }
    
    /// <summary>
    /// Проверяет возможность атаки; при ударе возвращает true (пора идти домой).
    /// Дистанция считается по горизонтали (XZ), чтобы разница по Y не мешала удару.
    /// </summary>
    private bool CheckAttackAndMaybeReturnHome()
    {
        if (bossTransform == null) return false;
        if (Time.time - lastAttackTime < attackCooldown) return false;
        
        Vector3 a = transform.position;
        Vector3 b = bossTransform.position;
        a.y = 0f;
        b.y = 0f;
        float distanceToBossXZ = Vector3.Distance(a, b);
        if (distanceToBossXZ <= attackDistance)
        {
            AttackBoss();
            lastAttackTime = Time.time;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Атакует босса (без анимации).
    /// </summary>
    private void AttackBoss()
    {
        if (bossController == null) return;
        
        bossController.TakeDamage(attackDamage);
        
        if (debug)
        {
            Debug.Log($"[AllyBotController] Бот атаковал босса, урон: {attackDamage}");
        }
    }
    
    /// <summary>
    /// Наносит урон боту (вызывается из BattleManager)
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (battleManager != null)
        {
            battleManager.DamageAlly(gameObject, damage);
        }
    }
    
    private void OnDestroy()
    {
        // Удаляем бота из BattleManager
        if (battleManager != null)
        {
            battleManager.RemoveAlly(gameObject);
        }
    }
}
