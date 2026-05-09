using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using FasterChantDevice.Models;

namespace FasterChantDevice.Services;

/// <summary>
/// Manages loading/saving hero schemes and global settings as JSON files.
/// Thread-safe: uses ReaderWriterLockSlim for Heroes/Settings access.
/// </summary>
public class SchemeManager
{
    private readonly string _dataDir;
    private readonly ReaderWriterLockSlim _lock = new();

    public AppSettings Settings { get; private set; } = new();
    public List<HeroScheme> Heroes { get; private set; } = new();

    public SchemeManager(string dataDir)
    {
        _dataDir = dataDir;
        Directory.CreateDirectory(Path.Combine(_dataDir, "heroes"));
    }

    public void LoadAll()
    {
        LoadSettings();
        LoadHeroes();
    }

    private void LoadSettings()
    {
        var path = Path.Combine(_dataDir, "settings.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
        }
        else
        {
            SaveSettings(); // create default
        }
    }

    public void SaveSettings()
    {
        var path = Path.Combine(_dataDir, "settings.json");
        string json;
        _lock.EnterReadLock();
        try { json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }); }
        finally { _lock.ExitReadLock(); }
        File.WriteAllText(path, json);
    }

    private void LoadHeroes()
    {
        Heroes.Clear();
        var heroDir = Path.Combine(_dataDir, "heroes");
        if (!Directory.Exists(heroDir)) return;

        foreach (var file in Directory.GetFiles(heroDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var hero = JsonSerializer.Deserialize<HeroScheme>(json);
                if (hero != null) Heroes.Add(hero);
            }
            catch (JsonException)
            {
                // Corrupt JSON — skip this file, it will be overwritten on next save
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[SchemeManager] Failed to read hero file {file}: {ex.Message}");
            }
        }
    }

    public void SaveHero(HeroScheme hero)
    {
        var safeName = SanitizeFileName(hero.Name);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            Debug.WriteLine($"[SchemeManager] Cannot save hero with empty/invalid name");
            return;
        }
        var path = Path.Combine(_dataDir, "heroes", $"{safeName}.json");
        var json = JsonSerializer.Serialize(hero, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);

        _lock.EnterWriteLock();
        try
        {
            // Update in-memory list — delete old file if name changed
            var existing = Heroes.FirstOrDefault(h => h.Name == hero.Name);
            if (existing != null)
            {
                var oldSafeName = SanitizeFileName(existing.Name);
                if (oldSafeName != safeName)
                {
                    var oldPath = Path.Combine(_dataDir, "heroes", $"{oldSafeName}.json");
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                }
                var idx = Heroes.IndexOf(existing);
                Heroes[idx] = hero;
            }
            else
            {
                Heroes.Add(hero);
            }
        }
        finally { _lock.ExitWriteLock(); }
    }

    public void DeleteHero(string name)
    {
        var safeName = SanitizeFileName(name);
        var path = Path.Combine(_dataDir, "heroes", $"{safeName}.json");
        if (File.Exists(path)) File.Delete(path);
        _lock.EnterWriteLock();
        try { Heroes.RemoveAll(h => h.Name == name); }
        finally { _lock.ExitWriteLock(); }
    }

    public HeroScheme? GetHero(string name)
    {
        _lock.EnterReadLock();
        try { return Heroes.FirstOrDefault(h => h.Name == name); }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// Read-lock helpers for external callers that iterate Heroes or read Settings.
    /// </summary>
    public void EnterReadLock() => _lock.EnterReadLock();
    public void ExitReadLock() => _lock.ExitReadLock();

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalid.Contains(c)));
    }

    /// <summary>
    /// Pick a line based on burst mode:
    /// - Burst ON  → return all lines for sequential sending
    /// - Burst OFF → return a single random line
    /// </summary>
    public string[] PickLines(List<string> lines, Random? rng = null)
    {
        if (lines.Count == 0) return Array.Empty<string>();
        rng ??= Random.Shared;

        _lock.EnterReadLock();
        try
        {
            if (Settings.BurstMode)
                return lines.ToArray();
            else
                return new[] { lines[rng.Next(lines.Count)] };
        }
        finally { _lock.ExitReadLock(); }
    }

    /// <summary>
    /// Pick a random taunt box, then pick lines from it.
    /// </summary>
    public string[] PickTauntLines(HeroScheme hero, Random? rng = null)
    {
        var boxes = hero.Triggers.Taunt.Boxes;
        if (boxes.Count == 0) return Array.Empty<string>();

        rng ??= Random.Shared;
        var box = boxes[rng.Next(boxes.Count)];
        return PickLines(box, rng);
    }
}
