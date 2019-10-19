using Microsoft.VisualBasic;
using static Bullseye.Targets;
using static Build.Buildary.Shell;
using static Build.Buildary.Log;
using static Build.Buildary.GitVersion;
using static Build.Buildary.Path;
using static Build.Buildary.File;
using static Build.Buildary.Directory;

namespace Build.Common
{
    public class ProjectDefinition
    {
        public string ProjectName { get; set; }
        
        public string SolutionPath { get; set; }
        
        public string WebProjectPath { get; set; }
        
        public string DockerImageName { get; set; }

        public static void Register(Buildary.Runner.RunnerOptions options, ProjectDefinition definition)
        {
            Info($"Project: {definition.ProjectName}");
            Info($"Configuration: {options.Config}");
            
            var gitVersion = GetGitVersion(ExpandPath("./"));
            Info($"Version: {gitVersion.FullVersion}");
            
            var commandBuildArgs = $"--configuration {options.Config}";
            var commandBuildArgsWithVersion = commandBuildArgs;
            if (!string.IsNullOrEmpty(gitVersion.PreReleaseTag))
            {
                commandBuildArgsWithVersion += $" --version-suffix \"{gitVersion.PreReleaseTag}\"";
            }

            definition.SolutionPath = ExpandPath(definition.SolutionPath);
            definition.WebProjectPath = ExpandPath(definition.WebProjectPath);
            
            Target("clean", () =>
            {
                CleanDirectory(ExpandPath("./output"));
                RunShell($"dotnet clean {definition.SolutionPath}");
            });
            
            Target("update-version", () =>
            {
                if (FileExists("./build/version.props"))
                {
                    DeleteFile("./build/version.props");
                }
                
                WriteFile("./build/version.props",
$@"<Project>
    <PropertyGroup>
        <VersionPrefix>{gitVersion.Version}</VersionPrefix>
    </PropertyGroup>
</Project>");
            });

            
            Target("docker-dev", () =>
            {
                Info("Starting the docker environment for development...");
                RunShell("cd dev/docker && docker-compose up -d");
            });
            
            Target("build", () =>
            {
                RunShell($"dotnet build {commandBuildArgsWithVersion} {definition.SolutionPath}");
            });
            
            Target("deploy", () =>
            {
                CleanDirectory(ExpandPath("./output/web"));
                RunShell($"dotnet publish --output {ExpandPath("./output/web")} {commandBuildArgsWithVersion} {definition.WebProjectPath}");
                DeleteFile(ExpandPath("./output/web/appsettings.Development.json"));
                CopyFile(ExpandPath("./docker/prod/Dockerfile"), ExpandPath("./output/Dockerfile"));
            });
            
            Target("docker-build", () =>
            {
                RunShell($"docker build -t {definition.DockerImageName}:{gitVersion.FullVersion} ./output");
            });
            
            Target("default", DependsOn("build"));
            
            Target("ci", DependsOn("clean", "update-version", "deploy", "docker-build"));
        }
    }
}