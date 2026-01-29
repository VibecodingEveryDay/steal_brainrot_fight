using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Управляет состоянием боя, HP всех участников и логикой победы/поражения.
/// </summary>
public class BattleManager : MonoBehaviour
{
    [Header("HP Settings")]
    [Tooltip("Базовое HP игрока")]
    [SerializeField] private float playerBaseHP = 100f;
    
    [Tooltip("Базовое HP союзника (бота)")]
    [SerializeField] private float allyBaseHP = 50f;
    
    [Tooltip("Делитель дохода для расчета HP босса (HP = (доход / делитель) * множитель)")]
    [SerializeField] private float bossHPDivider = 1f;
    
    [Tooltip("Множитель HP босса (HP = (доход / делитель) * множитель)")]
    [SerializeField] private float bossHPMultiplier = 1f;
    
    [Header("Damage by level (10–60)")]
    [Tooltip("Множитель урона игрока по уровню силы. X = уровень (10–60), Y = множитель. Например: (10,1) (60,2) — на 10 ур. x1, на 60 ур. x2")]
    [SerializeField] private AnimationCurve damageByLevelScaler = new AnimationCurve(
        new Keyframe(10f, 1f),
        new Keyframe(60f, 2f)
    );
    
    [Tooltip("Минимальный уровень для расчёта (старт)")]
    [SerializeField] private int minPowerLevel = 10;
    
    [Tooltip("Максимальный уровень силы")]
    [SerializeField] private int maxPowerLevel = 60;
    
    [Header("References")]
    [Tooltip("Ссылка на игрока")]
    [SerializeField] private Transform playerTransform;
    
    [Tooltip("Ссылка на BossController")]
    [SerializeField] private BossController bossController;
    
    [Tooltip("Ссылка на RedZoneSpawner")]
    [SerializeField] private RedZoneSpawner redZoneSpawner;
    
    private static BattleManager instance;
    
