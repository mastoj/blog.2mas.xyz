---
layout: post
title: 'ASP.NET 5: View Components'
date: '2015-05-09 20:38:05'
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

## What is view components?

Partial views in MVC has one limitation, which is that they doesn't have a controller for them. Not having controller makes it hard to have more complex logic associated with the view. View components changes that. Each view component consist of a view and a backing class, it's not a controller but almost. In this post we will take the post list which we created by injecting the "repository" into the index view in the [last post](http://blog.tomasjansson.com/asp-net-5-ioc-and-dependency-injection/) and create a reuseable view component instead which we can use in multiple views.

## Creating the view component

The purpose of this view component is to list all the posts for this simple blog, making it much easier to reuse in multiple views. An alternative would have been to use a partial view, but using a partial view would have forced us to get the data for the partial view in the controller for each of the views where we used the partial, but since a view component has a backing class that is not necessary.

### The ViewComponent class

Create a folder called `ViewComponents` where you can have all your view components and add a `PostListViewComponent` class in the folder. The implementation of the class should look like this: 

    public class PostListViewComponent : ViewComponent
    {
        private readonly Data _data;

        public PostListViewComponent(Data data)
        {
            _data = data;
        }

        public IViewComponentResult Invoke(string title)
        {
            ViewBag.Title = title;
            return View(_data.GetPosts());
        }
    }

We are inheriting from the class `ViewComponent` to get some extra help, like the `View` method in the base class. The `Invoke` method is what will be called from the views that adds this view component and it is returning a simple view that show the posts. We are also injecting the `Data` class in the constructor which makes it possible to get the data we need in this class instead of it being sent to it like you do with a partial view. The view we return has a `ViewBag` as a regular view which we take advantage of, and the `Invoke` method can also have arguments which you can pass to the view component when you use it.

### The View

The view is really straightforward:

    @model IEnumerable<OneManBlog.Model.PostModel>
    <div>
        <h3>@ViewBag.Title</h3>
        <ul>
            @foreach (var item in Model)
                {
                <li>@Html.ActionLink(item.Slug, "Index", "Post", new { slug = item.Slug })</li>
            }
        </ul>
    </div>

I want get into any detail what this does, but the hard part is where to put it. To get this to work it must be in the folder `Views\Shared\Components\PostList\` and the file must be called `Default.cshtml`. The important part is from `Components` and down, the first part just scope where it can be used. Since I want this to be used from all over the site I put it in `Shared`. 

With these two files added my solution look like this:

![View component solution structure]({{ site.url }}/assets/images/migrated/ViewComponent.PNG)

### Using the view component

To use this in any view all I have to do is add this line:

    @Component.Invoke("PostList", "Blog posts")        

So my new `Home\Index.cshtml` container part now looks like this:

    <div class="container">
        <h1>This is my new blog</h1>
        Hello from MVC NNUG! asdad sasdasdasd adasd asd sadsa

        @using (Html.BeginForm("Create", "Post", FormMethod.Post))
        {
            <div class="form-group">
                <label for="slug">Slug</label>
                <input type="text" name="slug" id="name" value="" class="form-control" />
            </div>
            <div class="form-group">
                <label for="content">Content</label>
                <textarea id="content" name="content" class="form-control"></textarea>
            </div>
            <input type="submit" value="Save" class="btn btn-default" />
        }
        @Component.Invoke("PostList", "Blog posts")        
    </div>

and my new `Post\Index.cshtml` looks like this:

    <div class="container">
        <h1>@Model.Slug</h1>
        <div>@Model.Content</div>
        @Component.Invoke("PostList", "Other blog posts")
    </div>

That's all there is to it. 

## Summary

This is something I really missed in regular partial views and something that I think will be really useful creating more simple components that is reusable across a site. I look forward to try out this feature in a larger project and so how well it fits in, but so far I think it is really promising.