using System.ComponentModel;
using Spectre.Console.Cli;

namespace BigTextSearcher.Commands;

public sealed class SearchSettings : CommandSettings
{
    [CommandArgument(0, "<INPUT>")]
    [Description("Input file path (supports .gz compressed files)")]
    public string InputPath { get; init; } = string.Empty;

    [CommandArgument(1, "<OUTPUT>")]
    [Description("Output file path for matching lines")]
    public string OutputPath { get; init; } = string.Empty;

    [CommandOption("-k|--keywords <KEYWORDS>")]
    [Description("Comma-separated list of keywords to search for")]
    [DefaultValue("chrome,edge,firefox")]
    public string Keywords { get; init; } = "chrome,edge,firefox";

    [CommandOption("-c|--case-sensitive")]
    [Description("Enable case-sensitive matching (default: case-insensitive)")]
    [DefaultValue(false)]
    public bool CaseSensitive { get; init; }

    [CommandOption("-b|--buffer-size <SIZE>")]
    [Description("Buffer size in MB for file operations")]
    [DefaultValue(4)]
    public int BufferSizeMb { get; init; } = 4;

    public int BufferSizeBytes => BufferSizeMb * 1024 * 1024;

    public string[] GetKeywordArray() => 
        Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}