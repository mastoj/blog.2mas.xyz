---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Let us throw neo4j
  into the mix'
date: '2014-06-28 11:47:15'
tags:
- cqrs
- event-sourcing
- elasticsearch
- cqrsshop
- eventstore
- neo4j
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
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * Integrating neo4j
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#Things change
My original plan was that this was going to be a summary post, but things change. So in this post I'll add integration with [neo4j](http://www.neo4j.org/) so I can ask really weird question like "for product x give me all other products that some customer has bought where the total order value was above y." Doing that type of query is really tricky in sql and document databases, but much easier in a graphdatabase as neo4j.

##We will be cheating
The quickes way to goal here is just to extend the `IndexingService`, and that is what I want to do. In the handlers methods I'll also update neo4j and not just elasticsearch. In a real environment you might want this as a separate service.

##Getting started
After you've installed neo4j, which you have to figure out yourself how to do (click download, yes, next yes or something like that), you need to add the `Neo4jClient` nuget package to the `Service` project. I want go into the details in how to connect and make the actual queries since there is tons of documentation on that. It took me roughly 30 minutes to figure everything out, so I guess you'll manage it as well.

##The result
I thought I show you the result before the actual code. The UI that comes with neo4j is a really amazing, easy to understand tool to explore your data. So after creating some customer, products and orders etc. you can get a full graphical overview of what is going on like in the picture below:

![](/content/images/2014/Jun/Graph.PNG)

I don't have nodes for the order lines, instead I just link a basket to a product but put all the value that customer is going to pay for that product on the relation, and that is what is highlighted in the picture. When a customer finished an order and paid for the product I link the customer directly to the product with a `BOUGHT` relation, this will make it really to find recommendation for customers based on what other customers has bought. An example cypher, the language to query neo4j, query that will get all the customers and products that has bought the product another customer is looking at might look like this:

    MATCH (p:Product {Id: '3b4173e6-b3cd-4565-868d-f810c5a04c43'})<-
    [:BOUGHT]-(c:Customer)-[:BOUGHT]->(p2:Product)
    WHERE p2.Id <> '3b4173e6-b3cd-4565-868d-f810c5a04c43'
    RETURN c,p2

Here I first locate the product we are looking at right now and which customer that has bought that product. The second `BOUGHT` relation finds all the products those customers has bought and the last where clause filter out so we don't get the product we are looking at right now in the result set. 

As you can see, using a graphdatabase you will be able to query really complex structures in a much simpler way than you would in a sql database.

##The updated IndexingService
Now when you know what we are going to produce, let's look at the code changes.

