---
layout: post
title: Owin middleware
date: '2013-11-18 09:42:00'
tags:
- net
- owin
- asp-net
---

Owin middleware

In the last [post](http://blog.tomasjansson.com/hello-owin) I showed you how to create a simple "Hello World" application, the next step is to take advantage of the Owin pipeline.

I'm going to use the application from the [last post](http://blog.tomasjansson.com/hello-owin) as a starting point for this blog post and add a example (stupid) authentication middleware, but first I will make a short recap of the previous post and also show you a really simple middleware. Let's get started.

As in the previous post I'm using a self hosted solution using the HttpListener from the [Katana Project](http://katanaproject.codeplex.com/), which is a open source project initiated from Microsoft's to aid development of OWIN-based web applications. I will go into more detail in different kinds of hosting in a later post, but as of now it will do with the self hosting solution from the Katana Project. The basic of this applications has two files, "Program.cs":

    using System;
    using Microsoft.Owin.Hosting;

    namespace HelloOwin
    {
        internal class Program
        {
            private static void Main(string[] args)
            {
                using (WebApp.Start<Startup>(url: "http://localhost:1337/"))
                {
                    Console.WriteLine("Application is running");
                    Console.ReadLine();
                }
            }
        }
    }

and "Startup.cs":

    using Owin;

    namespace HelloOwin
    {
        public class Startup
        {
            public void Configuration(IAppBuilder app)
            {
                app.Run(context =>
                {
                    var task = context.Response.WriteAsync("Hello world! " + context.Request.Path);
                    return task;
                });
            }
        }
    }

Running this application will print "Hello world!" and the requested paths. The way middleware works is that it got a reference to the next step in the pipeline and when it is done with its own processing it just calls the next step. If you really think about it middleware and application is actually the same thing, the difference is that an application just ignores to call the next step in the pipeline and therefor this is where the pipeline ends. Every call to the next middleware returns a task, which allows every middleware to execute some functionality when that task is finished making it possible to process the request on the way back again, this processing is done in the reverse order of the middleware definition. A simple illustration of middleware is seen below:

![Owin pipeline][1]

Let's get started with our first simple middleware. Let's say that we want to add some text to every start and end of the response stream, that's a simple thing to do with a middleware and it also captures most of the concepts involving middleware. To do so let's modify the "Startup.cs"

    using Owin;

    namespace HelloOwin
    {
        public class Startup
        {
            public void Configuration(IAppBuilder app)
            {
                app.Use((context, next) =>
                {
                    context.Response.WriteAsync("PIMPING IT! ");
                    return next().ContinueWith(task =>
                    {
                        context.Response.WriteAsync(" DONE PIMPING IT! ");
                    });
                });
                app.Use((context, next) =>
                {
                    context.Response.WriteAsync("PIMPING IT MORE! ");
                    return next().ContinueWith(task =>
                    {
                        context.Response.WriteAsync(" DONE PIMPING IT MORE! ");
                    });
                });

                app.Run(context =>
                {
                    var task = context.Response.WriteAsync("Hello world! " + context.Request.Path);
                    return task;
                });
            }
        }
    }
	
In the example above I've added to simple and easy to understand middlewares, both are actually doing the same thing but this will show you in which order the middlewares get executed on each request. But first we'll focus on one of them: 

	app.Use((context, next) =>
	{
		context.Response.WriteAsync("PIMPING IT! ");
		return next().ContinueWith(task =>
		{
			context.Response.WriteAsync(" DONE PIMPING IT! ");
		});
	});

First of, this is also using the `Use` extension method defined in the Owin assembly from the Katana Project. The `Use` method takes a `Func<IOwinContext, Task, Task>` as a parameter. Breaking it down gives us; an environment `IOwinContext` which is a wrapper over a `IDictionary<string, object>` that contains all the data in the request and response. The first `Task` is the next step in the pipeline and the last `Task` is just the return type of the `Func`. The simple example above will first print "PIMPING IT!" before the actual response and after the response it will add "DONE PIMPING IT!". The `next()` returns a new `Task` which is from the next step in the pipeline, and to that `Task` we can attach a function to get executed when finished. That is exactly what we are doing with the call to `ContinueWith`.

Putting both those middlewares to work at the same time will give us a response like:

    PIMPING IT! PIMPING IT MORE! Hello world! / DONE PIMPING IT MORE! DONE PIMPINT IT!
	
As you see the order in which the middlewares got executed are reversed on the way back.

## Stepping it up a notch

To show some more of the features and how to interact with the Owin environment I'll show you a simple and stupid authentication middleware. The goal of the middleware is to reject all request to a given url, and redirect to a login url if you try to access it without being authenticated. After visiting the login url you'll get automatically authenticated and a "user cookie" is set to show that your authenticated.

 > **NOTE:** this is not a demo of how to write a good authentication middleware, it is a deeper demo to middleware than the first demo 
 
 When writing more advanced middleware it most likely will be more common to encapsulate the middleware in a class instead of a `Func` the you pass the call to `Use`. An example of that could look something like:
 
	public class Startup
	{
		public void Configuration(IAppBuilder app)
		{
			var options = new StupidAuthOptions() { LoginPath = "/login", SecurePath = "/secure" };
			app.Use(typeof(StupidAuth), options);

			app.Run(context =>
			{
				var task = context.Response.WriteAsync("Hello world! " + context.Request.Path);
				return task;
			});
		}
	}

In the configuration code above I first define a set of options that I want to pass to my middleware, these options will be passed to the constructor when creating the middleware later on. So on the next line I'm configuring the `app` what the type of my middleware is and that I want to pass the options parameter when it is instantiated. The `StupidAuthOptions` is a really simple POCO: 

    public class StupidAuthOptions
    {
        public string SecurePath { get; set; }
        public string LoginPath { get; set; }
    }
	
I will now show you the actual middleware implementation step by step. The first part is the constructor and the `Invoke` method: 

	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;

	namespace HelloOwin
	{
		public class StupidAuth
		{
			private Func<IDictionary<string, object>, Task> _next;
			private StupidAuthOptions _options;

			public StupidAuth(Func<IDictionary<string, object>, Task> next, StupidAuthOptions options)
			{
				_next = next;
				_options = options;
			}

			public Task Invoke(IDictionary<string, object> environment)
			{
				....
			}
			....
		}
	}

A lot of the code is using conventions, and you see some of them in the example above. As you see the middleware doesn't implement any interface, but that is something you could add your self. The constructor takes as the first argument a `Func` which basically is the next step in pipeline. Calling this `Func` is executing the next step, but you always have the opportunity to not call the pipeline and making your middleware the final step. The `Invoke` method takes in the environment, as a `IDictionary`, with all relevant information for the request. If you think about it the signature of the `Invoke` method basically is the same as `Func<IDictionary<string, object>, Task>`. The return of a `Task` makes it easy to chain middleware and also execute a block of code on the way as I showed earlier.

I won't go into detail in the rest of the code, but I'll at least show it to you: 

    public class StupidAuth
    {
        private Func<IDictionary<string, object>, Task> _next;
        private StupidAuthOptions _options;
        private Dictionary<string, Func<IDictionary<string, object>, Task>> _requestDispatcher; 

        public StupidAuth(Func<IDictionary<string, object>, Task> next, StupidAuthOptions options)
        {
            _next = next;
            _options = options;
            _requestDispatcher = new Dictionary<string, Func<IDictionary<string, object>, Task>>()
            {
                {_options.LoginPath, LoginHandler},
                {_options.SecurePath, SecureHandler}
            };
        }

        private Task SecureHandler(IDictionary<string, object> environment)
        {
            if (!IsAuthenticated(environment))
            {
                return Redirect(environment, _options.LoginPath);
            }
            WriteToResponseStream(environment, "You are watching super secret stuff! ");
            return _next(environment);
        }

        private Task LoginHandler(IDictionary<string, object> environment)
        {
            if (IsAuthenticated(environment))
            {
                return WriteToResponseStream(environment, "Logged in!");
            }
            AddCookie(environment, "user", "john doe");
            return WriteToResponseStream(environment, "Logging in!");
        }

        public Task Invoke(IDictionary<string, object> environment)
        {
            var path = environment["owin.RequestPath"] as string;
            Func<IDictionary<string, object>, Task> handler;
            if (_requestDispatcher.TryGetValue(path, out handler))
            {
                return handler(environment);
            }
            return _next(environment);
        }

        private Task WriteToResponseStream(IDictionary<string, object> environment, string message)
        {
            var response = environment["owin.ResponseBody"] as Stream;
            var streamWriter = new StreamWriter(response);
            return Task.Factory.StartNew(() =>
            {
                streamWriter.Write(message);
                streamWriter.Dispose();
            });
        }

        private bool IsAuthenticated(IDictionary<string, object> environment)
        {
            var requestHeaders = environment["owin.RequestHeaders"] as IDictionary<string, string[]>;
            if (requestHeaders.ContainsKey("Cookie"))
            {
                var cookies = requestHeaders["Cookie"];
                var parsedCookie = ParseCookies(cookies[0]);
                return parsedCookie.ContainsKey("user");                
            }
            return false;
        }

        private IDictionary<string, string> ParseCookies(string cookies)
        {
            return cookies.Split(';')
                .Select(y => Uri.UnescapeDataString(y.Trim()).Split('='))
                .ToDictionary(y => y[0], y => y[1]);
        }

        private void AddCookie(IDictionary<string, object> environment, string key, string value)
        {
            var setCookieValue = string.Concat(
                Uri.EscapeDataString(key),
                "=",
                value);

            SetResponseHeader(environment, "Set-Cookie", setCookieValue);
        }

        private Task Redirect(IDictionary<string, object> environment, string loginPath)
        {
            SetResponseHeader(environment, "Location", loginPath);
            environment["owin.ResponseStatusCode"] = 302;
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);
            return tcs.Task;
        }

        private void SetResponseHeader(IDictionary<string, object> environment, string key, string value)
        {
            var headers = environment["owin.ResponseHeaders"] as IDictionary<string, string[]>;
            headers = headers ?? new Dictionary<string, string[]>();
            headers.Add(key, new [] {value});
        }
    }

The way I choose to structure the code is to keep a mapping of the path's this middleware will handle in a dictionary pointing to different handlers, and than use the `Invoke` method to find the correct dispatcher and handle the request. The rest of the code is just helper methods for setting headers or creating a redirect response. I thought I would show it since might help you get started writing more advanced middleware than this stupid authentication handler.

## Summary
Writing middleware is something that I think we will be doing a lot when we have changed our mind of thinking. As I showed you with some simple examples it could be really simple to write a piece of middleware, but I think you also realize that it could be really hard depending on what you are trying to solve. One question I didn't answer here is why you would like to use middleware? Writing good middleware will allow you to reuse them over and over again, you could stream line how you do things in your organization for example. Maybe you have a special setup of static files or you want all your web applications to use exactly the same authorization mechanism. I really look forward to see what kind of middleware that will pop up in the ecosystem.


  [1]: https://qbtmcq.dm2302.livefilestore.com/y2pCgbiQedecsgVAzavyDd0MHMy5oIqWUayyuWMX2u119bfjXdyuXqiom8p90qhUA9rsOsVUqT8rQOz5GT4ZCilMIgAUmhSkEWfcB_MAig2uNA/SimpleMiddlewarePipeline.png?psid=1