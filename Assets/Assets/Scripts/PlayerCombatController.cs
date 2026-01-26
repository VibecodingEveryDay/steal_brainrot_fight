using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Управляет анимациями ударов игрока и ультимейтом.
/// E - случайные удары (IsJab или IsUpperCrossJab).
/// Ультимейт - активация IsStrongBeat1 при близости к боссу.
/// </summary>
[RequireComponent(typeof(ThirdPersonController))]
public class PlayerCombatController : MonoBehaviour
{
    [Header("Attack Settings")]
    [Tooltip("Клавиша для удара")]
    [SerializeField] private KeyCode attackKey = KeyCode.E;
    
    [Tooltip("Кулдаун между ударами (в секундах)")]
    [SerializeField] private float attackCooldown = 0.5f;
    
    [Tooltip("Урон от обычного удара")]
    [SerializeField] private float baseAttackDamage = 10f;
    
    [Tooltip("Максимальное расстояние для нанесения урона боссу")]
    [SerializeField] private float attackRange = 3f;
    
    [Header("Ultimate Settings")]
    [Tooltip("Расстояние для активации ультимейта (близость к боссу)")]
    [SerializeField] private float ultimateActivationDistance = 3f;
    
    [Tooltip("Урон от ультимейта")]
    [SerializeField] private float ultimateDamage = 50f;
    
    [Header("References")]
    [Tooltip("Ссылка на ThirdPersonController")]
    [SerializeField] private ThirdPersonController thirdPersonController;
    
    [Tooltip("Ссылка на UltimateProgressBar")]
    [SerializeField] private UltimateProgressBar ultimateProgressBar;
    
    [Header("VFX Settings")]
    [Tooltip("Префаб VFX эффекта для удара")]
    [SerializeField] private GameObject attackVFXPrefab;
    
    [Tooltip("Количество VFX эффектов за удар")]
    [SerializeField] private int vfxCountPerAttack = 3;
    
    [Tooltip("Смещение VFX эффектов относительно игрока (по оси X)")]
    [SerializeField] private float vfxOffsetX = 0.5f;
    
    [Tooltip("Смещение VFX эффектов относительно игрока (по оси Y)")]
    [SerializeField] private float vfxOffsetY = 1f;
    
    [Tooltip("Смещение VFX эффектов относительно игрока (по оси Z)")]
    [SerializeField] private float vfxOffsetZ = 0.5f;
    
    [Tooltip("Радиус разброса VFX эффектов")]
    [SerializeField] private float vfxSpreadRadius = 0.3f;
    
    [Tooltip("Время жизни VFX эффекта (в секундах, 0 = не уничтожать автоматически)")]
    [SerializeField] private float vfxLifetime = 2f;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private Animator animator;
    private BattleManager battleManager;
    private BossController bossController;
    private AttackPowerSystem attackPowerSystem;
    private Transform playerTransform;
    
    // Состояние
    private float lastAttackTime = 0f;
    private bool isAttacking = false;
    
    // Список активных VFX эффектов для обновления позиции
    private List<VFXData> activeVFXList = new List<VFXData>();
    
    /// <summary>
    /// Данные VFX эффекта для отслеживания
    /// </summary>
    private class VFXData
    {
        public GameObject vfxInstance;
        public Vector3 localOffset; // Локальное смещение (X, Y, Z относительно игрока)
        public float spawnTime;
        public float lifetime;
        
        public VFXData(GameObject instance, Vector3 localOffset, float lifetime)
        {
            this.vfxInstance = instance;
            this.localOffset = localOffset;
            this.spawnTime = Time.time;
            this.lifetime = lifetime;
        }
    }
    
    // Аниматор параметры
    private static readonly int IsJabHash = Animator.StringToHash("IsJab");
    private static readonly int IsUpperCutJabHash = Animator.StringToHash("IsUpperCutJab");
    private static readonly int IsStrongBeat1Hash = Animator.StringToHash("IsStrongBeat1");
    
