---
layout: post
title: Effective continuous deployment with TeamCity, Octopus Deploy and PowerShell
date: '2013-05-15 07:38:00'
tags:
- powershell
- octopus-deploy
- devops
- teamcity
- continuous-deployment
---

I've been a solid user of [TeamCity](http://www.jetbrains.com/teamcity/) for the last 3-4 years and started using [Octopus Deploy](http://octopusdeploy.com/) for about a year ago. Since then I have set up multiple configurations at several different sites and thought I would share my way of setting up an effective continuous deployment environment with the world.

I will cover the basics as well as how you can configure TeamCity to create a new build configuration with just a few clicks. Before we get there we'll have to start at where we want to arrive and work our way from there. I will not cover how you install TeamCity and Octopus since that is already covered on their sites.

## UPDATE
I've gathered the Power Shell script and created a NuGet package to make it easier to use. The NuGet package can be found [here](https://nuget.org/packages/OctoWebSetup/) and if you're interested in the code it is up on [github](https://github.com/mastoj/OctoWebSetup).

## Setting up your web project
The application I want to deploy is a standard ASP.NET MVC 4 application, I have enabled nuget package restore and the only package I've added is the [OctoPack](http://nuget.org/packages/OctoPack) to make it easier to build the deploy package that is needed by Octopus. I like to enable the nuget package restore before I install the OctoPack package since how it is installed in the projects differs if you have nuget package restore enabled or not.  

When you have installed OctoPack all you need to do to build a deploy package is set the build parameter `RunOctoPack` to `true`, but I will come back to this later when setting up the build. We have some more things to cover first.

### Creating the nuspec file
If you aren't that familiar with [NuGet](http://nuget.codeplex.com/) package manager yet are really recommend you to get familiar with it. The format of the deployment packages used by Octopus has the same format as NuGet so you can just use NuGet to create the packages. To define some metadata and such we need a nuspec-file, which is a specification file for a nuget package. Creating such a file for a .NET-project is dead simple, you just run `nuget spec YourProject.csproj` which will create a `YourProject.nuspec` file. Include the file in your web project and change the parameters. The one I'm using looks something like this: 

    <?xml version="1.0"?>
    <package >
        <metadata>
            <id>DemoProject.Web</id>
            <version>1.0.0</version>
            <title>DemoProject.Web</title>
            <authors>Tomas Jansson</authors>
            <owners>Tomas Jansson</owners>
            <requireLicenseAcceptance>false</requireLicenseAcceptance>
            <description>This is a sample project used for demoing Octopus and TeamCity</description>
            <releaseNotes>Some cool text goes here.</releaseNotes>
            <copyright>Copyright 2013</copyright>
            <tags>Octopus TeamCity</tags>
        </metadata>
    </package>

Don't bother about the version number since that is something we will override anyway. 

### Adding the deployment scripts
Ok, this is something that you don't have to do, but I have included it to show you that isn't that hard to program IIS and with PowerShell and get as much as possible automated. 

Before I describe the scripts let set the stage. When Octopus run a deployment it goes through three phases in a successful deploy; pre deploy, deploy and post deploy. For each of these phases you can have a script running if it has the correct name; `PreDeploy.ps1`, `Deploy.ps1` or `PostDeploy.ps1` respectively. So the goal we have is in the pre deploy phase check if the web site or application exists and create it if it doesn't. With creating the application I mean basically do everything as creating application pool, creating site, creating application and set the bindings for the site. I would also be able to configure this as simple as possible and since the language is PowerShell I think [dot sourcing](http://technet.microsoft.com/en-us/library/ee176949.aspx#ECAA) of a configuration file is suitable. I have also decided to split the actual script that do the actual configuration in two scripts, one that contains all the functions manipulating IIS and one that uses the previous script to run through the configuration.

The first script is a sample configuration script `Dev.Config.ps1`:

    $config = @{
        ApplicationPools = @(
            @{
                Name = "DemoSiteAppPool";
                FrameworkVersion = "v4.0"
            },
            @{
                Name = "DemoSiteAppAppPool";
                FrameworkVersion = "v4.0"
            });
        Site = @{
            Name = "DemoSite";
            SiteRoot = "c:\tmp";
            AppPoolName = "DemoSiteAppPool";
            Port = 88
            Bindings = @(
                @{Port= 88; HostName= "*"}, 
                @{Port= 89; HostName= "DemoApp"}
            );
            Application = @{
                Name = "DemoApp";
                AppPoolName = "DemoSiteAppAppPool";
                ApplicationRoot = "c:\tmp"
            }
        };
    }

If you dot source that file you will get a `$config` object that has two properties; `ApplicationPools` and `Site`. Reading through this file I think it is pretty clear what I want it to configure so let's go through it from the top. First I define that I want two application pools, this is something that might be useful if you are running multiple apps under one site. After that I have one site defined and it should be set up on port 88, which is used when setting up the site. For the site I have also defined bindings which can override the original settings since this is something that you might want to change at a later time. The last part is the application and it is nested under the site.

Now when the config is defined let's look at the `IISConfiguration.ps1` file that contains all the helper functions for configuring IIS: 

    Import-Module WebAdministration

    $appPoolsPath = "IIS:\AppPools"
    $iisSitesPath = "iis:\sites"

    function Write-Info ($message) {
        Write-Host "Info:" $message
    }

    function Write-Error ($message) {
        Write-Host "Error:" $message
    }

    function GuardAgainstNull($value, $message) {
        if($value -eq $null) {
            Write-Error $message
            exit 1
        }    
    }

    function IISObjectExists($objectName) {
        return Test-Path $objectName
    }

    function WebAppExists($appName) {
        if($appName.ToLower().StartsWith("iis")) {
            return IISObjectExists $appName
        } else {
            return IISObjectExists "IIS:\Sites\$appName"
        }
    }

    function WebSiteExists($siteName) {
        return WebAppExists $siteName 
    }

    function AppPoolExists($appPoolName) {
        return IISObjectExists "$appPoolsPath\$appPoolName"
    }

    function GetIfNull($value, $default) {
        if ($value -eq $null) { $default } else { $value }
    }

    function CreateApplicationPool($appPoolName, $appPoolFrameworkVersion, $appPoolIdentityType, $userName, $password) {
        $appPoolFrameworkVersion = GetIfNull $appPoolFrameworkVersion "v4.0"
        $appPoolIdentityType = GetIfNull $appPoolIdentityType "ApplicationPoolIdentity"
        if($appPoolIdentityType -eq "SpecificUser") {
            GuardAgainstNull $userName "userName and password must be set when using SpecificUser"
            GuardAgainstNull $password "userName and password must be set when using SpecificUser"
        }
        
        if(AppPoolExists $appPoolName) {
            Write-Info "Application pool already exists"
        } else {
            Write-Info "Creating application pool: $appPoolName"
            $appPoolFullPath = "$appPoolsPath\$appPoolName"
            $appPool = new-item $appPoolFullPath
            if($appPoolIdentityType -ne "SpecificUser") {
                Set-ItemProperty $appPoolFullPath -name processModel -value @{identitytype="$appPoolIdentityType"}
            }
            else {
                Set-ItemProperty $appPoolFullPath -name processModel -value @{identitytype="$appPoolIdentityType"; username="$userName"; password="$password"}
            }
            Set-ItemProperty $appPoolFullPath managedRuntimeVersion "$appPoolFrameworkVersion"
            Write-Info "Application pool created"
        }
    }

    function GetNextSiteId {
        (dir $iisSitesPath | foreach {$_.id} | sort -Descending | select -first 1) + 1
    }

    function CreateSite($siteName, $siteRoot, $appPoolName, $port) {
        $port = GetIfNull $port 80
        GuardAgainstNull $siteName "siteName mest be set"
        GuardAgainstNull $siteRoot "siteRoot must be set"
        GuardAgainstNull $appPoolName "appPoolName must be set when creating a site"
        if(WebSiteExists $siteName) {
            Write-Info "Site $siteName already exists"
        } else {
            Write-Info "Creating site"
            if (!(Test-Path $siteRoot)) {
                Write-Info "Site root does not exist, creating..."
                [void](new-item $siteRoot -itemType directory)
            }

            $id = GetNextSiteId
            $sitePath = "$iisSitesPath\$siteName"
            new-item $sitePath -bindings @{protocol="http";bindingInformation="*:${port}:*"} -id $id -physicalPath $siteRoot
            Set-ItemProperty $sitePath -name applicationPool -value "$appPoolName"
            Write-Info "Site created, starting site"
            Start-Website $siteName
        }
    }

    function CreateApplication($siteName, $applicationName, $applicationRoot, $appPoolName) {
        GuardAgainstNull $siteName "siteName mest be set"
        GuardAgainstNull $applicationRoot "applicationRoot must be set"
        GuardAgainstNull $applicationName "applicationName must be set"
        GuardAgainstNull $appPoolName "appPoolName must be set"
        $applicationIISPath = ($iisSitesPath + "\" + $siteName + "\" + $applicationName)
        if(WebAppExists $applicationIISPath) {
            Write-Info "Application $siteName\$applicationName already exists"
        }
        else {
            Write-Info "Application does not exist, creating..."
            New-Item $applicationIISPath -physicalPath "$applicationRoot" -type Application
            Set-ItemProperty $applicationIISPath -name applicationPool -value "$appPoolName"
            Write-Info "Application Created" 
        }
    }

    function GetHostNamesForSite($siteName) {
        return $site.bindings.Collection | %{$_.bindingInformation.Split(":")[2]}
    }

    function ClearBindings($siteName) {
        Clear-ItemProperty "$iisSitesPath\$siteName" -Name bindings
    }

    function AddBindings($siteName, $bindings) {
        ForEach($binding in $bindings) {
            $port = $binding.Port
            $hostName = $binding.HostName
            New-WebBinding -Name $siteName -HostHeader $hostName -Port $port -Protocol "http"
        }
    }

    #@(@{Port= 83; HostName= "tomas"}, @{Port= 84; HostName= "tomas"})
    function SetBindings($siteName, $bindings) {
        Write-Info "Bindings will be deleted and added again"
        Write-Info "SiteName: $siteName"
        Write-Info "Bindings: $bindings"
        if($bindings -ne $null) {
            Write-Info "Deleting bindings"
            ClearBindings $siteName
            Write-Info "Adding bindings"
            AddBindings $siteName $bindings
        }
    }

    function CreateAppPools($appPoolsConfig) {
        Foreach($appPoolConfig in $appPoolsConfig) {
            $appPoolName = $appPoolConfig.Name
            $appPoolFrameworkVersion = $appPoolConfig.FrameworkVersion
            $appPoolIdentityType = $appPoolConfig.AppPoolIdentityType
            $userName = $appPoolConfig.UserName
            $password = $appPoolConfig.Password
            CreateApplicationPool $appPoolName $appPoolFrameworkVersion $appPoolIdentityType $userName $password
        }
    }

    function CreateSiteFromConfig($siteConfig) {
        $siteName = $siteConfig.Name
        $siteRoot = $siteConfig.SiteRoot
        $appPoolName = $siteConfig.AppPoolName
        $port = $siteConfig.Port
        CreateSite $siteName $siteRoot $appPoolName $port
        
        if($siteConfig.Bindings) {
            SetBindings $siteName $siteConfig.Bindings
        }
        if($siteConfig.Application) {
            $applicationName = $siteConfig.Application.Name
            $applicationRoot = $siteConfig.Application.ApplicationRoot
            $appPoolName = $siteConfig.Application.AppPoolName
            CreateApplication $siteName $applicationName $applicationRoot $appPoolName
        }
    }
    
I won't go through this file in detail, one thing that is worth mentioning though is that this could be a module instead of a simple script file but this is the way I decided to do it. If someone has a really good reason to why I should make it a module let me know. The functions that is most use full are `CreateAppPools` and `CreateSiteFromConfig` since those are the functions that will be called from execution script which is up next.

The purpose of the `DoConfig.ps1` script is just a separation of concerns. 

    $ErrorActionPreference = "Stop"

    function Get-ScriptDirectory
    {
        Split-Path $script:MyInvocation.MyCommand.Path
    }

    function Get-OctopusWebSiteNameFromConfig($conf) {
        if($conf.Site) {
            if($conf.Site.Application) {
                return $conf.Site.Name + "/" + $conf.Site.Application.Name
            }
            return $conf.Site.Name
        }
        Write-Error "Configuration is missing site"
        exit 1
    }

    if($configFile -eq $null) {
        $configFile = "Local.Config.ps1"
    }

    $configFilePath = (Get-ScriptDirectory) + "\$configFile"

    $IISConfigurationScriptPath = (Get-ScriptDirectory) + "\IISConfiguration.ps1"
    . $IISConfigurationScriptPath

    . $configFilePath

    CreateAppPools $config.ApplicationPools
    CreateSiteFromConfig $config.Site

    Set-OctopusVariable -Name "OctopusWebSiteName" -Value (Get-OctopusWebSiteNameFromConfig $config)

When looking at the script you might wonder where the variable `$configFile` comes from? The answer to this is it dependes :). You can set it yourself before running the script manually, but the purpose of this script is that it should be run by Octopus and in that context we will define the variable `$configFile` in Octopus so you can have different configuration files for different environments. This script uses both the script above and it also sets the Octopus variable `OctopusWebSiteName` on the last row which is really important. Setting that Octopus internal variable in the pre deploy phase means that it is set in the deploy phase and here Octopus will use it to set the path of the application you are deploying to point to the right folder.

The last and smallest script we will look at is `PreDeploy.ps`

    function Get-ScriptDirectory
    {
        Split-Path $script:MyInvocation.MyCommand.Path
    }

    $doConfigPath = (Get-ScriptDirectory) + "\DeployScripts\" + "DoConfig.ps1"
    . $doConfigPath

All this script does is calling `DoConfig.ps1`, the reason to why I've split the files this way is because you might want to do more things under pre deploy and this make it easier to follow what you are actually doing on a higher level.

The demo application I'm using looks like this:

![Solution overview][1]
    
## Configuring Octopus
We have actually covered the hardes part now with all that ground work. A solid foundation is always important :). I want show you in detail how to set up and configure Octopus, I'll just show how to set up the project. 

