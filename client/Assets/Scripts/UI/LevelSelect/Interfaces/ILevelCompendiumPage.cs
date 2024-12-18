using Networking;
using System.Collections.Generic;
using UnityEngine;

public interface ILevelCompendiumPage
{
    public Sprite Background { get; }
    public IEnumerable<ILevel> Levels { get; }

    public string GetPageName(int index, int total);
}