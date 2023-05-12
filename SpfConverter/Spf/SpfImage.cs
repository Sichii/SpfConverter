using System.Text;
using ImageMagick;
using SpfConverter.Memory;

namespace SpfConverter.Spf;

/// <summary>
///     Represents an SPF image
/// </summary>
public sealed class SpfImage
{
    /// <summary>
    /// The image header
    /// </summary>
    public SpfHeader Header { get; init; }
    
    /// <summary>
    /// The color palette for the image
    /// </summary>
    public SpfPalette Palette { get; init; }
    
    /// <summary>
    /// The frames of the image
    /// </summary>
    public ICollection<SpfFrame> Frames { get; init; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpfImage"/> class
    /// </summary>
    public SpfImage(SpfHeader header, SpfPalette palette, ICollection<SpfFrame> frames)
    {
        Header = header;
        Palette = palette;
        Frames = frames;
    }
    
    /// <summary>
    ///     Creates a new <see cref="SpfImage"/> from a <see cref="MagickImageCollection"/>
    /// </summary>
    public static SpfImage FromMagickImageCollection(MagickImageCollection collection)
    {
        var frames = new List<SpfFrame>();
        var header = new SpfHeader
        {
            Unknown2 = 1
        };
        
        //reduce colors to 256, no dithering
        collection.Quantize(
            new QuantizeSettings
            {
                Colors = 256,
                ColorSpace = ColorSpace.sRGB,
                DitherMethod = DitherMethod.No,
            });
        
        //create a mosaic of all images in the collection
        using var mosaic = collection.Mosaic();
        
        //get colors of the mosaic
        var palette = Enumerable.Range(0, mosaic.TotalColors)
                                .Select(i => mosaic.GetColormapColor(i))
                                .ToList();

        //map each image to the palette
        foreach (var image in collection)
        {
            //map the colors of each image to the palette
            image.Map(palette!);
            frames.Add(SpfFrame.FromMagickImage((MagickImage)image));
        }

        //create palette from colors
        var spfPalette = new SpfPalette(palette!);

        return new SpfImage(header, spfPalette, frames);
    }
    
    /// <summary>
    /// Write this SPF image to a file
    /// </summary>
    public void WriteSpf(string outputPath)
    {
        var writer = new SpanWriter(Encoding.Default, 1024, endianness: Endianness.LittleEndian);
        
        //write image header
        Header.Write(ref writer);
        //write rgb565 and argb1555 palette values
        Palette.Write(ref writer);
        //write frame count
        writer.WriteUInt32((uint)Frames.Count);

        var index = 0u;

        //write frame headers and set their starting addresses
        foreach (var frame in Frames)
        {
            var header = frame.Header;
            header.StartAddress = index;
            index += header.ByteCount;

            header.Write(ref writer);
        }
        
        //write total byte count
        writer.WriteUInt32(index);
        
        //write raw frame data
        foreach (var frame in Frames)
            frame.Write(ref writer);
        
        if(!outputPath.EndsWith(".spf", StringComparison.OrdinalIgnoreCase))
            outputPath += ".spf";

        File.WriteAllBytes(outputPath, writer.ToSpan().ToArray());
    }

    /// <summary>
    /// Writes this SPF image to the given output path, in the given format
    /// </summary>
    /// <exception cref="InvalidOperationException">Output path is not a directory and does not have an extension</exception>
    /// <exception cref="InvalidOperationException">Output path must be a directory when writing multiple frames</exception>
    public void WriteImg(string outputPath)
    {
        switch (Frames.Count)
        {
            //if there's only 1 frame
            case 1:
            {
                //if the output path is a directory, write to frame1.png
                if (Directory.Exists(outputPath))
                    outputPath = Path.Combine(outputPath, "frame1.png");
                //if the output path is a file, write to that file, but make sure it has an extension
                else if (!Path.HasExtension(outputPath))
                    throw new InvalidOperationException("Output path is not a directory and does not have an extension");

                //conver this SPF image to a MagickImageCollection, and write the first frame to the output path
                using var collection = ConvertToMagickImageCollection();

                if (outputPath.EndsWith(".png", StringComparison.Ordinal))
                    foreach (var image in collection)
                    {
                        image.ColormapSize = 256;
                        image.ColorSpace = ColorSpace.sRGB;
                    }

                collection[0].Write(outputPath);

                break;
            }
            default:
            {
                //if the output path is not a directory, throw an exception
                if (!Directory.Exists(outputPath))
                    throw new InvalidOperationException("Output path must be a directory when writing multiple frames");

                //convert this SPF image to a MagickImageCollection
                using var collection = ConvertToMagickImageCollection();
                var index = 1;

                foreach (var image in collection)
                {
                    image.ColormapSize = 256;
                    image.ColorSpace = ColorSpace.sRGB;
                }

                //write each frame, increasing number with each frame
                foreach (var image in collection)
                    image.Write(Path.Combine(outputPath, $"frame{index++}.png"));
                
                break;
            }
        }
    }

    /// <summary>
    /// Converts this SPF image to a MagickImageCollection
    /// </summary>
    private MagickImageCollection ConvertToMagickImageCollection()
    {
        var collection = new MagickImageCollection();

        foreach (var frame in Frames)
        {
            var image = frame.ToImage(Palette);
            
            collection.Add(image);
        }

        return collection;
    }

    /// <summary>
    /// Reads an SPF image from the given input path
    /// </summary>
    /// <exception cref="InvalidOperationException">File is not an spf file (make sure it has the .spf extension)"</exception>
    public static SpfImage Read(string inputPath)
    {
        //ensure we're reading an spf
        if (!inputPath.EndsWith(".spf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File is not an spf file (make sure it has the .spf extension)");

        //read the file into a buffer
        var buffer = File.ReadAllBytes(inputPath).AsSpan();
        var reader = new SpanReader(Encoding.Default, buffer, Endianness.LittleEndian);
        
        //read the header and palette
        var header = SpfHeader.Read(ref reader);
        var palette = SpfPalette.Read(ref reader);
        var frameCount = reader.ReadUInt32();
        var frames = new List<SpfFrame>();
        var frameHeaders = new List<SpfFrameHeader>();

        //read each frame header
        for (var i = 0; i < frameCount; i++)
        {
            var frameHeader = SpfFrameHeader.Read(ref reader);
            frameHeaders.Add(frameHeader);
        }

        //not sure if we actually need this
        // ReSharper disable once UnusedVariable
        var totalBytecount = reader.ReadUInt32();

        //using the frame headers to determine each frame's size, read each frame
        foreach (var frameHeader in frameHeaders)
        {
            var frame = SpfFrame.Read(frameHeader, ref reader);
            frames.Add(frame);
        }

        return new SpfImage(header, palette, frames);
    }
}