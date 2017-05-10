---
layout: post
title: Your application should be a pure function
date: '2014-11-09 15:36:53'
tags:
- functional
- cqrs
- event-sourcing
- cqrs-2
- cqrsshop
---

What does application has to do with functions you might ask? Everything is my answer! 

It's now a couple of months since I finished my blog serie about CQRS and event sourcing. You can read the ending discussion here: http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/. In those couple of months I had some more time to think about what I'm actually doing and then it hit me. Of course I touched on it through the blog serie, but I never wrote it down. Before I put it on print let us see what a pure function is

## Pure function
Let's keep it simple and take the defintion from [wikipedia](http://en.wikipedia.org/wiki/Pure_function). 

> In computer programming, a function may be described as a pure function if both these statements about the function hold:

> 1. The function always evaluates the same result value given the same argument value(s). The function result value cannot depend on any hidden information or state that may change as program execution proceeds or between different executions of the program, nor can it depend on any external input from I/O devices (usually—see below).
> 2. Evaluation of the result does not cause any semantically observable side effect or output, such as mutation of mutable objects or output to I/O devices (usually—see below).

What does this mean? If you put it in the context of your application you should be able to provide it a set of input and always expect the same output for that input. If your application depends on some kind of state that should be provided to the application as input. Also, you should not have any unexpected side effects like change in state of some object which you did not expect to change.

## Why is pure functions good?
If you have a pure function it's very easy to reason about it since you don't have to think about some kind of magic state. Two of the main reasons you should want to have your application as a pure function are:

1. A pure function is highly testable
2. You will focus on behaviour rather than state (what do you care about in your application)

## What does a "standard" application look likes?
Below I've attached a really simplifed picture of what most of the systems build look like today. 

![Standard application](/content/images/2014/11/StandardApplication-1.png)

The problem with this is that the application is both dependent on the input and the state, but the state is not given as input to the application. Another issue with this is that two models are mixed as one, both the model you do action against and the model which you query. Of course I've over simplified things since I put basically all the "layers" in a block called "application", but is that a bad thing? Adding all the layers will most likely screw this model up even more.

## Alternative solution with CQRS and event sourcing
You could see CQRS and event sourcing as an application where you have a write side and a read side of your application, but I think an alternative presentation might even make it more clear what is going on.

![Pure application](/content/images/2014/11/PureApplication.png)

What has happened here is that I have diveded the application into two parts; application and projection. As I wrote here, http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/, CQRS is just divided and conquer on an architectural level, and that is exactly what is going on here. I've turned the application into two pure functions, one which I still call "application" and one which I call "projections." The most important one here is the "application" since it is what creates change, the "projection" is just one or more interpretations of those changes. Input to the application could be a command and events if that is needed to execute the command, the important part is that they are also important to the application. I haven't drawn that here, since I consider it to be input as well. Before you call the application you read all or the relevant events from the event store and provide them as input so you keep the application as a pure function.

If it is not clear, the most important data store in that picture is the event store! That is where you store the set of changes of the system, and that is what matter!

Now when we have made this separation it is really easy to write test against both the application and the projection side of the system since they both are pure functions.

## Ending notes
If you come this long you've seen that I don't mention functional programming language, and that is because this hasn't to do with the language. You can achieve this in the language of your choice. Of course maybe functional languages will have some benefits, but that is not the point here. The point here is that you should start about what does your application really look like and what data do you have.