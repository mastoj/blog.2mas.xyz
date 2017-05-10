---
layout: post
title: Npm build with F# FAKE
date: '2015-11-15 22:34:03'
tags:
- fsharp
- fake
- npm
---

Most web project to day has some javascript in them, and you should really build the javascript to minify them and also to find stupid errors. It would be stupid to implement the build part all over again in F#, instead you should use the tooling that already exists, like `node` and `npm`. Even though `npm` is used to build the javascript application I still want to control the overall build flow with FAKE, and for that reason I created the [FAKE NpmHelper](http://fsharp.github.io/FAKE/apidocs/fake-npmhelper.html).

## Configure FAKE

The easiest way to get started is to install `node` and `npm` with `nuget` as part of the `build.cmd` before calling `build.fsx`. This will add `npm` to the default paths that is used by the helper. Don't worry, it is possible to override which `npm` file that should be used. A sample `build.cmd` can be found in my FAKE [sample](https://github.com/mastoj/FAKESimpleDemo) and looks like this:

```
echo off
cls
NuGet.exe "Install" "FAKE" "-OutputDirectory" "packages" "-ExcludeVersion"
NuGet.exe "Install" "OctopusTools" "-OutputDirectory" "packages" "-ExcludeVersion"
NuGet.exe "Install" "Node.js" "-OutputDirectory" "packages" "-ExcludeVersion"
NuGet.exe "Install" "Npm.js" "-OutputDirectory" "packages" "-ExcludeVersion"
"packages\FAKE\tools\Fake.exe" build.fsx %*
```

## Supported commands

There are only two supported commands where you get some type check, `Install` and `Run`. Below is the simplest possible sample to use those two.

```language-fsharp
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
```

I figured those two commands are the one you usually would like to run in this kind of scenario, but if you do want to run any of the other `npm` commands you can do so by using the `Custom` command parameter and just pass in the string you like. Or if it is something you think is commonly used send a PR or ping me about it :).

That's all, let me know if you have any questions.