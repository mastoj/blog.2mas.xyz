---
layout: post
title: Extending the NuGet Package Manager Console
date: '2012-10-25 07:17:00'
tags:
- nuget
---

If you read my previous [post](http://blog.tomasjansson.com/2011/04/creating-a-nuget-package/) about how to create a [NuGet](http://www.nuget.org) package this is a follow up on that post. I will in this post, most example code, show you how to extend the Package Manager Console in Visual Studio with a NuGet package. More precisely I will extend the Console with the feature of asking [http://www.classnamer.com](http://www.classnamer.com) to generate a class name for me since sometimes when I develop I run out of imagination and at those points it can be good to get generated class names like `CryptographicUserLogger` :).

The post consist of two parts; the first part shows you the code we will run in order to get the class name and initialize the scripts. So let's get started.

### Creating a PowerShell extension script
Since this is my first PowerShell ever the code might not be the best code ever, but that is not the point. The first part is to write the actual module that will do all the work. The code for the module ClassNamer.psm1 look like:

    function Get-ClassName {
        $ie = new-object -com InternetExplorer.Application
        $ie.navigate("http://www.classnamer.com/")
        if (!$ie) { Write-Host "variable is null" }
        while ($ie.Busy -eq $true) 
        { 
            Start-Sleep -Milliseconds 1000; 
        } 

        $doc = $ie.Document
        if (!$doc) 
        { 
            Write-Host "variable is null"
            return "SorryCantGiveYouAGenericClass" 
        }
        $answer = $doc.getElementByID("classname") 
        return $answer.innerHtml
    }

    Export-ModuleMember Get-ClassName

There is two part of this code, the function definition and the export command which I guess define which functions that are public from this module. The code is pretty straight forward, but here is a short description of the `Get-ClassName` function:

1. Create a IE object
2. Browse to get the page
3. Sleep while busy (should probably add a max loop counter)
4. Get the doc, which is the DOM of the web page
5. Query the doc object of the relevant element and extract the value

The next part is to write the script that will get the module imported, init.ps1, to the Package Manager Console. I basically copied the code from Phil Haack's [blog post](http://haacked.com/archive/2011/04/19/writing-a-nuget-package-that-adds-a-command-to-the.aspx) about the same topic. The code looks like: 

    param($installPath, $toolsPath, $package, $project)

    Import-Module (Join-Path $toolsPath ClassNamer.psm1)

When the above script is run it will import the module ClassNamer.psm1. Now all we need to do is to put both the PowerShell files in a tools folder and in the same folder as where we have tools folder we run the command `nuget spec ClassNamer`. The command will generate a ClassNamer.nuspec file for us with the default metadata. When we run nuget pack on the ClassNamer.nuspec file it will create a package with that contains the tools folder, and when we install the package in Visual Studio the init.ps1 script will be run by NuGet and install our module. If done correctly you will be able to get something like the image below when you've installed the package and run the new `Get-ClassName` command:

![ClassNamer screen shot]({{ site.url }}/assets/images/migrated/ClassNamer.png)