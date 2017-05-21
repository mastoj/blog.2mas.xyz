---
layout: post
title: State or intent? The missing discussion of NoSQL
date: '2013-04-20 07:33:00'
tags:
- cqrs
- event-sourcing
- nosql
- ddd
---

The NoSQL movement has been going on for quite a while now and that I think is a good thing. I really think that different storage mechanisms are good for different situations and we should be better to take advantage of the possibilities we have today. However, there is one discussion that is not as loud as NoSQL vs. SQL/Relational database, and that is the discussion of what we are storing in our database. I think that in many scenarios where the actual application is a little more complex and when you don't know how the application will evolve you would be better off with an event store and event sourcing.

Have you ever been in a situation where you wish that the you had the full log of what had happened in the application so you could refactor the data store? I have and I have also seen multiple attempts to solve this problem with a regular relational database where the state of the application is stored, and next to each table is an extra table that stores the changes to each column. Doesn't this seem a little bit redundant?

## What is event sourcing?
Event sourcing is a way to use events to build up your domain objects instead of just capturing the state they are in right now. The events are usually stored in an event store which is a storage for your events as the name implies. When you build an event store you are totally free to choose whatever storage mechanism you like, it doesn't matter if it is a relational database, file or a NoSQL database. The basics behind an event store is pretty straight forward and to implement it in a SQL database you would need one table with three columns; one column for the id, one column for the version of the object and one column for the serialized data.

### How does event sourcing work?
There is always one thing you need when using event sourcing, and that is an id to the aggregate created the event. The id is a must to read the events from the event store and re-apply them to your aggregate. You might also need to store the version of the aggregate the events belongs so you know in which order to re-apply the events as well to detect conflicts.

I've tried to simplify how it works in the illustration below. First the client ask the `OrderRepository` for an aggregate. If an aggregate is found the client make an action, in this case it adds an `OrderLine`. The aggregate is responsible to make sure that the action can be applied to the aggregate and this validation should be done before the resulting events are stored.

![Simple event sourcing][1]

The central part which I didn't mention above is that when the client ask a repository for an aggregate all the historical events for that aggregate are read and applied to the aggregate. When applying these events no validation should be made since the validation should have been done before the event is stored, once an event is stored it is part of the history and it is nothing that can change that fact. An event should never be deleted, if you have a scenario where you have found a bug you should do some compensating action to resolve that bug.

### When is event sourcing a good alternative?
I think event sourcing fits best with a DDD-application using CQRS, but you could use it in other situations as well. It is often mentioned together with CQRS, but you don't have to use event sourcing with CQRS even though I would recommend it. DDD, CQRS and event sourcing are three different things and should be treated such, but they do fit well together. I want go into the details of DDD or CQRS but I will make use of some of the terms from DDD in lack of other words to explain things.

## Why use event sourcing?
There are multiple reasons why you might want to use event sourcing, but I will only cover the functionality perspective here.

### You store the intention rather than the state of your application
When you use event sourcing the information stored is so much richer than when you only store the state as you normally do. For every state change you also store why the state changed. There are several things this enabled you to do like:

 * Create reports later on things that wasn't specified when the project started
 * Replay events to track down bugs
 * Refactor object structure without thinking about the database

### It drives you to better communication with the business
I think using event sourcing will help you focus ond the "why" instead of "what" something is, and that is something I think is really important. You will have a complete history of why something has a state rather than just the state of something, this will enable you to have a deeped understanding of the domain and make it easier to talk to the business.

The event that you are using should have name that reflects the domain which you are modelling and they should also be in past tense. A business person should be able to read the events and from those events tell you which state an object should be or should not be in. 

### Improved testing through black box testing
The testing part with event sourcing is where it really shines. Mocking an event store during testing is really simple and when you have done so you could do some really black box testing. What do I mean with that?I basically mean that you can see on your application the same way as the business sees it and it is their problem you are trying to solve. This makes it really suitable for writing great acceptance tests like this  made up example:

> Given a user with account 12334 has made a deposit of 400

> and has enabled credit on the account

> When a withdrawal on 500 is made

> Then the account is withdrawn 500

> and the user is notified that the account is overdrawn

Implementing the test for this specification is not hard when using event sourcing. First you add three events as a pre-condition; `AccountCreated`, `DepositMade` and `CreditEnabled`. After you have set up the test you execute the action: `WithdrawMoney`. To check if the test has passed you check if the two events `MoneyWithdrawn` and `AccountOverdrawn` has been created. This doesn't only check for things that happened, with this approach you can also check that you don't have any unwanted side effects. Checking for unwanted side effects is almost impossible when using a standard relational database, all you most of the time do is expecting the implementation not to have side effects.

### No impedance mismatch
Sine all you need to do read an object from an event store is reading all the events for the object and applying them to the object. The methods for applying the events is something you should have done anyway if you have a really clean design, this might just look a little bit different. You don't need to worry about how the objects will serialize down to the database and what the relations should like etc. If you are really "advanced" you could implement the applying of events by convention, but I personally like to do that part explicitly.

## Why is it consider more complex to use event sourcing?
I personally don't think using event sourcing is more complex if it is used in the right type of project. With that I mean a project that has some kind of complexity and not just a simple CRUD application. I even think that using event sourcing might drive your development to a place where you focus on what the application is supposed to do rather than what is the state of the application. As stated earlier, state is something that is derived from what has happened.

To be successful with event sourcing you almost always need a separate data store that stores the actual data you want to show for the user. A lot of people sees this as an additional complexity but all it really is it separation of concerns. I don't think you will right more code doing this separation, instead you will have the code better aligned for their specific purpose.

## Resources:
If you want to see some code I have played around with a simple framework which you can find here: [TJ.CQRS](https://github.com/mastoj/TJ.CQRS)

My framework is very inspired by a similar implementation by [Mark Nijhof](http://cre8ivethought.com/blog) which you can find here: [Fohjin](https://github.com/MarkNijhof/Fohjin). He has also gathered several of his blog posts about CQRS and put them in a book [CQRS - The Example](https://leanpub.com/cqrs)

[Martin Fowler](http://martinfowler.com/) has also written a great post about event sourcing at his [blog](http://martinfowler.com/eaaDev/EventSourcing.html).


  [1]: {{ site.url }}/assets/images/migrated/EventSourcing.png