### Creating the deployment step
Click on steps for your project in Octopus deploy and then "Add step". The step you should add is a "Deploy a NuGet package". Name your step and and choose the repository where you will publish your package and enter the name of your deploy package, the id from the nuspec-file, in the "NuGet package" field. Select the role you want to deploy to and use default on the rest.

When you have added your deployment step you also need to add one variable to your variables list. You need to add a variable `configFile` and set the name to the name of the configuration file. My sample looks like this:

![Variables overview][2]

When you have set up Octopus it is a good time to try everything out. First we need to build the deployment package: 

    MSBuild /t:Rebuild /p:RunOctoPack=true /p:OctopackPackageVersion=1.1.0 /p:PackageVersion=1.1.0 /p:OctoPackPublishPackageToFileShare=C:\Packages .\DemoProject.Web.sln

The command above will build the solution, create the deployment NuGet package and copy the package to C:\Packages which I have set up as a NuGet repository in NuGet.

The next thing is to go back to Octopus and create a release and then try to deploy it. This should create application pools for you, setting up the site, application and the bindings for the application.
    
## Configuring TeamCity
So far we have a nice deployment setup, but we want to take it a little bit further. For many of the customers I've been at we've had multiple projects and almost all of them have been web application. The setup in TeamCity I'll walk you through is useful if you are building and deploying multiple applications and want to make it easy to get started with a new application. 

