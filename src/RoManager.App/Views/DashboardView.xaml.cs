using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using RoManager.Core;

namespace RoManager.App.Views;

/** Р’СЂРµРјРµРЅРЅС‹Р№ РґР°С€Р±РѕСЂРґ: СЃРїРёСЃРѕРє Р°РєРєР°СѓРЅС‚РѕРІ РёР· С…СЂР°РЅРёР»РёС‰Р°. РџРѕР»РЅС‹Р№ РїРµСЂРµРЅРѕСЃ вЂ” СЌС‚Р°Рї 4. */
public sealed partial class DashboardView : UserControl
{
    public event Action LogoutRequested;

    public DashboardView()
    {
        InitializeComponent();
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        AccountsTitle.Text = Strings.I["dash.accounts"];
        LogoutButton.Content = Strings.I["dash.logout"];
        WipHint.Text = Strings.I["dash.wip"];
        Reload();
    }

    public void Reload()
    {
        AccountList.Children.Clear();

        List<AccountInfo> accounts;
        try { accounts = Vault.All()?.ToList() ?? new List<AccountInfo>(); }
        catch (Exception ex)
        {
            App.Dump("DashboardLoad", ex);
            accounts = new List<AccountInfo>();
        }

        AccountsCount.Text = accounts.Count.ToString();

        if (accounts.Count == 0)
        {
            AccountList.Children.Add(new TextBlock
            {
                Text = Loc.T("Р’ С…СЂР°РЅРёР»РёС‰Рµ РїРѕРєР° РЅРµС‚ Р°РєРєР°СѓРЅС‚РѕРІ.", "There are no accounts in the vault yet."),
                Style = (Style)Application.Current.Resources["AppMutedText"],
            });
            return;
        }

        foreach (var account in accounts)
            AccountList.Children.Add(CreateRow(account));
    }

    Border CreateRow(AccountInfo account)
    {
        var online = account.Online;
        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = (Brush)Application.Current.Resources[online ? "AppSuccess" : "AppMuted"],
        };

        var name = new TextBlock
        {
            Text = string.IsNullOrEmpty(account.Name) ? "вЂ”" : account.Name,
            Style = (Style)Application.Current.Resources["AppBody"],
            VerticalAlignment = VerticalAlignment.Center,
        };

        var last = new TextBlock
        {
            Text = account.LastOnline ?? "вЂ”",
            Style = (Style)Application.Current.Resources["AppMutedText"],
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(dot, 0);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(last, 2);
        name.Margin = new Thickness(10, 0, 10, 0);

        grid.Children.Add(dot);
        grid.Children.Add(name);
        grid.Children.Add(last);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["AppCard"],
            BorderBrush = (Brush)Application.Current.Resources["AppBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 14, 10),
            Child = grid,
        };
    }

    void OnLogout(object sender, RoutedEventArgs e) => LogoutRequested?.Invoke();
}
