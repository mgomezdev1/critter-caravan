#nullable enable
using Networking;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level Compendium Page", menuName = "Scriptable Objects/Level Compendium Page")]
public class InbuiltLevelCompendiumPage : ScriptableObject, ILevelCompendiumPage
{
    [SerializeField] private Sprite background;
    public Sprite Background => background;

    [SerializeField] private List<InbuiltLevel> levels = new();
    public IEnumerable<ILevel> Levels => levels;

    [SerializeField] private string pageName;
    public string PageName { get => pageName; set => pageName = value; }

    public string GetPageName(int index, int total)
    {
        return PageName;
    }
}