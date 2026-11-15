using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RoManager.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace RoManager.App.Views;

public sealed partial class WizardView : UserControl
{
    enum Step { File, Phrase, Pin, Confirm, CreatePhrase, CreatePin, CreateConfirmPin, Done }

    Step _step = Step.File;
    bool _creating;
    bool _filtering;

    string _keyContent;
    string _keyName = "";
    string _phrase = "";
    string _pin = "";
    string _generatedKey;

    public event Action Completed;
    public event Action CancelRequested;

    public WizardView()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    /** Начать сценарий заново: вход по ключу или создание нового ключа. */
    public void Restart(bool haveAccess)
    {
        _creating = !haveAccess;
        _keyContent = null;
        _keyName = "";
        _phrase = "";
        _pin = "";
        _generatedKey = null;

        KeyFileName.Text = "";
        PhraseBox.Text = "";
        CreatePhraseBox.Text = "";
        PinBox.Password = "";
        CreatePinBox.Password = "";
        CreateConfirmBox.Password = "";
        HideErrors();

        SetStep(_creating ? Step.CreatePhrase : Step.File);
    }

    public void ApplyLanguage()
    {
        var s = Strings.I;

        BackText.Text = s["common.back"];

        FileTitle.Text = s["wizard.keyFile"];
        DropHint.Text = s["wizard.dropHint"];
        ChooseFileButton.Content = s["wizard.openFile"];
        FileNextButton.Content = s["common.next"];

        PhraseTitle.Text = s["wizard.keyPhrase"];
        PhraseHint.Text = s["wizard.twoWords"];
        PhraseNextButton.Content = s["common.next"];

        PinTitle.Text = s["wizard.pin"];
        PinHint.Text = s["wizard.pinHint"];
        PinNextButton.Content = s["common.next"];

        ConfirmTitle.Text = s["wizard.confirm"];
        SummaryFileLabel.Text = s["wizard.file"];
        SummaryPhraseLabel.Text = s["wizard.phrase"];
        ConfirmButton.Content = s["wizard.confirmLogin"];

        CreatePhraseTitle.Text = s["wizard.createPhrase"];
        CreatePhraseHint.Text = s["wizard.twoWords"];
        CreatePhraseNextButton.Content = s["common.next"];

        CreatePinTitle.Text = s["wizard.createPin"];
        CreatePinHint.Text = s["wizard.createPinHint"];
        CreatePinNextButton.Content = s["common.next"];

        CreateConfirmTitle.Text = s["wizard.confirmPinTitle"];
        CreateConfirmHint.Text = s["wizard.confirmPinHint"];
        CreateConfirmButton.Content = s["common.next"];

        DoneTitle.Text = s["wizard.done"];
        DoneLine1.Text = s["wizard.doneLine1"];
        DoneLine2.Text = s["wizard.doneLine2"];
        DownloadKeyButton.Content = s["wizard.downloadKey"];
        ToMainButton.Content = s["wizard.toMain"];

        PhraseError.Text = s["wizard.phraseError"];
        PinError.Text = s["wizard.pinError"];
        CreatePhraseError.Text = s["wizard.phraseError"];
        CreatePinError.Text = s["wizard.pinError"];

        RefreshSummary();
        UpdateStepLabel();
    }

    /* ---------- шаги ---------- */

    void SetStep(Step step)
    {
        _step = step;

        FilePanel.Visibility = Visible(step == Step.File);
        PhrasePanel.Visibility = Visible(step == Step.Phrase);
        PinPanel.Visibility = Visible(step == Step.Pin);
        ConfirmPanel.Visibility = Visible(step == Step.Confirm);
        CreatePhrasePanel.Visibility = Visible(step == Step.CreatePhrase);
        CreatePinPanel.Visibility = Visible(step == Step.CreatePin);
        CreateConfirmPanel.Visibility = Visible(step == Step.CreateConfirmPin);
        DonePanel.Visibility = Visible(step == Step.Done);
        BackButton.Visibility = Visible(step != Step.Done);

        UpdateStepLabel();
    }

    static Visibility Visible(bool v) => v ? Visibility.Visible : Visibility.Collapsed;

    void UpdateStepLabel()
    {
        var (number, _) = _step switch
        {
            Step.File or Step.CreatePhrase => (1, 0),
            Step.Phrase or Step.CreatePin => (2, 0),
            Step.Pin or Step.CreateConfirmPin => (3, 0),
            Step.Confirm or Step.Done => (4, 0),
            _ => (1, 0),
        };
        StepLabel.Text = Strings.I.F("wizard.step", number);

        var primary = (Brush)Application.Current.Resources["AppPrimary"];
        var dim = (Brush)Application.Current.Resources["AppBorder"];
        foreach (var (dot, index) in new[] { (Dot1, 1), (Dot2, 2), (Dot3, 3), (Dot4, 4) })
            dot.Fill = index <= number ? primary : dim;

        if (_step == Step.Confirm) RefreshSummary();
    }

