---
layout: post
title: Pure functional applications (in F#)
date: '2014-12-16 07:12:07'
tags:
- functional
- event-sourcing
- cqrsshop
- eventstore
- fsharp
---

How did I end up here? I'm not an F# expert, so it is somewhat weird to be in an F# calendar with all these awesome F# developers. It started with me [tweeting](https://twitter.com/TomasJansson/status/536874619480064000) the obvious to [Sergey Tihon](https://twitter.com/sergey_tihon). My tweet, that this is a great initiative, was answered with a "question" if I wanted to join, and it is really hard to say no in the public so here we are. My goal of this post is to show some of my F# playground and hopefully also get some feedback on the work I've done so far. The topic of this post is based on a previous post ["Your application should be a pure function"](http://blog.tomasjansson.com/your-application-should-be-a-pure-function/) that I wrote about a month ago. I didn't provide any sample in F# so that is what I aim to do in this post as a part of the [F# advent calendar](https://sergeytihon.wordpress.com/2014/11/24/f-advent-calendar-in-english-2014/). If you haven't read the previous posts in the calendar I really recommend it, and also follow up the rest of the posts that are to come.

##TL;DR;
If you just want to see the code go to [GitHub](https://github.com/mastoj/FsCQRSShop).

##Credits to the people who deserve it
Most of what I will present are my take on how I want to implement things, but I have been inspired and copied/stole ideas and code from other people who really deservere some credit.

 * The guys behind the [FsUno.Prod](https://github.com/thinkbeforecoding/FsUno.Prod); [Jérémie Chassaing](https://twitter.com/thinkb4coding) and [Ruben Bartelink](https://twitter.com/rbartelink)
 * A lot of the thoughts here has also been inspired by what [Greg Young](https://twitter.com/gregyoung) has done in the general space of event sourcing and CQRS
 * I've also done an effort to add the ["Railway Oriented Programming"](http://fsharpforfunandprofit.com/rop/) "pattern" to the sample which I was introduced to by [Scott Wlaschin](https://twitter.com/ScottWlaschin)

##Let's get started
["Your application should be a pure function"](http://blog.tomasjansson.com/your-application-should-be-a-pure-function/) is something I can't say have lived up to in the past, but it is something I really try to do as much as I can in my current project. This is most important where you have some "real" logic/domain to talk about, since this makes it much easier to test the application as well as reason about it since it is just a function. Another great advantage of doing it this way is that as soon as you only have **one entry point** to your application you can pass the arguments through a pipeline streamlining things like authorization and logging for example. Before digging in to the code I just want to clarify that this is just a sample domain and the actions and events are probably somewhat stupid, but try to see beyond that. Also, I want walk all the code for my sample only the most important bits, but it is all available on [GitHub](https://github.com/mastoj/FsCQRSShop).

##General architecture
I've been studying, experimenting and now working with CQRS and event sourcing for some years now and one of the feelings that I had is that this is a functional way of doing things. Also, when people like Greg Young also mention it and when you find projects like the [**FsUno.Prod**](http://thinkbeforecoding.github.io/FsUno.Prod/) project where they are describing their "functional event sourcing" journey, you do get more confident in that you are on to something and that CQRS with event sourcing is really functional by nature. So the code samples below is  my take on how I would like it to work when implementing a CQRS based architecture with event sourcing. The idea is really simple, you pass in commands to the application and out comes events (or errors).

If you are interested in more general CQRS stuff I've written a serie of 10 blog posts about it which you can find [here](http://blog.tomasjansson.com/tag/cqrsshop/).

##Test first
As all good developers we start with a test :). The type of test I want to write are those that focus on the behavior of the application, since it is at the boundaries I want to have the test disregarding the internals of the application. The application specify external dependencies as functions and those can be passed in when building the application. There are two types of tests I want to be able to write; "positive" tests and "negative" tests:

    module ``When making customer preferred`` =

        [<Fact>]
        let ``the customer should get the discount``() =
            let id = Guid.NewGuid()
            Given ([(id, [CustomerCreated(CustomerId id, "tomas jansson")])], None)
            |> When (Command.CustomerCommand(MarkCustomerAsPreferred(CustomerId id, 80)))
            |> Expect [CustomerMarkedAsPreferred(CustomerId id, 80)]

        [<Fact>]
        let ``it should fail if customer doesn't exist``() =
            let id = Guid.NewGuid()
            Given ([], None)
            |> When (Command.CustomerCommand(MarkCustomerAsPreferred(CustomerId id, 80)))
            |> ExpectFail (InvalidState "Customer")
            
**Notes about the tests**

* The tests are somewhat implementation agnostic, that is, I don't specify in the test what part of the application I test, all I specify is the input and output for the application.
* The `Given` clause might look weird, but that's just to make it easer for my dummy event store in supporting the test. Also, the `Option` parameter to the `Given` clause is just a way to pass in external dependents if I need to when setting the pre conditions of the test.
* I can `Expect` events comming out for the application, or
* I can `ExpectFail`, which is a better way to handle errors than throwing exceptions.

###The specification
I wouldn't be able to write a test like that without some helper functions that made it possible. So my specification helper module look like this:

    let createTestApplication dependencies events = 
            let es = create()
            let toStreamId (id:Guid) = sprintf "%O" id
            let readStream id = readFromStream es (toStreamId id)
            events |> List.map (fun (id, evts) -> appendToStream es (toStreamId id) -1 evts) |> ignore
            let deps = match dependencies with
                       | None -> { defaultDependencies with readEvents = readStream}
                       | Some d -> { d with readEvents = readStream }

            let save res = Success res
            buildDomainEntry save deps

    let Given (events, dependencies) = events, dependencies
    let When command (events, dependencies) = events, dependencies, command

    let Expect expectedEvents (events, dependencies, command) = 
        printfn "Given: %A" events
        printfn "When: %A" command
        printfn "Expects: %A" expectedEvents
        command 
        |> (createTestApplication dependencies events) 
        |> (fun (Success (id, version, events)) -> events)
        |> should equal expectedEvents

    let ExpectFail failure (events, dependencies, command) =
        printfn "Given: %A" events
        printfn "When: %A" command
        printfn "Should fail with: %A" failure

        command 
        |> (createTestApplication dependencies events) 
        |> (fun r -> r = Failure failure)
        |> should equal true

There are basically four parts to it.

1. The `createTestApplication` function which basically sets up the infrastructure for the application, but the actual "application" is created with the call to `buildDomainEntry` and that is the same call I'm doing outside of the tests as well. The `createTestApplication` function creates a dummy event store and adds my pre-condition events to it which might be used by the application.
2. The `Given` and `When` are two simple helper functions to build up the test case.
3. The `Expect` is the "positive" test function where I check that I get the expected events when executing a command.
4. The `ExpectFail` is the "negative" test function where I check for expected error conditions in the application.

##Building the application
So far I don't have that much external dependencies, and I hope it stays that way, and my pipeline which the commands go through doesn't do much things either so the application building function is not that complex as you see below.

    let validateCommand c = 
        match c with
        | Command.BasketCommand(CheckoutBasket(id, addr)) -> 
            match addr.Street.Trim() with
            | "" -> Failure (ValidationError "Invalid address")
            | trimmed -> Success (BasketCommand(CheckoutBasket(id, {addr with Street = trimmed})))
        | _ -> Success c

    let buildDomainEntry save deps c = 
        (validateCommand c) >>= (handle deps) >>= save

I've added a simple validation function to the application pipeline to show how one could inject things to the pipeline. Other things that could be added to the `buildDomainEntry` function are logging, correlation handling, authorization and things like that. The result handling is inspired byt the ["Railway Oriented Programming"](http://fsharpforfunandprofit.com/rop/) and the type I've added to have support for that is also really simple, but important.

    type Error = 
        | InvalidState of string
        | NotSupportedCommand of string

    type Result<'T> =
        | Success of 'T
        | Fail of Error

    let bind switchFunction = 
        fun input -> match input with
                     | Success s -> switchFunction s
                     | Fail s -> Fail s

    let (>>=) input switchFunction = bind switchFunction input
    
##Commands and events
This is just the data types for the application, and I've just discriminated union to represent them. I don't show all the commands and events either, but I think you get the point.

**Commands**

    type Command = 
        | CustomerCommand of CustomerCommand
    and CustomerCommand = 
        | CreateCustomer of CustomerId:CustomerId * Name:string
        | MarkCustomerAsPreferred of CustomerId:CustomerId * Discount:int

**Events**

    type Event = 
        | CustomerCreated of Id:CustomerId * Name:string
        | CustomerMarkedAsPreferred of Id:CustomerId * Discount:int

These are the commands and events that goes in and out from the application, but what is actually passed in from the UI or sent to the data storage might be something else that is mapped to these types. The reason the might be something else is because serialization with DUs is not that pretty for other consumers than F# as of the moment.

##Handling the command and evolving state
The `handle` function is sort of a router, it takes a command and passes it to the correct "sub-handler" if you will.

    let handle deps c =
        match c with
        | Command.CustomerCommand(cc) -> handleCustomer deps cc
        | Command.BasketCommand(bc) -> handleBasket deps bc
        | Command.OrderCommand(oc) -> handleOrder deps oc
        | Command.ProductCommand(pc) -> handleProduct deps pc

Nothing magic going on there so we'll get going. Before we look into `handleCustomer` I'll go into the process of building up state from events. The general idea is just to do a left fold of all the events and executing a function evolving a state from one state to another based on every event. So the general function look like this.

    let evolve evolveOne initState events =
        List.fold (fun result e -> match result with
                                   | Failure f -> Failure f
                                   | Success (v,s) -> match (evolveOne s e) with
                                                      | Success s -> Success (v+1, s) 
                                                      | Failure f -> Failure f) 
                  (Success (-1, initState)) events  
                  
For the specific scenario of a customer we have parts:

    type Customer = 
        | Init
        | Created of CustomerInfo
        | Preferred of CustomerInfo * Discount:int

    let evolveOneCustomer state event =
        match state with
        | Init -> match event with
                  | CustomerCreated(id, name) -> Success ( Created{Id = id; Name = name})
                  | _ -> stateTransitionFail event state
        | Created info -> match event with
                          | CustomerMarkedAsPreferred(id, discount) -> Success (Preferred(info,discount))
                          | _ -> stateTransitionFail event state
        | Preferred (info, _) -> match event with
                                 | CustomerMarkedAsPreferred(id, discount) -> Success (Preferred(info,discount))
                                 | _ -> stateTransitionFail event state

    let evolveCustomer = evolve evolveOneCustomer

    let getCustomerState deps id = evolveCustomer initCustomer ((deps.readEvents id) |> (fun (_, e) -> e))

* `evolveOneCustomer` is sort of a state machine that executes the transitions. You shouldn't execute business logic concerning command execution, just logic concerning if you are allowed to make a state transition.
* `evolveCustomer` is a simple helper, created with the helper function `evolve`.
* `getCustomerState` is a function that actually produces the states and also gets the events to evolve the state from.

Now when we know how to evolve the state of a customer from a set of events it is time to handle the commands.

    let handleCustomer deps cc =
        let createCustomer id name (version, state) =
            match state with
            | Init -> Success (id, version, [CustomerCreated(CustomerId id, name)])
            | _ -> Failure (InvalidState "Customer")
        let markAsPreferred id discount (version, state) = 
            match state with
            | Init -> Failure (InvalidState "Customer")
            | _ -> Success (id, version, [CustomerMarkedAsPreferred(CustomerId id, discount)])

        match cc with
        | CreateCustomer(CustomerId id, name) -> 
            getCustomerState deps id >>= (createCustomer id name)
        | MarkCustomerAsPreferred(CustomerId id, discount) -> 
            getCustomerState deps id >>= (markAsPreferred id discount)
            
As you see the result type from the function is a `Result<'T>` so it can ride the train. In the `Success` scenarios I return three things; the id of "aggregate", the expected version that is expected in the event stream when committing and the events to commit. It's pretty straightforward. I know I can probably clean some of these things up, but that is a later project.

##Summary
What I presented here is my first attempt to a functional event sourced application and how I would like it to work. Building an event sourced application and treating your application as a pure function is so useful in many ways, and at the same time it also makes you focus on the most important parts when you have the infrastructure set up. You could argue that it would be the same if the application returned the object/document instead of events, but doing so will actually make your application "loose" data since you are only dealing with state and not what caused the state to change as you do in an event sourced application.

The code is running if you clone the whole thing from GitHub, but here I've gone through the most important parts of the code and if you have any question regarding it just comment below or send me a tweet. I didn't cover [Event Store](http://geteventstore.com/), which I do use in the sample application and recommend you to look at if you haven't since it is a perfect fit for event sourcing.

I'm not an F# expert so please suggest improvements if you have any. I know the code is somewhat verbose in some areas, but I'm still restructuring it a little bit now and then. If you think I've abused F# please let me know :).

So this finishes my contribution to the [F# advent calendar](https://sergeytihon.wordpress.com/2014/11/24/f-advent-calendar-in-english-2014/), it was fun to force my self to actually do this in F#. I've been thinking about it for a long time and had something going, but this forced me to actually do something that would work. 

Thanks for reading and Merry Christmas!