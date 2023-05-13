using ImageMagick;
using SpfConverter.Memory;

namespace SpfConverter.Spf;

/// <summary>
///     Represents a single frame in an SPF image
/// </summary>
public sealed class SpfFrame
{
    /// <summary>
    ///     The frame header
    /// </summary>
    public required SpfFrameHeader Header { get; set; }
    
    /// <summary>
    ///     The raw frame data. Each byte is a pixel/index to a color in the palette
    /// </summary>
    public required byte[] Data { get; set; }

    /// <summary>
    ///     Creates a new <see cref="SpfFrame"/> from a <see cref="MagickImage"/>
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">Pixel points to an color outside the bounds of the palette</exception>
    public static SpfFrame FromMagickImage(MagickImage image)
    {
        //construct header, but leave out startAddress
        //start address will be added later when the frames are actually written
        var header = new SpfFrameHeader
        {
            PixelWidth = (ushort)image.Width,
            PixelHeight = (ushort)image.Height,
            ByteWidth = (uint)image.Width,
            ByteCount = (uint)(image.Width * image.Height),
        };
        
        //get all pixels in the image
        var frameData = new byte[header.ByteCount];
        var pixels = image.GetPixels();
        var indexChannel = pixels.GetIndex(PixelChannel.Index);

        //the palette indexes are stored in the alpha channel of each pixel for some reason
        for (var i = 0; i < header.ByteCount; i++)
        {
            var index = pixels.GetPixel(i % header.PixelWidth, i / header.PixelWidth).GetChannel(indexChannel);
            
            if (index > 255)
                throw new IndexOutOfRangeException("Pixel points to an color outside the bounds of the palette");

            frameData[i] = (byte)index;
        }
        
        //create frame from header and data
        return new SpfFrame
        {
            Header = header,
            Data = frameData
        };
    }

    /// <summary>
    /// Writes the raw frame data to a buffer
    /// </summary>
    public void Write(ref SpanWriter writer) => writer.WriteBytes(Data);

    /// <summary>
    /// Reads an <see cref="SpfFrame"/> from a buffer
    /// </summary>
    public static SpfFrame Read(SpfFrameHeader header, ref SpanReader reader)
    {
        //read the raw frame data
        var data = reader.ReadBytes((int)header.ByteCount);
        
        return new SpfFrame
        {
            Header = header,
            Data = data
        };
    }

    /// <summary>
    ///     Converts the frame to a <see cref="MagickImage"/> using the given <see cref="SpfPalette"/>
    /// </summary>
    public MagickImage ToImage(SpfPalette palette)
    {
        //create a transparent image with the same dimensions as the frame
        var image = new MagickImage(MagickColors.Transparent, Header.PixelWidth, Header.PixelHeight);
        image.ColorSpace = ColorSpace.sRGB;

        //grab a reference to the image pixels
        var pixels = image.GetPixelsUnsafe();

        //for each pixel
        foreach ((var pixel, var index) in pixels.Select((p, i) => (p, i)))
        {
            //get the palette index from the frame data
            var paletteIndex = Data[index];
            
            //look up the color from the palette
            var color = palette.Colors565.ElementAt(paletteIndex);
            var alpha = ushort.MaxValue;

            //true black is transparent
            if (color is { R: 0, G: 0, B: 0 })
                alpha = 0;
            
            //set the pixel color
            pixel.SetValues(new[] { color.R, color.G, color.B, alpha });
        }

        return image;
    }
}