To make it simple I just pushed all the code into the `IndexingService`, I know it should probably be somewhere else, but this is just for show. So the updated version looks like this:

    internal class IndexingServie
    {
        private Indexer _indexer;
        private Dictionary<Type, Action<object>> _eventHandlerMapping;
        private Position? _latestPosition;
        private IEventStoreConnection _connection;
        private GraphClient _graphClient;

        public void Start()
        {
            _graphClient = CreateGraphClient();
            _indexer = CreateIndexer();
            _eventHandlerMapping = CreateEventHandlerMapping();
            ConnectToEventstore();
        }

        private GraphClient CreateGraphClient()
        {
            var graphClient = new GraphClient(new Uri("http://localhost:7474/db/data"));
            graphClient.Connect();
            DeleteAll(graphClient);
            return graphClient;
        }

        private void DeleteAll(GraphClient graphClient)
        {
            graphClient.Cypher.Match("(n)")
                .OptionalMatch("(n)-[r]-()")
                .Delete("n,r")
                .ExecuteWithoutResults();
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

        private void Handle(OrderCreated evt)
        {
            var existinBasket = _indexer.Get<Basket>(evt.BasketId);
            existinBasket.BasketState = BasketState.Paid;
            _indexer.Index(existinBasket);

            _graphClient.Cypher
                .Match("(customer:Customer)-[:HAS_BASKET]->(basket:Basket)-[]->(product:Product)")
                .Where((Basket basket) => basket.Id == evt.BasketId)
                .Create("customer-[:BOUGHT]->product")
                .ExecuteWithoutResults();
        }

        private void Handle(BasketCheckedOut evt)
        {
            var basket = _indexer.Get<Basket>(evt.Id);
            basket.BasketState = BasketState.CheckedOut;
            _indexer.Index(basket);
        }

        private void Handle(CustomerIsCheckingOutBasket evt)
        {
            var basket = _indexer.Get<Basket>(evt.Id);
            basket.BasketState = BasketState.CheckingOut;
            _indexer.Index(basket);
        }

        private void Handle(ItemAdded evt)
        {
            var existingBasket = _indexer.Get<Basket>(evt.Id);
            var orderLines = existingBasket.OrderLines;
            if (orderLines == null || orderLines.Length == 0)
            {
                existingBasket.OrderLines = new[] {evt.OrderLine};
            }
            else
            {
                var orderLineList = orderLines.ToList();
                orderLineList.Add(evt.OrderLine);
                existingBasket.OrderLines = orderLineList.ToArray();
            }

            _indexer.Index(existingBasket);

            _graphClient.Cypher
                .Match("(basket:Basket)", "(product:Product)")
                .Where((Basket basket) => basket.Id == evt.Id)
                .AndWhere((Product product) => product.Id == evt.OrderLine.ProductId)
                .Create("basket-[:HAS_ORDERLINE {orderLine}]->product")
                .WithParam("orderLine", evt.OrderLine)
                .ExecuteWithoutResults();
        }

        private void Handle(BasketCreated evt)
        {
            var newBasket = new Basket()
            {
                Id = evt.Id,
                OrderLines = null,
                BasketState = BasketState.Shopping
            };
            _indexer.Index(newBasket);
            _graphClient.Cypher
                .Create("(basket:Basket {newBasket})")
                .WithParam("newBasket", newBasket)
                .ExecuteWithoutResults();

            _graphClient.Cypher
                .Match("(customer:Customer)", "(basket:Basket)")
                .Where((Customer customer) => customer.Id == evt.CustomerId)
                .AndWhere((Basket basket) => basket.Id == evt.Id)
                .Create("customer-[:HAS_BASKET]->basket")
                .ExecuteWithoutResults();
        }

        private void Handle(ProductCreated evt)
        {
            var product = new Product()
            {
                Id = evt.Id,
                Name = evt.Name,
                Price = evt.Price
            };
            _indexer.Index(product);
            _graphClient.Cypher
                .Create("(product:Product {newProduct})")
                .WithParam("newProduct", product)
                .ExecuteWithoutResults();
        }

        private void Handle(CustomerMarkedAsPreferred evt)
        {
            var customer = _indexer.Get<Customer>(evt.Id);
            customer.IsPreferred = true;
            customer.Discount = evt.Discount;
            _indexer.Index(customer);

            _graphClient.Cypher
                .Match("(c:Customer)")
                .Where((Customer c) => c.Id == customer.Id)
                .Set("c = {c}")
                .WithParam("c", customer)
                .ExecuteWithoutResults();
        }

        private void Handle(CustomerCreated evt)
        {
            var customer = new Customer()
            {
                Id = evt.Id,
                Name = evt.Name
            };
            _indexer.Index(customer);

            _graphClient.Cypher
                .Create("(customer:Customer {newCustomer})")
                .WithParam("newCustomer", customer)
                .ExecuteWithoutResults(); 
        }

        public void Stop()
        {
        }
    }

The `CreateGraphClient` creates the graph client for us, as well as make sure the database is empty. The way I delete all the data is not how you should do it for large sets of data, again, this is just for show. When we have the graph client we can now update all our handlers to update the graph database as well as elasticsearch and we are all done.

##Everything has to come to an end
It has been a really productive week, but now I almost consider this blog series as done. I'll write one more post with some discussion and answers to questions some people might have regarding this type of design. I hope I can finish the discussion later today. The last part is available here: [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/).