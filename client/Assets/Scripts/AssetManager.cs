using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

#nullable enable
public class AssetManager : PersistentSingletonBehaviour<AssetManager>
{
    [Header("Defaults")]
    [SerializeField] private Sprite defaultLevelThumbnail;
    public static Sprite DefaultLevelThumbnail => Instance.defaultLevelThumbnail;
    [SerializeField] private Sprite defaultUserThumbnail;
    public static Sprite DefaultUserThumbnail => Instance.defaultUserThumbnail;

    protected override void Awake()
    {
        base.Awake();
    }

    private static readonly Dictionary<string, Texture2D> textureCache = new();
    private static readonly Dictionary<string, Sprite> spriteCache = new();

    public static readonly string LocalPrefix = "local::";
    /// <summary>
    /// Determines if the provided path is a local asset and extracts the local path.
    /// </summary>
    /// <param name="path">The path to evaluate.</param>
    /// <param name="localPath">The local path, if valid.</param>
    /// <returns>True if the path is a local asset; otherwise, false.</returns>
    public static bool IsLocalData(string path, [NotNullWhen(true)] out string? localPath)
    {
        if (path.StartsWith(LocalPrefix))
        {
            localPath = path[LocalPrefix.Length..];
            return true;
        }
        localPath = null;
        return false;
    }

    /// <summary>
    /// Downloads content asynchronously from a URI as a byte array.
    /// </summary>
    /// <param name="uri">The URI to download from.</param>
    /// <returns>A task representing the downloaded content as a byte array.</returns>
    public static async Task<byte[]> Download(string uri)
    {
        using HttpClient client = new();
        return await client.GetByteArrayAsync(uri);
    }

    /// <summary>
    /// Loads a Texture2D from a path (local or remote), caching results to avoid redownloading.
    /// </summary>
    /// <param name="path">The path to the texture (local or remote URI).</param>
    /// <param name="allowCachedResult">Whether to use the cached result if available.</param>
    /// <param name="cacheNewResult">Whether to cache newly downloaded textures.</param>
    /// <returns>A task representing the loaded Texture2D.</returns>
    public static async Task<Texture2D> GetTexture2D(string path, bool allowCachedResult = true, bool cacheNewResult = true)
    {
        // Check the cache
        if (allowCachedResult && textureCache.TryGetValue(path, out Texture2D cachedTexture))
        {
            return cachedTexture;
        }

        byte[] textureData;

        // Handle local paths
        if (IsLocalData(path, out string? localPath))
        {
            string fullPath = Path.Combine(Application.dataPath, localPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Local file not found: {fullPath}");

            textureData = await File.ReadAllBytesAsync(fullPath);
        }
        // Handle remote URIs
        else
        {
            textureData = await Download(path);
        }

        // Create Texture2D from the data
        Texture2D texture = new(2, 2);
        if (!texture.LoadImage(textureData))
        {
            throw new Exception($"Failed to create Texture2D from data at {path}");
        }

        // Optionally cache the texture
        if (cacheNewResult)
        {
            textureCache[path] = texture;
        }

        return texture;
    }

    public static async Task<Sprite> GetSprite(string path, bool allowCachedResult = true, bool cacheNewResult = true)
    {
        // Check the sprite cache first
        if (allowCachedResult && spriteCache.TryGetValue(path, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        // Get the Texture2D (this handles caching for textures)
        Texture2D texture = await GetTexture2D(path, allowCachedResult, cacheNewResult);

        // Create a new sprite
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

        // Cache the sprite
        if (cacheNewResult)
        {
            spriteCache[path] = sprite;
        }

        return sprite;
    }
}