---
layout: post
title: Securing your ASP.NET MVC 3 application
date: '2012-10-24 06:57:00'
tags:
- asp-net-mvc
- security
---

The following blog post is most likely well covered on the internet, but I thought I would write about it anyway just so I now where to find it next time I need it. As you probably have figured the topic that will be handled in this post is securing your ASP.NET MVC 3 application. When talking about security there are two things we need to consider; authentication and authorization. This post will handle the authentication part since authorization is specific to your application and most of the time it will be enought just to use the `AuthorizeAttribute`.

###Authentication done right in ASP.NET MVC 3
Authentication is the first step in my short security chain, first I authenticate the user to assign an identity, roles or claims to the user. If I'm not developing a simple web site where every user has access to every page I most often want to restrict access to all pages except those that I have explicitly white listed for anonymous access. So how do I go along and implementing white listing i ASP.NET MVC 3? It turns out that it is really easy since ASP.NET MVC 3 introduced global action filters which you can apply to all controllers and actions in the application, compared to local action filters which you apply only to a controller or action. When you have a global action filter in place you can apply a local action filter to those actions or controllers that you do want anonymous users have access to, like the log in action. The global action filter that I will use to restrict the access to the application look like:

    public class RequireAuthenticationAttribute : AuthorizeAttribute
    {
        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            var skipAuthorization = filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                                    filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(
                                        typeof(AllowAnonymousAttribute), true);
            if (!skipAuthorization)
            {
                base.OnAuthorization(filterContext);
            }
        }
    }
	
It is pretty straight forward but here are some comments about it. Everywhere it says *Authorization* really means *Authentication* if you ask me, but it is handled the same way in the framework. The `OnAuthorization` method checks if a local filter called `AllowAnonymousAttribute` (which is coming next) is defined on either the action or on the controller. If the local filter is defined we want to allow anonymous access and can therefor skip authentication, that is, the call to `base.OnAuthorization`. The code for the `AllowAnonymousAttribute` is probably one of the most simple class you have ever seen:

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class AllowAnonymousAttribute : Attribute
    {
    }

All the class do is defining an attribute that I can apply to classes (controllers) or methods (actions). It doesn't have to have any functionality since all I do in the `RequireAuthenticationAttribute` is checking if the `AllowAnonymousAttribute` is present.
	
To apply this global action filter you need to add the filters to your global action filters collection in your global.asax file. So in your global.asax file you need it to look something like this:

	protected void Application_Start()
	{
		...
		RegisterGlobalFilters(GlobalFilters.Filters);
		...
	}

	public static void RegisterGlobalFilters(GlobalFilterCollection filters)
	{
		filters.Add(new RequireAuthenticationAttribute());
		...
	}

In the `Application_Start` method I make a call to my `RegisterGlobalFilters` method and register the `RequireAuthenticationAttribute` so if you try to access any page, that is handled by MVC, you will be denied access to it since you are not authenticated. That is the exact behavior we want, but now it is time to start white listing actions. The actions I want to white list are the `LogOn` and `Register` actions in my `AccountController`:

    public class AccountController : Controller
    {
        [AllowAnonymous]
        public ActionResult LogOn()
        {
			...
        }

        [HttpPost]
        [AllowAnonymous]
        public ActionResult LogOn(LogOnModel model, string returnUrl)
        {
			...
        }

        [AllowAnonymous]
        public ActionResult Register()
        {
			...
        }

        [HttpPost]
        [AllowAnonymous]
        public ActionResult Register(RegisterModel model)
        {
			...
        }

		...
		...
    }

If you now try to access the application again you should be redirected to the log on page if you have a default ASP.NET MVC 3 application.

That is all there is to it basically. Applying this in a filter instead of having this functionality in a base controller as you would do in ASP.NET MVC 2 is much more "correct". When you have it in a global filter it is basically impossible to do a mistake compared to when you are forced to inherit a specific controller which you might forget.