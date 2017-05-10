---
layout: post
title: 'ASP.NET 5: Adding MVC to an application'
date: '2015-04-26 14:58:46'
tags:
- asp-net-mvc
- net
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

## Introducing the project.json file

The "project.json" file is basically your new csproj-file. There you store all the references for your project as well as other project specific features. You will have intellisense "towards" nuget while editing dependencies, which make it almost easier to edit this file instead of using the UI. I won't cover what every little detail is in the file, only what needed to finish the next step. So to add MVC to the project just modify the depdencies property to look like this:

    "dependencies": {
        "Microsoft.AspNet.Server.IIS": "1.0.0-beta4",
        "Microsoft.AspNet.Mvc": "6.0.0-beta4"
    },

That's version of MVC that's available while writing this post.

## Configure the application (updating the pipeline)

To start using the MVC "middleware" we need to add it to the pipeline. If a route matches one that MVC will handle that will end the pipeline, otherwise the request will just pass right through. Most middleware that Microsoft implement and other framework most likely will implement extension methods for the `IApplicationBuilder` interface to make it easier to add the middleware. So to add MVC to the pipeline the `Configure` method must be updated like so:

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc(rb =>
            {
                rb.MapRoute(
                    name: "default",
                    template: "{controller}/{action}/{id?}",
                    defaults: new {controller = "Home", action = "Index"});
            });

            app.Use(async (context, next) =>
            {
                await context.Response.WriteAsync("Hello world!");
            });
        }

All we're doing in the `Action` in the `UseMvc` method is setting up the routes as we would with an old MVC app, nothing has changed there.

## Adding Controller and View

This step is basically exactly the same as with the old Mvc, so I won't cover it in detail. Just add the `HomeController`:

    public class HomeController : Controller
    {
        public IActionResult Home()
        {
            return View();
        }
    }

and a `Index` view:

    @{
        Layout = null;
    }

    <!DOCTYPE html>

    <html>
    <head>
        <title>Hello</title>
    </head>
    <body>
        <div>
            <h1>Hello from MVC</h1>
        </div>
    </body>
    </html>

If you try to run the application now you'll get an error like:

    Unable to find the required services. Please add all the required services by calling 'IServiceCollection.AddMvc()' inside the call to 'IApplicationBuilder.UseServices(...)' or 'IApplicationBuilder.UseMvc(...)' in the application startup code.

What that means is that we have added MVC to the pipeline, but we haven't set up all internals of the middleware. And that is the next step.

## Configure services

To "hook" anything in the dependency resolving in ASP.NET 5 you need to add what you're trying to resolve to a `IServiceCollection`, and you do that in the `ConfigureServices` method in the "Startup.cs" file. Again we have extension methods, which I guess will be provided by most middleware creators, for the `IServiceCollection` interface. So to configure MVC, all you need to do is:

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc(rb =>
            {
                rb.MapRoute(
                    name: "default",
                    template: "{controller}/{action}/{id?}",
                    defaults: new {controller = "Home", action = "Index"});
            });

            app.Use(async (context, next) =>
            {
                await context.Response.WriteAsync("Hello world!");
            });
        }
    }

## Attribute routing

Now we have a working MVC application, but I thought we would make use of attribute routing which is new for MVC but has been available for Web API some time. You can mix standard routing and attribute routing if you want, but I like to use just attribute routing. So let's first remove the old routing:

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc();

            app.Use(async (context, next) =>
            {
                await context.Response.WriteAsync("Hello world!");
            });
        }
    }

Now nothing will work, so let's fix that by adding attribute routing to the `HomeController`:

    [Route("[controller]"), Route("/")]
    public class HomeController : Controller
    {
        [Route("[action]"), Route("")]
        public IActionResult Index()
        {
            return View();
        }
    }

I've basically added the same routing here but as attributes instead. For the controller I've added two routes, the default `"/"` and also one that matches the name of the controller `"[controller]"`. I did the same thing for the action, but with the action instead. If you start the application now and go to "/", "/Home" or "/Home/Index" the `Index` action will be called.

## Summary

This was the end of post two in this serie of posts. In the next step I'll add some simple functionality and try to style it using bootstrap. To be able to use bootstrap we must set up our client side build steps, and for that [Gulp](http://gulpjs.com/) will be used.