using System.Text.RegularExpressions;

namespace SatisfactoryLedger.ArchitectureTests;

/// <summary>
/// The layout of the source files themselves. IDE0130 (a namespace follows its folder) only runs in an IDE, not in a
/// command-line build, so it's checked here, where CI runs it.
/// </summary>
public partial class SourceLayoutTests
{
    private static readonly string[] SourceFolders = ["src", "tests", "tools"];

    /// <summary>Files allowed another namespace, and why.</summary>
    private static readonly Dictionary<string, string> Exceptions = new(StringComparer.Ordinal)
    {
        // Aspire's template: in this namespace, builder.AddServiceDefaults() works without a using.
        ["src/SatisfactoryLedger.ServiceDefaults/Extensions.cs"] = "Microsoft.Extensions.Hosting",
    };

    [Fact]
    public void Every_namespace_follows_its_project_and_folder()
    {
        string root = RepositoryRoot();
        List<string> wrong = [];

        foreach (string project in SourceFolders
                     .Select(folder => Path.Combine(root, folder))
                     .Where(Directory.Exists)
                     .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories)))
        {
            string projectFolder = Path.GetDirectoryName(project)!;
            string rootNamespace = Path.GetFileNameWithoutExtension(project);

            foreach (string file in Directory.EnumerateFiles(projectFolder, "*.cs", SearchOption.AllDirectories)
                         .Where(file => !IsBuildOutput(projectFolder, file)))
            {
                Match declared = Namespace().Match(File.ReadAllText(file));
                if (!declared.Success)
                {
                    continue; // Top-level statements (Program.cs) have no namespace.
                }

                string folder = Path.GetRelativePath(projectFolder, Path.GetDirectoryName(file)!);
                string expected = folder == "." ? rootNamespace : $"{rootNamespace}.{folder.Replace(Path.DirectorySeparatorChar, '.')}";
                string relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                if (Exceptions.TryGetValue(relative, out string? allowed))
                {
                    expected = allowed;
                }

                if (declared.Groups["name"].Value != expected)
                {
                    wrong.Add($"{Path.GetRelativePath(root, file)}: namespace {declared.Groups["name"].Value}, expected {expected}");
                }
            }
        }

        Assert.True(wrong.Count == 0, "Namespaces that don't follow their folder:\n" + string.Join("\n", wrong));
    }

    /// <summary>
    /// The projects that may reference the simulation (World.Simulation): the analysis service, which runs it
    /// (ADR-0026); its own tests; and these, which check its rules. The API and the World module's tests read the stored
    /// analysis instead (issue #47).
    /// </summary>
    private static readonly string[] SimulationUsers =
    [
        "SatisfactoryLedger.AnalysisService",
        "SatisfactoryLedger.ArchitectureTests",
        "SatisfactoryLedger.World.Simulation.Tests",
    ];

    [Fact]
    public void Only_the_analysis_service_and_the_simulations_own_tests_reference_the_simulation()
    {
        string root = RepositoryRoot();

        List<string> referencing =
        [
            .. SourceFolders
                .Select(folder => Path.Combine(root, folder))
                .Where(Directory.Exists)
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
                .Where(project => File.ReadAllText(project).Contains("SatisfactoryLedger.World.Simulation.csproj", StringComparison.Ordinal))
                .Select(project => Path.GetFileNameWithoutExtension(project))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(SimulationUsers, referencing);
    }

    private static bool IsBuildOutput(string projectFolder, string file)
    {
        string first = Path.GetRelativePath(projectFolder, file).Split(Path.DirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "SatisfactoryLedger.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("The repository root (SatisfactoryLedger.slnx) wasn't found");
    }

    [GeneratedRegex(@"^namespace\s+(?<name>[\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex Namespace();
}
