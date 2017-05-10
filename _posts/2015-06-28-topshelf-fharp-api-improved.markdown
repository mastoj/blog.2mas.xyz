---
layout: post
title: Topshelf F# api improved
date: '2015-06-28 17:05:01'
tags:
- fsharp
- topshelf
---

My plan was never to improve the F# api for [Topshelf](http://topshelf-project.com/), [Topshelf.FSharp](https://github.com/haf/Topshelf.FSharp), but [Henrik Feldt](https://twitter.com/henrikfeldt) asked me to when he saw what I was doing when working with a [Suave](http://suave.io/), which Henrik is a core contributor of, demo where I host the application in `Topshelf`. I implemented a simple Topshelf wrapper that had a nice, at least I think so, fluent api. The `Topshelf.FSharp` also had a fluent api, but it was somewhat more complicated so what I was asked to do was implement my concept in the existing `Topshelf.FSharp` project, and so I did (released as version 2.0.1 of the package).

## The API

The already existing API was good, but there some things that could be improved. The major plus with the existing function was that it already had existing functions for basically do every possible configuration of the service, so all I had to do was to find a nicer way to improve the fluent part of the API. The goal I had in mind, and what I implemented in my demo was an API looking somewhat like this:

    Service.Default
    |> with_start start
    |> with_recovery (ServiceRecovery.Default |> restart (min 10))
    |> with_stop stop
    |> run

It's really easy to follow and true to F#. There are many more functions you could execute before run to configure the service, but the most important ones are `with_start`, `with_stop` and `run`. The start and stop functions just take a single functions which are executed on start and stop and returns a bool and the run function is what executes the service and returns an `int` as expected. I won't cover any more details, but just look at the github repo if you want to know what configuration functions there is.

## Under the hood

So how do one build this type of API on top of another more OO oriented framework and one answer to this is to use a kind of builder pattern. All the functions before `run` function is executed just creates a specification of the service and what should happen when `run` is executed. The specification I ended up implementing for `Topshelf` look like this:

    type Service =
      { Start: HostControl -> bool
        Stop: HostControl -> bool
        HostConfiguration: (HostConfigurator -> HostConfigurator) list }
      static member Default =
          { Start = (fun _ -> true)
            Stop = (fun _ -> true)
            HostConfiguration = [] }

For a service to work you need a start and stop function, and that is what `with_start` and `with_stop` do. All the functions, except from `run`, take the `Service` type as the last parameter as well as returning a new instance of a `Service` making it possible to pipe the specification between all the configuration steps. The static `Default` member makes it easy to start the configuration. To configure the service all the configuration functions add a function to the list of `HostConfiguration` describing what should be done when the service is instantiated. This is done by a base function `add_host_configuration_step` which all the configuration functions partially applies like below:

    let add_host_configuration_step step service =
        {service with HostConfiguration = step::service.HostConfiguration}

    let enable_pause_and_continue =
        add_host_configuration_step (fun c -> c.EnablePauseAndContinue();c)

To start the service there are a couple of things we need to do, but first the code and then the description of it

    let toAction1 f = new Action<_>(f)
    let toFunc f = new Func<_>(f)

    let service_control (start : HostControl -> bool) (stop : HostControl -> bool) () =
      { new ServiceControl with
        member x.Start hc =
          start hc
        member x.Stop hc =
          stop hc }

    let create_service (hc:HostConfigurator) service_func =
      hc.Service<ServiceControl>(service_func |> toFunc) |> ignore

    let run service =
      let hostFactoryConfigurator hc =
          let createdHc = service.HostConfiguration |> List.fold (fun chc x -> x chc) hc
          service_control service.Start service.Stop
          |> create_service createdHc

      hostFactoryConfigurator |> toAction1 |> HostFactory.Run |> int

First we need to figure out what the we need to run the service, and the `Topshelf` `HostFactory.Run` method takes an `Action<HostConfigurator>`. To create action from a F# lambda I have a simple helper function, `toAction1`. And to create the actual lambda that takes a `HostConfigurator` we just create a function that has a single parameter `let hostFactoryConfigurator hc`, and now we can send this function to `toAction1` and we have what we need to run the service. To run the actual configuration in the `hostFactoryConfigurator` we just left fold over the `HostConfiguration` list with the first `HostConfiguration` as initial state and apply the functions. When we've done that we can just create our `ServiceControl` from the final `HostConfigurator`, after left fold, and pass in the start and stop functions and we're done.

## Improvements

The api can be improved a little bit, but now it works in a nice way. The major improvements that can be added is validation and create separate types for start and stop so we don't mix them. The validation wasn't in the API when I started implemented the change so I didn't add them now either and I don't think they are crucial either.
