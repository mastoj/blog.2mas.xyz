---
layout: post
title: Moving standard OWIN middleware to vNext middleware
date: '2014-11-09 17:50:48'
tags:
- owin
- asp-net
- vnext
- middleware
---

Earlier I wrote a post in how to create a OWIN middleware, http://blog.tomasjansson.com/useful-owin-middleware-healthcheck/. Of course I should have written it targeting [vNext](http://asp.net/vnext) but I didn't. One good thing about that is that I can explain how to create a vNext middleware from a simple OWIN middleware. 

Before we get started there are two tings:

1. vNext is changing, so the thing I show here might not be applicable when you read it. Most likely a interface might have been renamed or moved.
2. The main concepts are still quite similar with OWIN, so the migration shouldn't be to hard if your middleware is simple.

## Creating the middleware
All the code is here: https://github.com/mastoj/TJOwin. There you can find both the old standard OWIN middleware as well as the one targeting vnext. This part will only focus on the middleware and not the actual web application. The application I used to try the middleware is located on github and the startup code of what we want to achieve look like this:

    public class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            //TJOwin.HealthCheck.AppBuilderExtensions.UseHealthCheck(null);
            var config = new HealthCheckConfiguration();
            app.UseHealthCheck(config);
            app.Use(helloworldMiddleware);
        }

        private RequestDelegate helloworldMiddleware(RequestDelegate arg)
        {
            RequestDelegate rd = (context) => 
            {
                context.Response.WriteAsync("Hello world");
                return arg(context);
            };
            return rd;
        }
    }
    
In the `Configure` method you see the call to `UseHealthCheck(config)`, and that is where we start. But before you can add the actual code you need to add the project, which should be a `ASP.NET vNext Class Library`. I don't know if that will change, but you need that and not a standard class library.

### The ApplicationBuilderExtensions
To be able to call `UseHealthCheck` I need to create an extension method for `IApplicationBuilder`. The extension method class look like this:

    public static class AppBuilderExtensions
    {
        public static IApplicationBuilder UseHealthCheck(this IApplicationBuilder app, HealthCheckConfiguration config = null)
        {
            config = config ?? new HealthCheckConfiguration();
            return app.Use(next => new HealthCheckMiddleware(next, config).Invoke);
        }
    }
    
It might be hard to spot changes compared to OWIN here, but there are some. The `IApplicationBuilder` interface is new to start with. The `next` parameter is now of a type called `RequestDelegate` which has the following signature:

    public delegate Task RequestDelegate(HttpContext context);
    
Having a more explicit signature make it a little bit easier to use than before I think.

### The project file
The changes to the extensions class was all the code changes I did actually, except from that I just copied over all the old classes from the old project and everything worked. To get things to compile you need to add the right references to your `project.json` and in this case my `project.json` looked like:

    {
        "dependencies": {
            "Newtonsoft.Json": "6.0.5",
            "Microsoft.AspNet.Http": "1.0.0-alpha4"
        },

        "frameworks" : {
            "aspnet50" : { 
                "dependencies": {
                }
            },
            "aspnetcore50" : { 
                "dependencies": {
                    "System.Runtime": "4.0.20.0"
                }
            }
        }
    }
    
I added json.net for seralization and `Microsoft.AspNet.Http` since it contains the dll where `IApplicationBuilder` is defined.

I also opened up the properties of the project and changed to target `ASP.NET Core 5.0` which is the "core optimized" framework. This makes everything a little bit more lightweight and I don't have more in my application that I need.