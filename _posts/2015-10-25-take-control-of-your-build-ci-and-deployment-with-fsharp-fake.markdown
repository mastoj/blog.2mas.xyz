---
layout: post
title: Take control of your build, CI and deployment with FSharp FAKE
date: '2015-10-25 22:43:46'
tags:
- azure
- octopus-deploy
- fsharp
- fake
- appveyor
---

I've been using [TeamCity](https://www.jetbrains.com/teamcity/) for quite a while and trusted it to do the right thing when it comes to building. It has a lot of built in feature to do so, but the moment you start to define your build inside TeamCity you have painted yourself into a corner. I still think TeamCity is a great tooling triggering my builds, but I have come to realize that I should start to put the actual definition of the builds outside of TeamCity, or whatever CI tool I am using.

## Why build scripts?

Many people argue that it works just fine with using TeamCity and other CI tools to define the way you build, and it is when you have a set of smaller projects I think. The moment when you need to do more things in your build it make sense to put it in a separate script file and here are some of my reasons:

* You got your actual build process under source control together with your code instead of defined in a CI tool
* It is easier to handle versioning
* The way you build on the server is identical to the way you build locally
* You will not be locked in to one CI tool, if you use TeamCity today it is quite easy to switch to AppVeyor tomorrow and TravisCI the day after that.

## What is FAKE

