﻿using SpfConverter.Memory;

namespace SpfConverter.Spf;

/// <summary>
/// Represents the header of a single frame in an SPF image
/// </summary>
public sealed class SpfFrameHeader
{
    public ushort PadWidth { get; init; }
    public ushort PadHeight { get; init; }
    public ushort PixelWidth { get; init; }
    public ushort PixelHeight { get; init; }
    public static uint Unknown => 0xCCCCCCCC; // Every SPF has this value associated with it
    public uint Reserved { get; init; }
    public uint StartAddress { get; set; }
    public uint ByteWidth { get; init; }
    public uint ByteCount { get; init; }
    public uint SemiByteCount { get; init; }

    /// <summary>
    /// Writes the frame header to a buffer
    /// </summary>
    public void Write(ref SpanWriter writer)
    {
        writer.WriteUInt16(PadWidth);
        writer.WriteUInt16(PadHeight);
        writer.WriteUInt16(PixelWidth);
        writer.WriteUInt16(PixelHeight);
        writer.WriteUInt32(Unknown);
        writer.WriteUInt32(Reserved);
        writer.WriteUInt32(StartAddress);
        writer.WriteUInt32(ByteWidth);
        writer.WriteUInt32(ByteCount);
        writer.WriteUInt32(SemiByteCount);
    }

    /// <summary>
    /// Reads a frame header from the buffer
    /// </summary>
    public static SpfFrameHeader Read(ref SpanReader reader)
    {
        var padWidth = reader.ReadUInt16();
        var padHeight = reader.ReadUInt16();
        var pixelWidth = reader.ReadUInt16();
        var pixelHeight = reader.ReadUInt16();
        var _ = reader.ReadUInt32(); //unknown
        var reserved = reader.ReadUInt32();
        var startAddress = reader.ReadUInt32();
        var byteWidth = reader.ReadUInt32();
        var byteCount = reader.ReadUInt32();
        var semiByteCount = reader.ReadUInt32();
        
        return new SpfFrameHeader
        {
            PadWidth = padWidth,
            PadHeight = padHeight,
            PixelWidth = pixelWidth,
            PixelHeight = pixelHeight,
            Reserved = reserved,
            StartAddress = startAddress,
            ByteWidth = byteWidth,
            ByteCount = byteCount,
            SemiByteCount = semiByteCount
        };
    }
}