    public static BattleManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<BattleManager>();
            }
            return instance;
        }
    }
    
    // Текущее состояние боя
    private bool isBattleActive = false;
    private BrainrotObject currentBrainrot;
    private BattleZone currentBattleZone;
    
    // HP участников
    private float playerHP;
    private float bossHP;
    private Dictionary<GameObject, float> allyHP = new Dictionary<GameObject, float>();
    
    // События
    public System.Action<float> OnPlayerHPChanged;
    public System.Action<float> OnBossHPChanged;
    public System.Action OnBattleStarted;
    public System.Action OnBattleEnded;
    public System.Action OnPlayerDefeated;
    public System.Action OnBossDefeated;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        FindPlayer();
    }
    
    /// <summary>
    /// Находит игрока в сцене
    /// </summary>
    private void FindPlayer()
    {
        if (playerTransform == null)
        {
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
    }
    
    /// <summary>
    /// Получает Transform игрока
    /// </summary>
    public Transform GetPlayerTransform()
    {
        FindPlayer();
        return playerTransform;
    }
    
    /// <summary>
    /// Начинает бой
    /// </summary>
    public void StartBattle(BrainrotObject brainrotObject, BattleZone battleZone)
    {
        if (isBattleActive)
        {
            Debug.LogWarning("[BattleManager] Бой уже активен!");
            return;
        }
        
        if (brainrotObject == null)
        {
            Debug.LogError("[BattleManager] BrainrotObject равен null!");
            return;
        }
        
        if (battleZone == null)
        {
            Debug.LogError("[BattleManager] BattleZone равен null!");
            return;
        }
        
        isBattleActive = true;
        currentBrainrot = brainrotObject;
        currentBattleZone = battleZone;
        
        // Вычисляем HP босса на основе дохода (используем метод из BrainrotObject)
        // Формула: HP = (доход / делитель) * множитель
        double finalIncome = brainrotObject.GetFinalIncome();
        double incomeAfterDivider = bossHPDivider > 0 ? finalIncome / bossHPDivider : finalIncome;
        bossHP = (float)incomeAfterDivider * bossHPMultiplier;
        
        // Устанавливаем HP игрока
        playerHP = playerBaseHP;
        
        // Очищаем HP союзников
        allyHP.Clear();
        
        // Сброс счётчика урона для уведомления (сумма за этот бой)
        var damageNotify = FindFirstObjectByType<DamageNotifyManager>();
        if (damageNotify != null)
            damageNotify.ResetTotalDamage();
        
        // ВАЖНО: Находим BossController если он не назначен
        if (bossController == null)
        {
            bossController = FindFirstObjectByType<BossController>();
            if (bossController == null)
            {
                Debug.LogError("[BattleManager] BossController не найден в сцене!");
            }
        }
        
        // ВАЖНО: Проверяем, что в сцене только один BossController
        // Если найдено несколько, сбрасываем все лишние
        BossController[] allBossControllers = FindObjectsByType<BossController>(FindObjectsSortMode.None);
        if (allBossControllers != null && allBossControllers.Length > 1)
        {
            Debug.LogWarning($"[BattleManager] Найдено {allBossControllers.Length} BossController в сцене! Оставляем только первый, остальные сбрасываем.");
            for (int i = 1; i < allBossControllers.Length; i++)
            {
                if (allBossControllers[i] != null)
                {
                    allBossControllers[i].ResetBoss();
                }
            }
        }
        
        // ВАЖНО: Сбрасываем текущего босса перед инициализацией нового
        // Это гарантирует, что старые модели и состояние будут очищены
        if (bossController != null)
        {
            bossController.ResetBoss();
        }
        
        // #region agent log
        try { 
            System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BattleManager.cs:158\",\"message\":\"Before calling BossController.InitializeBoss\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); 
        } catch {}
        // #endregion
        
        // Уведомляем BossController о начале боя
        if (bossController != null)
        {
            bossController.InitializeBoss(brainrotObject, bossHP, battleZone);
            
            // #region agent log
            try { 
                System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BattleManager.cs:164\",\"message\":\"After calling BossController.InitializeBoss\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); 
            } catch {}
            // #endregion
        }
        else
        {
            Debug.LogError("[BattleManager] BossController равен null, босс не будет создан!");
        }
        
        // ВАЖНО: Находим RedZoneSpawner если он не назначен
        if (redZoneSpawner == null)
        {
            redZoneSpawner = FindFirstObjectByType<RedZoneSpawner>();
        }
        
        // Уведомляем RedZoneSpawner о начале боя
        if (redZoneSpawner != null)
        {
            redZoneSpawner.StartSpawning(bossHP);
        }
        
        // Вызываем события
        OnBattleStarted?.Invoke();
        OnPlayerHPChanged?.Invoke(playerHP);
        OnBossHPChanged?.Invoke(bossHP);
        
        Debug.Log($"[BattleManager] Бой начат! Босс: {brainrotObject.GetObjectName()}, HP: {bossHP}, BossController: {(bossController != null ? "найден" : "НЕ НАЙДЕН")}");
    }
    
    /// <summary>
    /// Заканчивает бой
    /// </summary>
    public void EndBattle()
    {
        if (!isBattleActive)
        {
            return;
        }
        
        isBattleActive = false;
        
        // Останавливаем спавн красных зон
        if (redZoneSpawner != null)
        {
            redZoneSpawner.StopSpawning();
        }
        
        // Сбрасываем босса
        if (bossController != null)
        {
            bossController.ResetBoss();
        }
        
        // Вызываем события
        OnBattleEnded?.Invoke();
        
        Debug.Log("[BattleManager] Бой закончен");
    }
    
    /// <summary>
    /// Наносит урон игроку
    /// </summary>
    public void DamagePlayer(float damage)
    {
        if (!isBattleActive) return;
        
        playerHP -= damage;
        if (playerHP < 0f)
        {
            playerHP = 0f;
        }
        
        OnPlayerHPChanged?.Invoke(playerHP);
        
        // Если HP игрока <= 0, игрок проиграл
        if (playerHP <= 0f)
        {
            DefeatPlayer();
        }
    }
    
    /// <summary>
    /// Множитель урона по текущему уровню силы игрока (10–60). Используется в DamageBoss.
    /// </summary>
    public float GetDamageScaleByLevel()
    {
        int level = 10;
        if (GameStorage.Instance != null)
            level = Mathf.Clamp(GameStorage.Instance.GetAttackPowerLevel(), minPowerLevel, maxPowerLevel);
        if (damageByLevelScaler == null || damageByLevelScaler.keys.Length == 0)
            return 1f;
        return Mathf.Max(0.01f, damageByLevelScaler.Evaluate(level));
    }
    
    /// <summary>
    /// Наносит урон боссу. Если applyLevelScaler == true (урон от игрока), урон умножается на DamageByLevelScaler по уровню силы 10–60.
    /// </summary>
    /// <param name="damage">Базовый урон</param>
    /// <param name="applyLevelScaler">Применять ли множитель по уровню силы (true для урона от игрока)</param>
    public void DamageBoss(float damage, bool applyLevelScaler = false)
    {
        if (!isBattleActive)
        {
            Debug.LogWarning("[BattleManager] Попытка нанести урон боссу, но бой не активен!");
            return;
        }
        
        float scaledDamage = applyLevelScaler ? damage * GetDamageScaleByLevel() : damage;
        
        float oldHP = bossHP;
        bossHP -= scaledDamage;
        if (bossHP < 0f)
        {
            bossHP = 0f;
        }
        
        Debug.Log($"[BattleManager] Босс получил урон: {scaledDamage} (база: {damage}, scale применён: {applyLevelScaler}), HP: {oldHP} -> {bossHP}");
        
        // Уведомление об уроне (MainCanvas->OverflowUiContainer->DamageNotify->DamageText)
        var damageNotifyManager = FindFirstObjectByType<DamageNotifyManager>();
        if (damageNotifyManager != null)
            damageNotifyManager.AddDamage((double)scaledDamage);
        
        // Обновляем сложность красных зон на основе текущего HP
        if (redZoneSpawner != null)
        {
            redZoneSpawner.UpdateBossHP(bossHP);
        }
        
        OnBossHPChanged?.Invoke(bossHP);
        
        // Если HP босса <= 0, босс побежден
        if (bossHP <= 0f)
        {
            DefeatBoss();
        }
    }
    
    /// <summary>
    /// Наносит урон союзнику
    /// </summary>
    public void DamageAlly(GameObject ally, float damage)
    {
        if (!isBattleActive) return;
        if (!allyHP.ContainsKey(ally)) return;
        
        allyHP[ally] -= damage;
        if (allyHP[ally] < 0f)
        {
            allyHP[ally] = 0f;
        }
        
        // Если HP союзника <= 0, удаляем его
        if (allyHP[ally] <= 0f)
        {
            RemoveAlly(ally);
        }
    }
    
    /// <summary>
    /// Добавляет союзника в бой
    /// </summary>
    public void AddAlly(GameObject ally)
    {
        if (!isBattleActive) return;
        
        if (!allyHP.ContainsKey(ally))
        {
            allyHP[ally] = allyBaseHP;
        }
    }
    
    /// <summary>
    /// Удаляет союзника из боя
    /// </summary>
    public void RemoveAlly(GameObject ally)
    {
        if (allyHP.ContainsKey(ally))
        {
            allyHP.Remove(ally);
        }
        
        // Уничтожаем союзника
        if (ally != null)
        {
            Destroy(ally);
        }
    }
    
    /// <summary>
    /// Игрок проиграл
    /// </summary>
    private void DefeatPlayer()
    {
        if (!isBattleActive) return;
        
        OnPlayerDefeated?.Invoke();
        
        // Телепортируем игрока обратно в лобби
        TeleportManager teleportManager = TeleportManager.Instance;
        if (teleportManager != null)
        {
            teleportManager.TeleportToLobby();
        }
        
        Debug.Log("[BattleManager] Игрок проиграл!");
    }
    
    /// <summary>
    /// Босс побежден
    /// </summary>
    private void DefeatBoss()
    {
        if (!isBattleActive) return;
        
        // Сразу скрыть уведомление об уроне
        var damageNotifyManager = FindFirstObjectByType<DamageNotifyManager>();
        if (damageNotifyManager != null)
            damageNotifyManager.ResetTotalDamage();
        
        OnBossDefeated?.Invoke();
        
        // ВАЖНО: Отмечаем брейнрота как побеждённого (снимаем флаг unfought)
        if (currentBrainrot != null)
        {
            currentBrainrot.MarkAsDefeated();
            Debug.Log($"[BattleManager] Брейнрот {currentBrainrot.GetObjectName()} отмечен как побеждённый");
        }
        
        // Берем оригинальный брейнрот в руки (если он доступен)
        if (currentBrainrot != null && playerTransform != null)
        {
            // Находим PlayerCarryController
            PlayerCarryController carryController = playerTransform.GetComponent<PlayerCarryController>();
            if (carryController == null)
            {
                carryController = FindFirstObjectByType<PlayerCarryController>();
            }
            
            if (carryController != null && carryController.CanCarry())
            {
                // ВАЖНО: Используем оригинальный брейнрот вместо создания нового
                // Проверяем, что оригинальный брейнрот существует и активен
                if (currentBrainrot.gameObject != null && currentBrainrot.gameObject.activeInHierarchy)
                {
                    // Убеждаемся, что брейнрот помечен как побежденный
                    currentBrainrot.SetUnfought(false);
                    
                    // Убеждаемся, что все дочерние объекты (моделька) активны
                    currentBrainrot.gameObject.SetActive(true);
                    
                    // Активируем все дочерние объекты рекурсивно
                    foreach (Transform child in currentBrainrot.transform)
                    {
                        if (child != null)
                        {
                            SetAllChildrenActive(child, true);
                        }
                    }
                    
                    // Убеждаемся, что все рендереры включены
                    Renderer[] renderers = currentBrainrot.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer != null)
                        {
                            renderer.enabled = true;
                            renderer.gameObject.SetActive(true);
                        }
                    }
                    
                    Debug.Log($"[BattleManager] Используем оригинальный брейнрот '{currentBrainrot.GetObjectName()}', активных рендереров: {renderers.Length}");
                    
                    // Берем оригинальный брейнрот в руки
                    currentBrainrot.Take();
                }
                else
                {
                    // Если оригинальный брейнрот недоступен, создаем новый из префаба
                    GameObject brainrotPrefab = LoadBrainrotPrefabByName(currentBrainrot.GetObjectName());
                    
                    if (brainrotPrefab == null)
                    {
                        Debug.LogWarning($"[BattleManager] Не удалось загрузить префаб брейнрота '{currentBrainrot.GetObjectName()}' и оригинальный недоступен!");
                        return;
                    }
                    
                    // Создаем копию брейнрота из префаба только если оригинальный недоступен
                    GameObject brainrotInstance = Instantiate(brainrotPrefab, playerTransform.position, Quaternion.identity);
                    BrainrotObject brainrotObj = brainrotInstance.GetComponent<BrainrotObject>();
                    
                    if (brainrotObj != null)
                    {
                        // Копируем данные из текущего брейнрота
                        brainrotObj.SetLevel(currentBrainrot.GetLevel());
                        brainrotObj.SetRarity(currentBrainrot.GetRarity());
                        brainrotObj.SetBaseIncome(currentBrainrot.GetBaseIncome());
                        brainrotObj.SetUnfought(false);
                        
                        // Активируем все компоненты
                        brainrotInstance.SetActive(true);
                        foreach (Transform child in brainrotInstance.transform)
                        {
                            if (child != null)
                            {
                                SetAllChildrenActive(child, true);
                            }
                        }
                        
                        Renderer[] renderers = brainrotInstance.GetComponentsInChildren<Renderer>(true);
                        foreach (Renderer renderer in renderers)
                        {
                            if (renderer != null)
                            {
                                renderer.enabled = true;
                                renderer.gameObject.SetActive(true);
                            }
                        }
                        
                        Debug.Log($"[BattleManager] Создан новый брейнрот из префаба '{currentBrainrot.GetObjectName()}' (оригинальный недоступен)");
                        
                        brainrotObj.Take();
                    }
                    else
                    {
                        Debug.LogError($"[BattleManager] У созданного брейнрота нет компонента BrainrotObject!");
                        Destroy(brainrotInstance);
                    }
                }
            }
        }
        
        // Телепортируем игрока с брейнротом в руках в дом
        TeleportManager teleportManager = TeleportManager.Instance;
        if (teleportManager != null)
        {
            teleportManager.TeleportToHouse();
        }
        
        Debug.Log("[BattleManager] Босс побежден!");
    }
    
    /// <summary>
    /// Получает текущее HP игрока
    /// </summary>
    public float GetPlayerHP()
    {
        return playerHP;
    }
    
    /// <summary>
    /// Получает текущее HP босса
    /// </summary>
    public float GetBossHP()
    {
        return bossHP;
    }
    
    /// <summary>
    /// Получает максимальное HP игрока
    /// </summary>
    public float GetPlayerMaxHP()
    {
        return playerBaseHP;
    }
    
    /// <summary>
    /// Получает максимальное HP босса
    /// </summary>
    public float GetBossMaxHP()
    {
        if (currentBrainrot == null) return 0f;
        // Формула: HP = (доход / делитель) * множитель
        double finalIncome = currentBrainrot.GetFinalIncome();
        double incomeAfterDivider = bossHPDivider > 0 ? finalIncome / bossHPDivider : finalIncome;
        return (float)incomeAfterDivider * bossHPMultiplier;
    }
    
    /// <summary>
    /// Получает текущее HP босса (для отображения в UI)
    /// </summary>
    public float GetBossCurrentHP()
    {
        return bossHP;
    }
    
    /// <summary>
    /// Проверяет, активен ли бой
    /// </summary>
    public bool IsBattleActive()
    {
        return isBattleActive;
    }
    
    /// <summary>
    /// Получает текущий брейнрот (босс)
    /// </summary>
    public BrainrotObject GetCurrentBrainrot()
    {
        return currentBrainrot;
    }
    
    /// <summary>
    /// Устанавливает ссылку на BossController
    /// </summary>
    public void SetBossController(BossController controller)
    {
        bossController = controller;
    }
    
    /// <summary>
    /// Устанавливает ссылку на RedZoneSpawner
    /// </summary>
    public void SetRedZoneSpawner(RedZoneSpawner spawner)
    {
        redZoneSpawner = spawner;
    }
    
    /// <summary>
    /// Рекурсивно активирует/деактивирует все дочерние объекты
    /// </summary>
    private void SetAllChildrenActive(Transform parent, bool active)
    {
        if (parent == null) return;
        
        parent.gameObject.SetActive(active);
        
        foreach (Transform child in parent)
        {
            if (child != null)
            {
                SetAllChildrenActive(child, active);
            }
        }
    }
    
    /// <summary>
    /// Загружает префаб брейнрота по имени из Resources
    /// </summary>
    private GameObject LoadBrainrotPrefabByName(string brainrotName)
    {
        if (string.IsNullOrEmpty(brainrotName))
        {
            return null;
        }
        
#if UNITY_EDITOR
        // В редакторе используем AssetDatabase для загрузки из папки
        string folderPath = "Assets/Assets/Resources/game/Brainrots";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null)
            {
                BrainrotObject brainrotObject = prefab.GetComponent<BrainrotObject>();
                if (brainrotObject != null)
                {
                    // Используем GetObjectName() для получения имени
                    string prefabName = brainrotObject.GetObjectName();
                    if (prefabName == brainrotName)
                    {
                        return prefab;
                    }
                }
            }
        }
#else
        // В билде используем Resources (путь относительно папки Resources)
        GameObject[] allPrefabs = Resources.LoadAll<GameObject>("game/Brainrots");
        
        foreach (GameObject prefab in allPrefabs)
        {
            if (prefab != null)
            {
                BrainrotObject brainrotObject = prefab.GetComponent<BrainrotObject>();
                if (brainrotObject != null)
                {
                    // Используем GetObjectName() для получения имени
                    string prefabName = brainrotObject.GetObjectName();
                    if (prefabName == brainrotName)
                    {
                        return prefab;
                    }
                }
            }
        }
#endif
        
        return null;
    }
}
