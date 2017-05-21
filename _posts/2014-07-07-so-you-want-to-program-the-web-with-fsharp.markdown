---
layout: post
title: So you want to program the web with FSharp?
date: '2014-07-07 18:11:38'
tags:
- functional
- owin
- simple-web
- fsharp
---

I couldn't find any really great how tos about doing web programming with FSharp, so I thought I would figure it out myself my own way.

# The problem
The problem with web programming and FSharp is two as I see it right now. One is regarding the tooling, the default tooling from Visual Studio which I think most people use doesn't have a project template for web together with FSharp. The second part is that a lot of web frameworks are mutable in one way or another so it is hard doing pure development. With that said I'll show I way/work around in how to do web development on OWIN in general and with Simple.Web to be more specific.

# FSharp and OWIN
To get started you start of as you should with any web project, create an empty C# web project.

![New project]({{ site.url }}/assets/images/migrated/NewProject.PNG)

I'm going for a windows hosting so also install the nuget package `Microsoft.Owin.Host.SystemWeb`. The next thing we are going to do is add a second project, this time a FSharp library. Update the `Library.fs` file so it looks something like this:


    namespace Web2Empty.Web
    open Owin

    type OwinAppSetup() =
        member this.Configuration (app:IAppBuilder) = 
            app.Run(fun c -> c.Response.WriteAsync("Hello FSharp World!"))

That is going to be your OWIN entry point. To get this to compile you need to add `Owin` nuget package to the FSharp project. When it compiles you add a line like this to your `Assembly.cs` file in the C# project and add a project reference to the FSharp project: 

    [assembly: OwinStartup(typeof(OwinAppSetup))]

This will tell the OWIN host to use the `OwinAppSetup` class which is defined in the FSharp class library to run the web application. If you run the application now you should get some output on the screen.

# FSharp and Simple.Web
The last couple of months I've come to like Simple.Web more and more, so I wanted to try to get that to work together with FSharp. To really put it to a test I wanted to use discriminated unions as well.

## Adding Simple.Web
Adding `Simple.Web` with json support is quite easy, just install the `Simple.Web.JsonNet` package in the FSharp project and you'll get the dependencies you packages. When it is installed you need to update `Newtonsoft.Json` to the latest version to get the FSharp support. 

## The sample data
The data structure I'll use in my requests look like this

    type Life =
        | Great of reason: string
        | Sucks of reason: string
        | Complex of Life * Life

    type LifeContainer = {Life: Life}

For some reason I need the container to be able to serialize the request when using Simple.Web, it doesn't bother me that much so I can live with it.

## Creating the handlers
`Simple.Web` is using a handler/class per route per request model, so to handle this I need to create one FSharp type per route per request. To support a `GET` request you need something like this:

    [<UriTemplate("/")>]
    type RootGetEndPoint() = 
        interface IGet with
            member this.Get() = Status.OK
        interface IOutput<LifeContainer> with
            member this.Output = {Life=Complex(Great("FSharp"), Sucks("Inheritance"))}

Here we first have the `UriTemplateAttribute` that defines which url this handler should handle. After that it is straight forward implementation of the interfaces we need to answer a request.

For the `POST` endpoint we don't need to use the container for the input but we still need it for the output: 

    [<UriTemplate("/")>]
    type RootPostEndPoint() = 
        let mutable life:Option<Life> = None
        interface IPost with
            member this.Post() = Status.OK    
        interface IInput<Life> with
            member this.Input with set(value) = life <- Some(value)
        interface IOutput<LifeContainer> with
            member this.Output = {Life = life.Value}

This handler takes the input and returns it with a 200 response.

The last part is the update of the `OwinAppSetup`: 

    type OwinAppSetup() =
        static member enforcedRefs = [typeof<Simple.Web.JsonNet.JsonMediaTypeHandler>] 
        member this.Configuration (app:IAppBuilder) = 
            JsonConvert.DefaultSettings <- fun () -> 
                let settings = new JsonSerializerSettings()
                settings.TypeNameHandling <- TypeNameHandling.Objects
                settings
            app.Run(fun c -> 
                Application.App(
                    fun _ -> 
                        c.Response.WriteAsync("Hello FSharp world!")).Invoke(c.Environment))

The first static part is to load the media type handler, I don't know a better way of doing it at the moment for Simple.Web and I don't think it exist a better way at the momement. The next part is the configuration part where I first configure `Json.NET` and then setting up the application. The application is using `Application.App` from `Simple.Web` to configure `Simple.Web` but we still run the next step if we didn't found a route. So if you go anywhere except for the root, "/", you'll see "Hello FSharp world!".

Witht that finished it is now possible to post discriminated unions like: 

    {
        "case":"Complex",
        "fields":[
            {"case":"Great","fields":["FSharp"]},
            {"case":"Sucks","fields":["What"]}
        ]
    }

This will return in a 200 and you'll get a container back with that `Life`. 

The full code for the fsharp file looks like this: 

    namespace Web2Empty.Web
    open System.Text
    open Owin
    open Simple.Web
    open Simple.Web.Behaviors
    open Simple.Web.MediaTypeHandling
    open Newtonsoft.Json

    type Life =
        | Great of reason: string
        | Sucks of reason: string
        | Complex of Life * Life

    type LifeContainer = {Life: Life}

    [<UriTemplate("/")>]
    type RootGetEndPoint() = 
        interface IGet with
            member this.Get() = Status.OK
        interface IOutput<LifeContainer> with
            member this.Output = {Life=Complex(Great("FSharp"), Sucks("Inheritance"))}

    [<UriTemplate("/")>]
    type RootPostEndPoint() = 
        let mutable life:Option<Life> = None
        interface IPost with
            member this.Post() = Status.OK    
        interface IInput<Life> with
            member this.Input with set(value) = life <- Some(value)
        interface IOutput<LifeContainer> with
            member this.Output = {Life = life.Value}

    type OwinAppSetup() =
        static member enforcedRefs = [typeof<Simple.Web.JsonNet.JsonMediaTypeHandler>] 
        member this.Configuration (app:IAppBuilder) = 
            JsonConvert.DefaultSettings <- fun () -> 
                let settings = new JsonSerializerSettings()
                settings.TypeNameHandling <- TypeNameHandling.Objects
                settings
            app.Run(fun c -> 
                Application.App(
                    fun _ -> 
                        c.Response.WriteAsync("Hello FSharp world!")).Invoke(c.Environment))

# Ending note
I did not work much on structuring the code into separate files etc. since it was not my goal, I just wanted it to work. Also, I'm not sure if this is the recommended way of doing things, but it works. Of course it would be better if you just needed one project instead of two, but this is a nice work around until that time. I'm not sure, but you might be able to manipulatae the `.fsproj` file to get it working without the extra project, but that is just a wild guess.

As you can see, there is nothing stopping us from doing all the code on the server in FSharp, we do miss some code generation support from Visual Studio with scaffolding, but that is something I want miss since I haven't found it useful for "real" business applications.
