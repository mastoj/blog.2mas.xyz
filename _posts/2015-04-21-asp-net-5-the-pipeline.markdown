---
layout: post
title: 'ASP.NET 5: The pipeline'
date: '2015-04-21 11:46:07'
tags:
- net
- asp-net
- mvc-6
- asp-net-5
---

A couple of weeks ago I had a presentation about ASP.NET 5 and MVC 6 at NNUG Oslo. The presentation wasn't recorded so I thought I just write some blog posts about it insted. This will be a serie of posts where I plan to go through the features that I demonstrated during the presentation, plus some more features that I didn't have time to cover. I'll start with the basic and show one thing at a time and then add features as we go along. So let's get started.

Post in this serie:

* [The pipeline](http://blog.tomasjansson.com/asp-net-5-the-pipeline/)
* [Adding MVC to an application](http://blog.tomasjansson.com/asp-net-5-adding-mvc-to-an-application)
* [Setting up frontend build (with grunt)](http://blog.tomasjansson.com/asp-net-5-setting-up-frontend-build-with-grunt/)
* [IoC and dependency injection](http://blog.tomasjansson.com/asp-net-5-ioc-and-dependency-injection/)
* [View Components](http://blog.tomasjansson.com/asp-net-5-view-components/)
* [Self-hosting the application](http://blog.tomasjansson.com/asp-net-5-self-hosting-the-application/)
* [Hosting your application in docker](http://blog.tomasjansson.com/asp-net-5-hosting-your-application-in-docker/)

Source code: https://github.com/mastoj/OneManBlog

## Introduction

ASP.NET 5 has taken a lot of inspiration from other web framework out there like ruby, nodejs and and the [Open Web Interface for .NET](http://owin.org/), OWIN. Microsoft made an implementation of the OWIN specification in the project called [Katana](http://katanaproject.codeplex.com/documentation) as an experiment. With experience gained from the Katana project Microsoft, and the ASP.NET team, began implementing ASP.NET 5. There are "adapters" available to plugin OWIN components in the pipeline for ASP.NET 5, even though there are some major differences between ASP.NET 5 and OWIN.

## What is this pipeline?

I'm not sure if pipeline is the "official" way to describe it but that's a mental model that I can relate to. 

{<1>}![The pipeline]({{ site.url }}/assets/images/migrated/Pipeline.png)

The way it works is that the browser makes a request to the server, the server forwards the request to the host listen to that port. The hosting environment could be a IIS on Windows or self host environemnt on Linux or Windows. The hosting environment start to execute the application pipeline starting with the first middleware. A piece of middleware can do two things as I see it:

* Respond directly and not execute the rest of the pipeline (it will still execute the middleware that's before it in the pipeline)
* Manipulate the response in one way or another, that could be authentication or add some extra data to a response

Usually application frameworks like ASP.NET MVC falls under the first category and frameworks that are cross cutting like authentication falls under the second one. This might be a simplified view of it but that's how I see it.

## The simplest possible thing

To understand The first thing we are going to do is create a simple "Hello world" application that we kan use directly from Visual Studio and the console. I won't cover cross platform development here, but the application will be able to run on other platforms. Also, I'm using a pre-release version of Visual Studio 2015 and ASP.NET 5 so things might change, but it is quite stable now I think.

The goal of the application we are developing is a simple blog engine for a single person, it doesn't make much sense but it will make it possible to show of the features I want to demonstrate. 

## Creating the application

Open visual studio and choose to create a new "ASP.NET Web Application", I called my application "OneManBlog". In the next step choose "ASP.NET 5 Preview Empty", this will most likely change name to something else later on. The main point is that it should be empty and it is targeting the new framework.

## Hello world!

To get started we open up the "Startup.cs" file and change the content to:

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                await context.Response.WriteAsync("Hello world!");
            });
        }
    }
    
The code above is all you need to get your "Hello world!" message. You also got the pipeline started. If we want to add another step to our pipeline we just copy the `app.Use` code like such:

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.Use(async (context, next) =>
            {
                await context.Response.WriteAsync("Hello world!");
                await next();
            });
            app.Use(async (context, next) =>
            {
                await context.Response.WriteAsync("This is sort of an echo Hello world!");
            });
        }
    }

Also notice that I updated the first function call to make a call to `next`, otherwise the next step in the pipeline wouldn't execute.

That is all you need to actually get started writing web applications in ASP.NET 5. If you look through the `context` variable in the `Func` that's passed to the `Use` method you see that you basically have access to the whole request and response and can start play with that. If you want to play around with this piece of code you can try to update it to only respond to a specific url, and/or a special header is in the request.

## Summary

This was the first post in how to get started with ASP.NET 5. The goal was to show you the simplest building block available for ASP.NET 5. In the next post I'll add MVC to the application since it will be difficult to manage and application if all you do is writing `Func` expressions as those above, and adding an abstraction like MVC will help you.
