#nullable enable
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level Compendium", menuName = "Scriptable Objects/Level Compendium")]
public class InbuiltLevelCompendium : ScriptableObject, ILevelCompendium
{
    public IEnumerable<ILevelCompendiumPage> Pages => pages;
    [SerializeField] private List<InbuiltLevelCompendiumPage> pages = new();
}