I'll use the assumption that you are lucky enough that you have a default github root that can be used for all your projects. Also, the user that runs the TeamCity build agent and server are running as a specific user that you have generated an ssh key for that is uploaded to github for easier access. For local experimentation you could use your own account.

To make things easier I'll use the [Octopus plugin for TeamCity](http://octopusdeploy.com/documentation/integration/teamcity). I have also enabled the NuGet server that is included in TeamCity so that all the deploy packages I build is published automatically to the NuGet server provided by TeamCity.

Creating template and separating things as I do here is not necessary if you only have one project, but it is really helpful if you are handling multiple projects and configurations. Also, this is not a guide to how should do it, this is a description of how I have set things up and what works for me.

### Creating the templates
The first template we will create is the one building a solution and creating the release packages. I call the template `BuildWebAndReleasePackage`. The settings you need to specify are the following:

 * General Settings
   - Build number format: `%conf.Version%.{0}`
 * Version Control Settings
   - Create a new git VCS root with the following settings:
     - Fetch URL: `git@github.com:<your github user>/%conf.GitProjectName%.git`
     - Authentication Method: Default Private Key (this requires that you have configured the server to run as a user with access to the repo and configured ssh)
     - Check "Make this VCS root available to all of the projects"
     - You can later go back and experiment with the branch specification, but I will use the default for this now.
  * Add one Visual Studio build step with the following settings
    - Solution file path: `%conf.PathToSolution%`
    - Check "Run OctoPack" which you have if you installed the TeamCity plugin
    - OctoPack package version: `%conf.OctopusPackageVersion%`
  * Build Triggering
    - Add a new VCS Trigger, default settings is fine
  * Add an AssemblyInfo patcher under "Additional Build Features"
    - Assembly version format: `%build.number%` 
  * Build parameters
    - Add one Environment variable `PackageVersion`: `%build.number%`

