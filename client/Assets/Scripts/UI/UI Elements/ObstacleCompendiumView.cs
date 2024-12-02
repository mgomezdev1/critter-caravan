using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;
using UnityEngine.WSA;

#nullable enable
[UxmlElement]
public partial class ObstacleCompendiumView : TabView
{
    [UxmlAttribute]
    public ObstacleCompendium Compendium {
        get { return compendium; }
        set { compendium = value; UpdateTabs(); }
    }
    private ObstacleCompendium compendium;
    [UxmlAttribute]
    public int NumRows
    {
        get { return numRows; }
        set { numRows = value; UpdateTabs(); }
    }
    private int numRows = 2;

    public HashSet<ObstacleData> HighlightedObstacles
    {
        get { return highlightedObstacles; }
        set { highlightedObstacles = value; UpdateHighlights(); }
    }
    private HashSet<ObstacleData> highlightedObstacles = new();

    [Range(0, 360f)]
    [UxmlAttribute]
    public float ObstacleDisplayRotation
    {
        get { return obstacleDisplayRotation; }
        set { obstacleDisplayRotation = value; UpdateRotations(); }
    }
    private float obstacleDisplayRotation = 0f;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ObstacleCompendiumView()
    {
        // build UI
        this.AddToClassList("category-holder");
    }
#pragma warning restore CS8618

    private readonly List<ObstaclePicker> obstaclePickers = new();

    public event Action<ObstacleCategory> OnCategorySelected;
    public event Action<ObstacleData> OnObstacleSelected;

    private void UpdateTabs()
    {
        if (numRows < 1) return;

        Clear();

        if (compendium == null) return;
        foreach (var category in compendium.categories)
        {
            string catName = category.categoryName;
            List<ObstacleData> catItems = category.obstacles;

            Tab newTab = new();
            newTab.AddToClassList("item-tab");
            newTab.selected += (_) => OnCategorySelected.Invoke(category);
            newTab.label = catName;
            newTab.iconImage = Background.FromSprite(category.categoryIcon);

            ScrollView itemScrollView = new() { 
                mode = ScrollViewMode.Horizontal,
            };
            VisualElement rowHolder = new();
            rowHolder.AddToClassList("category-content");
            itemScrollView.Add(rowHolder);

            VisualElement[] rows = new VisualElement[numRows];
            for (int i = 0; i < numRows; i++) {
                rows[i] = new VisualElement();
                rows[i].AddToClassList("item-row");
                rowHolder.Add(rows[i]);
            }
            int rowIndex = 0;

            foreach (var data in catItems)
            {
                var row = rows[rowIndex];
                rowIndex = (rowIndex + 1) % numRows;

                var holder = new ObstaclePicker() { Data = data };
                obstaclePickers.Add(holder);
                holder.OnSelected += (obstacle) => HandleSelectObstacle(obstacle);
                row.Add(holder);
            }

            newTab.Add(itemScrollView);
            Add(newTab);
        }

        UpdateHighlights();
        UpdateRotations();
    }

    private void HandleSelectObstacle(ObstacleData data)
    {
        OnObstacleSelected.Invoke(data);
    }

    public void UpdateHighlights()
    {
        foreach (var slot in obstaclePickers)
        {
            slot.EnableInClassList(UIManager.ACTIVE_BUTTON_CLASS, HighlightedObstacles.Contains(slot.Data));
        }
    }

    public void RemoveHighlight(ObstacleData data)
    {
        highlightedObstacles.Remove(data);
        UpdateHighlights();
    }
    public void AddHighlight(ObstacleData data)
    {
        highlightedObstacles.Add(data);
        UpdateHighlights();
    }
    public void SetEnabledHighlight(ObstacleData data, bool enable)
    {
        if (enable) { AddHighlight(data); }
        else { RemoveHighlight(data); }
    }
    public void ClearHighlights()
    {
        highlightedObstacles.Clear();
        UpdateHighlights();
    }

    /// <summary>
    /// Toggles the presence of the ObstacleData data in the highlight set
    /// </summary>
    /// <param name="data">The target data to toggle in the highlight set</param>
    /// <returns>Whether the highlight set contains the given ObstacleData after the toggle operation</returns>
    public bool ToggleHighlight(ObstacleData data)
    {
        bool result;
        if (highlightedObstacles.Contains(data))
        {
            highlightedObstacles.Remove(data);
            result = false;
        }
        else
        {
            highlightedObstacles.Add(data);
            result = true;
        }
        UpdateHighlights();
        return result;
    }

    private void UpdateRotations()
    {
        foreach (var slot in obstaclePickers)
        {
            slot.Rotation = ObstacleDisplayRotation;
        }
    }
}
