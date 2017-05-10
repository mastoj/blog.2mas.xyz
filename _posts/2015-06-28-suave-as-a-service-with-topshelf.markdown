---
layout: post
title: Suave as a service with Topshelf
date: '2015-06-28 17:14:00'
tags:
- fsharp
- topshelf
- suave
---

As you can read in [Topshelf F# api improved](http://blog.tomasjansson.com/topshelf-fharp-api-improved/) I started working on a demo, but it magically grown to include a change to the `Topshelf.FSharp` project. The good part with this is that to run `Suave` with `Topshelf` as a Windows service have never been easier than now. The example code will be made available official examples for Suave, I hope, but meanwhile you can find the code on [github](https://github.com/mastoj/SuaveTopShelfDemo).

## The code <tl;dr;>

```
open Suave
open Suave.Http.Successful
open Suave.Web 
open Suave.Http
open Suave.Http.Applicatives
open Suave.Http.Successful
open Topshelf
open System
open System.Threading

[<EntryPoint>]
let main argv = 
    printfn "%A" argv

    let cancellationTokenSource = ref None

    let home = choose [path "/" >>= GET >>= OK "Hello world"]
    let mind = choose [path "/mind" >>= GET >>= OK "Where is my mind?"]
    let app = choose [ home; mind ]

    let start hc = 
        let cts = new CancellationTokenSource()
        let token = cts.Token
        let config = { defaultConfig with cancellationToken = token}

        startWebServerAsync config app
        |> snd
        |> Async.StartAsTask 
        |> ignore

        cancellationTokenSource := Some cts
        true

    let stop hc = 
        match !cancellationTokenSource with
        | Some cts -> cts.Cancel()
        | None -> ()
        true

    Service.Default 
    |> display_name "ServiceDisplayName"
    |> instance_name "ServiceName"
    |> with_start start
    |> with_stop stop
    |> with_topshelf
```

## What is going on?

I don't think the code need much explanation, but here are some lines. First we create a `CancellationToken` which we pass to the `start` function. The `stop` can then use the `CancellationTokenSource` to cancel the `async` operation that we start in the `start` function. Right before `start` we define our `Suave` app, it consist of two web parts, `home` and `mind` which are combined to one app in `app`. When the `start` and `stop` functions are defined it is trivial to use the new updated version of `Topshelf.FSharp` to run the suave application as a service.
