#tool nuget:?package=GitVersion.CommandLine
#tool nuget:?package=vswhere
#addin nuget:?package=Cake.Figlet
#addin nuget:?package=Cake.Incubator&version=1.7.1
#addin nuget:?package=Cake.Git&version=0.16.1
#addin nuget:?package=Polly

using Polly;

var solutionName = "MvvmCross";
var repoName = "mvvmcross/mvvmcross";
var sln = new FilePath("./" + solutionName + ".sln");
var outputDir = new DirectoryPath("./artifacts");
var nuspecDir = new DirectoryPath("./nuspec");
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var verbosityArg = Argument("verbosity", "Minimal");
var verbosity = Verbosity.Minimal;

var isRunningOnAppVeyor = AppVeyor.IsRunningOnAppVeyor;
GitVersion versionInfo = null;

Setup(context => {
    versionInfo = context.GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
        OutputType = GitVersionOutput.Json
    });

    if (isRunningOnAppVeyor)
    {
        var buildNumber = AppVeyor.Environment.Build.Number;
        AppVeyor.UpdateBuildVersion(versionInfo.InformationalVersion
            + "-" + buildNumber);
    }

    var cakeVersion = typeof(ICakeContext).Assembly.GetName().Version.ToString();

    Information(Figlet(solutionName));
    Information("Building version {0}, ({1}, {2}) using version {3} of Cake.",
        versionInfo.SemVer,
        configuration,
        target,
        cakeVersion);

    Debug("Will push NuGet packages {0}", 
        ShouldPushNugetPackages(versionInfo.BranchName));

    verbosity = (Verbosity) Enum.Parse(typeof(Verbosity), verbosityArg, true);
});

Task("Clean").Does(() =>
{
    CleanDirectories("./**/bin");
    CleanDirectories("./**/obj");
    CleanDirectories(outputDir.FullPath);

    EnsureDirectoryExists(outputDir);
});

FilePath msBuildPath;
Task("ResolveBuildTools")
    .WithCriteria(() => IsRunningOnWindows())
    .Does(() => 
{
    var vsWhereSettings = new VSWhereLatestSettings
    {
        IncludePrerelease = true,
        Requires = "Component.Xamarin"
    };
    
    var vsLatest = VSWhereLatest(vsWhereSettings);
    msBuildPath = (vsLatest == null)
        ? null
        : vsLatest.CombineWithFilePath("./MSBuild/15.0/Bin/MSBuild.exe");

    if (msBuildPath != null)
        Information("Found MSBuild at {0}", msBuildPath.ToString());
});

Task("Restore")
    .IsDependentOn("ResolveBuildTools")
    .Does(() => 
{
    var settings = GetDefaultBuildSettings()
        .WithTarget("Restore");
    MSBuild(sln, settings);
});

Task("PatchBuildProps")
    .Does(() => 
{
    var buildProp = new FilePath("./Directory.build.props");
    XmlPoke(buildProp, "//Project/PropertyGroup/Version", versionInfo.SemVer);
});

