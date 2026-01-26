using UnityEngine;

/// <summary>
/// Система силы удара игрока.
/// Уровень силы удара покупается за доход и влияет на урон, наносимый боссу.
/// </summary>
public class AttackPowerSystem : MonoBehaviour
{
    [Header("Power Settings")]
    [Tooltip("Базовый множитель урона (на уровне 0)")]
    [SerializeField] private float baseDamageMultiplier = 1f;
    
    [Tooltip("Множитель урона за уровень (каждый уровень увеличивает урон на этот процент)")]
    [SerializeField] private float damageMultiplierPerLevel = 0.1f; // 10% за уровень
    
    [Tooltip("Базовая стоимость повышения уровня")]
    [SerializeField] private long baseLevelCost = 1000;
    
    [Tooltip("Множитель стоимости за уровень (стоимость = базовая * множитель^уровень)")]
    [SerializeField] private float levelCostMultiplier = 1.5f;
    
    [Header("References")]
    [Tooltip("Ссылка на GameStorage")]
    [SerializeField] private GameStorage gameStorage;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private static AttackPowerSystem instance;
    
    public static AttackPowerSystem Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<AttackPowerSystem>();
            }
            return instance;
        }
    }
    
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
        // Находим GameStorage если не назначен
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
    }
    
    /// <summary>
    /// Получает текущий уровень силы удара
    /// </summary>
    public int GetAttackPowerLevel()
    {
        if (gameStorage == null)
        {
            return 0;
        }
        
        return gameStorage.GetAttackPowerLevel();
    }
    
    /// <summary>
    /// Получает множитель урона на основе уровня силы удара
    /// </summary>
    public float GetDamageMultiplier()
    {
        int level = GetAttackPowerLevel();
        
        // Формула: множитель = базовый_множитель * (1 + уровень * множитель_за_уровень)
        float multiplier = baseDamageMultiplier * (1f + level * damageMultiplierPerLevel);
        
        return multiplier;
    }
    
    /// <summary>
    /// Получает стоимость повышения уровня силы удара
    /// </summary>
    public long GetLevelUpCost()
    {
        int currentLevel = GetAttackPowerLevel();
        
        // Формула: стоимость = базовая_стоимость * множитель^уровень
        long cost = (long)(baseLevelCost * Mathf.Pow(levelCostMultiplier, currentLevel));
        
        return cost;
    }
    
    /// <summary>
    /// Покупает повышение уровня силы удара
    /// </summary>
    public bool BuyLevelUp()
    {
        if (gameStorage == null)
        {
            Debug.LogError("[AttackPowerSystem] GameStorage не найден!");
            return false;
        }
        
        long cost = GetLevelUpCost();
        double currentBalance = gameStorage.GetBalanceDouble();
        
        // Проверяем, достаточно ли средств
        if (currentBalance < cost)
        {
            if (debug)
            {
                Debug.Log($"[AttackPowerSystem] Недостаточно средств для повышения уровня. Нужно: {cost}, есть: {currentBalance}");
            }
            return false;
        }
        
        // Вычитаем стоимость
        bool success = gameStorage.SubtractBalanceLong(cost);
        
        if (success)
        {
            // Повышаем уровень
            int newLevel = GetAttackPowerLevel() + 1;
            gameStorage.SetAttackPowerLevel(newLevel);
            
            if (debug)
            {
                Debug.Log($"[AttackPowerSystem] Уровень силы удара повышен до {newLevel}. Множитель урона: {GetDamageMultiplier():F2}");
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Получает базовый множитель урона
    /// </summary>
    public float GetBaseDamageMultiplier()
    {
        return baseDamageMultiplier;
    }
    
    /// <summary>
    /// Получает множитель урона за уровень
    /// </summary>
    public float GetDamageMultiplierPerLevel()
    {
        return damageMultiplierPerLevel;
    }
}
