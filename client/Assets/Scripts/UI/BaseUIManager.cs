using System.Collections.Generic;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.UIElements;

# nullable enable
public class Window
{
    public readonly string containerId;
    public readonly VisualElement container;

    public Window(VisualElement root, string containerId)
    {
        this.containerId = containerId;
        this.container = root.Q(containerId);
    }
    public Window(VisualElement container)
    {
        this.containerId = container.name;
        this.container = container;
    }

    public void Hide()
    {
        SetVisible(false);
    }
    public void Show()
    {
        SetVisible(true);
    }
    public void SetVisible(bool shown)
    {
        container.EnableInClassList("hidden", !shown);
    }
}

public class BaseUIManager : MonoBehaviour
{
#pragma warning disable CS8618
    [SerializeField] protected UIDocument _document;
#pragma warning restore CS8618
    public VisualElement Root => _document.rootVisualElement;
    protected Stack<Window> openWindows = new();
    protected List<Window> windows = new();

    public void CloseAllWindows()
    {
        while (openWindows.Count > 0)
        {
            CloseLastWindow();
        }
    }
    public void OpenWindow(Window window)
    {
        window.Show();
        openWindows.Push(window);
    }
    public void CloseLastWindow()
    {
        if (openWindows.TryPop(out Window lastWindow))
            lastWindow.Hide();
    }

    public Window? GetWindowByContainerId(string containerId)
    {
        foreach (var window in windows) {
            if (window.containerId == containerId) return window;
        }
        return null;
    }
    public void SetShownWindow(Window? window)
    {
        foreach (var otherWindow in windows)
        {
            otherWindow.SetVisible(window == otherWindow);
        }
        openWindows.Clear();
        if (window != null) { openWindows.Push(window); }
    }
    public void SetShownWindow(string containerId)
    {
        Window? window = GetWindowByContainerId(containerId);
        SetShownWindow(window);
    }
}