---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Build the API with
  simple.web'
date: '2014-06-27 20:44:56'
tags:
- cqrs
- event-sourcing
- cqrsshop
- simple-web
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
 * Building the API with Simple.Web
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#What are we doing?
I'm not planning to build a nice looking UI, but we'll see. The user interface will be the API as of the moment, and the views will later on be what is stored in elasticsearch. So here I'm planning to show you how to write the API with [Simple.Web](https://github.com/markrendle/Simple.Web). 

##The application setup
As I wrote above I'm going to use Simple.Web on top of [OWIN](http://owin.org/) as web framework. To get started you need to install a couple of nuget packages:
 
 * Microsoft.Owin.Host.SystemWeb
 * Simple.Web.JsonNet
 
Those two packages should bring all the dependencies needed.

Every OWIN application need a setup class and that is what is following: 

    [assembly: OwinStartup(typeof(OwinAppSetup))]
    namespace CQRSShop.Web
    {
        public class OwinAppSetup
        {
            public static Type[] EnforceReferencesFor =
                    {
                        typeof (Simple.Web.JsonNet.JsonMediaTypeHandler)
                    };

            public void Configuration(IAppBuilder app)
            {
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings()
                {
                    TypeNameHandling = TypeNameHandling.Objects
                };

                app.Run(context => Application.App(_ =>
                {
                    var task = context.Response.WriteAsync("Hello world!");
                    return task;
                })(context.Environment));
            }
        }
    }

First of all I'm forcing some references since Simple.Web need it for serialization. After that we have the acutal application setup with serialization settings and then starting the application with `app.Run`. `Application.App` is from the Simple.Web framework and if the request is not handled by the framework it will execute the inner function writing "Hello world!" to the output.

##Writing the base handler
The design we have chose allow it for us to have one handler that can handler all the commands, yes I'm exposing the commands straight from the API and leaving the id generation to the client. How bad can it be you might ask? Not so bad if you think about it, as long as you have a check that the id is unique for the aggregate you are creating. And since I'm using guids they are most likely unique and hopefully the clients use a proper guid generation framework. Also, are command doesn't return anything so that also simplifies everything.

Enough said, here is the base handler: 

    public abstract class BasePostEndpoint<TCommand> : IPost, IInput<TCommand> where TCommand : ICommand
    {
        public Status Post()
        {
            try
            {
                var connection = Configuration.CreateConnection();
                var domainRepository = new EventStoreDomainRepository(connection);
                var application = new DomainEntry(domainRepository);
                application.ExecuteCommand(Input);
            }
            catch (Exception)
            {
                return Status.InternalServerError;
            }

            return Status.OK;
        }

        public TCommand Input { set; private get; }
    }
    
In a Simple.Web language that code says an endpoint implementing this class will accept `POST` with the payload of type `TCommand`. If everything went as expected 200 is returned from the API, otherwise it will be a 400 error. This code could be improved if I would have a common `DomainException` and `ValidationException` to create better error responses, but this is not the focus of here.

##Writing a command handler
This is going to be remarkably simple since all my endpoints will all have the same structure:

    [UriTemplate("/api/customer")]
    public class PostEndpoint : BasePostEndpoint<CreateCustomer>
    {
         
    }

First we have the `UriTemplate` that specify where the endpoint is located. Then we have the implementation... oh wait, everything is implemented in the base class! Let's look at a more complex scenario:

    [UriTemplate("/api/basket/{BasketId}/items")]
    public class PostEndpoint : BasePostEndpoint<AddItemToBasket>
    {
         
    }

The reason this is more complex is because I have a little bit more complex `UriTemplate`. Again, there might be some improvements that can be done when I have code like this. As of the moment the `BasketId` parameter isn't validated and there is no check that the parameter has the same value as in the command.

##Something extra
I did some extra work just to show you how you can get this API discoverable, and with Simple.Web that is really simple. So let us start with root url "/api". 

    [UriTemplate("/api")]
    public class GetEndpoint : IGet, IOutput<IEnumerable<Link>>
    {
        public Status Get()
        {
            Output = LinkHelper.GetRootLinks();
            return 200;
        }

        public IEnumerable<Link> Output { get; set; }
    }

Now if we visit "/api" we'll see all the root links of the application. So let's implement a sample root link.

    [UriTemplate("/api/product")]
    [Root(Rel = "product", Title = "Product", Type = "application/vnd.cqrsshop.createproduct")]
    public class PostEndpoint : BasePostEndpoint<CreateProduct>
    {
         
    }

Adding that extra `Root` attribute to the `PostEndpoint` will make it appear as a link when you visit the "/api" url. I didn't do this for the whole application since it requires some strategy for links under roots etc, but hopefully you get the idea and get inspired and try it out.

With the api finished we need to create the integration with elasticsearch, you can read about that in [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/).