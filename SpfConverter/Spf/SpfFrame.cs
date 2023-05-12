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
        var pixels = image.GetPixels().ToList();
        var frameData = new byte[pixels.Count];
        var frameDataChannel = pixels.First().Channels - 1;

        //the palette indexes are stored in the alpha channel of each pixel for some reason
        for (var i = 0; i < pixels.Count; i++)
        {
            var pixel = pixels[i];
            var index = pixel.GetChannel(frameDataChannel);

            if (index > 256)
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
        foreach (var set in pixels.Select((p, i) => new { Pixel = p, Index = i }))
        {
            var pixelIndex = set.Index;
            
            //get the palette index from the frame data
            var paletteIndex = Data[pixelIndex];
            
            //look up the color from the palette
            var color = palette.Colors565.ElementAt(paletteIndex);

            //set the pixel color
            set.Pixel.SetValues(new[] { color.R, color.G, color.B, ushort.MaxValue });
        }

        return image;
    }
}