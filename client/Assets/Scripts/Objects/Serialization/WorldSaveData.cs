using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class WorldSaveDataConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(WorldSaveData);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is WorldSaveData worldData)
        {
            writer.WriteValue(worldData.Serialize(WorldManager.SerializationOptions));
        }
        else
        {
            writer.WriteNull();
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        string json = reader.Value as string;
        if (!string.IsNullOrEmpty(json))
        {
            return WorldSaveData.Deserialize(json, out _);
        }
        return null;
    }
}

#nullable enable
[Serializable]
public readonly struct WorldSerializationOptions
{
    public readonly int version;
    public readonly CompressionType compression;
    [NonSerialized] public readonly string repr;

    public WorldSerializationOptions(int version, CompressionType compression)
    {
        this.version = version;
        this.compression = compression;
        this.repr = $"v{version}{GetStringFromCompressionType(compression)}";
    }
    public static WorldSerializationOptions FromString(string compressedData, out string decompressedData)
    {
        Regex pattern = new(@"^v([0-9]+)([^0-9])");
        Match match = pattern.Match(compressedData);
        if (!match.Success) { throw new FormatException($"{typeof(WorldSerializationOptions).Name} failed to parse representation {compressedData}"); }
        int version = int.Parse(match.Groups[1].Value);
        CompressionType compression = GetCompressionTypeFromString(match.Groups[2].Value);
        
        string restInput = compressedData[match.Length..];
        decompressedData = StringLib.Decompress(restInput, compression);
        return new(version, compression);
    }

    public static string GetStringFromCompressionType(CompressionType type)
    {
        return type switch
        {
            CompressionType.None => "R",
            CompressionType.Base64 => "B",
            CompressionType.Base64Gzip => "C",
            _ => throw new ArgumentException($"The CompressionType {type} has no abbreviated string assigned.")
        };
    }
    public static CompressionType GetCompressionTypeFromString(string str)
    {
        return str switch
        {
            "R" => CompressionType.None,
            "B" => CompressionType.Base64,
            "C" => CompressionType.Base64Gzip,
            _ => throw new ArgumentException($"The string {str} cannot be converted to a valid Compression Type.")
        };
    }
}

[Serializable]
[MessagePackObject]
public struct InternalWorldSaveDataV2
{
    [Key(0)]
    public byte[][] obstacles;
}

[JsonConverter(typeof(WorldSaveDataConverter))]
public readonly struct WorldSaveData
{
    public readonly Vector2Int worldSize;
    public readonly List<ObstacleSaveData> obstacles;

    public const char OBSTACLE_SEPARATOR = ';';
    private static readonly Regex regex = new(@"^([0-9]+)x([0-9]+)(\{.*\})$");

    public WorldSaveData(Vector2Int worldSize, List<ObstacleSaveData> obstacles)
    {
        this.worldSize = worldSize;
        this.obstacles = obstacles;
    }

    public static WorldSaveData Deserialize(string rawData, out WorldSerializationOptions options)
    {
        if (string.IsNullOrEmpty(rawData))
            throw new ArgumentException("Input data cannot be null or empty.");

        options = WorldSerializationOptions.FromString(rawData, out string uncompressedData);

        if (options.version == 1)
        {
            Match match = regex.Match(uncompressedData);
            if (!match.Success)
                throw new FormatException("Input data does not match the expected format.");

            var size = new Vector2Int(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));

            var serializedOtherData = match.Groups[3].Value;
            var otherData = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(serializedOtherData)!;
            Debug.Log($"Deserializing {serializedOtherData} -> {otherData.Stringify()}");
            List<string> rawObstacleList = new();
            if (otherData.TryGetValue("obstacles", out JToken obstacleData))
            {
                string? rawObstacles = obstacleData.ToObject<string>();
                if (rawObstacles != null)
                {
                    rawObstacleList.AddRange(StringLib.SplitByOutermostSeparator(rawObstacles, OBSTACLE_SEPARATOR, '{', '}', StringSplitOptions.RemoveEmptyEntries));
                }
                otherData.Remove("obstacles");
            }

            List<ObstacleSaveData> obstacles = new();
            foreach (var rawObstacle in rawObstacleList)
            {
                ObstacleSaveData obstacle = ObstacleSaveData.Deserialize(rawObstacle, options.version);

                obstacles.Add(obstacle);
                //Debug.Log($"Obstacle deserialized: {obstacle}");
            }

            WorldSaveData result = new(size, obstacles);
            return result;
        }
        else if (options.version == 2)
        {
            byte[] bytes = Convert.FromBase64String(uncompressedData);
            uint[] sizeVals = BinaryUtils.DecodeLEB128(bytes, 2).ToArray();
            if (sizeVals.Length != 2)
            {
                throw new FormatException($"Unable to extract two values for the world size from the binary LEB128 representation of the world.");
            }
            Vector2Int size = new((int)sizeVals[0], (int)sizeVals[1]);

            var remainingBytes = bytes.Skip(BinaryUtils.GetLEB128Size(sizeVals)).ToArray();
            var data = MessagePackSerializer.Deserialize<InternalWorldSaveDataV2>(remainingBytes);

            List<ObstacleSaveData> obstacles = new();
            if (data.obstacles is null)
            {
                throw new FormatException($"Unable to extract obstacle byte list from the binary representation");
            }
            foreach (byte[] obstacleBytes in data.obstacles)
            {
                obstacles.Add(ObstacleSaveData.Deserialize(obstacleBytes));
            }

            WorldSaveData result = new(size, obstacles);
            return result;
        }
        throw new NotImplementedException($"Unable to deserialize data with schema version {options.version}.");
    }

    public string Serialize(WorldSerializationOptions options)
    {
        if (options.version == 1)
        {
            var data = new Dictionary<string, object>()
            {
                ["obstacles"] = string.Join(OBSTACLE_SEPARATOR,
                    obstacles.Select(o => o.Serialize(options.version))
                )
            };
            string serializedData = data.Count > 0 ? JsonConvert.SerializeObject(data) : "{}";

            string uncompressedData = $"{worldSize.x}x{worldSize.y}{serializedData}";
            return $"{options.repr}{StringLib.Compress(uncompressedData, options.compression)}";
        }
        else if (options.version == 2) {
            List<byte> bytes = new();
            var data = new InternalWorldSaveDataV2
            {
                obstacles = obstacles.Select(o => o.SerializeToBytes()).ToArray()
            };
            byte[] serializedData = MessagePackSerializer.Serialize(data);

            bytes.AddRange(BinaryUtils.EncodeLEB128((uint)worldSize.x, (uint)worldSize.y));
            bytes.AddRange(serializedData);
            return $"{options.repr}{StringLib.Compress(Convert.ToBase64String(bytes.ToArray()), options.compression)}";
        }
        throw new NotImplementedException($"Unable to serialize data with schema version {options.version}.");
    }

    public override string ToString()
    {
        return $"World Data: size={worldSize}, obstacles={string.Join(OBSTACLE_SEPARATOR, obstacles)}";
    }
}