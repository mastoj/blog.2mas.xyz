---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: The Goal'
date: '2014-06-25 18:37:21'
tags:
- cqrs
- event-sourcing
- elasticsearch
- cqrsshop
---

We need a change in the industry. All to often we defend old solutions just because we don't know the alternative. I know that sometime it might be good to slow things down and keep using the old stuff, but if that old stuff is storing state using SQL to do so everytime I don't think you're making that choice with the right reasons in mind. My contribution to this problem is a series of blog post where I'll walk you through how an alternative solution might look using event sourcing and CQRS. To store the event I'll use eventstore, and to store the view models I'll use elasticsearch. 

I'm planning to write a series of post, since it is too much to cover for one post, where I will walk you through different stages of the implementation of a simple web shop. Everything will be implemented in C# and F# to start with but I might change to 100 % F#, but that is something that will come later. The code will be available in my [CQRSShop repository on github](https://github.com/mastoj/CQRSShop). Please feel free to comment or come with pull request. Even though I'll use [eventstore](http://geteventstore.com) and [elasticsearch](http://www.elasticsearch.org/) I won't cover how to install those products. I will not cover CQRS in depth either since there is a lot of material about CQRS and what it is, this will be a more practical view of how to do things.

All the posts will be tagged so you can find them on this url: http://blog.tomasjansson.com/tag/cqrsshop/

Hope you'll enjoy the read.

##Content in the serie
 * [Project structure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-project-structure/)
 * [Infrastructure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-infrastructure/)
 * The goal
 * [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/)
 * [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/)
 * [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/)
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#What is the goal? 
This post should probably have been the first one, but better late than never :). So the purpose of this post is to show the target architecture, more than just the screenshot of the projects in the first [post](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-project-structure/).

##Application description
The application that is being developed is a simple web shop. It isn't a complete web shop with all the possible feature, just enough to show how to put the application together and how to store the views in elasticsearch (and maybe neo4j if I have time).

The features I plan to include are:

 * Create customer
 * Make customer preferred --> get's 25% discount
 * Create product
 * Create shopping basket
 * Add item to basket
 * Go to checkout
 * Cancel order
 * Goto payment
 * Make payment
 * Approve order, orders over 100000 needs approval
 * Start shipping of order, after this point the customer can't cancel
 * Ship order, marks the order as shipped
 
This features will be implemented and tested in an effective way where I also test the absence of side effects.

##Target architecture
The target architecture is illustrated in the my nice looking picture below:
![](/content/images/2014/Jun/Architecture.jpg)

Let me walk you through it. First we have the user, which have two options. First the user can ask for the data. If you follow the arrows down on the right side you see that there is no possible way for the user to go all the way down to eventstore, it can only query specialized views. The second thing the user can do is place an action through the UI (a web api in this application). The action will be translated to a command (if it is not already one when comming through the api). 

From the way from the UI to the dispatcher the commands go through a pipeline where you can inject functionality as logging and security checks. Since we have a single point of entry to the domain it is really easy to add functionality that concerns every command as a step in this pipeline.  

The dispatcher figure out what should handle the command and execute it. After execution the resulting events are stored in the eventstore.

When the eventstore is updated we have a service (this will be an actual windows service) that listen to changes to the eventstore and if it sees an event which it is interested in it will update the views which are stored in elasticsearch and maybe neo4j.

Now I hope you have a better understanding of what the goal is so we can continue with the implementation in the next post, [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/).