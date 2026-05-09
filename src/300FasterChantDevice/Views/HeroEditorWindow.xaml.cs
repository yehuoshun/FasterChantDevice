using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using _300FasterChantDevice.Services;
using _300FasterChantDevice.ViewModels;

namespace _300FasterChantDevice.Views;

public partial class HeroEditorWindow : Window
{
    private readonly SchemeManager _schemeManager;
    private HeroEditorViewModel? _vm;

    public HeroEditorWindow(SchemeManager schemeManager)
    {
        InitializeComponent();
        _schemeManager = schemeManager;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshHeroList();
        if (_schemeManager.Heroes.Count > 0)
        {
            HeroCombo.SelectedIndex = 0;
            LoadHero(_schemeManager.Heroes[0]);
        }
        else
        {
            _vm = new HeroEditorViewModel(_schemeManager);
            BuildTriggersUI();
            BuildPanelsUI();
        }

        // Burst toggle
        BurstToggle.IsChecked = _schemeManager.Settings.BurstMode;
        BurstToggle.Checked += (_, _) => _schemeManager.Settings.BurstMode = true;
        BurstToggle.Unchecked += (_, _) => _schemeManager.Settings.BurstMode = false;

        // Interval
        IntervalBox.Text = (_schemeManager.Settings.BurstIntervalMs / 1000.0).ToString("0.0");
        IntervalBox.TextChanged += (_, _) =>
        {
            if (double.TryParse(IntervalBox.Text, out var s) && s > 0)
                _schemeManager.Settings.BurstIntervalMs = (int)(s * 1000);
        };

        // Select triggers tab by default
        TriggersTab_Click(null!, null!);
    }

    // ===== Hero combo =====
    private void RefreshHeroList()
    {
        HeroCombo.ItemsSource = _schemeManager.Heroes.Select(h => h.Name).ToList();
    }

