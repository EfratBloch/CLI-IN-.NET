using System.CommandLine;

var extensions = new Dictionary<string, string>
{
    { "css", ".css" },
    { "cs", ".cs" },
    { "js", ".js" },
    { "python", ".py" },
    { "java", ".java" },
    { "txt",".txt" },
    { "html",".html" },

};
var excludedFolders = new[] { "bin", "obj", "debug", ".git", ".vs" };
var languageOption = new Option<string[]>(new[] { "--language", "-l" }, "language to include");
languageOption.AllowMultipleArgumentsPerToken = true;
languageOption.IsRequired= true;
var removeEmptyLinesOption = new Option<bool>(new[] { "--remove-empty-lines", "-r" }, "Remove empty lines from the source code");
var authorOption = new Option<string>(new[] { "--author", "-a" }, "Name of the file creator");
var noteOption = new Option<bool>(new[] { "--note", "-n" }, "Write the source file path as a comment");
var bundleOption = new Option<FileInfo>(new[] { "--output","-o" },"File path and name");
bundleOption.IsRequired = true;
var rspCommand = new Command("create-rsp", "Create a response file with a ready bundle command");
rspCommand.SetHandler(() =>
{
    Console.WriteLine("Enter output file path:");
    var output = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(output))
    {
        Console.WriteLine("Error: output path is required");
        return;
    }
    if (Directory.Exists(output))
    {
        Console.WriteLine("Error: output must be a file path, not a folder");
        return;
    }
    Console.WriteLine("Do you want to include the source path as a comment? (y/n):");
    var note = Console.ReadLine();
    if (note?.ToLower() != "y" && note?.ToLower() != "n")
    {
        Console.WriteLine("Error: please answer y or n");
        return;
    }
    Console.WriteLine("Enter language you want:");
    var languages = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(languages))
    {
        Console.WriteLine("Error: language is required");
        return;
    }
    var languageList = languages.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    foreach (var lang in languageList)
    {
        if (lang.ToLower() != "all" && !extensions.ContainsKey(lang.ToLower()))
        {
            Console.WriteLine("Error: language '" + lang + "' is not supported");
            return;
        }
    }
    Console.WriteLine("Enter sort:");
    var sort = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(sort))
    {
        sort = "name";
    }

    if (sort.ToLower() != "name" && sort.ToLower() != "type")
    {
        Console.WriteLine("Error: sort must be 'name' or 'type'");
        return;
    }
    Console.WriteLine("Do you want to remove empty lines? (y/n):");
    var removeemptylines = Console.ReadLine();
    if (removeemptylines?.ToLower() != "y" && removeemptylines?.ToLower() != "n")
    {
        Console.WriteLine("Error: please answer y or n");
        return;
    }
    Console.WriteLine("Enter author?:");
    var author = Console.ReadLine();
    author = author?.Trim();
    if (!string.IsNullOrEmpty(author) && author.Contains('"'))
    {
        Console.WriteLine("Error: author name cannot contain quotation marks");
        return;
    }
    var rspLines = new List<string>();
    rspLines.Add("bundle");
    rspLines.Add("--output");
    rspLines.Add("\"" + output + "\"");
    rspLines.Add("--language");
    foreach (var lang in languageList)
    {
        rspLines.Add(lang);
    }
    if (note.ToLower() == "y")
    {
        rspLines.Add("--note");
    }
    rspLines.Add("--sort");
    rspLines.Add(sort.ToLower());
    if (removeemptylines.ToLower() == "y")
    {
        rspLines.Add("--remove-empty-lines");
    }
    if (!string.IsNullOrEmpty(author))
    {
        rspLines.Add("--author");
        rspLines.Add("\"" + author + "\"");
    }
    File.WriteAllLines("bundle.rsp", rspLines);
    Console.WriteLine("Response file created: bundle.rsp");
});
var bundleCommand = new Command("bundle", "Bundle code files to a single file");
var sortOption = new Option<string>(new[] { "--sort", "-s" }, () => "name", "Sort by name or type");
sortOption.FromAmong("name", "type");
bundleCommand.AddOption(noteOption);
bundleCommand.AddOption(authorOption);
bundleCommand.AddOption(removeEmptyLinesOption);
bundleCommand.AddOption(sortOption);
bundleCommand.AddOption(bundleOption);
bundleCommand.AddOption(languageOption);
bundleCommand.SetHandler((output, languages,note,sort, removeEmptyLines,author) =>
{
    try
    {   
        foreach (var lang in languages)
        {
            if(lang.ToLower()!="all"&&!extensions.ContainsKey(lang.ToLower()))
            {
                Console.WriteLine($"Error: language '{lang}' is not supported");
                return;
            }
        }
        var wantedExtensions = languages.Any(l => l.ToLower() == "all")
        ? extensions.Values.ToList()
        : languages.Select(l => extensions[l.ToLower()]).ToList();
        var currentDir=Directory.GetCurrentDirectory();
        if (Directory.Exists(output.FullName))
        {
            Console.WriteLine("Error: output must be a file path, not a folder");
            return;
        }
        var files = Directory.GetFiles(currentDir, "*", SearchOption.AllDirectories)
        .Where(file =>
        {
            var relative = Path.GetRelativePath(currentDir, file);
            var parts = relative.Split(Path.DirectorySeparatorChar);
            return !parts.Take(parts.Length - 1)
             .Any(p => excludedFolders.Contains(p.ToLower()));
        })
        .Where(file => wantedExtensions.Contains(Path.GetExtension(file).ToLower()))
         .ToList().Where(file => Path.GetFullPath(file) != Path.GetFullPath(output.FullName)).ToList();
        if (files.Count == 0)
        {
            Console.WriteLine("No matching code files were found");
            return;
        }
        using var writer = new StreamWriter(output.FullName);
        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine("// Author: " + author);
        }
        if (sort == "type")
        {
            files = files.OrderBy(f => Path.GetExtension(f)).ThenBy(f => Path.GetFileName(f)).ToList();
        }
        else
        {
            files = files.OrderBy(f => Path.GetFileName(f)).ToList();
        }
        foreach (var file in files)
        {
            if (note)
            {
                writer.WriteLine("// Name: " + Path.GetFileName(file) + " | Source: " + Path.GetRelativePath(Directory.GetCurrentDirectory(), file));
            }
            var lines = File.ReadAllLines(file);

            if (removeEmptyLines)
            {
                lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            }

            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }
        }
        Console.WriteLine("File was created");
    }
    catch(DirectoryNotFoundException)
    {
        Console.WriteLine("Error: File path is invalid");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error: " + ex.Message);
    }


},bundleOption,languageOption,noteOption,sortOption, removeEmptyLinesOption,authorOption);

var rootCommand = new RootCommand("Root command for File Bundler CLI");
rootCommand.AddCommand(bundleCommand);
rootCommand.AddCommand(rspCommand);
await rootCommand.InvokeAsync(args);

