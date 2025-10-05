using BigTextSearcher.Commands;
using Spectre.Console.Cli;

namespace BigTextSearcher;

internal static class Program
{
    private static int Main(string[] args)
    {
        var app = new CommandApp<SearchCommand>();
        
        app.Configure(config =>
        {
            config.SetApplicationName("bigtextsearcher");
            config.ValidateExamples();
            
            config.AddExample("input.log.gz", "output.txt", "-k", "chrome,edge,firefox");
            config.AddExample("input.log.gz", "output.txt", "-k", "error,warning", "--case-sensitive");
        });

        return app.Run(args);
    }
}