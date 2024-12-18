using UnityEngine;
using UnityEngine.UIElements;

public class LevelBrowser : VisualElement
{
    [UxmlAttribute]
    public ILevelCompendium LevelCompendium { 
        get => levelCompendium;
        set
        {
            levelCompendium = value;
            UpdateLevels();
        }        
    }
    private ILevelCompendium levelCompendium;

    public LevelBrowser()
    {

    }

    public void UpdateLevels()
    {

    }
}