using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using UnityEngine;

public readonly struct ObstacleSaveData
{
    public readonly int cellId;
    public readonly int obstacleId;
    public readonly int colorId;
    public readonly CardinalDirection facingDirection;
    public readonly Obstacle.MoveType moveType;
    public readonly Dictionary<string, object> additionalData;

    public static readonly Regex regex = new(@"^([0-9]+)([XFL])([0-9]+)([NSEW])([0-9]+)(\{.*\})?$");

    public ObstacleSaveData(int obstacleId, Obstacle.MoveType moveType, int cellId, CardinalDirection facingDirection, int colorId)
    {
        this.obstacleId = obstacleId;
        this.moveType = moveType;
        this.cellId = cellId;
        this.facingDirection = facingDirection;
        this.colorId = colorId;
        additionalData = new();
    }

    public string Serialize(int version)
    {
        if (version == 1)
        {
            string serializedAddData = additionalData.Count > 0 ? JsonConvert.SerializeObject(additionalData) : "";
            return $"{obstacleId}{GetStringFromMoveType(moveType)}{cellId}{GetStringFromDirection(facingDirection)}{colorId}{serializedAddData}";
        }
        else if (version == 2)
        {
            byte[] bytes = SerializeToBytes();
            return Convert.ToBase64String(bytes);
        }

        throw new NotImplementedException($"Obstacle deserialization v{version} is not implemented");
    }
    public byte[] SerializeToBytes()
    {
        List<byte> bytes = new();
        byte facingBits = (byte)((byte)facingDirection & 0b11); // we'll assume this has at most length 2 (0-3)
        byte moveBits = (byte)((byte)moveType & 0b11); // we'll assume this has at most length 2 (0-3)
        byte colorBits = (byte)((byte)colorId & 0b1111); // and lastly, we'll assume the color byte has at most length 4 (0 - 15)
        byte controlByte = (byte)(moveBits | (facingBits << 2) | (colorBits << 4));
        bytes.Add(controlByte);
        bytes.AddRange(BinaryUtils.EncodeLEB128((uint)obstacleId, (uint)cellId));

        if (additionalData.Count > 0)
        {
            byte[] serializedAdditionalData = MessagePackSerializer.Serialize(additionalData);
            bytes.AddRange(serializedAdditionalData);
        }

        return bytes.ToArray();
    }

    public static ObstacleSaveData Deserialize(string rawData, int version)
    {
        if (version == 1)
        {
            if (string.IsNullOrEmpty(rawData))
                throw new ArgumentException("Input data cannot be null or empty.");

            Match match = regex.Match(rawData);
            if (!match.Success)
                throw new FormatException("Input data does not match the expected format.");

            var result = new ObstacleSaveData(
                int.Parse(match.Groups[1].Value),
                GetMoveTypeFromString(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                GetDirectionFromString(match.Groups[4].Value),
                int.Parse(match.Groups[5].Value)
            );

            if (match.Groups[6].Success)
            {
                try
                {
                    foreach (var kvp in JsonConvert.DeserializeObject<Dictionary<string, JToken>>(match.Groups[6].Value))
                    {
                        result.additionalData.Add(kvp.Key, kvp.Value);
                    }
                }
                catch (Exception e)
                {
                    throw new FormatException($"Failed to extract JSON payload: {e}");
                }
            }

            return result;
        }
        else if (version == 2)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(rawData);
            return Deserialize(bytes);
        }

        throw new NotImplementedException($"Obstacle deserialization v{version} is not implemented");
    }
    public static ObstacleSaveData Deserialize(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            throw new ArgumentException("Input data cannot be null or empty.");

        // Extract the control byte
        byte controlByte = bytes[0];
        byte moveBits = (byte)(controlByte & 0b11);             // Last 2 bits
        byte facingBits = (byte)((controlByte >> 2) & 0b11);    // Next 2 bits
        byte colorBits = (byte)((controlByte >> 4) & 0b1111);   // First 4 bits

        // Decode the move type, facing direction, and colorId from the control byte
        var moveType = (Obstacle.MoveType)moveBits;
        var facingDirection = (CardinalDirection)facingBits;
        int colorId = colorBits;

        // Decode obstacleId and cellId using LEB128
        var leb128Bytes = bytes.Skip(1); // Skip the control byte
        var decodedValues = BinaryUtils.DecodeLEB128(leb128Bytes, 2).ToArray();

        if (decodedValues.Length < 2)
            throw new FormatException("Not enough LEB128 data to decode obstacleId and cellId.");

        int obstacleId = (int)decodedValues[0];
        int cellId = (int)decodedValues[1];

        // Create and return the ObstacleSaveData object
        ObstacleSaveData result = new(obstacleId, moveType, cellId, facingDirection, colorId);

        // Calculate the start index of the additional data
        int leb128Size = BinaryUtils.GetLEB128Size(decodedValues[0]) + BinaryUtils.GetLEB128Size(decodedValues[1]);
        int additionalDataIndex = 1 + leb128Size; // 1 for the control byte

        // Extract and decode the additional data, if present
        if (additionalDataIndex < bytes.Length)
        {
            byte[] additionalDataBytes = bytes.Skip(additionalDataIndex).ToArray();
            var deserializedData = MessagePackSerializer.Deserialize<Dictionary<string, object>>(additionalDataBytes);
            foreach (var kvp in deserializedData)
            {
                result.additionalData.Add(kvp.Key, kvp.Value);
            }
        }

        return result;
    }

    public override string ToString()
    {
        return this.Serialize(1);
    }

    public static CardinalDirection GetDirectionFromString(string directionString)
    {
        return directionString switch
        {
            "N" => CardinalDirection.North,
            "S" => CardinalDirection.South,
            "E" => CardinalDirection.East,
            "W" => CardinalDirection.West,
            _ => throw new FormatException($"Invalid direction string: \"{directionString}\"")
        };
    }
    public static string GetStringFromDirection(CardinalDirection direction)
    {
        return direction switch
        {
            CardinalDirection.North => "N",
            CardinalDirection.South => "S",
            CardinalDirection.East => "E",
            CardinalDirection.West => "W",
            _ => throw new ArgumentException($"Invalid direction value: \"{direction}\"")
        };
    }

    public static Obstacle.MoveType GetMoveTypeFromString(string moveTypeString)
    {
        return moveTypeString switch
        {
            "X" => Obstacle.MoveType.Fixed,
            "F" => Obstacle.MoveType.Free,
            "L" => Obstacle.MoveType.Live,
            _ => throw new FormatException($"Invalid move type string: \"{moveTypeString}\"")
        };
    }
    public static string GetStringFromMoveType(Obstacle.MoveType moveType)
    {
        return moveType switch
        {
            Obstacle.MoveType.Fixed => "X",
            Obstacle.MoveType.Free => "F",
            Obstacle.MoveType.Live => "L",
            _ => throw new ArgumentException($"Invalid move type value: \"{moveType}\"")
        };
    }
}