---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Time for reflection'
date: '2014-06-27 18:47:11'
tags:
- cqrs
- event-sourcing
- cqrsshop
---

We need a change in the industry. All to often we defend old solutions just because we don't know the alternative. I know that sometime it might be good to slow things down and keep using the old stuff, but if that old stuff is storing state using SQL to do so everytime I don't think you're making that choice with the right reasons in mind. My contribution to this problem is a series of blog post where I'll walk you through how an alternative solution might look using event sourcing and CQRS. To store the event I'll use eventstore, and to store the view models I'll use elasticsearch. 

I'm planning to write a series of post since it is too much for one cover, where I will walk you through different stages of the implementation of a simple web shop. Everything will be implemented in C# and F# to start with but I might change to 100 % F#, but that is something that will come later. The code will be available in my [CQRSShop repository on github](https://github.com/mastoj/CQRSShop). Please feel free to comment or come with pull request. Even though I'll use [eventstore](http://geteventstore.com) and [elasticsearch](http://www.elasticsearch.org/) I won't cover how to install those products. I will not cover CQRS in depth either since there is a lot of material about CQRS and what it is, this will be a more practical view of how to do things.

All the posts will be tagged so you can find them on this url: http://blog.tomasjansson.com/tag/cqrsshop/

Hope you'll enjoy the read.

##Content in the serie
 * [Project structure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-project-structure/)
 * [Infrastructure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-infrastructure/)
 * [The goal](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-the-goal/)
 * [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/)
 * [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/)
 * Time for reflection
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#So what did just happen in the previous post?
In the last [post](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/) I implemented all the features for the application, but what was special with the implementation? Here I'll list the positive effects we've seen so far.

##CQRS is divide and conquer on an architectural level
A lot of the positive effects we see is because of CQRS. You can see CQRS as a divide and conquer algorithm on an architectural level. Since we are applying CQRS to the system it allows us to focus on just the behavior when implementing the functionality in the domain. When a feature is implemented we can switch focus to how the result should be presented for the user, which I'll do in a post or two. 

This divide and conquering technique is really powerful since we get less distraction when implementing a feature, and the same thing applies when we implement the views. Of course you have to know how to put the system together afterwards, which can seem like extra complexity, but you have to compare it to the alternative. If you're doing read and write at the same time you're handling two problems at the same time that might have two complete different models, or even worse, one model squeezed into the another model where it is just making a mess.

##No side effects
When working with events rather than state we are actually testing for absence of side effects as the same time as we are testing a feature. If we would get one event we are not expecting that should result in at least one failing test.

Testing the absence of side effects is something that is almost impossible in a system where you store the state instead of the events, since that basically would require you to check the state of the whole system. It is doable, but is really hard.

##Focus on behavior rather than the data
You can't ignore the data 100 % but the focus will shift from the data to the behavior and the intention of the application. When implementing the featueres the focus was all the time on what the result of an action should be instead of what the new state should be. This will make it easier to talk to the business side since they can describe the actions and what should happen. Often the business side really don't care about the state, since the state often is a technical representation rather than connected to the business.

The state should only be used as data for the user to make the next action.

##Events > state
If you have the events you can derive the state, but it is not possible to derive the events from the state. Why is this good? This is invaluable to fix really critical bugs in a system. If you find a invalid state somewhere in a system it is possible to find the pattern within the event stream that lead up to that state, but if you only have the state it might be impossible to derive what actually caused that invalid state and you won't be able to fix the problem except for that specific case.

##No mapping
I'm not sure if you noticed, but I basically haven't written a single line of mapping code between a database and my application. You could consider the state transitions in the aggregates are some sort of mapping, but I don't see it that way since the events are first class citizens of the domain. I only have one repository and that can get and save all types of aggregates in my domain, compared to one repository for every single type or complex repositories for weird constructs.

##Single point of entry
Since we only have one point of entry, the testing base class is made really simple and all my against the domain have the same structure. This also make it possible, as mentioned before, to add things generic functions that should be applied before every command in an easy manner. 

##F# is a powerful language
If you followed so far you've seen that F# make a perfect sort of "dsl" for us. Since we can express the commands and events in such a compact and powerful way it is really easy to grasp the functionality of the system since we can see them all on one page.

##What is the future?
I'll finish this blog series, but after that I'll consider rewriting the code in 100% F# instead of a mixture. I think it will be a great fit, but I thought I would finish this first.

Now when all the features is implemented it is time to implement the api, and that will be done in [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/).