Task("Build")
    .IsDependentOn("ResolveBuildTools")
    .IsDependentOn("Clean")
    .IsDependentOn("PatchBuildProps")
    .IsDependentOn("Restore")
    .Does(() =>  {

    var settings = GetDefaultBuildSettings()
        .WithProperty("DebugSymbols", "True")
        .WithProperty("DebugType", "Embedded")
        .WithProperty("Version", versionInfo.SemVer)
        .WithProperty("PackageVersion", versionInfo.SemVer)
        .WithProperty("InformationalVersion", versionInfo.InformationalVersion)
        .WithProperty("NoPackageAnalysis", "True")
        .WithTarget("Build");

    settings.BinaryLogger = new MSBuildBinaryLogSettings 
    {
        Enabled = true,
        //FileName = "mvvmcross.binlog"
    };

    // TODO change back to this when parallel builds are working with Xamarin.Android again
    // MSBuild(sln, settings);

    var buildItems = new string[] 
    {
        "./MvvmCross/MvvmCross.csproj",
        "./MvvmCross/MvvmCross.Platforms.Wpf.csproj",
        "./MvvmCross.Android.Support/Fragment/MvvmCross.Droid.Support.Fragment.csproj",
        "./MvvmCross.Android.Support/Design/MvvmCross.Droid.Support.Design.csproj",
        "./MvvmCross.Android.Support/Core.Utils/MvvmCross.Droid.Support.Core.Utils.csproj",
        "./MvvmCross.Android.Support/Core.UI/MvvmCross.Droid.Support.Core.UI.csproj",
        "./MvvmCross.Android.Support/V7.AppCompat/MvvmCross.Droid.Support.V7.AppCompat.csproj",
        "./MvvmCross.Android.Support/V7.Preference/MvvmCross.Droid.Support.V7.Preference.csproj",
        "./MvvmCross.Android.Support/V7.RecyclerView/MvvmCross.Droid.Support.V7.RecyclerView.csproj",
        "./MvvmCross.Android.Support/V14.Preference/MvvmCross.Droid.Support.V14.Preference.csproj",
        "./MvvmCross.Android.Support/V17.Leanback/MvvmCross.Droid.Support.V17.Leanback.csproj",
        "./MvvmCross.Plugins/Location/MvvmCross.Plugin.Location.csproj",
        "./MvvmCross.Plugins/Location.Fused/MvvmCross.Plugin.Location.Fused.csproj",
        "./MvvmCross.Plugins/PictureChooser/MvvmCross.Plugin.PictureChooser.csproj",
        "./MvvmCross.Plugins/Email/MvvmCross.Plugin.Email.csproj",
        "./MvvmCross.Plugins/Accelerometer/MvvmCross.Plugin.Accelerometer.csproj",
        "./MvvmCross.Plugins/Color/MvvmCross.Plugin.Color.csproj",
        "./MvvmCross.Plugins/FieldBinding/MvvmCross.Plugin.FieldBinding.csproj",
        "./MvvmCross.Plugins/File/MvvmCross.Plugin.File.csproj",
        "./MvvmCross.Plugins/Json/MvvmCross.Plugin.Json.csproj",
        "./MvvmCross.Plugins/JsonLocalization/MvvmCross.Plugin.JsonLocalization.csproj",
        "./MvvmCross.Plugins/Messenger/MvvmCross.Plugin.Messenger.csproj",
        "./MvvmCross.Plugins/MethodBinding/MvvmCross.Plugin.MethodBinding.csproj",
        "./MvvmCross.Plugins/Network/MvvmCross.Plugin.Network.csproj",
        "./MvvmCross.Plugins/PhoneCall/MvvmCross.Plugin.PhoneCall.csproj",
        "./MvvmCross.Plugins/PictureChooser/MvvmCross.Plugin.PictureChooser.csproj",
        "./MvvmCross.Plugins/ResourceLoader/MvvmCross.Plugin.ResourceLoader.csproj",
        "./MvvmCross.Plugins/ResxLocalization/MvvmCross.Plugin.ResxLocalization.csproj",
        "./MvvmCross.Plugins/Share/MvvmCross.Plugin.Share.csproj",
        "./MvvmCross.Plugins/Sidebar/MvvmCross.Plugin.Sidebar.csproj",
        "./MvvmCross.Plugins/Visibility/MvvmCross.Plugin.Visibility.csproj",
        "./MvvmCross.Plugins/WebBrowser/MvvmCross.Plugin.WebBrowser.csproj",
        "./MvvmCross.Plugins/All/MvvmCross.Plugin.All.csproj",
        "./MvvmCross.Forms/MvvmCross.Forms.csproj",
        "./MvvmCross.Analyzers/CodeAnalysis/MvvmCross.CodeAnalysis.csproj"
    };

    // workaround for Xamarin.Android throwing AAPT error -2, instead of building sln :(
    foreach(var buildItem in buildItems)
    {   
        var filePath = new FilePath(buildItem);
        var name = filePath.GetFilenameWithoutExtension();

        settings.BinaryLogger.FileName = name + ".binlog";

        MSBuild(filePath, settings);
    }

    var testItems = GetFiles("./UnitTests/*.UnitTest/*.UnitTest.csproj");
    foreach(var testItem in testItems)
    {
        var name = testItem.GetFilenameWithoutExtension();

        settings.BinaryLogger.FileName = name + ".binlog";

        MSBuild(testItem, settings);
    }
});

Task("UnitTest")
    .IsDependentOn("Build")
    .Does(() =>
{
    EnsureDirectoryExists(outputDir + "/Tests/");

    var testPaths = GetFiles("./UnitTests/*.UnitTest/*.UnitTest.csproj");
    var testsFailed = false;
    foreach(var project in testPaths)
    {
        var projectName = project.GetFilenameWithoutExtension();
        var testXml = new FilePath(outputDir + "/Tests/" + projectName + ".xml").MakeAbsolute(Context.Environment);
        try 
        {
            DotNetCoreTool(project,
                "xunit",  "-fxversion 2.0.0 --no-build -parallel none -configuration " + 
                configuration + " -xml \"" + testXml.FullPath + "\"");
        }
        catch
        {
            testsFailed = true;
        }
    }

    if (isRunningOnAppVeyor)
    {
        foreach(var testResult in GetFiles(outputDir + "/Tests/*.xml"))
            AppVeyor.UploadTestResults(testResult, AppVeyorTestResultsType.XUnit);
    }

    if (testsFailed)
        throw new Exception("Tests failed :(");
});

Task("PublishPackages")
    .WithCriteria(() => !BuildSystem.IsLocalBuild)
    .WithCriteria(() => IsRepository(repoName))
    .WithCriteria(() => ShouldPushNugetPackages(versionInfo.BranchName))
    .Does (() =>
{
    // Resolve the API key.
    var nugetKeySource = GetNugetKeyAndSource();
    var apiKey = nugetKeySource.Item1;
    var source = nugetKeySource.Item2;

    var nugetFiles = GetFiles(solutionName + "*/**/bin/" + configuration + "/**/*.nupkg");

    var policy = Policy
        .Handle<Exception>()
        .WaitAndRetry(5, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(1.5, retryAttempt)));

    foreach(var nugetFile in nugetFiles)
    {
        policy.Execute(() =>
            NuGetPush(nugetFile, new NuGetPushSettings {
                Source = source,
                ApiKey = apiKey
            })
        );
    }
});