    private void HeroCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HeroCombo.SelectedItem is string name)
        {
            var hero = _schemeManager.GetHero(name);
            if (hero != null) LoadHero(hero);
        }
    }

    private void NewHero_Click(object sender, RoutedEventArgs e)
    {
        var hero = new Models.HeroScheme { Name = "新英雄" };
        _schemeManager.SaveHero(hero);
        RefreshHeroList();
        HeroCombo.SelectedItem = hero.Name;
        LoadHero(hero);
    }

    private void DeleteHero_Click(object sender, RoutedEventArgs e)
    {
        if (HeroCombo.SelectedItem is not string name) return;
        var result = MessageBox.Show($"确定删除英雄方案「{name}」？", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _schemeManager.DeleteHero(name);
            RefreshHeroList();
            if (_schemeManager.Heroes.Count > 0)
            {
                HeroCombo.SelectedIndex = 0;
                LoadHero(_schemeManager.Heroes[0]);
            }
            else
            {
                _vm = new HeroEditorViewModel(_schemeManager);
                BuildTriggersUI();
                BuildPanelsUI();
            }
        }
    }

    // ===== Load hero into editor =====
    private void LoadHero(Models.HeroScheme hero)
    {
        _vm = new HeroEditorViewModel(_schemeManager);
        _vm.LoadHero(hero);
        BuildTriggersUI();
        BuildPanelsUI();
    }

    // ===== Tab switching =====
    private void TriggersTab_Click(object sender, RoutedEventArgs e)
    {
        TriggersPanel.Visibility = Visibility.Visible;
        PanelsPanel.Visibility = Visibility.Collapsed;
        TriggersTabBtn.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xe9, 0x45, 0x60));
        PanelsTabBtn.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2a, 0x2a, 0x4a));
    }

    private void PanelsTab_Click(object sender, RoutedEventArgs e)
    {
        TriggersPanel.Visibility = Visibility.Collapsed;
        PanelsPanel.Visibility = Visibility.Visible;
        PanelsTabBtn.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xe9, 0x45, 0x60));
        TriggersTabBtn.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2a, 0x2a, 0x4a));
    }

    // ===== Build triggers UI dynamically =====
    private void BuildTriggersUI()
    {
        if (_vm == null) return;
        TriggersContent.Children.Clear();

        // Layout: each event type gets a label + textblock
        AddTriggerSection("开局", _vm, nameof(_vm.GameStartText));
        AddTriggerSection("击杀", _vm, nameof(_vm.KillText));
        AddTriggerSection("死亡", _vm, nameof(_vm.DeathText));
        AddTriggerSection("助攻", _vm, nameof(_vm.AssistText));

        // Taunt section
        var tauntLabel = new TextBlock
        {
            Text = "骚话　　触发方式：",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 12, 0, 4)
        };
        TriggersContent.Children.Add(tauntLabel);

        var tauntTauntModeCombo = new ComboBox
        {
            ItemsSource = new[] { "手动", "定时", "手动+定时" },
            SelectedIndex = _schemeManager.Settings.TauntMode switch
            {
                "manual" => 0,
                "timer" => 1,
                _ => 2
            },
            Width = 100
        };
        // ... (simplified - would add mode toggle + interval)

        for (int i = 0; i < _vm.TauntBoxes.Count; i++)
        {
            var idx = i; // capture
            var tb = new TextBox
            {
                AcceptsReturn = true,
                Text = _vm.TauntBoxes[idx],
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x2a, 0x2a, 0x4a)),
                Foreground = System.Windows.Media.Brushes.White,
                Height = 60,
                Margin = new Thickness(0, 0, 0, 8)
            };
            tb.TextChanged += (_, _) =>
            {
                if (idx < _vm.TauntBoxes.Count)
                    _vm.TauntBoxes[idx] = tb.Text;
            };
            TriggersContent.Children.Add(tb);
        }

        var addTauntBtn = new Button
        {
            Content = "+ 添加文本框",
            Width = 100,
            Margin = new Thickness(0, 0, 0, 12)
        };
        addTauntBtn.Click += (_, _) =>
        {
            _vm.AddTauntBox();
            BuildTriggersUI(); // refresh
        };
        TriggersContent.Children.Add(addTauntBtn);
    }

    private void AddTriggerSection(string label, HeroEditorViewModel vm, string propertyName)
    {
        var titleBlock = new TextBlock
        {
            Text = label,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 12, 0, 4)
        };
        TriggersContent.Children.Add(titleBlock);

        var textBox = new TextBox
        {
            AcceptsReturn = true,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x2a, 0x2a, 0x4a)),
            Foreground = System.Windows.Media.Brushes.White,
            Height = 50,
            TextWrapping = System.Windows.TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // Bind text to viewmodel
        textBox.Text = propertyName switch
        {
            nameof(HeroEditorViewModel.GameStartText) => vm.GameStartText,
            nameof(HeroEditorViewModel.KillText) => vm.KillText,
            nameof(HeroEditorViewModel.DeathText) => vm.DeathText,
            nameof(HeroEditorViewModel.AssistText) => vm.AssistText,
            _ => ""
        };

        textBox.TextChanged += (_, _) =>
        {
            switch (propertyName)
            {
                case nameof(HeroEditorViewModel.GameStartText): vm.GameStartText = textBox.Text; break;
                case nameof(HeroEditorViewModel.KillText): vm.KillText = textBox.Text; break;
                case nameof(HeroEditorViewModel.DeathText): vm.DeathText = textBox.Text; break;
                case nameof(HeroEditorViewModel.AssistText): vm.AssistText = textBox.Text; break;
            }
        };

        TriggersContent.Children.Add(textBox);
    }

    // ===== Build panels UI =====
    private void BuildPanelsUI()
    {
        if (_vm == null) return;

        PanelList.ItemsSource = _vm.Panels.Select((p, i) =>
            string.IsNullOrEmpty(p.Name) ? $"{i}. （空）" : $"{i}. {p.Name}").ToList();

        if (_vm.Panels.Count > 0)
            PanelList.SelectedIndex = 0;
    }

    private void PanelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm == null || PanelList.SelectedIndex < 0) return;
        _vm.SelectedPanel = _vm.Panels[PanelList.SelectedIndex];
        PanelNameBox.Text = _vm.SelectedPanelName;
        PanelContentBox.Text = _vm.SelectedPanelText;
    }

    private void PanelName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.SelectedPanelName = PanelNameBox.Text;
            BuildPanelsUI(); // refresh list to show new name
        }
    }

    private void PanelContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_vm != null)
            _vm.SelectedPanelText = PanelContentBox.Text;
    }

    // ===== Save on close =====
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        _vm?.Save();
        _schemeManager.SaveSettings();
    }
}
