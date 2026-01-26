using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Прогресс бар ультимейта.
/// Заполняется со временем и показывает готовность ультимейта.
/// </summary>
public class UltimateProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Image для прогресс бара (Fill)")]
    [SerializeField] private Image progressFill;
    
    [Tooltip("Text для отображения процентов (опционально)")]
    [SerializeField] private TextMeshProUGUI progressText;
    
    [Header("Progress Settings")]
    [Tooltip("Скорость заполнения прогресса (в единицах в секунду, от 0 до 1)")]
    [SerializeField] private float fillSpeed = 0.1f;
    
    [Tooltip("Максимальное значение прогресса (обычно 1.0)")]
    [SerializeField] private float maxProgress = 1f;
    
    [Header("Visual Settings")]
    [Tooltip("Цвет заполненного прогресса")]
    [SerializeField] private Color filledColor = Color.yellow;
    
    [Tooltip("Цвет незаполненного прогресса")]
    [SerializeField] private Color emptyColor = Color.gray;
    
    [Header("References")]
    [Tooltip("Ссылка на BattleManager (для проверки активности боя)")]
    [SerializeField] private BattleManager battleManager;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private float currentProgress = 0f;
    private bool isUltimateReady = false;
    
    private void Awake()
    {
        // Автоматически находим компоненты если не назначены
        if (progressFill == null)
        {
            progressFill = GetComponentInChildren<Image>();
        }
        
        if (progressText == null)
        {
            progressText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }
    
    private void Start()
    {
        // Находим BattleManager если не назначен
        if (battleManager == null)
        {
            battleManager = BattleManager.Instance;
        }
        
        // Инициализируем прогресс бар
        UpdateProgressBar();
    }
    
    private void Update()
    {
        // Проверяем, активен ли бой
        if (battleManager == null || !battleManager.IsBattleActive())
        {
            // Если бой не активен, не заполняем прогресс
            return;
        }
        
        // Заполняем прогресс со временем
        if (currentProgress < maxProgress)
        {
            currentProgress += fillSpeed * Time.deltaTime;
            currentProgress = Mathf.Clamp(currentProgress, 0f, maxProgress);
            
            // Проверяем, готов ли ультимейт
            if (currentProgress >= maxProgress && !isUltimateReady)
            {
                isUltimateReady = true;
                OnUltimateReady();
            }
            
            // Обновляем визуальное представление
            UpdateProgressBar();
        }
    }
    
    /// <summary>
    /// Обновляет визуальное представление прогресс бара
    /// </summary>
    private void UpdateProgressBar()
    {
        // Обновляем fill amount
        if (progressFill != null)
        {
            progressFill.fillAmount = currentProgress / maxProgress;
            
            // Обновляем цвет в зависимости от прогресса
            if (isUltimateReady)
            {
                progressFill.color = filledColor;
            }
            else
            {
                // Интерполируем цвет между empty и filled
                float t = currentProgress / maxProgress;
                progressFill.color = Color.Lerp(emptyColor, filledColor, t);
            }
        }
        
        // Обновляем текст процентов
        if (progressText != null)
        {
            int percentage = Mathf.RoundToInt((currentProgress / maxProgress) * 100f);
            progressText.text = $"{percentage}%";
        }
    }
    
    /// <summary>
    /// Вызывается когда ультимейт готов
    /// </summary>
    private void OnUltimateReady()
    {
        if (debug)
        {
            Debug.Log("[UltimateProgressBar] Ультимейт готов!");
        }
        
        // Можно добавить визуальные эффекты (мигание, анимация и т.д.)
    }
    
    /// <summary>
    /// Сбрасывает прогресс ультимейта
    /// </summary>
    public void ResetProgress()
    {
        currentProgress = 0f;
        isUltimateReady = false;
        UpdateProgressBar();
        
        if (debug)
        {
            Debug.Log("[UltimateProgressBar] Прогресс ультимейта сброшен");
        }
    }
    
    /// <summary>
    /// Проверяет, готов ли ультимейт
    /// </summary>
    public bool IsUltimateReady()
    {
        return isUltimateReady && currentProgress >= maxProgress;
    }
    
    /// <summary>
    /// Получает текущий прогресс (от 0 до 1)
    /// </summary>
    public float GetProgress()
    {
        return currentProgress / maxProgress;
    }
    
    /// <summary>
    /// Устанавливает скорость заполнения
    /// </summary>
    public void SetFillSpeed(float speed)
    {
        fillSpeed = speed;
    }
    
    /// <summary>
    /// Устанавливает максимальное значение прогресса
    /// </summary>
    public void SetMaxProgress(float max)
    {
        maxProgress = max;
        currentProgress = Mathf.Clamp(currentProgress, 0f, maxProgress);
        UpdateProgressBar();
    }
}
