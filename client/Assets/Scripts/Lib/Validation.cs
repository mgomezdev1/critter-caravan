#nullable enable
public class ValidationResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Value { get; set; }

    public ValidationResult(bool valid, string? message = null, T? value = default)
    {
        this.Success = valid;
        Value = value;
        this.Message = message;
    }
}

public static class Validation
{
    public static ValidationResult<T> Success<T>(T value, string? message = null)
    {
        return new ValidationResult<T>(true, message, value);
    }
    public static ValidationResult<T> Failure<T>(string message)
    {
        return new ValidationResult<T>(false, message);
    }

    public static ValidationResult<string> AllowAll(string value)
    {
        return Success(value);
    }
    public static ValidationResult<T> RejectAll<T>(string _)
    {
        return Failure<T>($"This field rejects all values.");
    }
    public static ValidationResult<int> Integer(string value)
    {
        if (int.TryParse(value, out int parsed)) return Success(parsed);
        return Failure<int>($"{value} must be an integer.");
    }
    public static ValidationResult<float> Numeric(string value)
    {
        if (float.TryParse(value, out float parsed)) return Success(parsed);
        return Failure<float>($"{value} must be a number");
    }
}