using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public static class BinaryUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<byte> EncodeLEB128(params uint[] values)
    {
        return EncodeLEB128(values.AsEnumerable());
    }
    public static IEnumerable<byte> EncodeLEB128(IEnumerable<uint> values)
    {
        foreach (var value in values)
        {
            foreach (byte b in EncodeLEB128(value))
            {
                yield return b;
            }
        }
    }
    public static IEnumerable<byte> EncodeLEB128(uint value)
    {
        do
        {
            byte currentByte = (byte)(value & 0x7F); // Extract 7 bits
            value >>= 7; // Shift right by 7 bits

            if (value != 0)
            {
                currentByte |= 0x80; // Set the continuation bit
            }

            yield return currentByte;
        } while (value != 0);
    }

    public static IEnumerable<uint> DecodeLEB128(IEnumerable<byte> bytes, int maxLength = -1)
    {
        uint current = 0;
        int shift = 0;
        int count = 0;

        foreach (var b in bytes)
        {
            if (maxLength >= 0 && count >= maxLength) { yield break; }

            current |= (uint)(b & 0x7F) << shift; // Add 7 bits to the current value
            if ((b & 0x80) == 0) // Check if it's the last byte
            {
                yield return current;
                count++;
                current = 0;
                shift = 0;
            }
            else
            {
                shift += 7;
            }
        }

        if (shift > 0)
        {
            throw new System.FormatException("Incomplete LEB128 sequence.");
        }
    }

    internal static int GetLEB128Size(uint value)
    {
        int size = 0;
        do
        {
            value >>= 7;
            size++;
        } while (value > 0);
        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetLEB128Size(IEnumerable<uint> values)
    {
        int size = 0;
        foreach (var value in values)
        {
            size += GetLEB128Size(value);
        }
        return size;
    }
}