---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Implementing the first
  features'
date: '2014-06-25 20:06:01'
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
 * [Infrastructure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-infrastructure/)
 * [The goal](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-the-goal/)
 * The first feature
 * [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/)
 * [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/)
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#Look mom... I'm doing TDD, almost.
I'm going to implement the features using TDD on a high level. The first feature I'll implement is to create a customer. I'm not going to do strict TDD since I know sort of where I'm going with this. So let us get started.

##The base test class
All my test against the domain follow a simple pattern:

* Given a set of events (events as precondition)
* When I do something (a command)
* Then I should get these events as the result or I should get this exception

As long as I don't have to test some complex algorithm all my test can follow that pattern, so I'll write a simple base test class to handle this pattern. I'll not use fluent assertions or anything fancy like that since I don't think I need it to make my code readable. The code for the base test class looks like this: 

    public class TestBase
    {
        private InMemoryDomainRespository _domainRepository;
        private DomainEntry _domainEntry;
        private Dictionary<Guid, IEnumerable<IEvent>> _preConditions = new Dictionary<Guid, IEnumerable<IEvent>>();

        private DomainEntry BuildApplication()
        {
            _domainRepository = new InMemoryDomainRespository();
            _domainRepository.AddEvents(_preConditions);
            return new DomainEntry(_domainRepository);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            IdGenerator.GuidGenerator = null;
            _preConditions = new Dictionary<Guid, IEnumerable<IEvent>>();
        }

        protected void When(ICommand command)
        {
            var application = BuildApplication();
            application.ExecuteCommand(command);
        }

        protected void Then(params IEvent[] expectedEvents)
        {
            var latestEvents = _domainRepository.GetLatestEvents().ToList();
            var expectedEventsList = expectedEvents.ToList();
            Assert.AreEqual(expectedEventsList.Count, latestEvents.Count);

            for (int i = 0; i < latestEvents.Count; i++)
            {
                Assert.AreEqual(expectedEvents[i], latestEvents[i]);
            }
        }

        protected void WhenThrows<TException>(ICommand command) where TException : Exception
        {
            Assert.Throws<TException>(() => When(command));
        }

        protected void Given(params IEvent[] existingEvents)
        {
            _preConditions = existingEvents
                .GroupBy(y => y.Id)
                .ToDictionary(y => y.Key, y => y.AsEnumerable());
        }
    }

One thing that the base class uses which I haven't implemented yet is the `DomainEntry`. The `DomainEntry` is exactly what it says it is, it is the entry point for the domain and its responsibility is to put together all the dependencies used by the domain. The code is not that to follow and the initial code for the `DomainEntry` looks like this: 

    public class DomainEntry
    {
        private readonly CommandDispatcher _commandDispatcher;

        public DomainEntry(IDomainRepository domainRepository, IEnumerable<Action<ICommand>> preExecutionPipe = null, IEnumerable<Action<object>> postExecutionPipe = null)
        {
            preExecutionPipe = preExecutionPipe ?? Enumerable.Empty<Action<ICommand>>();
            postExecutionPipe = CreatePostExecutionPipe(postExecutionPipe);
            _commandDispatcher = CreateCommandDispatcher(domainRepository, preExecutionPipe, postExecutionPipe);
        }

        public void ExecuteCommand(ICommand command)
        {
            _commandDispatcher.ExecuteCommand(command);
        }

        private CommandDispatcher CreateCommandDispatcher(IDomainRepository domainRepository, IEnumerable<Action<ICommand>> preExecutionPipe, IEnumerable<Action<object>> postExecutionPipe)
        {
            var commandDispatcher = new CommandDispatcher(domainRepository, preExecutionPipe, postExecutionPipe);
            return commandDispatcher;
        }

        private IEnumerable<Action<object>> CreatePostExecutionPipe(IEnumerable<Action<object>> postExecutionPipe)
        {
            if (postExecutionPipe != null)
            {
                foreach (var action in postExecutionPipe)
                {
                    yield return action;
                }
            }
        }
    }

As I'll show later we are going to modify this class when we add the mapping for command to handler.

Now we have all the pieces so we can start writing our first test.

##Writing our first test
I'll go straight to the code: 

    [TestFixture]
    public class CreateCustomerTest : TestBase
    {
        [Test]
        public void WhenCreatingTheCustomer_TheCustomerShouldBeCreatedWithTheRightName()
        {
            Guid id = Guid.NewGuid();
            When(new CreateCustomer(id, "Tomas"));
            Then(new CustomerCreated(id, "Tomas"));
        }
    }

