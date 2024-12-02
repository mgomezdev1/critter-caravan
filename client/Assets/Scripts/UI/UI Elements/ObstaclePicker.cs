using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

[UxmlElement]
public partial class ObstaclePicker : Button
{
    private readonly VisualElement spriteHolder;
    private readonly Label label;

    public event Action<ObstacleData> OnSelected;

    [UxmlAttribute]
    public ObstacleData Data {
        get { return data; } 
        set { data = value; UpdateObstacleDisplay(); } 
    }
    private ObstacleData data;

    [UxmlAttribute]
    public float Rotation
    {
        get { return rotation; }
        set { rotation = value; UpdateRotation(); }
    }
    private float rotation = 0;

    public ObstaclePicker()
    {
        // build UI
        this.AddToClassList("item");
        spriteHolder = new VisualElement();
        spriteHolder.AddToClassList("display-image");
        Add(spriteHolder);
        label = new Label();
        label.AddToClassList("display-label");
        Add(label);
        UpdateRotation();

        this.RegisterCallback<PointerDownEvent>(HandlePointerDown, TrickleDown.TrickleDown);
    }

    private void HandlePointerDown(PointerDownEvent evt)
    {
        OnSelected.Invoke(Data);
        evt.StopPropagation();
    }

    private void UpdateObstacleDisplay()
    {
        spriteHolder.style.backgroundImage = new StyleBackground(data != null ? data.obstacleSprite : null);
        label.text = data != null ? data.obstacleName : string.Empty;
    }

    private void UpdateRotation()
    {
        spriteHolder.style.rotate = new StyleRotate(new Rotate(rotation));
    }
}
