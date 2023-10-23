using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;
using System.Diagnostics;
using System.Reflection;

MSBuildLocator.RegisterDefaults();

Startup();

static void Startup()
{

    var solutionDir = Path.GetFullPath(AppContext.BaseDirectory + @"..\..\..\..\");

    CreateProjects(solutionDir);

    var projectGraph = new ProjectGraph(Path.Combine(solutionDir, "A\\A.csproj"));

    var stopwatch = Stopwatch.StartNew();

    Console.WriteLine("Calling InferHotReloadProfile");
    InferHotReloadProfile(projectGraph);

    stopwatch.Stop();
    Console.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds:N0}ms");


    stopwatch.Reset();
    stopwatch.Start();

    Console.WriteLine("Calling InferHotReloadProfileOptimized");
    InferHotReloadProfileOptimized(projectGraph);

    stopwatch.Stop();
    Console.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds:N0}ms");


    Console.WriteLine("Press enter to exit.");
    Console.ReadLine();
}

static void CreateProjects(string solutionDir)
{

    foreach (var chr in Enumerable.Range('A', 26).Select(i => ((char)i)))
    {
        var name = chr.ToString();

        var projDir = Path.Combine(solutionDir, name);
        var projFile = Path.Combine(projDir, name + ".csproj");
        if (!Directory.Exists(projDir))
        {
            Directory.CreateDirectory(projDir);
        }
        if (!File.Exists(projFile))
        {
            Console.WriteLine("Creating " + projFile);

            File.WriteAllText(projFile, GetCsProj(chr));
        }


    }
}

static string GetCsProj(char chr)
{

    if (chr == 'Z')
    {
        return @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>  
  </PropertyGroup>

</Project>
";
    }

    var next = (char)(chr + 1);

    return @$"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>  
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""..\{next}\{next}.csproj"" />
  </ItemGroup>

</Project>
";

}




static void InferHotReloadProfile(ProjectGraph projectGraph)
{
    var queue = new Queue<ProjectGraphNode>(projectGraph.EntryPointNodes);

    ProjectInstance? aspnetCoreProject = null;
    var maxQueueSize = 0;
    var countProcessed = 0;


    while (queue.Count > 0)
    {
        var currentNode = queue.Dequeue();


        var projectCapability = currentNode.ProjectInstance.GetItems("ProjectCapability");

        maxQueueSize = System.Math.Max(maxQueueSize, queue.Count);
        countProcessed++;

        foreach (var item in projectCapability)
        {
            if (item.EvaluatedInclude == "AspNetCore")
            {
                aspnetCoreProject = currentNode.ProjectInstance;
                break;
            }
            else if (item.EvaluatedInclude == "WebAssembly")
            {
                // We saw a previous project that was AspNetCore. This must he a blazor hosted app.
                if (aspnetCoreProject is not null && aspnetCoreProject != currentNode.ProjectInstance)
                {
                    Console.WriteLine($"HotReloadProfile: BlazorHosted. {aspnetCoreProject.FullPath} references BlazorWebAssembly project {currentNode.ProjectInstance.FullPath}.");
                    return;
                }

                Console.WriteLine("HotReloadProfile: BlazorWebAssembly.");
                return;
            }
        }

        foreach (var project in currentNode.ProjectReferences)
        {
            queue.Enqueue(project);
        }

    }
    Console.WriteLine($"HotReloadProfile: Max Queue count was {maxQueueSize:N0} Count Processed was {countProcessed:N0}");





}


static void InferHotReloadProfileOptimized(ProjectGraph projectGraph)
{
    var queue = new Queue<ProjectGraphNode>(projectGraph.EntryPointNodes);

    ProjectInstance? aspnetCoreProject = null;
    var maxQueueSize = 0;
    var countProcessed = 0;

    var visited = new HashSet<ProjectGraphNode>();

    while (queue.Count > 0)
    {
        var currentNode = queue.Dequeue();


        var projectCapability = currentNode.ProjectInstance.GetItems("ProjectCapability");

        maxQueueSize = System.Math.Max(maxQueueSize, queue.Count);
        countProcessed++;

        foreach (var item in projectCapability)
        {
            if (item.EvaluatedInclude == "AspNetCore")
            {
                aspnetCoreProject = currentNode.ProjectInstance;
                break;
            }
            else if (item.EvaluatedInclude == "WebAssembly")
            {
                // We saw a previous project that was AspNetCore. This must he a blazor hosted app.
                if (aspnetCoreProject is not null && aspnetCoreProject != currentNode.ProjectInstance)
                {
                    Console.WriteLine($"HotReloadProfile: BlazorHosted. {aspnetCoreProject.FullPath} references BlazorWebAssembly project {currentNode.ProjectInstance.FullPath}.");
                    return;
                }

                Console.WriteLine("HotReloadProfile: BlazorWebAssembly.");
                return;
            }
        }

        foreach (var project in currentNode.ProjectReferences)
        {
            if (visited.Add(project))
            {
                queue.Enqueue(project);
            }
        }

    }
    Console.WriteLine($"InferHotReloadProfileOptimized: Max Queue count was {maxQueueSize:N0} Count Processed was {countProcessed:N0}");





}