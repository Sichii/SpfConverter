using SpfConverter.Memory;

namespace SpfConverter.Spf;

/// <summary>
///     Represents the header of an SPF image
/// </summary>
/// <remarks>This class likely controls which part of the palette is used,
/// but since we don't really utilize these values as intended,
/// and always set Unknonwn2 to 1,
/// it seems the RGB565 palette is always used this way.
/// This works in our favor because RGB565 has transparency anyway with true black,
/// and also has high color fidelity due to the extra green bit.
/// However, there's a good probability something in the client uses these values and does not
/// populate the "unused" palette correctly, leading to an image that does not translate correctly</remarks>
/// TODO: if we ever run into an image that doesn't translate correctly, we should investigate this
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