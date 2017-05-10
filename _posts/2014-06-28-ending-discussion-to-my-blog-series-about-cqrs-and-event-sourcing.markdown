---
layout: post
title: Ending discussion to my blog series about CQRS and event sourcing
date: '2014-06-28 17:48:30'
tags:
- cqrs
- event-sourcing
- cqrs-2
- elasticsearch
- cqrsshop
- eventstore
- neo4j
---

In the last couple of [posts](http://blog.tomasjansson.com/tag/cqrsshop/) I've walked you through how to get started writing an application with eventstore, elasticsearch and finally neo4j. The code is on [github](https://github.com/mastoj/CQRSShop). I'm quite sure that implementing an application with multiple views without using event sourcing will be much harder. There are a couple of reasons why and that is what I thought I would start this discussion with.

##Content in the serie
 * [Project structure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-project-structure/)
 * [Infrastructure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-infrastructure/)
 * [The goal](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-the-goal/)
 * [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/)
 * [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/)
 * [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/)
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * Ending discussion

#Storing state is like a low quality mp3 audio file
What I mean with this is that when you store the state of an application you are loosing data, the intention is lost when you move from one state to another. If you compare that to storing the events where you are only storing the intention of the action, but if you know the intention you can derive the state you want. "The state you want" is the key part here, what if you want to derive a different state later on? That is almost impossible (depending on the complexity of the view) if you are using a regular state database like sql or a document store, but when using event sourcing you're free to do so at any point in time since you have a complete log of the system. Of course you can achieve the same with say a sql database by creating shadow tables where you have a complete log of tranisitions of states, but at that point you're basically trying to implement an eventstore without realizing it.

#Creating views with event sourcing is the same as updating them
When you are creating the views in an event sourced system the view is created the same way at any point in time, you process the events and update the view. This makes it really easy recreate the views if you find that something is wrong, you just fix the code that creates the views and than process all the events again.

If you have an application based on a state database plus using events to update views like a graph database you need to maintain two different processes to update a view. On process it the one you run to recreate a view, you have to have that in place if you need to create the views from an existing database. You also need a process to handle the events to update the views, that is, you have two set of codes to maintain instead of just one.

#What do I do if the logic that created an event is wrong?
Let's say that the code that raises the event indicating that a customer should be a preferred customer has a bug. How do we fix all the customer that now has the wrong state? In that case I think you should first fix the bug, then find which of the customers that could possibly have been affected by the bug an run a counter action on those customers. Doing that you have that the history is preserved and the bug is fixed. Another way you can solve this to is that you expose the logic that calculate if a customer as a service, so it does the calculation upon request, but then you wouldn't get the event telling you that the thing has happened. The important part is that such a request doesn't change anything, it should just get the data without side effects. I believe that in the long run the first example is better and will keep things simpler, but as you see, there is more than one way to solve the issue. The key point here is that you will be able to fix it since you have the complete history of the system.

If you have the same issue in a "traditional" system, when you have fixed the issue you will loose the history of the actual error which might be really bad depending on the domain.

#Eventstore as a service bus
In a system we are building at a current project we are not using this model, and I think it starts to be more complex than it have to. If you use the eventstore to store the changes of the system you could basically use it as your "service bus" at the same time. This is exactly what we did with the example application where the service is just listening to changes to the eventstore and then updates accordingly. So if you use eventstore you could end up with less technology, that is, it can be used for your main storage as well as the service bus for the system.

#You just want to try something new
No, the thing is that I want to use the right tool for the right work. In the domain model where I'm actually doing things I want to store the changes, and derive the state from the changes. Then I want to use other database for my views since eventstore isn't optimal to query against, of course you can do it but asking after relations is much simpler in a graph database. The same thing applies for searching, you can enable full text searching in a sql database, but it is better to use a tool like elasticsearch which have that as its primary focus. If you need tabular data you can have a projection of the events into a sql database which is greate for tabular data.

So the right tool for the right work is what I'm going for. The samething applied for the programming language I choose in the example project. I used F# for the contracts, some types and events since it is really powerful expressing those types of constructs. I also think that you could probably save a lot if rewriting everything using eventstore since all I'm doing in the domain model is really functional. Applying events is a left fold. Executing an command is a function call with a derived state and command as parameter.

#Storing history
In many system the customer want to have an audit log. I've seen, and implemented myself, many systems where you have two tables of everything that you want history on. This is hard to maintain and you are still missing data if you don't add a column of the extra history table that indicate why that is a history row. Also, querying against this type of data can be problematic depending on the structure of the data. 

#Changing a database is hard when storing state
If you have an application where you store the state it is hard to make changes to this since then you know you have to go over all the code in all the layers and change the structure of that code, and also write some migration script. I you are using event sourcing you just change the event and you are done, as long as you don't change the name of an event or a property of the event, then you have to do some migration of the events. I think it is less common that an event might change than a database changes, since event are smaller and more concise so they are harder to model wrong ones you talked to the business.

Changing the view database in a event sources system is easy. You just drop the database, update the logic and re-applies all the event once again.

#Give it a try!
I really believe that change is good, just have a look at evolution. We know how and can build system as we allways have, but that doesn't mean we can't do it better. If you try this out for real I think you'll end up as a happier developer, have better code with less bugs, understand the business better, react to business needs faster, be able to write better near real time systems (if that is something you want) and learn a ton of new things at the same time. 

This is not harder just using a sql database, it is just different. Working with sql is something many of us developer has learned in college and/or at the university. We have also been working with it for multiple years. This give us a false perception that it is much easier to work with sql, I say it isn't. It is different! Different doesn't mean it is harder, just that you might just know how it works just yet, but it isn't that hard to learn if you really give it a try and that is what I ask of you. Give this a try. If I can write an application and 10 blog post about it just 3 evenings you can learn how to do it as well.