This feature is quite simple, if you had a more complex system you might want to put the customer handling process in a separate application and only handle the ordering in this application. I you've added just this piece of code it won't compile, we need to add the command and the events. 

##Time for some f# magic!
All the commands and events will be defined as record set in f#. The reason for this is that they are immutable value type structures, which means that I can compare two difference instances and they will check if they are equal by comparing the values rather than if the reference the same object. Also, it is a really compact and readable way to define the commands. So in the contracts project we add two files; "Commands.fs" and "Events.fs". The first version of "Commands.fs" looks like this: 

    namespace CQRSShop.Contracts.Commands
    open CQRSShop.Infrastructure
    open System

    type CreateCustomer = {Id: Guid; Name: string } with interface ICommand

The code above defines a f# set, which will be a class when used from C# that has equal and hashcode methods already implemented. A really powerful construct.

The first version of our "Events.fs" almost looks the same:

    namespace CQRSShop.Contracts.Commands
    open CQRSShop.Infrastructure
    open System

    type CustomerCreated = {Id: Guid; Name: string } 
        with interface IEvent with member this.Id with get() = this.Id

As you can see I have to implement the `IEvent` interface which specifies that all the events must have an id. So implementing the events are a little more verbose than the commands, but still much less verbose than it would be if we implemented them in C#.

Now we actually can run the test, but it fails and we get the exception: `System.ApplicationException : Missing handler for CreateCustomer.` So let's fix that.

The first step we need to do is to update the `DomainEntry` so it know how the command should be routed. So the `CreateCommandDispatcher` method should be updated to something like this: 

        private CommandDispatcher CreateCommandDispatcher(IDomainRepository domainRepository, IEnumerable<Action<ICommand>> preExecutionPipe, IEnumerable<Action<object>> postExecutionPipe)
        {
            var commandDispatcher = new CommandDispatcher(domainRepository, preExecutionPipe, postExecutionPipe);

            var customerCommandHandler = new CustomerCommandHandler();
            commandDispatcher.RegisterHandler<CreateCustomer>(customerCommandHandler);

            return commandDispatcher;
        }

This won't fix the test, now we must implement the command handler. I look to put all the command handlers in a folder in the domain, an alternative way of grouping them is that each command handler is located in a folder together with the aggregate it is handling. The code for the `CustomerCommandHandler` that handle the `CreateCustomer` command looks like this: 

    internal class CustomerCommandHandler : IHandle<CreateCustomer>
    {
        public CustomerCommandHandler()
        {
        }

        public IAggregate Handle(CreateCustomer command)
        {
            return Customer.Create(command.Id, command.Name);
        }
    }

It is still quite straightforward, but there is one last step and that is to create the `Customer` aggregate. 

    internal class Customer : AggregateBase
    {
        public Customer()
        {
            RegisterTransition<CustomerCreated>(Apply);
        }

        private Customer(Guid id, string name)
        {
            RaiseEvent(new CustomerCreated(id, name));
        }

        private void Apply(CustomerCreated obj)
        {
            Id = obj.Id;
        }

        public static IAggregate Create(Guid id, string name)
        {
            return new Customer(id, name);
        }
    }

What is going on here you might thing? It is not as complicated as it might first look. The public `Create` method is where we are actually doing things with the customer, it is in this method logic related to customer creation should be placed. The we have a public constructor that registers all transitions for the object that should be applied when an event is raised. The reason for this being public is because we need to create an "empty" aggregate and then build it up by applying events later on. The private constructor just raises the event that the customer has been the created and the `Apply` method is doing the state transition. This way we have a nice separation of concern between checking if we have a valid state and doing the transition.

##Changes made to existing code
During the implementation I added a simple `IHandle<TCommand>` interface to simplify things. 

    public interface IHandle<in TCommand> where TCommand : ICommand
    {
        IAggregate Handle(TCommand command);
    }
    
This is an interface that all the command handlers should implement and simplifies the registration of the command handlers in the `DomainEntry`. This change also resulted in a change in the `CommandDispatcher` in how routes are registered:

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
            _routes =  new Dictionary<Type, Func<object, IAggregate>>();
        }

        public void RegisterHandler<TCommand>(IHandle<TCommand> handler) where TCommand : class, ICommand
        {
            _routes.Add(typeof (TCommand), command => handler.Handle(command as TCommand));
        }

        public void ExecuteCommand<TCommand>(TCommand command) where TCommand : ICommand
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

And this finishes the first test. In the next post my goal is to implement the rest of the domain. One thing that some people can argue against is that it seems to be a little bit verbose, and for this case only I agree but the domain will grow. Also, you haven't seen me writing any ugly Entity framework code or any sql mapping code, all just work and is strongly typed.

The next part is [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/).