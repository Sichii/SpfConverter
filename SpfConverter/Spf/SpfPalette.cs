using ImageMagick;
using SpfConverter.Memory;
using SpfConverter.Utility;

namespace SpfConverter.Spf;

/// <summary>
///     Represents the color palette of an SPF image
/// </summary>
public sealed class SpfPalette
{
    /// <summary>
    /// The first set of colors in the palette, which are in RGB565 format
    /// </summary>
    public ICollection<IMagickColor<ushort>> Colors565 { get; init; }
    /// <summary>
    /// The second set of colors in the palette, which are in RGB1555 format
    /// </summary>
    public ICollection<IMagickColor<ushort>> Colors1555 { get; init; }
    /// <summary>
    /// The amount of padding in the palette (unused colors)
    /// </summary>
    public int Padding { get; init; }
    private const ushort FIVE_BIT_MASK = 0b11111;
    private const ushort SIX_BIT_MASK = 0b111111;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpfPalette"/> class
    /// </summary>
    /// <param name="colors565">Up to 256 colors</param>
    /// <exception cref="ArgumentException">Palette can only contain 256 colors</exception>
    public SpfPalette(ICollection<IMagickColor<ushort>> colors565)
    {
        if (colors565.Count > 256)
            throw new ArgumentException("Palette can only contain 256 colors");
        
        Colors565 = colors565;
        Padding = 256 - colors565.Count;

        //@formatter:off
        Colors1555 = Enumerable.Range(0, Colors565.Count)
                               .Select(_ => new MagickColor(0, 0, 0, 0))
                               .OfType<IMagickColor<ushort>>()
                               .ToList();
        //@formatter:on
    }
    
    /// <summary>
    /// Writes the palette to a buffer
    /// </summary>
    public void Write(ref SpanWriter writer)
    {
        //write the 565 bytes first
        Write565(ref writer);
        //then the 1555 bytes
        Write1555(ref writer);
    }

    /// <summary>
    /// Reads a palette from a buffer
    /// </summary>
    public static SpfPalette Read(ref SpanReader reader)
    {
        //read the 565 bytes first
        var rgb565 = Read565(ref reader);
        //then the 1555 bytes
        var rgb1555 = Read1555(ref reader);

        return new SpfPalette(rgb565)
        {
            Colors1555 = rgb1555
        };
    }

    private static ICollection<IMagickColor<ushort>> Read565(ref SpanReader reader)
    {
        var colors = new List<IMagickColor<ushort>>();

        //read 256 colors
        for (var i = 0; i < 256; i++)
        {
            //the colors are encded in 16bits (rgb565)
            var color = reader.ReadUInt16();
            //@formatter:off
            //shift and mask to get colors as bytes
            //then normalize colors to true color (16bit)
            var r = MathEx.ScaleRange<int, ushort>(color >> 11, 0, FIVE_BIT_MASK, 0, ushort.MaxValue);
            var g = MathEx.ScaleRange<int, ushort>((color >> 5) & SIX_BIT_MASK, 0, SIX_BIT_MASK, 0, ushort.MaxValue);
            var b = MathEx.ScaleRange<int, ushort>(color & FIVE_BIT_MASK, 0, FIVE_BIT_MASK, 0, ushort.MaxValue);
            //@formatter:on

            var magickColor = new MagickColor(r, g, b);
            colors.Add(magickColor);
        }

        return colors;
    }
    
    private static ICollection<IMagickColor<ushort>> Read1555(ref SpanReader reader)
    {
        var colors = new List<IMagickColor<ushort>>();
        
        //read 256 colors
        for (var i = 0; i < 256; i++)
        {
            //the colors are encded in 16bits (rgba1555)
            var color = reader.ReadUInt16();
            //@formatter:off
            //TODO: do i bother reading the alpha? not sure what use it would be
             //shift and mask to get colors as bytes
            //then normalize colors to true color (16bit)
            var r = MathEx.ScaleRange<int, ushort>((color >> 10) & FIVE_BIT_MASK, 0, FIVE_BIT_MASK, 0, ushort.MaxValue);
            var g = MathEx.ScaleRange<int, ushort>((color >> 5) & FIVE_BIT_MASK, 0, FIVE_BIT_MASK, 0, ushort.MaxValue);
            var b = MathEx.ScaleRange<int, ushort>(color & FIVE_BIT_MASK, 0, FIVE_BIT_MASK, 0, ushort.MaxValue);
            //@formatter:on

            var magickColor = new MagickColor(r, g, b);
            colors.Add(magickColor);
        }

        return colors;
    }
    
    private void Write565(ref SpanWriter writer)
    {
        foreach (var color in Colors565)
        {
            //@formatter:off
            var r = MathEx.ScaleRange(color.R, 0, ushort.MaxValue, 0, 0b11111);
            var g = MathEx.ScaleRange(color.G, 0, ushort.MaxValue, 0, 0b111111);
            var b = MathEx.ScaleRange(color.B, 0, ushort.MaxValue, 0, 0b11111);
            //@formatter:on
            var rgb565 = (ushort)((r << 11) | (g << 5) | b);
            writer.WriteUInt16(rgb565);
        }

        if (Padding > 0)
            writer.WriteBytes(new byte[Padding * 2]);
    }
    
    private void Write1555(ref SpanWriter writer)
    {
        foreach (var color in Colors565)
        {
            //@formatter:off
            var r = MathEx.ScaleRange<ushort, byte>(color.R, 0, ushort.MaxValue, 0, 0b11111);
            var g = MathEx.ScaleRange<ushort, byte>(color.G, 0, ushort.MaxValue, 0, 0b11111);
            var b = MathEx.ScaleRange<ushort, byte>(color.B, 0, ushort.MaxValue, 0, 0b11111);
            //@formatter:on
            
            var rgb1555 = (ushort) ((r << 10) | (g << 5) | b);
            
            //if there is alpha, set alpha bit i guess?
            //if(color.A < ushort.MaxValue)
            //  rgb1555 |= 0x8000;
            
            writer.WriteUInt16(rgb1555);
        }
        
        if (Padding > 0)
            writer.WriteBytes(new byte[Padding * 2]);
    }
}