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
    public static SpfImage FromMagickImageCollection(MagickImageCollection collection, DitherMethod ditherMethod)
    {
        var frames = new List<SpfFrame>();
        var header = new SpfHeader
        {
            Unknown2 = 1
        };
        
        //get the transparency map for each image
        var alphaMaps = collection
                        .Select(GetTransparencyMap)
                        .ToList();
        
        //reduce colors to 255 using specified dithering
        collection.Quantize(
            new QuantizeSettings
            {
                Colors = 255,
                ColorSpace = ColorSpace.sRGB,
                DitherMethod = ditherMethod,
            });

        //reapply transparency maps
        foreach ((var image, var alphaMap) in collection.Zip(alphaMaps))
            ApplyTransparencyMap(image, alphaMap);

        //create a mosaic of all images in the collection
        using var mosaic = collection.Mosaic();
        
        //get colors of the mosaic
        var colors = GetPalette(mosaic);

        //convert each image to an SpfFrame
        //do not use image.Map(), it seems to mess up the colors
        foreach (var image in collection)
            frames.Add(SpfFrame.FromMagickImage((MagickImage)image));

        //create palette from colors
        var spfPalette = new SpfPalette(colors!);

        return new SpfImage(header, spfPalette, frames);
    }

    private static List<IMagickColor<ushort>>? GetPalette(IMagickImage<ushort> image)
    {
        //if the image doesnt use a palette, return null
        if (image.ColormapSize <= 0)
            return null;
        
        return Enumerable.Range(0, image.ColormapSize)
                         .Select(i => image.GetColormapColor(i)!)
                         .ToList();
    }

    /// <summary>
    ///     The idea is to get every pixel that is either transparent, or points to a transparent color in the palette
    /// </summary>
    private static ICollection<(int X, int Y)> GetTransparencyMap(IMagickImage<ushort> image)
    {
        var palette = GetPalette(image);
        var pixels = image.GetPixels();
        var width = image.Width;
        var height = image.Height;
        var map = new List<(int X, int Y)>();
        var indexChannel = pixels.GetIndex(PixelChannel.Index);
        var alphaChannel = pixels.GetIndex(PixelChannel.Alpha);

        //for each pixel in the image
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                //get the pixel at that point
                var pixel = pixels.GetPixel(x, y);

                //if this image uses a palette
                if ((palette != null) && (indexChannel != -1))
                {
                    //grab the frame data, find the color in the palette, and check if it's transparent
                    var frameData = (byte)pixel.GetChannel(indexChannel);
                    var paletteColor = palette[frameData];
                    
                    if(paletteColor.A == 0)
                        map.Add((x, y));
                } else if ((alphaChannel != -1) && (pixel.GetChannel(alphaChannel) == 0))
                    //if this image doesn't use a palette, check if the pixel is transparent
                    map.Add((x, y));
            }

        return map;
    }

    private static void ApplyTransparencyMap(IMagickImage<ushort> image, ICollection<(int X, int Y)> transparencyMap)
    {
        //get the current colors in the palette, we will need this later
        var beforePalette = GetPalette(image)!;
        
        //increase the color map size by 1
        //for some reason this causes the entire color map to go greyscale
        image.ColormapSize += 1;
        var transparentIndex = (ushort)(image.ColormapSize - 1);

        //set the last color in the color map to transparent
        image.SetColormapColor(
            transparentIndex,
            new MagickColor(
                0,
                0,
                0,
                0));

        //replace all the other colors that were previously in the color map
        for (var i = 0; i < beforePalette.Count; i++)
        {
            var color = beforePalette[i];

            image.SetColormapColor(i, color);
        }
        
        var pixels = image.GetPixels();
        var indexChannel = pixels.GetIndex(PixelChannel.Index);
        
        //for each point in the transparency map
        foreach (var point in transparencyMap)
        {
            //grab that pixel and set it's index to the trasparent color in the palette
            var pixel = pixels.GetPixel(point.X, point.Y);
            pixel.SetChannel(indexChannel, transparentIndex);
        }
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
                //if the output path is not a directory, create a directory where the output path is, then save within
                if (!Directory.Exists(outputPath))
                {
                    var path = outputPath.Split('.');
                    Directory.CreateDirectory(path[0]);
                    outputPath = path[0];
                }

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
            try
            {
                var image = frame.ToImage(Palette);
                collection.Add(image);
            }
            catch
            {
                // Ignored
            }
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