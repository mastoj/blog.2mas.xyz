---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Project structure'
date: '2014-06-24 19:29:12'
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
 * Project structure
 * [Infrastructure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-infrastructure/)
 * [The goal](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-the-goal/)
 * [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/)
 * [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/)
 * [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/)
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#Solution structure
This introduction post will walk you through the project I would like to see in a project and a short comment about the intention with the project.

The projects that will be present are the one in the following screen shot:
![Project structure](/content/images/2014/Jun/Project-structure-1.PNG)
The rest of this post will give you a brief introduction to each of these projects and what I think should put into them. Note that there isn't such a thing as a generic solution to every problem so for your specific problem it might be good to have a different structure.

##Contracts
The contracts project will contain all the commands that will go into the system and events that will come out as a result of those commands. All the building blocks for putting together those commands and events will also be in this project.

To implement this I thing it is really beneficial to use F# instead of C#. The reason for this is that it compact way of defining new types that are value types, that is, you can compare them by "value" instead of by reference. These types will also be immutable by default which make it easier to write code with less side effects, and also easier to test things (will be covered in a later post)

##Domain
This is where all the logic related to the domain should go. It's not more complex that that, exactly what will go into this project will be covered in later posts.

##Infrastructure
In the infrastructure we put everything that specify how we store the things we do, that is, connect to the eventstore. This will also contain the implementation of the base aggregate class that all our aggregates should implement to make life easier. It also contains the interfaces for what an event and command is.

##Service
This project is the most difficult to define since it depends so much on the application you are building. With that said it will contain the code that will react on events and update the read models/views in my example. You could also have things that take a long time in this project.  

##Web
Straight forward, this is where all the code that is facing the user should be. It could the web api, html or both. For my example I'll use [Simple.Web](https://github.com/markrendle/Simple.Web).

##Search
Since I'm going to store all the views and readmodels in elasticsearch I've a dedicated project to query that model. This project will also contains the actual DTOs that I'm storing in elasticsearch.

##Domain tests
Quite straight forward, this will contain all the tests against the domain. You could of course have tests against the other parts as well if you like, but for me that is not that necessary since the domain will use all the other parts, like infrastructure, so if I get the domain work the other parts has to work as well.

This finish the first post in an unknown number of posts. Hopefully I finish a couple of more this week, but the weather is nice so I don't know if I have time.

The next part is [Infrastructure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-infrastructure/).