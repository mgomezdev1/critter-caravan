using System.Collections.Generic;

public interface ILevelCompendium
{
    public IEnumerable<ILevelCompendiumPage> Pages { get; }
}