The second template we need is the one that trigger the deployment, I call it ´DeployWithOctopus`. Use the following settings:

  * General Settings
    - Build number format: %conf.BuildNumber% (we will set this with a dependency)
  * Don't add any VCS settings
  * Add a "OctopusDeploy: Release" build step
    - Octopus URL: `<url to your Octopus server>`
    - API key: `<api key to a user on your Octopus server>`
    - Project: `%conf.OctoProjectName%`
    - Release number: `%build.number%`
    - Deploy to: `%conf.DeployEnvironment%`
    - Check "Wait for deployment to complete"

I usually create a "dummy" project called something like "Templates project" where I put all the templates so the are accessible from every other project and that also makes it possible to share things like the Octopus api key between projects so I don't have to enter that for each of the projects I have.    
    
### Using the templates
Now when we have the templates we need to create our configurations using the templates. Let's start with the 'BuildWebAndReleasePackage'. Navigate to the template and locate the button "Create build configuration". Fill in the form you are presented with something like:

![Creating build configuration][3]

The `conf.GitProjectName` is only the name of your git repo if you configured your VCS root correctly. The `conf.OctopusPackageVersion` is using the build number that is generated by `conf.Version` together with the incremental id of the build. When you have created the configuration move it to the project it belongs and the first configuration is all set.

The next configuration to create is the release configuration, which is of course based on the `DeployWithOctopus` template. There are some more stuff to configure here but it's not that complicated. First you create a build configuration from the template. Create a build from the template specifying values for all the fields except `conf.BuildNumber`. When it is created you move it to the same projects as the previous configuration. The next thing is to set up a snapshot dependency between the configurations, this sort of means that this configuration will use the same checkout as the previous configuration. To create this dependency go to "Dependencies" and choose "Add new snapshot dependency" (you can probably use artifact dependency as well but I choose snapshot). Here you find the configuration choose that one. The next thing we need is to set a trigger for the configuration so go to "Build Triggers" and choose "Add new trigger". Choose "Finish Build Trigger" and select the other configuration as well as check "Trigger after successful build only" checkbox. Now go back to the parameters and click the %conf.BuildNumber% and as value you start writing "%dep" which will give you a list of things starting with that. Choose the one that resembles `%dep.bt4.build.number%`, you can have a different number then the 4 I have depending on the state of your TeamCity server.

## Summary
This a setup, or variants of it, in scenarios where I have to handle more than just one project. It allows me to streamline how we do deployment and also formalize the procedure. In a way I use TeamCity and Octopus as the living documentation of how we get things from our code repository to the production environment. You can make the templates even more complex, like make the configuration just listen to certain branches for example. The way we have it in my current project is that we have a development template that build from every possible branch except master, but it doesn't create a deployment. If you want a deployment you have to trigger it manually so we don't flood the test environments for each push to every single branch. Then we have a release template that only listens to checkins to the master branch. When there is a checkin at the master branchwe build a release and deploy it to the test environment so we can verify it works. We have also added things like automatic release note generation between two master branch checkins for that template, it's a simple generation that creates a line for each commit and generates a markdown file with the links to the commits and appends it to the release so it shows up with the release in Octopus. Really nice and I might write about it later, I think this is enough for now. 

I hope someone find this useful, please comment if something is wrong or could be done better in another way. 

  [1]: https://public.blu.livefilestore.com/y2pBq3DbzywLNaGYrCsAaqTE17orjnSdJswnl2BQdy5QDayuLn_Wb5BsrpAIA0fnMFObB_1A9uDpbO7y4zfamCmI9e2GuvPD2okoJi93sfdGgg/OctoTeam01.PNG?psid=1
  [2]: https://public.blu.livefilestore.com/y2p1TwfqnKIWqATT7cvFvAXDwLx7thEIBh2nJs44r3PmGtgt25-Wk0Jb0Tcob8Yb9pB9HjdIK3dtum0BpmYwNUB5PnOXitIYfdjrS9SIiIjwPo/OctoTeam02.PNG?psid=1
  [3]: https://public.blu.livefilestore.com/y2pTXYbmVwxIofOD1-LA1ZRaADNbkffUICg8Uz3EYcV2Z7Q2ilo1fULCrs-JQs83a3AslVHmXKPI2PCMjFMdr2q7skDf0WYwhJpORtxPDPMKFc/OctoTeam03.PNG?psid=1