    private void Awake()
    {
        // Сохраняем Transform игрока
        playerTransform = transform;
        
        // Находим ThirdPersonController
        if (thirdPersonController == null)
        {
            thirdPersonController = GetComponent<ThirdPersonController>();
        }
        
        // Находим Animator
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        // Проверяем наличие аниматора
        if (animator == null)
        {
            Debug.LogError("[PlayerCombatController] Animator не найден! Добавьте компонент Animator к игроку или его дочерним объектам.");
        }
        else if (debug)
        {
            Debug.Log($"[PlayerCombatController] Animator найден: {animator.name}");
            
            // Проверяем наличие параметров в аниматоре
            bool hasIsJab = false;
            bool hasIsUpperCutJab = false;
            
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "IsJab" && param.type == AnimatorControllerParameterType.Trigger)
                {
                    hasIsJab = true;
                }
                if (param.name == "IsUpperCutJab" && param.type == AnimatorControllerParameterType.Trigger)
                {
                    hasIsUpperCutJab = true;
                }
            }
            
            if (!hasIsJab)
            {
                Debug.LogWarning("[PlayerCombatController] Параметр 'IsJab' (Trigger) не найден в Animator Controller!");
            }
            if (!hasIsUpperCutJab)
            {
                Debug.LogWarning("[PlayerCombatController] Параметр 'IsUpperCutJab' (Trigger) не найден в Animator Controller!");
            }
        }
    }
    
    private void Start()
    {
        // Находим BattleManager
        battleManager = BattleManager.Instance;
        
        // Находим AttackPowerSystem
        attackPowerSystem = AttackPowerSystem.Instance;
    }
    
    private void Update()
    {
        // Обрабатываем ввод для удара (работает всегда, не только в бою)
        HandleAttackInput();
        
        // Обновляем позиции активных VFX эффектов
        UpdateVFXPositions();
        
        // Проверяем, активен ли бой для остальной логики
        if (battleManager == null || !battleManager.IsBattleActive())
        {
            return;
        }
        
        // Находим BossController если нужно
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<BossController>();
        }
        
        // Проверяем возможность активации ультимейта
        CheckUltimateActivation();
    }
    
    /// <summary>
    /// Обрабатывает ввод для удара
    /// </summary>
    private void HandleAttackInput()
    {
        // Проверяем кулдаун
        if (Time.time - lastAttackTime < attackCooldown)
        {
            return;
        }
        
        // Проверяем, не выполняется ли уже атака
        if (isAttacking)
        {
            return;
        }
        
        // Проверяем нажатие клавиши
        bool attackPressed = false;
        
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            attackPressed = Keyboard.current.eKey.wasPressedThisFrame;
        }
        else if (debug)
        {
            Debug.LogWarning("[PlayerCombatController] Keyboard.current == null. Проверьте настройки Input System.");
        }
#else
        attackPressed = Input.GetKeyDown(attackKey);
