using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UIDebugManager : MonoBehaviour
{
    public UnityEvent OnStepClicked = new();

    UIDocument _document;
    Button _stepButton;
    void Awake()
    {
        _document = GetComponent<UIDocument>();
        _stepButton = _document.rootVisualElement.Q("step-button") as Button;
        _stepButton.RegisterCallback<ClickEvent>(HandleStepClicked);
    }

    private void OnDisable()
    {
        _stepButton.UnregisterCallback<ClickEvent>(HandleStepClicked);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void HandleStepClicked(ClickEvent evt) {
        OnStepClicked.Invoke();
    }
}
