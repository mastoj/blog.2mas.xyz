---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Integrating Elasticsearch'
date: '2014-06-28 09:13:01'
tags:
- cqrs
- event-sourcing
- elasticsearch
- cqrsshop
- eventstore
---

We need a change in the industry. All to often we defend old solutions just because we don't know the alternative. I know that sometime it might be good to slow things down and keep using the old stuff, but if that old stuff is storing state using SQL to do so everytime I don't think you're making that choice with the right reasons in mind. My contribution to this problem is a series of blog post where I'll walk you through how an alternative solution might look using event sourcing and CQRS. To store the event I'll use eventstore, and to store the view models I'll use elasticsearch. 

I'm planning to write a series of post, since it is too much to cover for one post, where I will walk you through different stages of the implementation of a simple web shop. Everything will be implemented in C# and F# to start with but I might change to 100 % F#, but that is something that will come later. The code will be available in my [CQRSShop repository on github](https://github.com/mastoj/CQRSShop). Please feel free to comment or come with pull request. Even though I'll use [eventstore](http://geteventstore.com) and [elasticsearch](http://www.elasticsearch.org/) I won't cover how to install those products. I will not cover CQRS in depth either since there is a lot of material about CQRS and what it is, this will be a more practical view of how to do things.

All the posts will be tagged so you can find them on this url: http://blog.tomasjansson.com/tag/cqrsshop/

Hope you'll enjoy the read.

##Content in the serie
 * [Project structure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-project-structure/)
 * [Infrastructure](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-infrastructure/)
 * [The goal](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-the-goal/)
 * [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/)
 * [The rest of the features](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-rest-of-the-features/)
 * [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/)
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * Integrating elasticsearch
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#Time to create the views
In most system you need to be able to show the data to the user in one way, so in this example I've decided to use elasticsearch. The update of the views will be taken care of by a separate service, which also make that the views will be eventual consistent. In this specific sample it will be consisten within less than half a second if everything is up and running and the service is connected to the eventstore to listen for changes. 

##What do we want a view of?
Since we have access to a complete audit log of the system we can basically create any view we can think of so we just have to think of a few. The views I choose to create to illustrate the integration is a list of the products so we can search for products, the customers and baskets. For the basket, customer and the orders a graph database like neo4j might make more sense so I might just do that in a future post but for now we put everything except the orders in elasticsearch. And since elasticsearch is a document/search database using lucene it will be really fast to lookup a single document as well as doing full text search.

##Disclamer
Since this is a demo I'll show you a quick way of doing things which you might not want to do in a production system. I won't store the last read position in the stream, but instead rebuilding the views every single time the service starts. That is doable for small amount of data, but probably not something you want.

##Creataing the hosting service with topshelf
To make it easier to host and debug the service I'll use [topshelf](http://topshelf-project.com/), which is an great framework to create windows services. To get started with topshelf you just create a console application and then install the `Topshelf` nuget package. When topshelf is added to your console project update your "Program.cs" to look something like this: 

    class Program
    {
        public static void Main(string[] args)
        {
            HostFactory.Run(x =>
            {
                x.Service<IndexingServie>(s =>
                {
                    s.ConstructUsing(name => new IndexingServie());
                    s.WhenStarted(tc => tc.Start());
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("CQRSShop.Service");
                x.SetDisplayName("CQRSShop.Service");
                x.SetServiceName("CQRSShop.Service");
            });
        }
    }

That's all to it. If you build this project now you'll get an exe file which you can install as a service, and at the same time if you just try to debug it in Visual Studio that will work as well. The code above is really straightforward. It will run a service of type `IndexingService` and when it starts it should call the `Start` method and `Stop` on stop. It can't be much simpler than that so let's keep on going to the `IndexingService`.

##Creating the indexing service
To understand what is going on we take it piece by piece.

###Constructing it
    internal class IndexingServie
    {
        private Indexer _indexer;
        private Dictionary<Type, Action<object>> _eventHandlerMapping;
        private Position? _latestPosition;
        private IEventStoreConnection _connection;

        public void Start()
        {
            _indexer = CreateIndexer();
            _eventHandlerMapping = CreateEventHandlerMapping();
            ConnectToEventstore();
        }

        private Indexer CreateIndexer()
        {
            var indexer = new Indexer();
            indexer.Init();
            return indexer;
        }

        private void ConnectToEventstore()
        {

            _latestPosition = Position.Start;
            _connection = EventStoreConnectionWrapper.Connect();
            _connection.Connected +=
                (sender, args) => _connection.SubscribeToAllFrom(_latestPosition, false, HandleEvent);
            Console.WriteLine("Indexing service started");
        }
        
        ...
    }
    
The `Indexer` class which is instantiated is a wrapper on top of [Nest](http://nest.azurewebsites.net/) which is the .NET client for elasticsearch. `Init` is called when creating it to create the index we are going to put our documents in.

###Connecting to eventstore
To connect to the eventstore I have a simple wrapper that can be reused for possible other subscribers of the eventstore, and I also get less code in the `IndexingService`. The code for the `EventStoreConnectionWrapper` is really simple: 

    public class EventStoreConnectionWrapper
    {
        private static IEventStoreConnection _connection;

        public static IEventStoreConnection Connect()
        {
            ConnectionSettings settings =
                ConnectionSettings.Create()
                    .UseConsoleLogger()
                    .KeepReconnecting()
                    .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"));
            var endPoint = new IPEndPoint(IPAddress.Loopback, 1113);
            _connection = EventStoreConnection.Create(settings, endPoint, null);
            _connection.Connect();
            return _connection;
        }
    }

Note that this is for the v 3.0 RC of eventstore, there might be minor changes for previous versions. Of course you shouldn't store the user name and password in code like this but for demo purposes it is just fine.

###Creating the mapping for the events
Since we are not interested in exactly all the events as of the moment, and we need a way to know what to do for each event we create a simple mapping dictionary for that: 

    private Dictionary<Type, Action<object>> CreateEventHandlerMapping()
    {
        return new Dictionary<Type, Action<object>>()
        {
            {typeof (CustomerCreated), o => Handle(o as CustomerCreated)},
            {typeof (CustomerMarkedAsPreferred), o => Handle(o as CustomerMarkedAsPreferred)},
            {typeof (BasketCreated), o => Handle(o as BasketCreated)},
            {typeof (ItemAdded), o => Handle(o as ItemAdded)},
            {typeof (CustomerIsCheckingOutBasket), o => Handle(o as CustomerIsCheckingOutBasket)},
            {typeof (BasketCheckedOut), o => Handle(o as BasketCheckedOut)},
            {typeof (OrderCreated), o => Handle(o as OrderCreated)},
            {typeof (ProductCreated), o => Handle(o as ProductCreated)}
        }; 
    }

Here we have all the mappings in one place instead of doing some really boring if/else or switch/case programming later on.

###Handle the events
We first have a method that will be called for each event, that was what specified in the subscription earlier.


    private void HandleEvent(EventStoreCatchUpSubscription arg1, ResolvedEvent arg2)
    {
        var @event = EventSerialization.DeserializeEvent(arg2.OriginalEvent);
        if (@event != null)
        {
            var eventType = @event.GetType();
            if (_eventHandlerMapping.ContainsKey(eventType))
            {
                _eventHandlerMapping[eventType](@event);
            }
        }
        _latestPosition = arg2.OriginalPosition;
    }

What this does is that it deserialize the event (see code below), finds the type of the event and check if it exists in our mapping. If it exists in our mapping we execute the function that mapping is pointing too.

The actual handlers will all follow a pattern like this:

    private void Handle(ItemAdded evt)
    {
        var basket = _indexer.Get<Basket>(evt.Id);
        var orderLines = basket.OrderLines;
        if (orderLines == null || orderLines.Length == 0)
        {
            basket.OrderLines = new[] {evt.OrderLine};
        }
        else
        {
            var orderLineList = orderLines.ToList();
            orderLineList.Add(evt.OrderLine);
            basket.OrderLines = orderLineList.ToArray();
        }
        _indexer.Index(basket);
    }

First we get the existing document from elasticsearch, then we update it and save it again. I won't show all the handlers since they are really simple and on github.

###Event deserialization
I extracted the event deserialization to a separate class since it really doesn't have much with the indexing to do. The deserializer code looks like this:

    public class EventSerialization
    {
        public static object DeserializeEvent(RecordedEvent originalEvent)
        {
            if (originalEvent.Metadata != null)
            {
                var metadata = DeserializeObject<Dictionary<string, string>>(originalEvent.Metadata);
                if (metadata != null && metadata.ContainsKey(EventClrTypeHeader))
                {
                    var eventData = DeserializeObject(originalEvent.Data, metadata[EventClrTypeHeader]);
                    return eventData;
                }
            }
            return null;
        }

        private static T DeserializeObject<T>(byte[] data)
        {
            return (T)(DeserializeObject(data, typeof(T).AssemblyQualifiedName));
        }

        private static object DeserializeObject(byte[] data, string typeName)
        {
            var jsonString = Encoding.UTF8.GetString(data);
            try
            {
                return JsonConvert.DeserializeObject(jsonString, Type.GetType(typeName));
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }
        public static string EventClrTypeHeader = "EventClrTypeName";
    }

There is one entry point, `DeserializeEvent`, that expects a `RecordedEvent` as parameter. The rest of the code is bascially just reverting the serialization of the code in the repository: https://github.com/mastoj/CQRSShop/blob/master/src/CQRSShop.Infrastructure/EventStoreDomainRepository.cs. First we check for the metadata since we have stored the type of the event in the metadata for each event. When we have the type we can deserialize the actual event data and we got our event.

##Indexing to elasticsearch
To be able to index anything you need something to index, so first we define our documents that we would like to store in the index.

###The documents
    public class Customer
    {
        [ElasticProperty(Index = FieldIndexOption.not_analyzed)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsPreferred { get; set; }
        public int Discount { get; set; }
    }
    
    public class Basket
    {
        [ElasticProperty(Index = FieldIndexOption.not_analyzed)]
        public Guid Id { get; set; }
        [ElasticProperty(Type = FieldType.nested)]
        public OrderLine[] OrderLines { get; set; }
        public BasketState BasketState { get; set; }
        [ElasticProperty(Index = FieldIndexOption.not_analyzed)]
        public Guid OrderId { get; set; }
    }

    public enum BasketState
    {
        Shopping,
        CheckingOut,
        CheckedOut,
        Paid
    }

    public class Product
    {
        [ElasticProperty(Index = FieldIndexOption.not_analyzed)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
    }

We have three documents that we want to index; `Customer`, `Basket` and `Product`. For all of them we add one attribute that tells `Nest` to not analyze the `Id` property before indexing the documents. This means that it will be searchable but only as the full value. Also, if you have an `Id` `Nest` will use that as the document id in elasticsearch.

###Indexing the documents
*The code I'll show to you here is probably **NOT** what you want in your production environment. You'll probably take advantage of features like multiple indexes, aliases and so on. This is just for demo purpose.*

    internal class Indexer
    {
        private readonly ElasticClient _esClient;
        private string _index = "cqrsshop";

        public Indexer()
        {
            var settings = new ConnectionSettings(new Uri("http://localhost:9200"));
            settings.SetDefaultIndex(_index);
            _esClient = new ElasticClient(settings);
        }

        public TDocument Get<TDocument>(Guid id) where TDocument : class
        {
            return _esClient.Get<TDocument>(id.ToString()).Source;
        }

        public void Index<TDocument>(TDocument document) where TDocument : class
        {
            _esClient.Index(document, y => y.Index(_index));
        }

        public void Init()
        {
            _esClient.CreateIndex(_index, y => y
                .AddMapping<Basket>(m => m.MapFromAttributes())
                .AddMapping<Customer>(m => m.MapFromAttributes())
                .AddMapping<Product>(m => m.MapFromAttributes()));
        }
    }

Let's start from the bottom, the `Init` method. Here we create the actual index in elasticsearch and and the mapping for our documents to that index. If we change our document structure we need to recreate the index and mapping. Then we have to simple wrapper methods; `Get<T>` and `Index<T>`.

##And we are done!
Now I finished what I started, but I think I'll do at least two more posts. One where I show how to integrate neo4j, and one with a summary of what, why and answers to some of the questions some people have against this type of architecture.

##Screen shots from result in elasticsearch
If we have created a customer, a product and a basket we can query elasticsearch using [Sense](https://github.com/bleskes/sense), which is/was a plugin to chrome. I'm not sure if you need a license for it now since it moved into another product called Marvel. If we ask to search for everything you would got a result like this.

![Sample result from sense]({{ site.url }}/assets/images/migrated/Sense_Result.PNG)

Elasticsearch is now up and running, but I'll add one more integration in [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/).