#endif
        
        if (attackPressed)
        {
            if (debug)
            {
                Debug.Log("[PlayerCombatController] Нажата клавиша атаки (E)");
            }
            PerformAttack();
        }
    }
    
    /// <summary>
    /// Выполняет атаку (случайный удар)
    /// </summary>
    private void PerformAttack()
    {
        if (animator == null)
        {
            Debug.LogWarning("[PlayerCombatController] Animator не найден!");
            if (debug)
            {
                Debug.LogError("[PlayerCombatController] Animator не найден! Проверьте, что компонент Animator добавлен к игроку или его дочерним объектам.");
            }
            return;
        }
        
        // Сначала сбрасываем все триггеры, чтобы избежать конфликтов
        animator.ResetTrigger(IsJabHash);
        animator.ResetTrigger(IsUpperCutJabHash);
        
        // Выбираем случайный тип удара
        int randomAttack = Random.Range(0, 2);
        
        if (randomAttack == 0)
        {
            // IsJab
            animator.SetTrigger(IsJabHash);
            if (debug)
            {
                Debug.Log($"[PlayerCombatController] Запущен триггер IsJab (Hash: {IsJabHash})");
            }
            // Сбрасываем триггер через небольшое время
            StartCoroutine(ResetTriggerAfterDelay(IsJabHash, 0.1f));
        }
        else
        {
            // IsUpperCutJab
            animator.SetTrigger(IsUpperCutJabHash);
            if (debug)
            {
                Debug.Log($"[PlayerCombatController] Запущен триггер IsUpperCutJab (Hash: {IsUpperCutJabHash})");
            }
            // Сбрасываем триггер через небольшое время
            StartCoroutine(ResetTriggerAfterDelay(IsUpperCutJabHash, 0.1f));
        }
        
        isAttacking = true;
        lastAttackTime = Time.time;
        
        // Спавним VFX эффекты
        SpawnAttackVFX();
        
        // Наносим урон боссу только если бой активен и игрок в диапазоне атаки
        if (battleManager != null && battleManager.IsBattleActive())
        {
            DealDamageToBoss(baseAttackDamage);
        }
        
        if (debug)
        {
            Debug.Log($"[PlayerCombatController] Выполнен удар: {(randomAttack == 0 ? "IsJab" : "IsUpperCutJab")}");
        }
        
        // Сбрасываем флаг атаки через небольшое время
        Invoke(nameof(ResetAttackFlag), 0.5f);
    }
    
    /// <summary>
    /// Сбрасывает флаг атаки
    /// </summary>
    private void ResetAttackFlag()
    {
        isAttacking = false;
    }
    
    /// <summary>
    /// Проверяет возможность активации ультимейта
    /// </summary>
    private void CheckUltimateActivation()
    {
        if (ultimateProgressBar == null) return;
        if (bossController == null) return;
        if (animator == null) return;
        
        // Проверяем, заполнен ли прогресс ультимейта
        if (!ultimateProgressBar.IsUltimateReady())
        {
            return;
        }
        
        // Проверяем близость к боссу
        float distanceToBoss = Vector3.Distance(transform.position, bossController.transform.position);
        
        if (distanceToBoss <= ultimateActivationDistance)
        {
            // Активируем ультимейт
            ActivateUltimate();
        }
    }
    
    /// <summary>
    /// Активирует ультимейт
    /// </summary>
    private void ActivateUltimate()
    {
        if (animator == null) return;
        
        // Запускаем анимацию ультимейта
        animator.SetTrigger(IsStrongBeat1Hash);
        
        // Наносим урон боссу
        DealDamageToBoss(ultimateDamage);
        
        // Сбрасываем прогресс ультимейта
        if (ultimateProgressBar != null)
        {
            ultimateProgressBar.ResetProgress();
        }
        
        if (debug)
        {
            Debug.Log("[PlayerCombatController] Активирован ультимейт!");
        }
    }
    
    /// <summary>
    /// Наносит урон боссу с учетом силы удара (без проверки расстояния)
    /// </summary>
    private void DealDamageToBoss(float baseDamage)
    {
        // Проверяем, что бой активен
        if (battleManager == null)
        {
            battleManager = BattleManager.Instance;
            if (battleManager == null)
            {
                if (debug)
                {
                    Debug.LogWarning("[PlayerCombatController] BattleManager не найден!");
                }
                return;
            }
        }
        
        if (!battleManager.IsBattleActive())
        {
            if (debug)
            {
                Debug.Log("[PlayerCombatController] Бой не активен, урон не наносится");
            }
            return;
        }
        
        // Находим BossController если не найден
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<BossController>();
            if (bossController == null)
            {
                if (debug)
                {
                    Debug.LogWarning("[PlayerCombatController] BossController не найден!");
                }
                return;
            }
        }
        
        // Проверяем, что босс активен
        if (!bossController.gameObject.activeSelf)
        {
            if (debug)
            {
                Debug.LogWarning("[PlayerCombatController] Босс не активен!");
            }
            return;
        }
        
        // Вычисляем финальный урон с учетом силы удара
        float finalDamage = baseDamage;
        
        if (attackPowerSystem != null)
        {
            float powerMultiplier = attackPowerSystem.GetDamageMultiplier();
            finalDamage *= powerMultiplier;
        }
        
        // Наносим урон боссу через BattleManager (который обновит HP и вызовет события)
        float hpBefore = battleManager.GetBossCurrentHP();
        battleManager.DamageBoss(finalDamage);
        float hpAfter = battleManager.GetBossCurrentHP();
        
        Debug.Log($"[PlayerCombatController] Нанесен урон боссу: {finalDamage} (базовый: {baseDamage}), HP: {hpBefore} -> {hpAfter}");
    }
    
    /// <summary>
    /// Устанавливает ссылку на UltimateProgressBar
    /// </summary>
    public void SetUltimateProgressBar(UltimateProgressBar progressBar)
    {
        ultimateProgressBar = progressBar;
    }
    
    /// <summary>
    /// Сбрасывает триггер аниматора через заданное время
    /// </summary>
    private IEnumerator ResetTriggerAfterDelay(int triggerHash, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (animator != null)
        {
            animator.ResetTrigger(triggerHash);
            if (debug)
            {
                Debug.Log($"[PlayerCombatController] Триггер сброшен (Hash: {triggerHash})");
            }
        }
    }
    
    /// <summary>
    /// Спавнит VFX эффекты для удара
    /// </summary>
    private void SpawnAttackVFX()
    {
        if (attackVFXPrefab == null)
        {
            if (debug)
            {
                Debug.LogWarning("[PlayerCombatController] Префаб VFX эффекта не назначен!");
            }
            return;
        }
        
        if (playerTransform == null)
        {
            playerTransform = transform;
        }
        
        // Получаем позицию и направление игрока
        Vector3 playerPosition = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;
        Vector3 playerRight = playerTransform.right;
        Vector3 playerUp = playerTransform.up;
        
        // Спавним VFX эффекты
        for (int i = 0; i < vfxCountPerAttack; i++)
        {
            // Вычисляем локальное смещение (X, Y, Z относительно игрока)
            // X - вправо/влево, Y - вверх/вниз, Z - вперед/назад
            Vector3 baseLocalOffset = new Vector3(vfxOffsetX, vfxOffsetY, vfxOffsetZ);
            
            // Добавляем случайный разброс
            Vector2 randomCircle = Random.insideUnitCircle * vfxSpreadRadius;
            Vector3 localOffset = baseLocalOffset + new Vector3(randomCircle.x, randomCircle.y, 0f);
            
            // Вычисляем мировую позицию с учетом текущего направления игрока
            Vector3 worldOffset = playerForward * localOffset.z + 
                                playerRight * localOffset.x + 
                                playerUp * localOffset.y;
            Vector3 vfxPosition = playerPosition + worldOffset;
            
            // Создаем VFX эффект
            GameObject vfxInstance = Instantiate(attackVFXPrefab, vfxPosition, Quaternion.identity);
            
            // Поворачиваем VFX в направлении удара (вперед от игрока)
            if (playerForward != Vector3.zero)
            {
                vfxInstance.transform.rotation = Quaternion.LookRotation(playerForward, playerUp);
            }
            
            // Сохраняем данные VFX для отслеживания (сохраняем локальное смещение)
            VFXData vfxData = new VFXData(vfxInstance, localOffset, vfxLifetime);
            activeVFXList.Add(vfxData);
            
            // Уничтожаем VFX через заданное время, если lifetime > 0
            if (vfxLifetime > 0f)
            {
                StartCoroutine(DestroyVFXAfterDelay(vfxData, vfxLifetime));
            }
            
            if (debug)
            {
                Debug.Log($"[PlayerCombatController] Заспавнен VFX эффект #{i + 1} с локальным смещением: {localOffset}");
            }
        }
    }
    
    /// <summary>
    /// Обновляет позиции активных VFX эффектов
    /// </summary>
    private void UpdateVFXPositions()
    {
        if (playerTransform == null)
        {
            playerTransform = transform;
        }
        
        // Получаем текущие направления игрока
        Vector3 playerPosition = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;
        Vector3 playerRight = playerTransform.right;
        Vector3 playerUp = playerTransform.up;
        
        // Обновляем позиции всех активных VFX эффектов
        for (int i = activeVFXList.Count - 1; i >= 0; i--)
        {
            VFXData vfxData = activeVFXList[i];
            
            // Проверяем, что VFX эффект еще существует
            if (vfxData.vfxInstance == null)
            {
                activeVFXList.RemoveAt(i);
                continue;
            }
            
            // Вычисляем новую позицию с учетом текущего направления игрока
            // Преобразуем локальное смещение в мировые координаты
            // localOffset.x - вправо/влево, localOffset.y - вверх/вниз, localOffset.z - вперед/назад
            Vector3 worldOffset = playerForward * vfxData.localOffset.z + 
                                playerRight * vfxData.localOffset.x + 
                                playerUp * vfxData.localOffset.y;
            
            // Обновляем позицию VFX эффекта (всегда спереди игрока)
            vfxData.vfxInstance.transform.position = playerPosition + worldOffset;
            
            // Обновляем поворот VFX эффекта (всегда в направлении игрока)
            if (playerForward != Vector3.zero)
            {
                vfxData.vfxInstance.transform.rotation = Quaternion.LookRotation(playerForward, playerUp);
            }
        }
    }
    
    /// <summary>
    /// Уничтожает VFX эффект через заданное время
    /// </summary>
    private IEnumerator DestroyVFXAfterDelay(VFXData vfxData, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Удаляем из списка активных VFX
        if (activeVFXList.Contains(vfxData))
        {
            activeVFXList.Remove(vfxData);
        }
        
        // Уничтожаем VFX эффект
        if (vfxData.vfxInstance != null)
        {
            Destroy(vfxData.vfxInstance);
        }
    }
}