[FAKE](http://fsharp.github.io/FAKE/) is a domain specific language, [dsl](https://en.wikipedia.org/wiki/Domain-specific_language), for build tasks implemented in [F#](http://fsharp.org/). That it is implemented in F# doesn't mean you can only build F# projects, in fact you should event be able to build java project if you would like to. F#, along with many functional programming languages, is terrific when you want to create a dsl, since functional programming languages often are more expressive and not as verbose as OO languages. This is not an introduction to FAKE, and the documentation is [here](http://fsharp.github.io/FAKE/) if you want to check it out. Later in the post I will go through what my FAKE script look like to solve the problems I imagined.

## The demo

The thing I wanted to try was this:

* Simple .NET Web app that includes some js building
* GitHub as source control
* AppVeyor as a CI engine to execute build
* Octopus Deploy to handle deployment
* Deploy to Azure Web App
* Most of it configured in FAKE

Illustration of the flow from code to deployment:

![The build deploy process]({{ site.url }}/assets/images/migrated/CodeToDeploy.JPG)

I think the end result is really good since it is the first time I used FAKEand AppVeyor. If you want to see the sample app and also the builds scripts they are in this repo at github: https://github.com/mastoj/FAKESimpleDemo

### Appveyor

I could as well used TeamCity, but I thought I would try [Appveyor](http://www.appveyor.com/) out. The goal of AppVeyor is:

> AppVeyor aims to give powerful Continuous Integration and Deployment tools to every .NET developer without the hassle of setting up and maintaining their own build server. - [About AppVeyor](http://www.appveyor.com/about)

Their goal is the reason I think AppVeyor is interesting and I wanted to take it for a spin. The experience has been truly amazing with really fast build times and easy to get started, and another really positive aspect is that I don't have to think about maintaining the build server.

AppVeyor can do a lot more than I did, one might even argue that I should use AppVeyor instead of Octopus Deploy for deployment, but this is a proof of concept for a scenario were I still will have Octopus Deploy and I really like Octopus Deploy :). 

When you configure AppVeyor you can do so in the UI or with a yml-file. Of course I choose to use a yml-file since I do want as much as I can in source control. The configuration file I created is minimal since I have FAKE that take care of the actual build process:

```language-yml
environment:
  version: 1.0.0
assembly_info:
  patch: false
build_script:
  build.cmd
test: off
```

This specifies a environment variable `version` and that AppVeyor should not run tests or patch the assmebly information file. To start the build the `build.cmd default` commmand should be used. I can still run test and patch the assembly information file, but that is something I want to do from FAKE and not define in AppVeyor.

I alos added some environment settings in the UI like this:

![AppVeyor environment settings]({{ site.url }}/assets/images/migrated/AppVeyorEnv.PNG)

The reason I added them there and not in source control is because that is not I want to have on GitHub. The same would apply if I needed some other "secret" settings during build, then I would add that a a environment setting in the UI. I do the same thing if I use TeamCity.

### Octopus Deploy

I'm not going to go through what Octopus Deploy and what you can do with it, all you nned to know is that it is a great tool for handling deployments to multiple different environments locally as well as in the cloud. 

To deploy to an Azure Web App you need to create an account under Azure that is an Azure account. You create accounts in `Environment->Accounts`. To create an Azure account you will need a Management Certificate (.pfx) and your subscriptiong id. I don't remember which guide I followed to generate mine, but this might work: https://azure.microsoft.com/en-us/blog/obtaining-a-certificate-for-use-with-windows-azure-web-sites-waws/

When that is done you just add your project and creates a deployment step of type `Deploy an Azure Web App` and you are good to go. The step configuration should like something like: 

![Octopus deploy configuration]({{ site.url }}/assets/images/migrated/OctopusDeployStep.PNG)

### FAKE

This is were all the magic happens. I will try to break down this file: https://github.com/mastoj/FAKESimpleDemo/blob/master/build.fsx and explain the different parts of it. I expect you to know the minimum basics of FAKE, that is, what is a target and how you set up a build process with the `==>` operator.

#### Bootstrap

To get FAKE running easily I also have a bootstrap file [`build.cmd`](https://github.com/mastoj/FAKESimpleDemo/blob/master/build.cmd). 

```
@echo off
cls
NuGet.exe "Install" "FAKE" "-OutputDirectory" "packages" "-ExcludeVersion"
NuGet.exe "Install" "OctopusTools" "-OutputDirectory" "packages" "-ExcludeVersion"
NuGet.exe "Install" "Node.js" "-OutputDirectory" "packages" "-ExcludeVersion"
NuGet.exe "Install" "Npm.js" "-OutputDirectory" "packages" "-ExcludeVersion"
"packages\FAKE\tools\Fake.exe" build.fsx %*
```

All this file does is installing the tools I need to build the project, and then calls the build script with the arguments provided.

I separated the build script into some modules to make it easier to follow and I thought I would go through this module by module.

#### Npm module

The demo application had a simple js-app (that alerted "Hello") to show that it is possible to build js-apps with FAKE. I would be stupid if I did not use the tools from the js-community to do the heavy lifting here, so that is exactly what I did. You can find the [`package.json` here](https://github.com/mastoj/FAKESimpleDemo/blob/master/src/FAKESimple.Web/package.json) and the [`gulpfile.js` here](https://github.com/mastoj/FAKESimpleDemo/blob/master/src/FAKESimple.Web/gulpfile.js). I won't cover those since it is a different topic. When npm and gulp was configured all I needed to do was trigger it from FAKE and to do so I wrote a simple `Npm` wrapper. (I might extract this and try to submit a PR to FAKE later). The code is quite simple:

```language-fsharp
module Npm =
  open System

  let npmFileName =
    match isUnix with
      | true -> "/usr/local/bin/npm"
      | _ -> "./packages/Npm.js/tools/npm.cmd"

  type InstallArgs =
    | Standard
    | Forced

  type NpmCommand =
    | Install of InstallArgs
    | Run of string

  type NpmParams = {
    Src: string
    NpmFilePath: string
    WorkingDirectory: string
    Command: NpmCommand
    Timeout: TimeSpan
  }

  let npmParams = {
    Src = ""
    NpmFilePath = npmFileName
    Command = Install Standard
    WorkingDirectory = "."
    Timeout = TimeSpan.MaxValue
  }

  let parseInsallArgs = function
    | Standard -> ""
    | Forced -> " --force"

  let parse command =
    match command with
    | Install installArgs -> sprintf "install%s" (installArgs |> parseInsallArgs)
    | Run str -> sprintf "run %s" str

  let run npmParams =
    let npmPath = Path.GetFullPath(npmParams.NpmFilePath)
    let arguments = npmParams.Command |> parse
    let result = ExecProcess (
                  fun info ->
                    info.FileName <- npmPath
                    info.WorkingDirectory <- npmParams.WorkingDirectory
                    info.Arguments <- arguments
                  ) npmParams.Timeout
    if result <> 0 then failwith (sprintf "'npm %s' failed" arguments)

  let Npm f =
    npmParams |> f |> run
```

The `Npm` function is the important part. There I take the default arguments, pass it through `f` which adds changes to the arguments and then that is passed to `run`. The `run` method parse the `Command` property and execute `Npm`. It doesn't get simpler than that.

#### OctoHelpers module

There were two things I wanted to do with Octopus Deploy; create release and create deploy. This helper module is a wrapper around the `Octo` module in FAKE since there were a lot of similarities between the steps.

```language-fsharp
module OctoHelpers =
  let executeOcto command =
    let serverName = environVar "OCTO_SERVER"
    let apiKey = environVar "OCTO_KEY"
    let server = { Server = serverName; ApiKey = apiKey }
    Octo (fun octoParams ->
        { octoParams with
            ToolPath = "./packages/octopustools"
            Server   = server
            Command  = command }
    )
```

All that is going on here is that I read some things from environment variables that should be configured on AppVeyor (or the CI you use) and then execute an action against Octopus Deploy. 

#### AppVeyorHelper module

I extracted the things that dealt with AppVeyor integration to a separate module to keep my targets clean (I get to the targets soon). It is actually one thing I'm doing on AppVeyor and that is publishing the artifacts to the nuget feed hosted by AppVeyor.

```language-fsharp
module AppVeyorHelpers =
  let execOnAppveyor arguments =
    let result =
      ExecProcess (fun info ->
        info.FileName <- "appveyor"
        info.Arguments <- arguments
        ) (System.TimeSpan.FromMinutes 2.0)
    if result <> 0 then failwith (sprintf "Failed to execute appveyor command: %s" arguments)
    trace "Published packages"

  let publishOnAppveyor folder =
    !! (folder + "*.nupkg")
    |> Seq.iter (fun artifact -> execOnAppveyor (sprintf "PushArtifact %s" artifact))
```

The `publishOnAppveyor` function takes a folder and finds all the nuget packages in it. After that it executes the `appveyor` command to publish every package to the feed.

#### Settings module

The module name isn't perfect, but what is. It contains all the variables that are used in the targets as well as two three helper functions:

```language-fsharp
module Settings =
  let buildDir = "./.build/"
  let packagingDir = buildDir + "FAKESimple.Web/_PublishedWebsites/FAKESimple.Web"
  let deployDir = "./.deploy/"
  let testDir = "./.test/"
  let projects = !! "src/**/*.csproj" -- "src/**/*.Tests.csproj"
  let testProjects = !! "src/**/*.Tests.csproj"
  let packages = !! "./**/packages.config"

  let getOutputDir proj =
    let folderName = Directory.GetParent(proj).Name
    sprintf "%s%s/" buildDir folderName

  let build proj =
    let outputDir = proj |> getOutputDir
    MSBuildRelease outputDir "ResolveReferences;Build" [proj] |> ignore

  let getVersion() =
    let buildCandidate = (environVar "APPVEYOR_BUILD_NUMBER")
    if buildCandidate = "" || buildCandidate = null then "1.0.0" else (sprintf "1.0.0.%s" buildCandidate)
```

The `getOutputDir` is used to get the name of the folder of a file, it says `proj` but it could be any file. I'm using it to create one output folder per project instead of everything ending up in the same folder or under `bin/release` as it usually does when building from VS. It is using the convention that the parent folder name of a project file is the name of the project. The `build` function is a wrapper to build one single project at a time to be able to specify the output folder per project. I don't bother to explain the `getVersion` function.

#### Targets module

This is where I define all the separate steps. When I have all my helpers settled it is quite straighforward:

```language-fsharp
module Targets =
  Target "Clean" (fun() ->
    CleanDirs [buildDir; deployDir; testDir]
  )

  Target "RestorePackages" (fun _ ->
    packages
    |> Seq.iter (RestorePackage (fun p -> {p with OutputPath = "./src/packages"}))
  )

  Target "Build" (fun() ->
    projects
    |> Seq.iter build
  )

  Target "Web" (fun _ ->
    Npm (fun p ->
      { p with
          Command = Install Standard
          WorkingDirectory = "./src/FAKESimple.Web/"
      })

    Npm (fun p ->
      { p with
          Command = (Run "build")
          WorkingDirectory = "./src/FAKESimple.Web/"
      })
  )

  Target "CopyWeb" (fun _ ->
    let targetDir = packagingDir @@ "dist"
    let sourceDir = "./src/FAKESimple.Web/dist"
    CopyDir targetDir sourceDir (fun x -> true)
  )

  Target "BuildTest" (fun() ->
    testProjects
    |> MSBuildDebug testDir "Build"
    |> ignore
  )

  Target "Test" (fun() ->
    !! (testDir + "/*.Tests.dll")
        |> xUnit2 (fun p ->
            {p with
                ShadowCopy = false;
                HtmlOutputPath = Some (testDir @@ "xunit.html");
                XmlOutputPath = Some (testDir @@ "xunit.xml");
            })
  )

  Target "Package" (fun _ ->
    trace "Packing the web"
    let version = getVersion()
    NuGet (fun p ->
          {p with
              Authors = ["Tomas Jansson"]
              Project = "FAKESimple.Web"
              Description = "Demoing FAKE"
              OutputPath = deployDir
              Summary = "Does this work"
              WorkingDir = packagingDir
              Version = version
              Publish = false })
              (packagingDir + "/FAKESimple.Web.nuspec")
  )

  Target "Publish" (fun _ ->
    match buildServer with
    | BuildServer.AppVeyor ->
        publishOnAppveyor deployDir
    | _ -> ()
  )

  Target "Create release" (fun _ ->
    let version = getVersion()
    let release = CreateRelease({ releaseOptions with Project = "FAKESimple.Web"; Version = version }, None)
    executeOcto release
  )

  Target "Deploy" (fun _ ->
    let version = getVersion()
    let deploy = DeployRelease(
                  { deployOptions with
                      Project = "FAKESimple.Web"
                      Version = version
                      DeployTo = "Prod"
                      WaitForDeployment = true})
    executeOcto deploy
  )

  Target "Default" (fun _ ->
    ()
  )
```

The responsibility of each target is as follows:

* `Clean` - removes all the output files so I get a clean build
* `RestorePackages` - gets all the packages from nuget
* `Build` - take my definition of project files and runs the build helper for each one
* `Web` - restore all the node modules and then builds the js-app using `Npm`
* `CopyWeb` - copy the js-app to the web application so it can be added to the package for deployment
* `BuildTest` - same as `Build` but for the test projects
* `Test` - execute the tests
* `Package` - packages the web application to a nuget package that I can use for deployment
* `Publish` - publishes all the packages to the nuget feed, in this case AppVeyor
* `Create release` - creates a release on Octopus Deploy
* `Deploy` - deploy the release created
* `Default` - empty default step that I can always use to run everything

There were many steps there, but I think it is nice to have that separation in place. I could probably combine some of the steps, but I like it as it is since it is easier to move things around if I want to.

#### The build process

The last part is to defined the dependencies between all the targets, and here it is:

```language-fsharp
"Clean"
==> "RestorePackages"
==> "Build"
==> "Web"
==> "CopyWeb"
==> "BuildTest"
==> "Test"
==> "Package"
==> "Publish"
==> "Create release"
==> "Deploy"
==> "Default"

RunTargetOrDefault "Default"
```

I most likely only want to run down to `Test` locally and need to make some adjustments to run everything locally, but it is doable. The important part is that I can create an actual artifact locally, and that I'm doing it the same way as I would on the build server.


## Summary

This post became longer than I thought, but I hope you see the use of using FAKE. Once more I think F# shows that it is a good fit for many different problems, not just science and math. If I start a new .NET-project today I would definitely add FAKE as one of the first things. That gives me a reliable build that executes the same way on the server and locally as well as version control of the build process compared to having it all configured in the CI server.

If you find any improvements, please comment here or on GitHub. You should be able to clone the [repo](https://github.com/mastoj/FAKESimpleDemo) and just execute `build.cmd Test` to get started.