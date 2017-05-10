---
layout: post
title: Creating a NuGet package
date: '2012-10-25 07:10:00'
tags:
- nuget
---

If you read my last post you probably know that NuGet is here to stay. To start using NuGet packages, either throught the Package Manager Console or the "Add Library Package Reference" dialogue box, shouldn't be that hard to figure out. But what about creating packages? It turns out it is almost as simple as using them. I will try to cover the most basic scenarios in this post about how to create your own package and get it up on [nuget.org](http://www.nuget.org/). In the rest of the post I assume you have signed up on [nuget.org](http://www.nuget.org) and you have found your access key under your account.

### Ok, so let's get busy
In the previous version of NuGet, it is currently on version 1.2, it wasn't possible to create a package directly from the csproj-file, but now it is. So in the most basic scenarios all you need to do create a package is run to runt the `nuget pack` command on a csproj-file like:

    c:\path\to\your\project>nuget pack DoSomeAwesomeStuff.csproj

Running that command will make the project build and create a .nupkg file that is your first NuGet package!. That wasn't hard, was it? But it is more to it, when running that command NuGet will look in you project and see if you have any NuGet references in your project and include those as NuGet references, which is great! But, there is one feature that is lacking if you ask me... and that is that the content folder, and most likely the tool folder as well, is not added by convention when running this command. Luckily for us it is not that hard to create a work around for this issue. Instead of running the above command you can use:

    c:\path\to\your\project>nuget spec DoSomeAwesomeStuff.csproj

which will create a NuGet spec-file for that project. The file will look something like: 

	<?xml version="1.0"?>
	<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
	  <metadata>
		<id>$id$</id>
		<version>$version$</version>
		<authors>$author$</authors>
		<owners>$author$</owners>
		<licenseUrl>http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE</licenseUrl>
		<projectUrl>http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE</projectUrl>
		<iconUrl>http://ICON_URL_HERE_OR_DELETE_THIS_LINE</iconUrl>
		<requireLicenseAcceptance>false</requireLicenseAcceptance>
		<description>$description$</description>
		<tags>Tag1 Tag2</tags>
	  </metadata>
	</package>

All the metadata tags, the ones like `$tagName$`, will be replaced by the value in the `assembly.cs` file if you don't change it to some constant value. When you have this file you can start modifying it to include some content files for example. Content files are files that are placed in the content folder in the created NuGet package, all files in the content folder will be copied to project where the package is installed. So one of my final spec-files look like this: 

	<?xml version="1.0"?>
	<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
	  <metadata>
		<id>$id$</id>
		<version>$version$</version>
		<authors>Tomas Jansson</authors>
		<owners>Tomas Jansson</owners>
		<licenseUrl>https://github.com/mastoj/SimpleCompression/blob/master/SimpleCompression/license.txt</licenseUrl>
		<projectUrl>https://github.com/mastoj/SimpleCompression</projectUrl>
		<iconUrl>https://github.com/mastoj/SimpleCompression/raw/master/SimpleCompression/SimpleCompression_icon.png</iconUrl>
		<requireLicenseAcceptance>false</requireLicenseAcceptance>
		<description>$description$</description>
		<tags>ASP.NET MVC3 Compression javascript css</tags>
	  </metadata>
	  <files>
		<file src="content\App_Start\*.*" target="content\App_Start" />
	  </files>
	</package>

As you can see I've swapped some of the metadata parameters against static values that is not present in the `assembly.cs` file, so it is no problem at all to mix static values with generic ones. But the most important part is that I've added an element to the root node, `files`, which specify where I have the content I want to include and where I want it to go in the package. That's all you need to do if you want to include one or more files, in the above example I'm including a startup file for example.

When I now run the `nuget pack` command on the csproj-file, *not* the spec-file, NuGet will look at the spec file and include the content files I have specified and the NuGet references will still be added since they are specified in the .csproj-file. It will also use the metadata from the `assembly.cs` to update the metadata for the package.

The last step in the process is to submit the package to [nuget.org](http://www.nuget.org/), and that is as simple as creating the package. I recommend to do it in two steps the first time, set api access key and the submit. When you have set the api access key you don't have to write it every time you submit or update a package. So here are the commands:

    c:\path\to\your\project>nuget setApiKey xxxxxx-xxxx-xxxx-xxxx-xxxxxx
    c:\path\to\your\project>nuget push YourProject.x.x.nupkg


I think that is all to it. Of course there are some other ways as well, like creating spec-file from the dll/exe-file for example, but this is the way I think is most efficient way to do it and will probably be the way I'll create NuGet packages in the future. Of course it would be nice if you could specify everything in the project so you didn't have to update the spec-file, but that seems to be something I'll have to live with.

In a coming post I'll show an example of how you can use a NuGet package to extend the functionality of the Package Manager Console, that is creating your own PowerShell commands that gets included in the Console.