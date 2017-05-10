---
layout: post
title: Hello Owin!
date: '2013-11-11 09:41:00'
tags:
- net
- owin
- asp-net
---

I'm planning to write a couple of short posts describing how to get started with Owin and how Owin works. This first will show you how to create the simplest self hosted "Hello World" application.

Let's get started
When creating a self hosted Owin application you don't have to create the project from the web application project template in Visual Studio, or you really shouldn't since the whole point with self hosting is that you control the hosting. So in Visual Studio create a standard console application. The next thing you need to do is installing four [NuGet packages](http://www.nuget.org); Owin, Microsoft.Owin, Microsoft.Owin.Hosting and Microsoft.Owin.Host.HttpListener. There is a dependency from Microsoft.Owin.Hosting to Owin and Microsoft.Owin so in reality you onle need to run the two following commands in the package manager console:

    install-package Microsoft.Owin.Hosting
	install-package Microsoft.Owin.Hosting.HttpListener
	
The Microsoft.* packages comes with a lot of helper functionality that will make it easier to get started. 

The next thing to do is to write the application, let's get right to the code:

    using System;
    using Microsoft.Owin.Hosting;
    using Owin;

    namespace HelloOwin
    {

        class Program
        {
            static void Main(string[] args)
            {
                using(WebApp.Start<Startup>(url: "http://localhost:9765/"))
                {
                    Console.WriteLine("Appication is running");
                    Console.ReadLine();
                }
            }
        }
	}
	
As you can see this is a regular console application with the common `Main` method. When you run this application the `WebApp.Start` method will start an application of type `Startup` with the url ´http://localhost:9765´. The next thing to do is to write the actual application, that is, the `Startup` class.

    using Owin;

    namespace HelloOwin
    {
        public class Startup
        {
            public void Configuration(IAppBuilder app)
            {
                app.Run(context =>
                {
                    var task = context.Response.WriteAsync("Hello world!");
                    return task;
                });
            }
        }
    }
	
By convention the `Configuration` method, that has the same signature as the one in the example, is run to configure the application. The `app.Run` method takes a `Func` as parameter that should take the Owin context as a parameter. The `Func` must return a `Task`.

In the next post I will cover middleware and how you can setup multiple handlers for a request.