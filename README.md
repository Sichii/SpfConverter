# SpfConverter
Tool to convert images to and from .SPF

# Usage

This is a command line utility

  -t, --tospf      Converts the input to an SPF image

  -f, --fromspf    Converts the input SPF image to whatever format is specified by the output (and is supported by
                   Magick.NET)

  -i, --input      Required. The input path. To convert multiple input files to a multi-frame SPF, specify a directory
                   containing multiple input files

  -o, --output     Required. The output path. To convert a multi-frame SPF to multiple images, specify a directory

  -d, --dither     (Default: None) The dithering algorithm to use when converting to SPF. Valid values are: None,
                   FloydSteinberg, Riemersma

  --help           Display this help screen.

  --version        Display version information.

Press any key to exit

# Credits

Most of the work was done by [FallenDev](https://github.com/FallenDev), I just made it pretty
