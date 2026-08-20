using Zshot.Features.Screenshot.Ocr;
using Zshot.Frameworks;
using Zshot.Helpers;
using Zshot.Language;

namespace Zshot.Features.Setting;

public sealed partial class OcrSetting : PageBase
{

    public OcrSetting()
    {
        InitializeComponent();
        string? key = SecretStorageService.Load("apiKey");
        if (!string.IsNullOrEmpty(key))
        {
            ApiKeyBox.Password = key;
        }
    }


    /// <summary>模型缺失时 OCR 会退回 Windows 本地识别，这里显示实际会用的引擎。</summary>
    public string OcrEngineText { get; } =
        RapidOcrRecognizer.ModelsAvailable ? "PP-OCRv6 Small" : Lang.Zshot_OcrEngineWindowsFallback;


    public string TranslationBaseUrl
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationBaseUrl = value;
            }
        }
    } = AppConfig.TranslationBaseUrl;


    public string TranslationModel
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationModel = value;
            }
        }
    } = AppConfig.TranslationModel;


    public string TranslationTargetLanguage
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationTargetLanguage = value;
            }
        }
    } = AppConfig.TranslationTargetLanguage;


    public string TranslationPrompt
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationPrompt = value;
            }
        }
    } = AppConfig.TranslationPrompt;


    public double TranslationTimeoutSeconds
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.TranslationTimeoutSeconds = (int)value;
            }
        }
    } = AppConfig.TranslationTimeoutSeconds;


    private void ApiKeyBox_LostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SecretStorageService.Save("apiKey", ApiKeyBox.Password);
    }

}
