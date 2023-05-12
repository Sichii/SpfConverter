﻿using CommandLine;

namespace SpfConverter;

public class Options
{
    [Option(
        't',
        "tospf",
        Required = false,
        HelpText = "Converts the input to an SPF image")]
    public bool ToSpf { get; set; }

    [Option(
        'f',
        "fromspf",
        Required = false,
        HelpText = "Converts the input SPF image to whatever format is specified by the output (and is supported by Magick.NET)")]
    public bool FromSpf { get; set; }

    [Option(
        'i',
        "input",
        Required = true,
        HelpText =
            "The input path. To convert multiple input files to a multi-frame SPF, specify a directory containing multiple input files")]
    public string Input { get; set; } = null!;

    [Option(
        'o',
        "output",
        Required = true,
        HelpText = "The output path. To convert a multi-frame SPF to multiple images, specify a directory")]
    public string Output { get; set; } = null!;
}