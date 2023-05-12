// See https://aka.ms/new-console-template for more information

using CommandLine;
using ImageMagick;
using SpfConverter;
using SpfConverter.Spf;

Parser.Default.ParseArguments<Options>(args)
      .WithParsed(
          o =>
          {
              if (!o.FromSpf && !o.ToSpf)
              {
                  Console.WriteLine("Please specify either --tospf (-t), or --fromspf (-f)");

                  return;
              }
              
              var inputPaths = new List<string>();
              
              if(Directory.Exists(o.Input))
                  inputPaths.AddRange(Directory.GetFiles(o.Input));
              else if(File.Exists(o.Input))
                  inputPaths.Add(o.Input);
              else
              {
                  Console.WriteLine("Input path does not exist");

                  return;
              }

              if (o.ToSpf)
              {
                  using var imageCollection = new MagickImageCollection();

                  foreach (var path in inputPaths)
                      imageCollection.Add(path);

                  var spfImage = SpfImage.FromMagickImageCollection(imageCollection);
                  spfImage.WriteSpf(o.Output);
              }
              else if (o.FromSpf)
              {
                  var spfImage = SpfImage.Read(o.Input);
                  spfImage.WriteImg(o.Output);
              }
          });
          
Console.WriteLine("Press any key to exit");
Console.ReadLine();