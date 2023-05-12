using SpfConverter.Memory;

namespace SpfConverter.Spf;

/// <summary>
///     Represents the header of an SPF image
/// </summary>
public sealed class SpfHeader
{
    public uint Unknown1 { get; set; }
    public uint Unknown2 { get; set; }
    public uint ColorFormat { get; set; }
    
    /// <summary>
    ///     Writes the header to a buffer
    /// </summary>
    public void Write(ref SpanWriter writer)
    {
        writer.WriteUInt32(Unknown1);
        writer.WriteUInt32(Unknown2);
        writer.WriteUInt32(ColorFormat);
    }

    /// <summary>
    ///     Reads the header from the buffer
    /// </summary>
    public static SpfHeader Read(ref SpanReader reader)
    { 
        var unknown1 = reader.ReadUInt32();
        var unknown2 = reader.ReadUInt32();
        var colorFormat = reader.ReadUInt32();

        return new SpfHeader
        {
            Unknown1 = unknown1,
            Unknown2 = unknown2,
            ColorFormat = colorFormat
        };
    }
}