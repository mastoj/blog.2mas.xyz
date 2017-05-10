---
layout: post
title: 'ASP.NET 5: IoC and dependency injection'
date: '2015-05-02 17:42:55'
tags:
- asp-net-mvc
- net
- asp-net
- asp-net-5
- mvc
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

## Constructor injection in the controller

The way the `PostController` is implemented at the moment is not that testable since it has a dependency on a "third party" component, the data handling. An instance of the `Data` class is created directly in the controller, which a really bad design. A better way to do it would be to inject it in the constructor. So instead of having this: 

    [Route("[controller]")]
    public class PostController : Controller
    {
        public static Data _data = new Data();

        [Route("{slug}", Name = "GetPost")]
        public IActionResult Index(string slug)
        {
            return View(_data.Get(slug));
        }

        [Route("[action]")]
        public IActionResult Create(PostModel model)
        {
            _data.Add(model);
            return RedirectToAction("Index", new {slug = model.Slug});
        }
    }

we should get it injected in the constructor like this:

    [Route("[controller]")]
    public class PostController : Controller
    {
        private static Data _data;

        public PostController(Data data)
        {
            _data = data;
        }

        [Route("{slug}", Name = "GetPost")]
        public IActionResult Index(string slug)
        {
            return View(_data.Get(slug));
        }

        [Route("[action]")]
        public IActionResult Create(PostModel model)
        {
            _data.Add(model);
            return RedirectToAction("Index", new {slug = model.Slug});
        }
    }

So far so good. But the instance of the `Data` class must be created somewhere, and that's done in the `ConfigureServices` method in the `Startup` class:

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton<Data>();
        }

Most likely you shouldn't add it as a singleton, but since mine is an in-memory "database" I must have it as a singleton so it is stored between the request. It's not production code, it just shows the feature.

## Property injection in the controller

You can also use property injection if you like, but I recommend constructor injection. In beta4 the way to do property injection is to use the `FromServices` attribute, like so: 

    [FromServices]
    public Data Data
    {
        get;
        set;
    }

It is important that it is a public property for it to work. Prior to beta4 the attribute name was `Activate`, but that is now `FromServices`. 

## Injection in the view

Another nice little feature is that you can inject things into the views. For example, let say we want a list in index view: 

    <div>
        <h3>Blog posts</h3>
        <ul>
            @foreach (var item in Data.GetPosts())
            {
                <li>@Html.ActionLink(item.Slug, "Index", "Post", new {slug = item.Slug})</li>
            }
        </ul>
    </div>

Here it might look like I'm using `Data` directly, but that is an instance. And to inject the instance in the view you use the `inject` keyword in the razor view:

    @inject OneManBlog.Model.Data Data

## Summary

Dependency injection is a nice way to decouple your application from third party dependencies, so it is nice that they have thought of it from the very first start in ASP.NET 5. As you see above it is not that hard to get started. The next post will most likely be about creating reusable `ViewComponents`.