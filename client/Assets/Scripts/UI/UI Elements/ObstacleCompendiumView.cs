using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

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

    public ObstacleCompendiumView()
    {
        // build UI
        this.AddToClassList("category-holder");
    }

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
                holder.OnSelected += (obstacle) => OnObstacleSelected.Invoke(obstacle);
                row.Add(holder);
            }

            newTab.Add(itemScrollView);
            Add(newTab);
        }
    }
}
