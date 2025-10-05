using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BigTextSearcher.Commands;

public sealed class SearchCommand : AsyncCommand<SearchSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        try
        {
            ValidateSettings(settings);
            
            var keywords = settings.GetKeywordArray();
            DisplayConfiguration(settings, keywords);

            var stopwatch = Stopwatch.StartNew();
            var matchCount = await ProcessFileAsync(settings, keywords);
            stopwatch.Stop();

            DisplayResults(matchCount, stopwatch.Elapsed, settings.OutputPath);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static void ValidateSettings(SearchSettings settings)
    {
        if (!File.Exists(settings.InputPath))
            throw new FileNotFoundException($"Input file not found: {settings.InputPath}");

        var outputDir = Path.GetDirectoryName(settings.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            throw new DirectoryNotFoundException($"Output directory not found: {outputDir}");

        if (settings.BufferSizeMb < 1)
            throw new ArgumentException("Buffer size must be at least 1 MB", nameof(settings.BufferSizeMb));
    }

    private static void DisplayConfiguration(SearchSettings settings, string[] keywords)
    {
        var inputSize = new FileInfo(settings.InputPath).Length / 1024.0 / 1024.0;
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Input File", settings.InputPath);
        table.AddRow("Input Size", $"{inputSize:N1} MB");
        table.AddRow("Output File", settings.OutputPath);
        table.AddRow("Keywords", string.Join(", ", keywords));
        table.AddRow("Case Sensitive", settings.CaseSensitive ? "Yes" : "No");
        table.AddRow("Buffer Size", $"{settings.BufferSizeMb} MB");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static async Task<long> ProcessFileAsync(SearchSettings settings, string[] keywords)
    {
        return await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Scanning lines[/]");
                task.IsIndeterminate = true;

                var isGzipped = settings.InputPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
                var matchCount = isGzipped
                    ? await ProcessGzipFileAsync(settings, keywords, task)
                    : await ProcessPlainFileAsync(settings, keywords, task);

                task.StopTask();
                return matchCount;
            });
    }

    private static async Task<long> ProcessGzipFileAsync(
        SearchSettings settings,
        string[] keywords,
        ProgressTask progressTask)
    {
        using var fileStream = new FileStream(
            settings.InputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            settings.BufferSizeBytes,
            true);
            
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream, Encoding.UTF8, true, settings.BufferSizeBytes);
        await using var writer = new StreamWriter(
            settings.OutputPath,
            false,
            Encoding.UTF8,
            settings.BufferSizeBytes);

        return await ProcessStreamAsync(reader, writer, keywords, settings.CaseSensitive, progressTask);
    }

    private static async Task<long> ProcessPlainFileAsync(
        SearchSettings settings,
        string[] keywords,
        ProgressTask progressTask)
    {
        using var reader = new StreamReader(
            settings.InputPath,
            Encoding.UTF8,
            true,
            settings.BufferSizeBytes);
            
        await using var writer = new StreamWriter(
            settings.OutputPath,
            false,
            Encoding.UTF8,
            settings.BufferSizeBytes);

        return await ProcessStreamAsync(reader, writer, keywords, settings.CaseSensitive, progressTask);
    }

    private static async Task<long> ProcessStreamAsync(
        StreamReader reader,
        StreamWriter writer,
        string[] keywords,
        bool caseSensitive,
        ProgressTask progressTask)
    {
        long totalLines = 0;
        long matchCount = 0;
        var lastUpdate = DateTime.UtcNow;
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line == null)
                continue;

            totalLines++;

            if (ContainsAnyKeyword(line, keywords, comparison))
            {
                await writer.WriteLineAsync(line);
                matchCount++;
            }

            if (totalLines % 100_000 == 0 || (DateTime.UtcNow - lastUpdate).TotalSeconds > 0.5)
            {
                progressTask.Description = $"[green]Scanned:[/] {totalLines:N0} lines, [yellow]Matched:[/] {matchCount:N0}";
                lastUpdate = DateTime.UtcNow;
            }
        }

        progressTask.Description = $"[green]Completed:[/] {totalLines:N0} lines scanned, [yellow]{matchCount:N0}[/] matches found";
        return matchCount;
    }

    private static bool ContainsAnyKeyword(string input, string[] keywords, StringComparison comparison)
    {
        foreach (var keyword in keywords)
        {
            if (input.Contains(keyword, comparison))
                return true;
        }
        return false;
    }

    private static void DisplayResults(long matchCount, TimeSpan elapsed, string outputPath)
    {
        AnsiConsole.WriteLine();
        
        var panel = new Panel(
            new Markup($"[green]Success![/]\n\n" +
                      $"Matches found: [yellow]{matchCount:N0}[/]\n" +
                      $"Time elapsed: [cyan]{elapsed.TotalSeconds:N2}[/] seconds\n" +
                      $"Output file: [link]{Path.GetFullPath(outputPath)}[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        };

        AnsiConsole.Write(panel);
    }
}
