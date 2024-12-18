using System.Collections.Generic;

public class LevelCompendium : ILevelCompendium
{
    public IEnumerable<ILevelCompendiumPage> Pages => pages;
    private readonly List<LevelCompendiumPage> pages = new();
}