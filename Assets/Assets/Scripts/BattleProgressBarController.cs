using UnityEngine;
using MagicPigGames;

/// <summary>
/// Управляет видимостью ProgressBar во время битвы с боссом.
/// Включает прогресс-бар когда начинается бой, выключает когда бой заканчивается.
/// </summary>
public class BattleProgressBarController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Ссылка на ProgressBar компонент (HorizontalProgressBar или другой)")]
    [SerializeField] private ProgressBar progressBar;
    
    [Tooltip("Ссылка на BattleManager (автоматически находится если не назначен)")]
    [SerializeField] private BattleManager battleManager;
    
    [Header("Settings")]
    [Tooltip("Если включено, скрывать прогресс-бар по умолчанию (при старте игры)")]
    [SerializeField] private bool hideOnStart = true;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private void Awake()
    {
        // Находим ProgressBar если не назначен
        if (progressBar == null)
        {
            progressBar = GetComponent<ProgressBar>();
            if (progressBar == null)
            {
                progressBar = GetComponentInChildren<ProgressBar>();
            }
        }
        
        if (progressBar == null)
        {
            Debug.LogError("[BattleProgressBarController] ProgressBar не найден! Убедитесь, что компонент ProgressBar или HorizontalProgressBar находится на этом GameObject или его дочерних объектах.");
        }
    }
    
    private void Start()
    {
        // Находим BattleManager если не назначен
        if (battleManager == null)
        {
            battleManager = BattleManager.Instance;
        }
        
        if (battleManager == null)
        {
            Debug.LogError("[BattleProgressBarController] BattleManager не найден!");
            return;
        }
        
        // Подписываемся на события боя
        battleManager.OnBattleStarted += OnBattleStarted;
        battleManager.OnBattleEnded += OnBattleEnded;
        battleManager.OnBossHPChanged += OnBossHPChanged;
        
        // Скрываем прогресс-бар по умолчанию, если нужно
        if (hideOnStart && progressBar != null)
        {
            SetProgressBarActive(false);
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        if (battleManager != null)
        {
            battleManager.OnBattleStarted -= OnBattleStarted;
            battleManager.OnBattleEnded -= OnBattleEnded;
            battleManager.OnBossHPChanged -= OnBossHPChanged;
        }
    }
    
    private void Update()
    {
        // Обновляем прогресс-бар каждый кадр, если бой активен
        if (battleManager != null && battleManager.IsBattleActive() && progressBar != null)
        {
            UpdateBossHPBar();
        }
    }
    
    /// <summary>
    /// Вызывается когда начинается бой
    /// </summary>
    private void OnBattleStarted()
    {
        if (debug)
        {
            Debug.Log("[BattleProgressBarController] Бой начался, включаем прогресс-бар");
        }
        
        SetProgressBarActive(true);
    }
    
    /// <summary>
    /// Вызывается когда бой заканчивается
    /// </summary>
    private void OnBattleEnded()
    {
        if (debug)
        {
            Debug.Log("[BattleProgressBarController] Бой закончился, выключаем прогресс-бар");
        }
        
        SetProgressBarActive(false);
    }
    
    /// <summary>
    /// Вызывается когда изменяется HP босса
    /// </summary>
    private void OnBossHPChanged(float newHP)
    {
        UpdateBossHPBar();
    }
    
    /// <summary>
    /// Обновляет прогресс-бар HP босса
    /// </summary>
    private void UpdateBossHPBar()
    {
        if (progressBar == null || battleManager == null) return;
        
        float currentHP = battleManager.GetBossCurrentHP();
        float maxHP = battleManager.GetBossMaxHP();
        
        if (maxHP > 0f)
        {
            // Вычисляем прогресс от 0 до 1
            float progress = currentHP / maxHP;
            progress = Mathf.Clamp01(progress);
            
            // Устанавливаем прогресс в ProgressBar
            progressBar.SetProgress(progress);
            
            if (debug)
            {
                Debug.Log($"[BattleProgressBarController] HP босса обновлено: {currentHP}/{maxHP} ({progress * 100f:F1}%)");
            }
        }
    }
    
    /// <summary>
    /// Устанавливает активность прогресс-бара
    /// </summary>
    private void SetProgressBarActive(bool active)
    {
        if (progressBar == null) return;
        
        // Включаем/выключаем GameObject с ProgressBar
        progressBar.gameObject.SetActive(active);
        
        // Если включаем, сразу обновляем HP
        if (active)
        {
            UpdateBossHPBar();
        }
        
        if (debug)
        {
            Debug.Log($"[BattleProgressBarController] Прогресс-бар {(active ? "включен" : "выключен")}");
        }
    }
}
