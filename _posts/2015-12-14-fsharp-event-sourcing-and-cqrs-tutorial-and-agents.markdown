---
layout: post
title: F#, event sourcing and CQRS tutorial... and agents
date: '2015-12-14 13:15:00'
tags:
- functional
- cqrs
- event-sourcing
- fsharp
---

Last year I wrote a post about [Pure Functional Application](http://blog.2mas.xyz/pure-application-in-fsharp/) for the F# advent calendar, I really think it is a great initiative so I signed up again. This is my contribution to this year's F# advent calendar, you can find all the other excellent posts on [@sergey_tihon](https://twitter.com/sergey_tihon)'s blog: https://sergeytihon.wordpress.com/tag/fsadvent/

One would expect that I would write a totally different post this year, but instead I decided to make my last year's post more concrete. With that I mean I would like to introduce a tutorial for you to follow. I won't cover the whole tutorial in this post since it is described in the tutorial which you can find on [github](https://github.com/mastoj/LibAAS). The tutorial covers how it might be to work in a project where you have put in the time to set up the boilerplate for a project using CQRS and event sourcing in F#. It might not be production ready either, but it might give you some inspiration of how you can approach application development.

Covering the exercises in the tutorial here would be a little bit boring since they are covered in the tutorial so instead I thought I would explain how the in-memory event store is implemented using an F# agent. You can find the code I will cover in this folder on github: https://github.com/mastoj/LibAAS/tree/master/ex4/done/LibAAS.Infrastructure.

## Agents

I hope there are some people out there not that familiar with F# that follow along in this calendar since it is a great opportunity to learn some F#. I'll try to make this post understandable for most developers out there and that is why I'll write a short section about agents. Agents in F# is usually used as alias for `MailboxProcessor`, so you often see something like this in code where agents are used:

```language-fsharp
type Agent<'TMessage> = MailboxProcessor<'TMessage>
```

The way I see agents is like in-process workers that can keep some kind of state. You can compare it to actors in the [actor model](https://en.wikipedia.org/wiki/Actor_model) but much simpler. They are really great if all you need is a async worker inside your process or a nice way of storing state in your application.

Agents work with an inbox to which you can send messages, when a message arrives in the inbox the agent will read it an act upon it. The type of the message must be defined before hand and it can be of any type, a discriminated union is often used as message type. You can use simple types like `string` in this example

```language-fsharp
type Agent<'T> = MailboxProcessor<'T>

let agent = Agent.Start(fun (inbox:Agent<string>) ->
    let rec loop() =
        async {
            let! msg = inbox.Receive()
            match msg with
            | "" -> 
                printfn "Stopping agent"
            | _ -> 
                printfn "Message recieved: %s" msg
                return! loop() 
        }
    loop() 
)

let post (agent:Agent<'T>) message = agent.Post message

"hello" |> post agent
"" |> post agent
"hello" |> post agent
```

We first make an alias for the `MailboxProcessor` type. When the alias is created it can be used to start the agent with `Agent.Start`. The `Start` function takes a function as argument and this is the body of the agent. The structure you see in this simple example is probably the most common one as far as I know. The body is usually a recursive function in which you listen to new messages with the `inbox.Receive`, if you want to continue process after a message you just make a recursive call. You can define the recursive function to take a state parameter to keep track inside the agent between messages. I also defined a simple helper so it is easier to post messages to agents with the pipe operator. If you run the code above you should see two messages printed, the last one will not be printed since we are not doing a recursive call on empty messages and that stops the agent.

## Event store

What is an event store? Short answer: a data store that store events. It is almost that simple. The simplest possible event store need two functions:

* Get events given a stream id
* Save events given a stream id, expected version and events

The first function should return the list of events for the given stream id. A stream is just a way to group events that belong together.

The second function should append the events to a given stream with the stream id given that the version of the stream is the same as the expected version. A version of a stream is basically the number of events in the stream, this prevents concurrency issues and is also the transaction boundary when working against the event store.

That was a short introduction to what an event store is, there is plenty of more information online, but feel free to ask here if you have questions. Next up is the implementation of the event store in F#.

## Event store implementation

The implementation I will describe here is using agents, mainly because it is a nice way to abstract away the basics of an event store. With that in place you can easily create different types of event stores by changing two functions as you'll see. 

### The messages

First let's define some simple helpers for our agent:

```language-fsharp
module AgentHelper

type Agent<'T> = MailboxProcessor<'T>
let post (agent:Agent<'T>) message = agent.Post message
let postAsyncReply (agent:Agent<'T>) messageConstr = agent.PostAndAsyncReply(messageConstr)
```

Now when we got that out of our way we can start with the acutal implementation. We will start with the messages and some types that help us stay out of trouble.

```language-fsharp
type StreamId = StreamId of int
type StreamVersion = StreamVersion of int

type SaveResult = 
    | Ok
    | VersionConflict

type Messages<'T> = 
    | GetEvents of StreamId * AsyncReplyChannel<'T list option>
    | SaveEvents of StreamId * StreamVersion * 'T list * AsyncReplyChannel<SaveResult>
    | AddSubscriber of string * (StreamId * 'T list -> unit)
    | RemoveSubscriber of string
```

Just by reading this type definitions you can almost understand how the event store is supposed to work. We have a generic `Messages` type, where the generic parameter defines the type of event that we want to store in the event store. We have four actions we will be able to do against the event store:

1. Get the events for a stream.
2. Save events for a stream.  
    a. When saving you can have version conflict and to indicate that we use the `SaveResult` type.
3. You can add multiple subscribers, where the first string is an id of the subscriber (should probably be wrapped in a type). A subscriber will be called every time some events have been saved.
4. You can remove an existing subscriber based on the string id.

### State format

To make the agent flexible we need to keep an internal state that can be provided when creating the agent. The definition of the state type looks like this:

```language-fsharp
type internal EventStoreState<'TEvent,'THandler> = 
    {
        EventHandler: 'THandler
        GetEvents: 'THandler -> StreamId -> ('TEvent list option * 'THandler) 
        SaveEvents: 'THandler -> StreamId -> StreamVersion -> 'TEvent list -> (SaveResult * 'THandler)
        Subscribers: Map<string, (StreamId * 'TEvent list -> unit)>
    }
```

What I call `EventHandler` here is the the "thing" that stores the actual events, it can be an internal map or a connection to an external db. The methods `GetEvents` and `SaveEvents` uses the `EventHandler` to get or save events. The last thing in the state is the subscribers which we also need to keep track of.

### Agent body

Next up is the actual implementation of the agent. I give you the code right away and then walk you through it:

```language-fsharp
let eventSourcingAgent<'T, 'TEventHandler> (eventHandler:'TEventHandler) getEvents saveEvents (inbox:Agent<Messages<'T>>) = 
    let initState = 
        {
            EventHandler = eventHandler
            Subscribers = Map.empty
            GetEvents = getEvents
            SaveEvents = saveEvents
        }
    let rec loop state = 
        async {
            let! msg = inbox.Receive()
            match msg with
            | GetEvents (id, replyChannel) ->
                let (events, newHandler) = state.GetEvents state.EventHandler id
                replyChannel.Reply(events)
                return! loop {state with EventHandler = newHandler}
            | SaveEvents (id, expectedVersion, events, replyChannel) ->
                let (result, newHandler) = state.SaveEvents state.EventHandler id expectedVersion events
                if result = Ok then state.Subscribers |> Map.iter (fun _ sub -> sub(id, events)) else ()
                replyChannel.Reply(result)
                return! loop {state with EventHandler = newHandler}
            | AddSubscriber (subId, subFunction) ->
                let newState = {state with Subscribers = (state.Subscribers |> Map.add subId subFunction)}
                return! loop newState
            | RemoveSubscriber subId ->
                let newState = {state with Subscribers = (state.Subscribers |> Map.remove subId )}
                return! loop newState
        }
    loop initState
```

To create the agent we need the `eventHandler` a function to `getEvents` and `saveEvents`, nothing to fancy about that. The first thing we do in the function is to create the `initState` with the input and an empty `Map` for our subscribers. Next up is the recursive loop (remember it from the section above?). We first wait until there is a new message in the inbox, when we get one we match on the message type. 

For `GetEvents` we use the `GetEvents` method on the state passing in the `EventHandler` and the id of the string. When we have the events we reply back to the callee and finish it of with a recursive call with the new state (if it has changed).

`SaveEvents` works almost the same way, with the addition of notifying the subscribers if we manage to save the events. We also reply back to the callee with the result of the save operation before making the recursive call to wait for the next message.

The implementation of `AddSubscriber` and `RemoveSubscriber` do what you would expect them to, it adds or removes a subscriber for the `Subscribers` map we have in the state and make a recursive call to wait for the next message.

### In-memory implementation

To make it a little bit easier for a user to work with an agent it make sense to hide it behind some kind of type, which also make it easier to swap for another implementation later, and that type looks like this:

```language-fsharp
type EventStore<'TEvent, 'TError> = 
    {
        GetEvents: StreamId -> Result<StreamVersion*'TEvent list, 'TError>
        SaveEvents: StreamId -> StreamVersion -> 'TEvent list -> Result<'TEvent list, 'TError>
        AddSubscriber: string -> (StreamId * 'TEvent list -> unit) -> unit
        RemoveSubscriber: string -> unit
    }
```

The `SaveEvents` and `GetEvents` method returns something of type `Result`, and that is taken from [Railway Oriented Programming](http://fsharpforfunandprofit.com/rop/) which is a really nice way to handle errors in an application without introducing side effects like exceptions. The `Result` type is defined as:

```language-fsharp
[<AutoOpen>]
module ErrorHandling
 
type Result<'TResult, 'TError> = 
    | Success of 'TResult
    | Failure of 'TError

let ok x = Success x
let fail x = Failure x
```

Together with the type we have defined two helpers `ok` and `fail` to make it easier to create a `Result` through piping. 

We also need a function to create a wrapper around an agent that create an instance of an `EventStore`.

```language-fsharp
let createEventStore<'TEvent, 'TError> (versionError:'TError) agent =
    let getEvents streamId : Result<StreamVersion*'TEvent list, 'TError> = 
        let result = (fun r -> GetEvents (streamId, r)) |> postAsyncReply agent |> Async.RunSynchronously
        match result with
        | Some events -> (StreamVersion (events |> List.length), events) |> ok
        | None -> (StreamVersion 0, []) |> ok

    let saveEvents streamId expectedVersion events : Result<'TEvent list, 'TError> = 
        let result = (fun r -> SaveEvents(streamId, expectedVersion, events, r)) |> postAsyncReply agent |> Async.RunSynchronously
        match result with
        | Ok -> events |> ok
        | VersionConflict -> versionError |> fail

    let addSubscriber subId subscriber = 
        (subId,subscriber) |> AddSubscriber |> post agent

    let removeSubscriber subId = 
        subId |> RemoveSubscriber |> post agent

    { GetEvents = getEvents; SaveEvents = saveEvents; AddSubscriber = addSubscriber; RemoveSubscriber = removeSubscriber}
```

It is nothing to complicated going on, the `getEvents` function takes a stream id and wrap it in a `GetEvents` message which is sent to the agent. After sending the message we wait for the reply and return the events together with the current version of the stream wrapped in a `Result` type. The `saveEvents` method works almost the same way, that is, we wrap the input in a `SaveEvents` message and pass it to the agent and wait for the reply. If we get a `VersionConflict` back we translate it to the provided error to keep this code isolated from other code.

Now we have all the pieces to put together our in-memory event store. The in-memory event store will use a simple map as a storage for the events for easy lookup. 

```language-fsharp
let createInMemoryEventStore<'TEvent, 'TError> (versionError:'TError) =
    let initState : Map<StreamId, 'TEvent list> = Map.empty

    let saveEventsInMap map id expectedVersion events = 
        match map |> Map.tryFind id with
        | None -> 
            (Ok, map |> Map.add id events)
        | Some existingEvents ->
            let currentVersion = existingEvents |> List.length |> StreamVersion
            match currentVersion = expectedVersion with
            | true -> 
                (Ok, map |> Map.add id (existingEvents@events))
            | false -> 
                (VersionConflict, map)

    let getEventsInMap map id = Map.tryFind id map, map

    let agent = createEventStoreAgent initState getEventsInMap saveEventsInMap
    createEventStore<'TEvent, 'TError> versionError agent
```

* The `initState` is of course an empty map since we don't have any events when we start. 
* The `saveEventsInMap` uses the `id` argument to lookup in the `map` argument (current state), if the result is `None` the entry is added to the map with the `events`. If the entry already exist we check the version before appending the `events` to the stream.
* The `getEventsInMap` will just do a lookup in the `map` and returning an `Option` type together with the new map which is the same as the input.

With these three functions we can now call the `createEventStore` function to create our in-memory event store and we are done.

## Taking it out for a spin

The simplest way to actually try the event store out is to use it in a fsharp script. So in the same folder as the I have the files for the implementation I also have a simple script with the following content:

```language-fsharp
#load "AgentHelper.fs"
#load "ErrorHandling.fs"
#load "EventStore.fs"

open EventStore

let inMemoryEventStore = createInMemoryEventStore<string,string> "This is a version error"
inMemoryEventStore.AddSubscriber "FirstSubscriber" (printfn "%A")
let res0 = inMemoryEventStore.SaveEvents (StreamId 1) (StreamVersion 0) ["Hello";"World"]
let res1 = inMemoryEventStore.SaveEvents (StreamId 1) (StreamVersion 1) ["Hello2";"World2"]
let res2 = inMemoryEventStore.SaveEvents (StreamId 1) (StreamVersion 2) ["Hello2";"World2"]

[res0;res1;res2] |> List.mapi (fun i v -> printfn "%i: %A" i v)
```

We keep it really simple and only storing strings, as well as using a string as our error indicator. Executing this code with mono `fsharpi --exec Script.fsx` or on Windows `fsi --exec Script.fsx` should give the following output:

```
(StreamId 1, ["Hello"; "World"])
(StreamId 1, ["Hello2"; "World2"])
0: Success ["Hello"; "World"]
1: Failure "This is a version error"
2: Success ["Hello2"; "World2"]
```

The first two lines are from the subscriber and last in the script I print all the results.

## Now it is your turn

There is room for a lot of improvement here I guess, but it is a good starting point. Feel free to try the tutorial and also come with suggestion to what can simplify the infrastructure part. The goal of this implementation was to make it easy to use in a tutorial, and I think I manage that since the user only need to use code like the one in the last script.

With all this in place it shouldn't be that hard to implement an agent that is using [eventstore](https://geteventstore.com/) or a event simple one backed by a SQL database. All you need to do is send in the connection as the `EventHandler` and then implement the `GetEvents` and `SaveEvents` method accepting the connection (`EventHandler`) as an argument and returning the result for these two methods together with the new `EventHandler` state, the state could be the same as the input to the function.

And this finishes of my contribution to this year's F# calendar. I hope you enjoyed the read and learned something. Let me know if you have any questions!

Merry Christmas!