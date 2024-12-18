using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;

#nullable enable
public static class Persistence
{
    private static bool PrewriteCheck(string internalPath, out string fullPath, bool allowOverwrite = true)
    {
        fullPath = Path.Join(Application.persistentDataPath, internalPath);
        if (File.Exists(fullPath) && !allowOverwrite)
        {
            return false;
        }

        string? internalDirectory = Path.GetDirectoryName(fullPath);
        if (internalDirectory != null) Directory.CreateDirectory(internalDirectory);
        return true;
    }

    public static async Task<T?> ReadJsonFile<T>(string internalPath)
    {
        try
        {
            string filePath = Path.Join(Application.persistentDataPath, internalPath);
            if (!File.Exists(filePath)) { return default; }

            string jsonContent = await File.ReadAllTextAsync(filePath);
            return string.IsNullOrWhiteSpace(jsonContent) ? default : JsonConvert.DeserializeObject<T>(jsonContent);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return default;
        }
    }
    public static async Task<bool> WriteJsonFile<T>(string internalPath, T content, bool allowOverwrite = true)
    {
        try
        {
            if (!PrewriteCheck(internalPath, out string filePath, allowOverwrite)) return false;

            string jsonContent = JsonConvert.SerializeObject(content, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, jsonContent);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
    }

    public static T? ReadJsonFileSync<T>(string internalPath)
    {
        try
        {
            string filePath = Path.Join(Application.persistentDataPath, internalPath);
            if (!File.Exists(filePath)) { return default; }

            string jsonContent = File.ReadAllText(filePath);
            return string.IsNullOrWhiteSpace(jsonContent) ? default : JsonConvert.DeserializeObject<T>(jsonContent);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return default;
        }
    }

    public static bool WriteJsonFileSync<T>(string internalPath, T content, bool allowOverwrite = true)
    {
        try
        {
            if (!PrewriteCheck(internalPath, out string filePath, allowOverwrite)) return false;

            string jsonContent = JsonConvert.SerializeObject(content, Formatting.Indented);
            File.WriteAllText(filePath, jsonContent);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
    }
}