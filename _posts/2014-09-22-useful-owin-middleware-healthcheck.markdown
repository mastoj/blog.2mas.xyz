---
layout: post
title: 'Useful OWIN middleware: HealthCheck'
date: '2014-09-22 05:15:11'
tags:
- owin
- asp-net
---

In many of todays web application we often forget to implement some kind of health check. I think this is because we tend to overthink the problem. Health check might be hard to solve in a generic way that checks the complete system health, but it doesn't have to be hard to implement a generic middleware that every application can hook into and provide the status of the application but don't have to implement the actual endpoint. One advantage you get with this approach is that each application developer still controls what needs to be checked, but they all should follow a protocol to provide the result which makes it easier to monitor. The sample middleware I'm going to show is clean and simple, but that doesn't mean it fits everyone. 

## The goal
The goal of the middleware is to make it really simple to add health check to an existing application and specify how the health check should be executed inside the boundaries of the application. The code in the application should look like this:

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            var config =new HealthCheckConfiguration("/hc", new Checker());
            app.UseHealthCheck(config);
            app.Run(async context =>
            {
                await context.Response.WriteAsync("Hello");
            });
        } 
    }

    public class Checker : ICheckHealth
    {
        public HealthStatus CheckHealth()
        {
            return new HealthStatus()
            {
                Components = new[] {new ComponentHealth("Universe", ComponentStatus.Weird)}
            };
        }
    }

Here I have the `Checker` which is specific for this application, and I've also a simple extension making it possible to use `app.UseHealthCheck(config)`.

## The health of the application
They way I thought about this is that just responding with a `pong` to a request doesn't actually say something about the health of the application, how do you know that the external dependencies are up and running with a simple `pong`? So to tackle this I implented a couple of classes, first the `HealthStatus`:

    public class HealthStatus
    {
        public IEnumerable<ComponentHealth> Components { get; set; }
    }

the goal with this class is to give the application developer a chance to specify the health of different components that the application is dependent upon. The `ComponentHealth` is also a straightforward class:

    public class ComponentHealth
    {
        public string ComponentName { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ComponentStatus StatusName
        {
            get { return Status; }
        }

        public ComponentStatus Status { get; set; }

        public ComponentHealth(string componentName, ComponentStatus status)
        {
            ComponentName = componentName;
            Status = status;
        }
    }

    public enum ComponentStatus
    {
        Healthy,    // Ok
        Fatal,      // Error
        Weird       // Might be problems
    }

Each component has a name and status. I'm using [Json.NET](http://james.newtonking.com/json) to provide the result and to serialize the status with both the enum value and the enum name I'm using the `JsonConverter` attribute.

## Health checking
I've abstracted away the actual health checking so that the applications can define how it should be checked. I have also implemented a default health checker if you only want a simple `pong` response. The abstractions and implementation are also straightforward: 

    public interface ICheckHealth
    {
        HealthStatus CheckHealth();
    }

    internal class DefaultHealthChecker : ICheckHealth
    {
        public HealthStatus CheckHealth()
        {
            return new HealthStatus()
            {
                Components = new [] { new ComponentHealth("Web", ComponentStatus.Healthy)}
            };
        }
    }

## Configurating the middleware
Since I want to make it easy for the application developer to specify health checker and endpoint for the health check I've provided a simple configuration class:

    public class HealthCheckConfiguration
    {
        public ICheckHealth HealthChecker { get; private set; }
        public string Endpoint { get; private set; }

        public HealthCheckConfiguration(string endpoint = null, ICheckHealth healthChecker = null)
        {
            HealthChecker = healthChecker ?? new DefaultHealthChecker();
            Endpoint = endpoint ?? "/api/healthcheck";
        }
    }

## The actual middleware
Now when we have all the pieces it is rather simple to implement the actual middleware:

    public class HealthCheckMiddleware : OwinMiddleware
    {
        private ICheckHealth _healthChecker;
        private string _endpointUrl;

        public HealthCheckMiddleware(OwinMiddleware next, HealthCheckConfiguration config) : base(next)
        {
            _healthChecker = config.HealthChecker;
            _endpointUrl = config.Endpoint;
        }

        public override async Task Invoke(IOwinContext context)
        {
            if (context.Request.Uri.AbsolutePath == _endpointUrl)
            {
                var healthStatus = _healthChecker.CheckHealth();
                var response = JsonConvert.SerializeObject(healthStatus);
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(response);
            }
            else
            {
                await Next.Invoke(context);
            }
        }
    }

If the `AbsolutePath` matches the endpoint url exactly the health check is going to run, if it isn't the next middleware in the pipeline will be invoked. To make it easier to use I've also added an extension to the `IAppBuilder` interface: 

    public static class AppBuilderExtensions
    {
        public static IAppBuilder UseHealthCheck(this IAppBuilder app, HealthCheckConfiguration config = null)
        {
            config = config ?? new HealthCheckConfiguration();
            return app.Use<HealthCheckMiddleware>(config);
        }
    }

The extension above is what makes it possible to use `app.UseHealthCheck(config)`.

## Ending comments
I know this might now be optimal for many scenarios, but it's often good to start simple. Also, one might think that it's better if the application has a heartbeat instead of being passive and listens to request. There is pros and cons with both I would say. Being passive and listen to an request also tests that the application actually is reachable, which might not be tested in a heartbeat scenario.

The code above could also be a foundation for many small simple Owin middlewares that you might think of. I you want to look at the code in a more structured way I've put the code up on [github](https://github.com/mastoj/TJOwin). I'm planning to write more small middlewares for things that I want to use and put it here.
