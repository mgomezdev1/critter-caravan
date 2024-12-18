using System;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public abstract partial class DataField : VisualElement
{
    protected readonly Label label;
    protected readonly Label errorLabel;

    public TextField InputField { get; private set; }

    [UxmlAttribute]
    public string LabelText
    {
        get { return label.text; }
        set { label.text = value; }
    }

    [UxmlAttribute]
    public string ErrorText
    {
        get { return errorLabel.text; }
        set
        {
            errorLabel.text = value;
            UpdateValidState();
        }
    }

    [UxmlAttribute]
    public string TextValue
    {
        get { return InputField.value; }
        set { InputField.value = value; }
    }
    [UxmlAttribute]
    public string Placeholder
    {
        get { return InputField.textEdition.placeholder; }
        set { InputField.textEdition.placeholder = value; }
    }

    [UxmlAttribute]
    public bool HideInput
    {
        get { return InputField.textEdition.isPassword; }
        set { InputField.textEdition.isPassword = value; }
    }

    protected VisualElement FieldRow { get; set; }
    public override VisualElement contentContainer => FieldRow;

    public DataField()
    {
        // Initialization of internal values
        FieldRow = new VisualElement();
        FieldRow.AddToClassList("field-row");
        this.AddToClassList("field");

        // Initialization of children
        label = new Label();
        label.AddToClassList("field-label");
        VisualElement inputFieldHolder = new() { name = "InputFieldHolder" };
        inputFieldHolder.style.flexGrow = new StyleFloat(1.0f);
        InputField = new TextField();
        InputField.AddToClassList("field-input");
        inputFieldHolder.Add(InputField);
        FieldRow.Add(inputFieldHolder);
        errorLabel = new Label();
        errorLabel.AddToClassList("field-error");
        errorLabel.style.display = DisplayStyle.None;

        // Attaching children to own component tree
        this.hierarchy.Add(label);
        this.hierarchy.Add(FieldRow);
        this.hierarchy.Add(errorLabel);

        InputField.RegisterValueChangedCallback(HandleChangeEvent);
        SetValidState(true);
    }

    protected void HandleChangeEvent(ChangeEvent<string> evt)
    {
        UpdateValue(evt.newValue);
    }
    protected abstract void UpdateValue(string rawValue);

    protected void UpdateValidState()
    {
        SetValidState(string.IsNullOrEmpty(ErrorText));
    }
    protected void SetValidState(bool valid)
    {
        errorLabel.style.display = valid ? DisplayStyle.None : DisplayStyle.Flex;
        this.EnableInClassList("invalid", !valid);
    }
}

#nullable enable
[UxmlElement]
public partial class GenericField<T> : DataField
{
    public T? Value
    {
        get { return lastValidValue; }
        set { lastValidValue = Value; InputField.SetValueWithoutNotify(value?.ToString() ?? ""); }
    }
    protected T? lastValidValue = default;

    public Func<string, ValidationResult<T>> Validator
    {
        get { return validator; }
        set { validator = value; }
    }
    protected Func<string, ValidationResult<T>> validator = Validation.RejectAll<T>;

    public virtual event Action<T>? OnSuccessfulEdit;

    protected override void UpdateValue(string rawValue)
    {
        if (validator == null)
        {
            Debug.LogWarning($"Field {this.name} has no validator. Value will always be default.");
            return;
        }

        ValidationResult<T> validationResult = validator(rawValue);
        if (validationResult.Success)
        {
            T result = validationResult.Value
                ?? throw new Exception($"Validator succeeded but returned a null value from raw value \"{rawValue}\"");
            lastValidValue = result;
            OnSuccessfulEdit?.Invoke(result);
            ErrorText = "";
        }
        else
        {
            if (string.IsNullOrEmpty(validationResult.Message))
            {
                throw new Exception($"Validator rejected value \"{rawValue}\" but didn't specify a reason");
            }
            ErrorText = validationResult.Message ?? "";
        }
    }
}

[UxmlElement]
public partial class StringField : GenericField<string>
{
    public StringField() : base()
    {
        validator = Validation.AllowAll;
    }
}

[UxmlElement]
public partial class IntField : GenericField<int>
{
    public IntField() : base()
    {
        InputField.textEdition.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
        validator = Validation.Integer;
    }
}

[UxmlElement]
public partial class NumericField : GenericField<float>
{
    public NumericField() : base()
    {
        InputField.textEdition.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
        validator = Validation.Numeric;
    }
}