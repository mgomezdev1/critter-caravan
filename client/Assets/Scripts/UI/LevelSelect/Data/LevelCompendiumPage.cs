using Networking;
using System.Collections.Generic;
using UnityEngine;

#nullable enable
public class LevelCompendiumPage : ILevelCompendiumPage
{
    public Sprite Background { get; set; }
    public IEnumerable<ILevel> Levels => levels;
    private readonly List<ILevel> levels;

    public string? PageName { get; set; }

    public LevelCompendiumPage(Sprite background, List<ILevel> levels, string? pageName = null)
    {
        Background = background;
        this.levels = levels;
        PageName = pageName;
    }

    public string GetPageName(int index, int total)
    {
        if (PageName == null)
        {
            return $"Page {index + 1}/{total}";
        }
        return PageName;
    }
}