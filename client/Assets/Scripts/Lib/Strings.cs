using System.IO.Compression;
using System.IO;
using System.Text;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public static class Base64Compress
{
    /// <summary>
    /// Compresses a serialized string into a Gzip + Base64 encoded string.
    /// </summary>
    /// <param name="serializedString">The input string to compress.</param>
    /// <returns>A compressed Base64 string.</returns>
    public static string Compress(string serializedString)
    {
        if (string.IsNullOrEmpty(serializedString))
            throw new ArgumentException("Input string cannot be null or empty.");

        byte[] rawBytes = Encoding.UTF8.GetBytes(serializedString);

        using (var outputStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            {
                gzipStream.Write(rawBytes, 0, rawBytes.Length);
            }

            byte[] compressedBytes = outputStream.ToArray();
            return Convert.ToBase64String(compressedBytes);
        }
    }

    /// <summary>
    /// Decompresses a Gzip + Base64 encoded string back to the original serialized string.
    /// </summary>
    /// <param name="compressedString">The compressed Base64 string to decompress.</param>
    /// <returns>The decompressed original string.</returns>
    public static string Decompress(string compressedString)
    {
        if (string.IsNullOrEmpty(compressedString))
            throw new ArgumentException("Input string cannot be null or empty.");

        byte[] compressedBytes = Convert.FromBase64String(compressedString);

        using (var inputStream = new MemoryStream(compressedBytes))
        using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
        using (var outputStream = new MemoryStream())
        {
            gzipStream.CopyTo(outputStream);
            byte[] decompressedBytes = outputStream.ToArray();
            return Encoding.UTF8.GetString(decompressedBytes);
        }
    }
}

public enum CompressionType
{
    None = 0,
    Base64 = 1,
    Base64Gzip = 2,
}

public static class StringLib
{
    public const char ESCAPE_PREFIX = '\\';
    public static string AddEscapeLayer(string input, char charToEscape)
    {
        return input.Replace($"{ESCAPE_PREFIX}", $"{ESCAPE_PREFIX}{ESCAPE_PREFIX}")
            .Replace($"{charToEscape}", $"{ESCAPE_PREFIX}{charToEscape}");
    }
    public static string RemoveEscapeLayer(string input, char charToEscape)
    {
        return input.Replace($"{ESCAPE_PREFIX}{charToEscape}", $"{charToEscape}")
            .Replace($"{ESCAPE_PREFIX}{ESCAPE_PREFIX}", $"{ESCAPE_PREFIX}");
    }

    public static string[] SplitByCharacterIgnoreEscaped(string input, char separator)
    {
        Regex splitter = new(@$"(?<!{ESCAPE_PREFIX}){separator}");
        return splitter.Split(input);
    }

    public static IEnumerable<string> SplitByOutermostSeparator(string input, char separator, char blockOpenChar, char blockCloseChar, StringSplitOptions options = StringSplitOptions.None, char escapePrefix = ESCAPE_PREFIX, bool keepEscapePrefixes = true)
    {
        int level = 0;
        StringBuilder currentItem = new();
        bool isEscaped = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            // Handle escape character: treat the next character as literal
            if (c == escapePrefix && !isEscaped)
            {
                isEscaped = true;
                // only add the escape prefix to the output if the option to keep it is set.
                if (keepEscapePrefixes) currentItem.Append(c);
                continue;
            }

            // If escape is active, treat the current character as part of the current item
            if (isEscaped)
            {
                currentItem.Append(c);
                isEscaped = false; // Reset escape state after appending
                continue;
            }

            // Check for separator when at level 0 (not inside blocks)
            if (c == separator && level == 0)
            {
                if (currentItem.Length > 0 || !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
                    yield return currentItem.ToString();
                currentItem.Clear();
                continue;
            }

            // Track nesting level for block characters
            if (c == blockOpenChar) level++;
            if (c == blockCloseChar) level--;

            // Append the current character to the current item
            currentItem.Append(c);
        }

        // Yield the last part (if any)
        if (currentItem.Length > 0 || !options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
        {
            yield return currentItem.ToString();
        }

        // Error handling for unbalanced blocks (this is a symptom of strings that aren't managed by this system in a healthy way)
        if (level != 0)
        {
            throw new InvalidOperationException("Unmatched block delimiters detected.");
        }
    }

    public static string Compress(string input, CompressionType compression)
    {
        return compression switch
        {
            CompressionType.None => input,
            CompressionType.Base64 => Convert.ToBase64String(Encoding.UTF8.GetBytes(input)),
            CompressionType.Base64Gzip => Base64Compress.Compress(input),
            _ => throw new InvalidOperationException($"The CompressionType {compression} is invalid or not yet implemented.")
        };
    }
    public static string Decompress(string input, CompressionType compression)
    {
        return compression switch
        {
            CompressionType.None => input,
            CompressionType.Base64 => Encoding.UTF8.GetString(Convert.FromBase64String(input)),
            CompressionType.Base64Gzip => Base64Compress.Decompress(input),
            _ => throw new InvalidOperationException($"The CompressionType {compression} is invalid or not yet implemented.")
        };
    }

    public static string Stringify<K, V>(this Dictionary<K, V> dictionary)
    {
        StringBuilder result = new();
        result.Append('{');
        result.Append(string.Join(",", dictionary.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
        result.Append('}');
        return result.ToString();
    }
}