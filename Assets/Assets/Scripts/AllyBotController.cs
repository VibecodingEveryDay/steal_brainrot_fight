using UnityEngine;

/// <summary>
/// Контроллер союзников-ботов.
/// Боты автоматически атакуют босса во время боя.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class AllyBotController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Скорость движения бота")]
    [SerializeField] private float moveSpeed = 4f;
    
    [Tooltip("Скорость поворота бота")]
    [SerializeField] private float rotationSpeed = 5f;
    
    [Tooltip("Расстояние атаки (когда бот близко к боссу, он атакует)")]
    [SerializeField] private float attackDistance = 2f;
    
    [Header("Attack Settings")]
    [Tooltip("Урон от атаки бота")]
    [SerializeField] private float attackDamage = 5f;
    
    [Tooltip("Интервал между атаками (в секундах)")]
    [SerializeField] private float attackCooldown = 1.5f;
    
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
        // Проверяем, активен ли бой
        if (battleManager == null || !battleManager.IsBattleActive())
        {
            // Если бой не активен, останавливаем бота
            if (animator != null)
            {
                animator.SetFloat(SpeedHash, 0f);
            }
            return;
        }
        
        // Находим BossController если нужно
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<BossController>();
            if (bossController != null)
            {
                bossTransform = bossController.transform;
            }
        }
        
        // Если босс не найден, ничего не делаем
        if (bossTransform == null) return;
        
        // Движемся к боссу и атакуем
        MoveTowardsBoss();
        CheckAttack();
    }
    
    /// <summary>
    /// Инициализирует бота
    /// </summary>
    public void Initialize(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;
        isInitialized = true;
        
        if (debug)
        {
            Debug.Log($"[AllyBotController] Бот инициализирован на позиции: {spawnPosition}");
        }
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
        
        // Движемся к боссу
        Vector3 movement = direction * moveSpeed * Time.deltaTime;
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
        if (bossTransform == null) return;
        if (Time.time - lastAttackTime < attackCooldown) return;
        
        // Проверяем расстояние до босса
        float distanceToBoss = Vector3.Distance(transform.position, bossTransform.position);
        if (distanceToBoss <= attackDistance)
        {
            // Атакуем босса
            AttackBoss();
            lastAttackTime = Time.time;
        }
    }
    
    /// <summary>
    /// Атакует босса
    /// </summary>
    private void AttackBoss()
    {
        if (bossController == null) return;
        
        // Запускаем анимацию атаки
        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
        
        // Наносим урон боссу
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
