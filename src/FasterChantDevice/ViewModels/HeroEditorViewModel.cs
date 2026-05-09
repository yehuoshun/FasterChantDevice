using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using FasterChantDevice.Models;

namespace FasterChantDevice.ViewModels;

public class HeroEditorViewModel : INotifyPropertyChanged
{
    private readonly Services.SchemeManager _schemeManager;
    private HeroScheme _hero;

    public HeroEditorViewModel(Services.SchemeManager schemeManager, HeroScheme? hero = null)
    {
        _schemeManager = schemeManager;
        _hero = hero ?? new HeroScheme();

        // Initialize panels 0-9
        while (Panels.Count < 10)
            Panels.Add(new PhrasePanel());

        // Initialize taunt with one empty box
        if (TauntBoxes.Count == 0)
            TauntBoxes.Add(string.Empty);
    }

    // --- Hero name ---
    public string HeroName
    {
        get => _hero.Name;
        set { _hero.Name = value; OnPropertyChanged(); }
    }

    // --- Trigger phrases (single textblock, line-based) ---
    private string _gameStartText = "";
    public string GameStartText
    {
        get => _gameStartText;
        set
        {
            _gameStartText = value;
            _hero.Triggers.GameStart = SplitLines(value);
            OnPropertyChanged();
        }
    }

    private string _killText = "";
    public string KillText
    {
        get => _killText;
        set
        {
            _killText = value;
            _hero.Triggers.Kill = SplitLines(value);
            OnPropertyChanged();
        }
    }

    private string _deathText = "";
    public string DeathText
    {
        get => _deathText;
        set
        {
            _deathText = value;
            _hero.Triggers.Death = SplitLines(value);
            OnPropertyChanged();
        }
    }

    private string _assistText = "";
    public string AssistText
    {
        get => _assistText;
        set
        {
            _assistText = value;
            _hero.Triggers.Assist = SplitLines(value);
            OnPropertyChanged();
        }
    }

    // --- Taunt (multiple textblocks) ---
    public ObservableCollection<string> TauntBoxes { get; set; } = new();

    // --- Panels (left list + right editor) ---
    public ObservableCollection<PhrasePanel> Panels { get; set; } = new();

    private PhrasePanel? _selectedPanel;
    public PhrasePanel? SelectedPanel
    {
        get => _selectedPanel;
        set
        {
            _selectedPanel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedPanelText));
            OnPropertyChanged(nameof(SelectedPanelName));
        }
    }

    public string SelectedPanelText
    {
        get => SelectedPanel != null ? string.Join("\n", SelectedPanel.Lines) : "";
        set
        {
            if (SelectedPanel != null)
            {
                SelectedPanel.Lines = SplitLines(value);
                OnPropertyChanged();
            }
        }
    }

    public string SelectedPanelName
    {
        get => SelectedPanel?.Name ?? "";
        set
        {
            if (SelectedPanel != null)
            {
                SelectedPanel.Name = value;
                OnPropertyChanged();
            }
        }
    }

    // --- Actions ---
    public void Save()
    {
        // Sync taunt boxes back to hero
        _hero.Triggers.Taunt.Boxes = TauntBoxes
            .Select(text => SplitLines(text))
            .Where(l => l.Count > 0)
            .ToList();

        _schemeManager.SaveHero(_hero);
    }

    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

    // --- Apply hero to editor ---
    public void LoadHero(HeroScheme hero)
    {
        _hero = hero;
        HeroName = hero.Name;
        GameStartText = string.Join("\n", hero.Triggers.GameStart);
        KillText = string.Join("\n", hero.Triggers.Kill);
        DeathText = string.Join("\n", hero.Triggers.Death);
        AssistText = string.Join("\n", hero.Triggers.Assist);

        TauntBoxes.Clear();
        foreach (var box in hero.Triggers.Taunt.Boxes)
            TauntBoxes.Add(string.Join("\n", box));
        if (TauntBoxes.Count == 0)
            TauntBoxes.Add(string.Empty);

        Panels.Clear();
        for (int i = 0; i < 10; i++)
        {
            if (i < hero.Panels.Count)
                Panels.Add(hero.Panels[i]);
            else
                Panels.Add(new PhrasePanel());
        }
    }

    // --- Add taunt box ---
    public void AddTauntBox() => TauntBoxes.Add(string.Empty);

    public void RemoveTauntBox(int index)
    {
        if (TauntBoxes.Count > 1 && index >= 0 && index < TauntBoxes.Count)
            TauntBoxes.RemoveAt(index);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
