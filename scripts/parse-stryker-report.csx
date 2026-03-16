// Usage:
//   dotnet dotnet-script ./scripts/parse-stryker-report.csx -- <report.html> [file-filter]
//
// Parses a Stryker.NET HTML report and extracts survived/uncovered mutants.
// The report HTML contains an embedded JSON object (app.report = {...}) with
// mutation results for every source file in the project.
//
// The optional [file-filter] argument does a substring match on the file name,
// showing only matching files. Without it, all files are printed.
//
// Examples:
//   # Show all files
//   dotnet dotnet-script ./scripts/parse-stryker-report.csx -- test/.../mutation-report.html
//
//   # Filter to a specific class
//   dotnet dotnet-script ./scripts/parse-stryker-report.csx -- test/.../mutation-report.html AdminDashboardMiddleware

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

if (Args.Count < 1)
{
    Console.WriteLine("Usage: dotnet dotnet-script ./scripts/parse-stryker-report.csx -- <report.html> [file-filter]");
    Environment.Exit(1);
}

var htmlPath = Args[0];
var fileFilter = Args.Count > 1 ? Args[1] : null;

var content = File.ReadAllText(htmlPath);

var marker = "app.report = {";
var idx = content.IndexOf(marker);
if (idx == -1)
{
    Console.WriteLine("ERROR: Could not find 'app.report' JSON in HTML file.");
    Environment.Exit(1);
}

var jsonStart = idx + "app.report = ".Length;

// Find the matching closing brace using a depth counter
var depth = 0;
var inString = false;
var escapeNext = false;
var endIdx = jsonStart;
for (var i = jsonStart; i < content.Length; i++)
{
    var c = content[i];
    if (escapeNext)
    {
        escapeNext = false;
        continue;
    }
    if (c == '\\' && inString)
    {
        escapeNext = true;
        continue;
    }
    if (c == '"' && !escapeNext)
    {
        inString = !inString;
        continue;
    }
    if (inString) continue;
    if (c == '{')
    {
        depth++;
    }
    else if (c == '}')
    {
        depth--;
        if (depth == 0)
        {
            endIdx = i + 1;
            break;
        }
    }
}

var jsonStr = content[jsonStart..endIdx];
var data = JsonSerializer.Deserialize<JsonElement>(jsonStr);

if (!data.TryGetProperty("files", out var files))
{
    Console.WriteLine("ERROR: No 'files' property found in report JSON.");
    Environment.Exit(1);
}

foreach (var file in files.EnumerateObject())
{
    var fname = file.Name;
    if (fileFilter != null && !fname.Contains(fileFilter))
        continue;

    var mutants = file.Value.TryGetProperty("mutants", out var mutantsEl)
        ? mutantsEl.EnumerateArray().ToList()
        : new List<JsonElement>();

    var survived = new List<JsonElement>();
    var noCoverage = new List<JsonElement>();
    var killedCount = 0;

    foreach (var m in mutants)
    {
        var status = m.GetProperty("status").GetString();
        if (status == "Survived") survived.Add(m);
        else if (status == "NoCoverage") noCoverage.Add(m);
        else if (status == "Killed") killedCount++;
    }

    var total = killedCount + survived.Count + noCoverage.Count;

    var shortName = fname.Contains('/') ? fname[(fname.LastIndexOf('/') + 1)..] : fname;
    Console.WriteLine($"File: {shortName}");
    Console.WriteLine($"  Total testable: {total}, Killed: {killedCount}, Survived: {survived.Count}, NoCoverage: {noCoverage.Count}");
    if (total > 0)
        Console.WriteLine($"  Score: {(double)killedCount * 100 / total:F1}%");
    Console.WriteLine();

    foreach (var m in survived)
    {
        var loc = m.GetProperty("location");
        var start = loc.GetProperty("start");
        var end = loc.GetProperty("end");
        var id = m.GetProperty("id").ToString();
        var mutatorName = m.GetProperty("mutatorName").GetString();
        var replacement = m.TryGetProperty("replacement", out var rep) ? rep.GetString() : "N/A";

        Console.WriteLine($"  [SURVIVED] id={id}  {mutatorName}");
        Console.WriteLine($"    Line {start.GetProperty("line")}:{start.GetProperty("column")} - {end.GetProperty("line")}:{end.GetProperty("column")}");
        Console.WriteLine($"    Replacement: {replacement}");
        Console.WriteLine();
    }

    foreach (var m in noCoverage)
    {
        var loc = m.GetProperty("location");
        var start = loc.GetProperty("start");
        var end = loc.GetProperty("end");
        var id = m.GetProperty("id").ToString();
        var mutatorName = m.GetProperty("mutatorName").GetString();
        var replacement = m.TryGetProperty("replacement", out var rep) ? rep.GetString() : "N/A";

        Console.WriteLine($"  [NO COVERAGE] id={id}  {mutatorName}");
        Console.WriteLine($"    Line {start.GetProperty("line")}:{start.GetProperty("column")} - {end.GetProperty("line")}:{end.GetProperty("column")}");
        Console.WriteLine($"    Replacement: {replacement}");
        Console.WriteLine();
    }
}