Task("UploadAppVeyorArtifact")
    .WithCriteria(() => isRunningOnAppVeyor)
    .Does(() => 
{

    Information("Artifacts Dir: {0}", outputDir.FullPath);

    var uploadSettings = new AppVeyorUploadArtifactsSettings();

    var artifacts = GetFiles(solutionName + "*/**/bin/" + configuration + "/**/*.nupkg")
        + GetFiles(outputDir.FullPath + "/**/*");

    foreach(var file in artifacts) {
        Information("Uploading {0}", file.FullPath);

        if (file.GetExtension().Contains("nupkg"))
            uploadSettings.ArtifactType = AppVeyorUploadArtifactType.NuGetPackage;
        else
            uploadSettings.ArtifactType = AppVeyorUploadArtifactType.Auto;

        AppVeyor.UploadArtifact(file.FullPath, uploadSettings);
    }
});

Task("Default")
    .IsDependentOn("Build")
    .IsDependentOn("UnitTest")
    .IsDependentOn("PublishPackages")
    .IsDependentOn("UploadAppVeyorArtifact")
    .Does(() => 
{
});

RunTarget(target);

MSBuildSettings GetDefaultBuildSettings()
{
    var settings = new MSBuildSettings 
    {
        Configuration = configuration,
        ToolPath = msBuildPath,
        Verbosity = verbosity,
        ArgumentCustomization = args => args.Append("/m"),
        ToolVersion = MSBuildToolVersion.VS2017
    };

    return settings;
}

bool ShouldPushNugetPackages(string branchName)
{
    if (StringComparer.OrdinalIgnoreCase.Equals(branchName, "develop"))
        return true;

    return IsMasterOrReleases(branchName) && IsTagged().Item1;
}

bool IsMasterOrReleases(string branchName)
{
    if (StringComparer.OrdinalIgnoreCase.Equals(branchName, "master"))
        return true;

    if (branchName.StartsWith("release/", StringComparison.OrdinalIgnoreCase) ||
        branchName.StartsWith("releases/", StringComparison.OrdinalIgnoreCase))
        return true;

    return false;
}

bool IsRepository(string repoName)
{
    if (isRunningOnAppVeyor)
    {
        var buildEnvRepoName = AppVeyor.Environment.Repository.Name;
        Information("Checking repo name: {0} against build repo name: {1}", repoName, buildEnvRepoName);
        return StringComparer.OrdinalIgnoreCase.Equals(repoName, buildEnvRepoName);
    }
    else
    {
        try
        {
            var path = MakeAbsolute(sln).GetDirectory().FullPath;
            using (var repo = new LibGit2Sharp.Repository(path))
            {
                var origin = repo.Network.Remotes.FirstOrDefault(
                    r => r.Name.ToLowerInvariant() == "origin");
                return origin.Url.ToLowerInvariant() == 
                    "https://github.com/" + repoName.ToLowerInvariant();
            }
        }
        catch(Exception ex)
        {
            Information("Failed to lookup repository: {0}", ex);
            return false;
        }
    }
}

Tuple<bool, string> IsTagged()
{
    var path = MakeAbsolute(sln).GetDirectory().FullPath;
    using (var repo = new LibGit2Sharp.Repository(path))
    {
        var head = repo.Head;
        var headSha = head.Tip.Sha;
        
        var tag = repo.Tags.FirstOrDefault(t => t.Target.Sha == headSha);
        if (tag == null)
        {
            Debug("HEAD is not tagged");
            return Tuple.Create<bool, string>(false, null);
        }

        Debug("HEAD is tagged: {0}", tag.FriendlyName);
        return Tuple.Create<bool, string>(true, tag.FriendlyName);
    }
}

Tuple<string, string> GetNugetKeyAndSource()
{
    var apiKeyKey = string.Empty;
    var sourceKey = string.Empty;
    if (isRunningOnAppVeyor)
    {
        apiKeyKey = "NUGET_APIKEY";
        sourceKey = "NUGET_SOURCE";
    }
    else
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(versionInfo.BranchName, "develop"))
        {
            apiKeyKey = "NUGET_APIKEY_DEVELOP";
            sourceKey = "NUGET_SOURCE_DEVELOP";
        }
        else if (IsMasterOrReleases(versionInfo.BranchName))
        {
            apiKeyKey = "NUGET_APIKEY_MASTER";
            sourceKey = "NUGET_SOURCE_MASTER";
        }
    }

    var apiKey = EnvironmentVariable(apiKeyKey);
    if (string.IsNullOrEmpty(apiKey))
        throw new Exception(string.Format("The {0} environment variable is not defined.", apiKeyKey));

    var source = EnvironmentVariable(sourceKey);
    if (string.IsNullOrEmpty(source))
        throw new Exception(string.Format("The {0} environment variable is not defined.", sourceKey));

    return Tuple.Create(apiKey, source);
}
