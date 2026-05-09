using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace _300FasterChantDevice.Models;

public class HeroScheme
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("triggers")]
    public TriggerConfig Triggers { get; set; } = new();

    [JsonPropertyName("panels")]
    public List<PhrasePanel> Panels { get; set; } = new();
}

public class TriggerConfig
{
    [JsonPropertyName("game_start")]
    public List<string> GameStart { get; set; } = new();

    [JsonPropertyName("kill")]
    public List<string> Kill { get; set; } = new();

    [JsonPropertyName("death")]
    public List<string> Death { get; set; } = new();

    [JsonPropertyName("assist")]
    public List<string> Assist { get; set; } = new();

    [JsonPropertyName("taunt")]
    public TauntConfig Taunt { get; set; } = new();
}

public class TauntConfig
{
    [JsonPropertyName("boxes")]
    public List<List<string>> Boxes { get; set; } = new();
}

public class PhrasePanel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("lines")]
    public List<string> Lines { get; set; } = new();
}
