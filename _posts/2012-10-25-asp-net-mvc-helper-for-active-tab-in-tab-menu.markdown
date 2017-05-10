---
layout: post
title: ASP.NET MVC helper for active tab in tab menu
date: '2012-10-25 07:08:00'
tags:
- asp-net-mvc
---

This will be my first post on my own blog in this giant thing called "the web". I will mostly publish things like this, but I will take me my freedom to write anything I feel like :).

Now to the problem, when using the template that comes with asp.net mvc the tabs will not get any specific styling when you are on the active tab. If you are not looking for a very advanced solution it is not that hard to write a simple helper class to handle the active tab selection, so that is what I did. The base  functionality is in the following helper method:

	public static string ActiveTab(this HtmlHelper helper, string activeController, string[] activeActions, string cssClass)
	{
	    string currentAction = helper.ViewContext.Controller.
	            ValueProvider.GetValue("action").RawValue.ToString();
	    string currentController = helper.ViewContext.Controller.
	            ValueProvider.GetValue("controller").RawValue.ToString();

	    string cssClassToUse = currentController == activeController 
	         && activeActions.Contains(currentAction)
	                               ? cssClass
	                               : string.Empty;
	    return cssClassToUse;
	}
The ideas behind the code is that you should be able to write something like:

    Html.ActiveTab("controllerName", new string[]{"actionName1", "actionName2"}, "active")

to return "active" if the current action is actionName1 or actionName2 in the controller controllerName. Example markup at the bottom, but first some more static methods to make it easier to use the extension method:

	private const string DefaultCssClass = "active";

	public static string ActiveTab(this HtmlHelper helper, string activeController, string[] activeActions)
	{
	    return helper.ActiveTab(activeController, activeActions, DefaultCssClass);
	}

	public static string ActiveTab(this HtmlHelper helper, string activeController, string activeAction)
	{
	    return helper.ActiveTab(activeController, new string[] { activeAction }, DefaultCssClass);
	}

	public static string ActiveTab(this HtmlHelper helper, string activeController, string activeAction, string cssClass)
	{
	    return helper.ActiveTab(activeController, new string[] {activeAction}, cssClass);
	}
The markup I use for my menu is the following:

	<div id="menu">
	   <ul>
	      <li class="first <%=Html.ActiveTab("Company", "Index") %>">
	         <%=Html.ActionLink("Home", "Index", "Company") %>
	      </li>
	      <li class="last <%=Html.ActiveTab("Example", "Index") %>">
	         <%=Html.ActionLink("Example", "Index", "Example") %>
	      </li>
	   </ul>
	</div>

That's basically all there is to it. If you have any suggestions for improvement feel free to comment.