    void OnBack(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case Step.File:
            case Step.CreatePhrase:
                CancelRequested?.Invoke();
                break;
            case Step.Phrase:
                SetStep(Step.File);
                break;
            case Step.Pin:
                SetStep(Step.Phrase);
                break;
            case Step.Confirm:
                SetStep(Step.Pin);
                break;
            case Step.CreatePin:
                SetStep(Step.CreatePhrase);
                break;
            case Step.CreateConfirmPin:
                SetStep(Step.CreatePin);
                break;
        }
    }

    /* ---------- выбор ключевого файла ---------- */

    void OnDropDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = Strings.I["wizard.dropHint"];
    }

    void OnDropDragLeave(object sender, DragEventArgs e)
    {
        DropZone.Opacity = 1.0;
    }

    async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault();
            if (file != null) await LoadKeyFile(file);
        }
        catch (Exception ex)
        {
            App.Dump("DropKeyFile", ex);
        }
        finally
        {
            deferral.Complete();
        }
    }

    void OnDropZoneTapped(object sender, TappedRoutedEventArgs e) => OnChooseFile(sender, e);

    async void OnChooseFile(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Desktop };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.Handle);
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file != null) await LoadKeyFile(file);
        }
        catch (Exception ex)
        {
            App.Dump("ChooseKeyFile", ex);
        }
    }

    async Task LoadKeyFile(StorageFile file)
    {
        try
        {
            _keyContent = await FileIO.ReadTextAsync(file);
            _keyName = file.Name;
            KeyFileName.Text = _keyName;
            FileNextButton.IsEnabled = !string.IsNullOrWhiteSpace(_keyContent);
        }
        catch (Exception ex)
        {
            App.Dump("ReadKeyFile", ex);
            _keyContent = null;
            FileNextButton.IsEnabled = false;
        }
    }

    void OnFileNext(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_keyContent))
        {
            CoreHost.PushStatus(Strings.I["err.selectKey"], "danger");
            return;
        }
        SetStep(Step.Phrase);
        PhraseBox.Focus(FocusState.Programmatic);
    }

    /* ---------- фраза ---------- */

    static bool PhraseOk(string phrase) =>
        (phrase ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 2;

    void OnPhraseNext(object sender, RoutedEventArgs e)
    {
        if (!PhraseOk(PhraseBox.Text))
        {
            PhraseError.Visibility = Visibility.Visible;
            return;
        }
        PhraseError.Visibility = Visibility.Collapsed;
        _phrase = PhraseBox.Text.Trim();
        SetStep(Step.Pin);
        PinBox.Focus(FocusState.Programmatic);
    }

    /* ---------- PIN ---------- */

    static bool PinOk(string pin) => Regex.IsMatch(pin ?? "", @"^\d{4,6}$");

    void FilterDigits(PasswordBox box)
    {
        if (_filtering) return;
        _filtering = true;
        try
        {
            var digits = new string((box.Password ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length > 6) digits = digits[..6];
            if (digits != box.Password) box.Password = digits;
        }
        finally
        {
            _filtering = false;
        }
    }

    void OnPinChanged(object sender, RoutedEventArgs e)
    {
        FilterDigits(PinBox);
        PinError.Visibility = Visibility.Collapsed;
    }

    void OnCreatePinChanged(object sender, RoutedEventArgs e)
    {
        FilterDigits(CreatePinBox);
        CreatePinError.Visibility = Visibility.Collapsed;
    }

    void OnCreateConfirmChanged(object sender, RoutedEventArgs e)
    {
        FilterDigits(CreateConfirmBox);
        CreateConfirmError.Visibility = Visibility.Collapsed;
    }

    void OnPinNext(object sender, RoutedEventArgs e)
    {
        if (!PinOk(PinBox.Password))
        {
            PinError.Visibility = Visibility.Visible;
            return;
        }
        PinError.Visibility = Visibility.Collapsed;
        _pin = PinBox.Password;
        SetStep(Step.Confirm);
    }

    void RefreshSummary()
    {
        SummaryFileValue.Text = string.IsNullOrEmpty(_keyName) ? Strings.I["wizard.keyJson"] : _keyName;
        SummaryPhraseValue.Text = MaskPhrase(_phrase);
        SummaryPinValue.Text = new string('•', Math.Max(4, _pin.Length));
    }

    static string MaskPhrase(string phrase)
    {
        var trimmed = (phrase ?? "").Trim();
        if (trimmed.Length == 0) return "•••• ••••";
        return string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string('•', Math.Min(w.Length, 6))));
    }

    /* ---------- вход ---------- */

    async void OnConfirmAccess(object sender, RoutedEventArgs e)
    {
        ConfirmButton.IsEnabled = false;
        ConfirmError.Visibility = Visibility.Collapsed;
        CoreHost.PushStatus(Loc.T("Открываю хранилище…", "Unlocking the vault…"), "busy");

        var (ok, msg) = await Task.Run(() => Vault.TryUnlock(_keyContent, _phrase, _pin));
        if (!ok)
        {
            ConfirmError.Text = msg;
            ConfirmError.Visibility = Visibility.Visible;
            CoreHost.PushStatus(msg, "danger");
            ConfirmButton.IsEnabled = true;
            return;
        }

        await Task.Run(() =>
        {
            Roblox.RestartTimers();
            Roblox.ApplyMultiInstance();
        });

        ConfirmButton.IsEnabled = true;
        Completed?.Invoke();
    }

    /* ---------- создание ключа ---------- */

    void OnCreatePhraseNext(object sender, RoutedEventArgs e)
    {
        if (!PhraseOk(CreatePhraseBox.Text))
        {
            CreatePhraseError.Visibility = Visibility.Visible;
            return;
        }
        CreatePhraseError.Visibility = Visibility.Collapsed;
        _phrase = CreatePhraseBox.Text.Trim();
        SetStep(Step.CreatePin);
        CreatePinBox.Focus(FocusState.Programmatic);
    }

    void OnCreatePinNext(object sender, RoutedEventArgs e)
    {
        if (!PinOk(CreatePinBox.Password))
        {
            CreatePinError.Visibility = Visibility.Visible;
            return;
        }
        CreatePinError.Visibility = Visibility.Collapsed;
        _pin = CreatePinBox.Password;
        SetStep(Step.CreateConfirmPin);
        CreateConfirmBox.Focus(FocusState.Programmatic);
    }

    async void OnCreateConfirm(object sender, RoutedEventArgs e)
    {
        var confirm = CreateConfirmBox.Password;
        if (!PinOk(confirm))
        {
            CreateConfirmError.Text = Strings.I["wizard.pinError"];
            CreateConfirmError.Visibility = Visibility.Visible;
            return;
        }
        if (confirm != _pin)
        {
            CreateConfirmError.Text = Strings.I["wizard.pinMismatch"];
            CreateConfirmError.Visibility = Visibility.Visible;
            return;
        }
        CreateConfirmError.Visibility = Visibility.Collapsed;

        CreateConfirmButton.IsEnabled = false;
        // Порядок как в 2.1.0: сохраняем файл (если выбрали место), затем открываем сейв.
        var content = BuildKeyFile(_phrase, _pin);
        var saved = await SaveKeyFile(content);

        var (ok, msg) = await Task.Run(() => Vault.TryUnlock(content, _phrase, _pin));
        if (!ok)
        {
            CoreHost.PushStatus(msg, "danger");
            CreateConfirmButton.IsEnabled = true;
            return;
        }
        await Task.Run(() => Vault.Save());

        _generatedKey = content;
        CoreHost.PushStatus(saved ? Strings.I["status.keyCreated"] : Strings.I["status.keyCreatedUnsaved"],
            saved ? "success" : "busy");
        CreateConfirmButton.IsEnabled = true;
        SetStep(Step.Done);
    }

    static string BuildKeyFile(string phrase, string pin)
    {
        const int iterations = Crypto.DefaultIterations;
        var salt = Crypto.Salt();
        var (_, check) = Crypto.DeriveKey(salt, phrase, pin, iterations);
        return Jsons.S(new
        {
            version = 2,
            alg = "AES-256-GCM",
            kdf = "PBKDF2-SHA256",
            iterations,
            salt,
            check,
            created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            note = "RoManager key"
        });
    }

    async Task<bool> SaveKeyFile(string content)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = "rom-key.json",
            };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.Handle);
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            var file = await picker.PickSaveFileAsync();
            if (file == null) return false;
            await FileIO.WriteTextAsync(file, content);
            return true;
        }
        catch (Exception ex)
        {
            App.Dump("SaveKeyFile", ex);
            return false;
        }
    }

    async void OnDownloadKey(object sender, RoutedEventArgs e)
    {
        var content = Vault.GetKeyFileContent() ?? _generatedKey;
        if (content == null)
        {
            CoreHost.PushStatus(Strings.I["status.sessionLocked"], "danger");
            return;
        }
        if (await SaveKeyFile(content))
            CoreHost.PushStatus(Strings.I["status.keySaved"], "success");
    }

    void OnToMain(object sender, RoutedEventArgs e) => Completed?.Invoke();

    void HideErrors()
    {
        PhraseError.Visibility = Visibility.Collapsed;
        PinError.Visibility = Visibility.Collapsed;
        ConfirmError.Visibility = Visibility.Collapsed;
        CreatePhraseError.Visibility = Visibility.Collapsed;
        CreatePinError.Visibility = Visibility.Collapsed;
        CreateConfirmError.Visibility = Visibility.Collapsed;
    }
}
