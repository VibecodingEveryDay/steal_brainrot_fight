using UnityEngine;
using UnityEngine.UI;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Базовый менеджер UI для всех игр
/// Интегрирует Canvas, TextMeshPro и YG2 Localization
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private CanvasScaler canvasScaler;
    
    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject settingsPanel;
    
    [Header("Mobile UI")]
    [SerializeField] private GameObject mobileUIContainer; // Контейнер для мобильного UI (джойстик, кнопки)
    
    private static UIManager instance;
    public static UIManager Instance => instance;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeUI()
    {
        // Настройка Canvas для разных разрешений
        if (canvasScaler != null)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            canvasScaler.matchWidthOrHeight = 0.5f;
        }
        
        // Настройка мобильного UI
        SetupMobileUI();
        
#if Localization_yg
        // Подписка на изменение языка
        YG2.onSwitchLang += OnLanguageChanged;
        // Применяем язык при старте
        OnLanguageChanged(YG2.lang);
#endif
    }
    
    private void SetupMobileUI()
    {
        // Определяем, нужно ли показывать мобильный UI
        bool isMobile = false;
        
#if EnvirData_yg
        isMobile = YG2.envir.isMobile || YG2.envir.isTablet;
#else
        isMobile = Application.isMobilePlatform || Input.touchSupported;
#endif
        
        if (mobileUIContainer != null)
        {
            mobileUIContainer.SetActive(isMobile);
        }
    }
    
#if Localization_yg
    private void OnLanguageChanged(string lang)
    {
        // Обновляем все локализованные тексты
        UpdateAllLocalizedTexts();
    }
    
    private void UpdateAllLocalizedTexts()
    {
        // Находим все компоненты с локализованным текстом и обновляем их
        LocalizedText[] localizedTexts = FindObjectsByType<LocalizedText>(FindObjectsSortMode.None);
        foreach (var localizedText in localizedTexts)
        {
            localizedText.UpdateText();
        }
        
        // Обновляем локализованные кнопки
        LocalizedButton[] localizedButtons = FindObjectsByType<LocalizedButton>(FindObjectsSortMode.None);
        foreach (var localizedButton in localizedButtons)
        {
            localizedButton.UpdateButtonText();
        }
    }
    
    private void OnDestroy()
    {
        YG2.onSwitchLang -= OnLanguageChanged;
    }
#endif
    
    public void ShowPanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }
    
    public void HidePanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
    
    // Публичные свойства для доступа к панелям
    public GameObject MainMenuPanel => mainMenuPanel;
    public GameObject GamePanel => gamePanel;
    public GameObject SettingsPanel => settingsPanel;
    public GameObject MobileUIContainer => mobileUIContainer;
}
