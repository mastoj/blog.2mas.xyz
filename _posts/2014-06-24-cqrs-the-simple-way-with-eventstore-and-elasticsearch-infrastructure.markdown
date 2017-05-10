---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Infrastructure'
date: '2014-06-24 20:16:39'
tags:
- cqrs
- event-sourcing
- cqrsshop
---

We need a change in the industry. All to often we defend old solutions just because we don't know the alternative. I know that sometime it might be good to slow things down and keep using the old stuff, but if that old stuff is storing state using SQL to do so everytime I don't think you're making that choice with the right reasons in mind. My contribution to this problem is a series of blog post where I'll walk you through how an alternative solution might look using event sourcing and CQRS. To store the event I'll use eventstore, and to store the view models I'll use elasticsearch. 

I'm planning to write a series of post, since it is too much to cover for one post, where I will walk you through different stages of the implementation of a simple web shop. Everything will be implemented in C# and F# to start with but I might change to 100 % F#, but that is something that will come later. The code will be available in my [CQRSShop repository on github](https://github.com/mastoj/CQRSShop). Please feel free to comment or come with pull request. Even though I'll use [eventstore](http://geteventstore.com) and [elasticsearch](http://www.elasticsearch.org/) I won't cover how to install those products. I will not cover CQRS in depth either since there is a lot of material about CQRS and what it is, this will be a more practical view of how to do things.

All the posts will be tagged so you can find them on this url: http://blog.tomasjansson.com/tag/cqrsshop/

Hope you'll enjoy the read.

##Content in the serie
 * [Project structure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-project-structure/)
 * Infrastructure
 * [The goal](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-the-goal/)
 * [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/)
 * [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/)
 * [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/)
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#Let's start in the deep end
Writing the infrastructure code is most likely the most technical complex code in this project, other parts might be complex due to the actual domain. The repositories and code I'll show here is more complex than the average SQL repository, but the good thing is that we only need one and just one repository for all the aggregates in our domain. The code to the infrastructure project is located on this url: https://github.com/mastoj/CQRSShop/tree/master/src/CQRSShop.Infrastructure

##The interfaces
There are a couple of interfaces that I thought I will cover first, this interfaces are quite straightforwad so I won't go into great details (the post will most likely be quite long either way).
 
 * `ICommand` - marker interface for our commands. This can make it possible to find command handlers through reflection (but I like to do that explicitly)
 * `IEvent` - interface for all our events, which should have an id on them so we know which aggregate that was the cause for an event
 * `IAggregate` - defines the basic functionality for an aggregate, the base implementation will be covered in more detail later in this post 
 * `IDomainRepository` - interface for what we expect from a domain repository, there will be one repository for eventstore as well as a simple in-memory one used for testing

##The command dispatcher
The purpose for the command dispatcher is to route the command to the registered handler, execute the handler and then save the result. Registration of ther to route the message will be done from the actual domain, so this is just the infrastructure part. The implementation is listed below, with comments after the code.

    public class CommandDispatcher
    {
        private Dictionary<Type, Func<object, IAggregate>> _routes;
        private IDomainRepository _domainRepository;
        private readonly IEnumerable<Action<object>> _postExecutionPipe;
        private readonly IEnumerable<Action<ICommand>> _preExecutionPipe;

        public CommandDispatcher(IDomainRepository domainRepository, IEnumerable<Action<ICommand>> preExecutionPipe, IEnumerable<Action<object>> postExecutionPipe)
        {
            _domainRepository = domainRepository;
            _postExecutionPipe = postExecutionPipe;
            _preExecutionPipe = preExecutionPipe ?? Enumerable.Empty<Action<ICommand>>();
            _routes = new Dictionary<Type, Func<object, IAggregate>>();
        }

        public void RegisterHandler<TCommand>(Func<TCommand, IAggregate> handle) where TCommand : class, ICommand
        {
            _routes.Add(typeof(TCommand), o => handle(o as TCommand));
        }

        public void ExecuteCommand(ICommand command)
        {
            var commandType = command.GetType();

            RunPreExecutionPipe(command);
            if (!_routes.ContainsKey(commandType))
            {
                throw new ApplicationException("Missing handler for " + commandType.Name);
            }
            var aggregate = _routes[commandType](command);
            var savedEvents = _domainRepository.Save(aggregate);
            RunPostExecutionPipe(savedEvents);
        }

        private void RunPostExecutionPipe(IEnumerable<object> savedEvents)
        {
            foreach (var savedEvent in savedEvents)
            {
                foreach (var action in _postExecutionPipe)
                {
                    action(savedEvent);
                }
            }
        }

        private void RunPreExecutionPipe(ICommand command)
        {
            foreach (var action in _preExecutionPipe)
            {
                action(command);
            }
        }
    }
    
There are three parameters for the constructor, one is the domain repository since that is needed to save the events after a command finished. The `preExecutionPipe` and `postExecutionPipe` is not mandatory to use, but I show here how you can easily inject methods before and after the execution of a command. This could be things like logging or some security check.

There are two public methods; `RegisterHandler` that is called to register one handler that a command should be routed to, and then there is the `ExecuteCommand` that does the actual routing and execution of a command.

##The base repository
There are some logic that is common for the repository for eventstore as well as for the in-memory repository. See below for the code and comments below the code. 

    public abstract class DomainRepositoryBase : IDomainRepository
    {
        public abstract IEnumerable<IEvent> Save<TAggregate>(TAggregate aggregate) where TAggregate : IAggregate;
        public abstract TResult GetById<TResult>(Guid id) where TResult : IAggregate, new();

        protected int CalculateExpectedVersion(IAggregate aggregate, List<IEvent> events)
        {
            var expectedVersion = aggregate.Version - events.Count;
            return expectedVersion;
        }

        protected TResult BuildAggregate<TResult>(IEnumerable<IEvent> events) where TResult : IAggregate, new()
        {
            var result = new TResult();
            foreach (var @event in events)
            {
                result.ApplyEvent(@event);
            }
            return result;
        }
    }

Two methods here, one that is used to calculate the version of an aggregate, this is important so you don't try to save events when you have the wrong version. The second method, `BuildAggregate` is used to build up an aggregate for a series of events.

##The repositories
My eventstore repository is based on the implementation here: http://geteventstore.com/blog/20130220/getting-started-part-2-implementing-the-commondomain-repository-interface/, so I won't go into details of how it works. If you understand that blog post you understand my code as well since it is a simplified version.

The in-memory won't be covered since it is not mandatory to have, but if you're interested feel free to check it out on github.

##The base aggregate
The last thing that will be cover is probably one of the most important classes. One thing that is important to realize before looking to the code is that when working with aggregates in this model the transition of state is separated from the logic that defines if the transition is valid. With that said the code looks like this: 

    public class AggregateBase : IAggregate
    {
        public int Version
        {
            get
            {
                return _version;
            }
            protected set
            {
                _version = value;
            }
        }

        public Guid Id { get; protected set; }

        private List<IEvent> _uncommitedEvents = new List<IEvent>();
        private Dictionary<Type, Action<IEvent>> _routes = new Dictionary<Type, Action<IEvent>>();
        private int _version = -1;

        public void RaiseEvent(IEvent @event)
        {
            ApplyEvent(@event);
            _uncommitedEvents.Add(@event);
        }

        protected void RegisterTransition<T>(Action<T> transition) where T : class
        {
            _routes.Add(typeof(T), o => transition(o as T));
        }

        public void ApplyEvent(IEvent @event)
        {
            var eventType = @event.GetType();
            if (_routes.ContainsKey(eventType))
            {
                _routes[eventType](@event);
            }
            Version++;
        }

        public IEnumerable<IEvent> UncommitedEvents()
        {
            return _uncommitedEvents;
        }

        public void ClearUncommitedEvents()
        {
            _uncommitedEvents.Clear();
        }
    }

There are two base properties for all aggregates; `Id` and `Version`. I think those properties are sort of self explaining, the id tells you what id the aggregate has since an aggregate is something that has an id compared to a value object. The version is an important property on an aggregate when doing things with event sourcing. The `RaiseEvent` method is called in the aggregates when logic has passed to do a transition. `RegisterTransition` is used a simple helper method to register transition that should be applied in the case of an event. `ApplyEvent` is called when an event should be applied to the aggregate, this will make a call to the registered transition method for the aggregate and change the state of the aggregate. The last two methods are the `UncommitedEvents` and `ClearUncommitedEvents` which are used to get all the changes caused by a command before saving them, and after save the events are cleared.

That finish the second post, please feel free to comment. The next part in the series is [The goal](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-the-goal/).

