---
layout: post
title: 'CQRS the simple way with eventstore and elasticsearch: Implementing the rest
  of the features'
date: '2014-06-27 17:24:26'
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
 * [The first feature](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-implementing-the-first-features/)
 * The rest of the features
 * [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/)
 * [Building the API with Simple.Web](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-build-the-api-with-simple-web/)
 * [Integrating elasticsearch](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-integrating-elasticsearch/)
 * [Integrating neo4j](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-let-us-throw-neo4j-into-the-mix/)
 * [Ending discussion](http://blog.tomasjansson.com/ending-discussion-to-my-blog-series-about-cqrs-and-event-sourcing/)

#Some starting notes
First off, all the code is available to github. All I'm providing here is feature by feature implementation and some comments.

Before we start implement the features we need to make a slight change to the `TestBase` class to get better exception handling when expecting exceptions. The `WhenThrows` method should be updated to this: 

    protected void WhenThrows<TException>(ICommand command) where TException : Exception
    {
        try
        {
            When(command);
            Assert.Fail("Expected exception " + typeof(TException));
        }
        catch (TException)
        {
        }
    }
This will try to execute the command and only catch the exception we are expecting, do we get another exception we get the correct stacktrace for that exception instead of a modified stacktrace.

I'll try to group all the features to respective aggregate, and the be as concise as possible focusing on the code. The format will be "Feature group" --> "Feature" --> "Test" and implementation "Code". When showing the code I won't show the changes to the `DomainEntry` since they are just one or two, if any, lines for each feature.

#Customer features
We're not 100 % done with the customer yet. As of the moment it is possible to create duplicate user, and that is something we need to prevent. And we also need to add the functionality to make the customer preferred so they can get a discount.

##Preventing duplicate users
When we are trying to create a user with an id that is not unique we should get an exception telling us what the problem is.
###Test
    [Test]
    public void GivenAUserWithIdXExists_WhenCreatingACustomerWithIdX_IShouldGetNotifiedThatTheUserAlreadyExists()
    {
        Guid id = Guid.NewGuid();
        Given(new CustomerCreated(id, "Something I don't care about"));
        WhenThrows<CustomerAlreadyExistsException>(new CreateCustomer(id, "Tomas"));
    }

###Code
First we are missing an exception so the code won't even compile. So we add that class to the "Exceptions" folder in the domain project.

    public class CustomerAlreadyExistsException : Exception
    {
        public CustomerAlreadyExistsException(Guid id, string name) : base(CreateMessage(id, name))
        {
            
        }

        private static string CreateMessage(Guid id, string name)
        {
            return string.Format("A customer with id {0} already exists, can't create customer for {1}", id, name);
        }
    }

The next thing is to actually implement the feature. To get the test green we need to update our `CustomerCommandHandler` to make the duplicate check.

    internal class CustomerCommandHandler : IHandle<CreateCustomer>
    {
        private readonly IDomainRepository _domainRepository;

        public CustomerCommandHandler(IDomainRepository domainRepository)
        {
            _domainRepository = domainRepository;
        }

        public IAggregate Handle(CreateCustomer command)
        {
            try
            {
                var customer = _domainRepository.GetById<Customer>(command.Id);
                throw new CustomerAlreadyExistsException(command.Id, command.Name);
            }
            catch (AggregateNotFoundException)
            {
                // We expect not to find anything
            }
            return Customer.Create(command.Id, command.Name);
        }
    }

Adding those changes will make the build fail since we modified the constructor. To make it build and all the tests green again also we need to update the code where we instantiate the `CustomerCommandHandler` in the `DomainRepository`. That's a one line fix, so I won't even show it.

##Make customer preferred
It should be possible to mark a customer as preferred and also specify what discount the customer should have.
###Test
    [TestFixture]
    public class MarkCustomerAsPreferredTest : TestBase
    {
        [TestCase(25)]
        [TestCase(50)]
        [TestCase(70)]
        public void GivenTheUserExists_WhenMarkingCustomerAsPreferred_ThenTheCustomerShouldBePreferred(int discount)
        {
            Guid id = Guid.NewGuid();
            Given(new CustomerCreated(id, "Superman"));
            When(new MarkCustomerAsPreferred(id, discount));
            Then(new CustomerMarkedAsPreferred(id, discount));
        }
    }

###Command

    type MarkCustomerAsPreferred = {Id: Guid; Discount: int } with interface ICommand

###Event
    type CustomerMarkedAsPreferred = {Id: Guid; Discount: int }
        with interface IEvent with member this.Id with get() = this.Id


###Handler
The following was added to the `CustomerCommandHandler`:

    public IAggregate Handle(MarkCustomerAsPreferred command)
    {
        var customer = _domainRepository.GetById<Customer>(command.Id);
        customer.MakePreferred(command.Discount);
        return customer;
    }

###The aggregate
As of know we really don't need to take care of the state to get the test to pass, all we need to do is to raise the event indicating that we have stored the change. So the following should be added to the `Customer` class: 


    public void MakePreferred(int discount)
    {
        RaiseEvent(new CustomerMarkedAsPreferred(Id, discount));
    }

#Product features
It might be that product should be in a whole other system administrating just the product and what you send in to this domain is the product id, name and price when adding to the basket. Since this is just for show I included the product her. I've a really simplified web shop, you can only add products and when they've been added they will be there forever and will never run out. Also, the only thing I need to prevent is duplicate insert as I did with customer.

##Creating a product
You should be able to add a product the same way as you can add a customer. To add a product you need to provide the id, name and price. You can't add duplicate products.

###Tests
I'm using test cases to make sure the values aren't hard coded, except from that everything is straightforward.

    [TestFixture]
    public class CreateProductTests : TestBase
    {
        [TestCase("ball", 1000)]
        [TestCase("train", 10000)]
        [TestCase("universe", 999999)]
        public void WhenCreatingAProduct_TheProductShouldBeCreatedWithTheCorrectPrice(string productName, int price)
        {
            Guid id = Guid.NewGuid();
            When(new CreateProduct(id, productName, price));
            Then(new ProductCreated(id, productName, price));
        }

        [Test]
        public void GivenProductXExists_WhenCreatingAProductWithIdX_IShouldGetNotifiedThatTheProductAlreadyExists()
        {
            Guid id = Guid.NewGuid();
            Given(new ProductCreated(id, "Something I don't care about", 9999));
            WhenThrows<ProductAlreadyExistsException>(new CreateProduct(id, "Sugar", 999));
        }
    }

###Command
    type CreateProduct = {Id: Guid; Name: string; Price: int } with interface ICommand


###Event
    type ProductCreated = {Id: Guid; Name: string; Price: int }
        with interface IEvent with member this.Id with get() = this.Id

###Handler
    internal class ProductCommandHandler : 
        IHandle<CreateProduct>
    {
        private readonly IDomainRepository _domainRepository;

        public ProductCommandHandler(IDomainRepository domainRepository)
        {
            _domainRepository = domainRepository;
        }

        public IAggregate Handle(CreateProduct command)
        {
            try
            {
                var product = _domainRepository.GetById<Product>(command.Id);
                throw new ProductAlreadyExistsException(command.Id, command.Name);
            }
            catch (AggregateNotFoundException)
            {
                // We expect not to find anything
            }
            return Product.Create(command.Id, command.Name, command.Price);
        }
    }

###Aggregate
To get the test pass as of the moment I don't need to store the price and name.

    internal class Product : AggregateBase
    {
        public Product()
        {
            RegisterTransition<ProductCreated>(Apply);
        }

        private void Apply(ProductCreated obj)
        {
            Id = obj.Id;
        }

        private Product(Guid id, string name, int price) : this()
        {
            RaiseEvent(new ProductCreated(id, name, price));
        }

        public static IAggregate Create(Guid id, string name, int price)
        {
            return new Product(id, name, price);
        }
    }

#Shopping basket features
I've chosen to put a lot of the functionality in the basket since the data coming out from it should be used both by the ordering aggregate and to show the value of the basket. Keeping the pricing calculation and where the customer are in the shopping process here until the payment is made simplify a lot of things.

##Create shopping basket
When the shopping basket is created I send in the customer id. That is probably not something you'll require in a real system since a customer should be able to create a basket without being logged in.

###Tests
Three tests for the basket creation. The happy path and the negative paths when the customer doesn't exists or the basket already exists.

    [TestFixture]
    public class CreateBasketTests : TestBase
    {
        [Test]
        public void GivenCustomerWithIdXExists_WhenCreatingABasketForCustomerX_ThenTheBasketShouldBeCreated()
        {
            var id = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            int discount = 0;
            string name = "John doe";
            Given(new CustomerCreated(customerId, name));
            When(new CreateBasket(id, customerId));
            Then(new BasketCreated(id, customerId, discount));
        }

        [Test]
        public void GivenNoCustomerWithIdXExists_WhenCreatingABasketForCustomerX_IShouldGetNotified()
        {
            var id = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            WhenThrows<AggregateNotFoundException>(new CreateBasket(id, customerId));
        }

        [Test]
        public void GivenCustomerWithIdXExistsAndBasketAlreadyExistsForIdY_WhenCreatingABasketForCustomerXAndIdY_IShouldGetNotified()
        {
            var id = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            string name = "John doe";
            int discount = 0;
            Given(new BasketCreated(id, Guid.NewGuid(), discount),
                new CustomerCreated(customerId, name));
            WhenThrows<BasketAlreadExistsException>(new CreateBasket(id, customerId));
        }

        [Test]
        public void GivenACustomerWithADiscount_CreatingABasketForTheCustomer_TheDiscountShouldBeIncluded()
        {
            var id = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            int discount = 89;
            string name = "John doe";
            Given(new CustomerCreated(customerId, name),
                new CustomerMarkedAsPreferred(customerId, discount));
            When(new CreateBasket(id, customerId));
            Then(new BasketCreated(id, customerId, discount));
        }
    }

###Command
    type CreateBasket = { Id: Guid; CustomerId: Guid} with interface ICommand

###Event
    type BasketCreated = { Id: Guid; CustomerId: Guid}
        with interface IEvent with member this.Id with get() = this.Id

###Handler
    internal class BasketCommandHandler :
        IHandle<CreateBasket>
    {
        private readonly IDomainRepository _domainRepository;

        public BasketCommandHandler(IDomainRepository domainRepository)
        {
            _domainRepository = domainRepository;
        }

        public IAggregate Handle(CreateBasket command)
        {
            try
            {
                var basket = _domainRepository.GetById<Basket>(command.Id);
                throw new BasketAlreadExistsException(command.Id);
            }
            catch (AggregateNotFoundException)
            {
                //Expect this
            }
            var customer = _domainRepository.GetById<Customer>(command.CustomerId);
            return Basket.Create(command.Id, customer);
        }
    }

###Aggregate
    internal class Basket : AggregateBase
    {
        private Basket(Guid id, Guid customerId, int discount) : this()
        {
            RaiseEvent(new BasketCreated(id, customerId, discount));
        }

        public Basket()
        {
            RegisterTransition<BasketCreated>(Apply);
        }

        private void Apply(BasketCreated obj)
        {
            Id = obj.Id;
        }

        public static IAggregate Create(Guid id, Customer customer)
        {
            return new Basket(id, customer.Id, customer.Discount);
        }
    }

##Add item to basket
There are two happy path to consider; adding an item for a customer with no discount and for a customer with discount. I don't consider all the negative cases to keep me a little bit shorter.

###Tests
    [TestFixture]
    public class AddItemToBasketTest : TestBase
    {
        [TestCase("NameA", 100, 10)]
        [TestCase("NameB", 200, 20)]
        public void GivenWeHaveABasketForARegularCustomer_WhenAddingItems_ThePriceOfTheBasketShouldNotBeDiscounted(string productName, int itemPrice, int quantity)
        {
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var id = Guid.NewGuid();
            Given(new ProductCreated(productId, productName, itemPrice),
                new BasketCreated(id, customerId, 0));
            When(new AddItemToBasket(id, productId, quantity));
            Then(new ItemAdded(id, productId, productName, itemPrice, itemPrice, quantity));
        }

        [TestCase("NameA", 100, 10, 10, 90)]
        [TestCase("NameB", 200, 20, 80, 40)]
        public void GivenWeHaveABasketForAPreferredCustomer_WhenAddingItems_ThePriceOfTheBasketShouldBeDiscounted(string productName, int itemPrice, int quantity, int discountPercentage, int discountedPrice)
        {
            var customerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var id = Guid.NewGuid();
            Given(new CustomerCreated(customerId, "John Doe"),
                new CustomerMarkedAsPreferred(customerId, discountPercentage),
                new ProductCreated(productId, productName, itemPrice),
                new BasketCreated(id, customerId, discountPercentage));
            When(new AddItemToBasket(id, productId, quantity));
            Then(new ItemAdded(id, productId, productName, itemPrice, discountedPrice, quantity));
        }
    }

###Command
    type AddItemToBasket = { Id: Guid; ProductId: Guid; Quantity: int } with interface ICommand

###Event
I had a minor issue when implementing the feature so I needed to add an implementation of `ToString` so this event is a little more verbose but I thought I keep it for you to see.

    type ItemAdded = { Id: Guid; ProductId: Guid; ProductName: string; OriginalPrice: int; DiscountedPrice: int; Quantity: int}
        with 
        override this.ToString() = sprintf "Item added. Id: %O, Price: %d, Discounted: %d, Quantity: %d" this.Id this.OriginalPrice this.DiscountedPrice this.Quantity
        interface IEvent with member this.Id with get() = this.Id


###Handler
The handler is updated to handle the add item command: 

    public IAggregate Handle(AddItemToBasket command)
    {
        var basket = _domainRepository.GetById<Basket>(command.Id);
        var product = _domainRepository.GetById<Product>(command.ProductId);
        basket.AddItem(product, command.Quantity);
        return basket;
    }

###Aggregate
We only need to add one method to the aggregate that adds the item and calculate the price. 

public void AddItem(Product product, int quantity)
    {
        var discount = (int)(product.Price * ((double)_discount/100));
        var discountedPrice = product.Price - discount;
        RaiseEvent(new ItemAdded(Id, product.Id, product.Name, product.Price, discountedPrice, quantity));
    }

##Proceed to checkout
The proceed to checkout feature is just a way to keep track of the customer for future analytics purposes. When at the checkout the user can add shipping address and proceed to payment.

###Test
One simple test for the happy path.

    [TestFixture]
    public class ProceedCheckoutBasketTests : TestBase
    {
        [Test]
        public void GivenABasket_WhenCreatingABasketForCustomerX_ThenTheBasketShouldBeCreated()
        {
            var id = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            int discount = 0;
            Given(new BasketCreated(id, customerId, discount));
            When(new ProceedToCheckout(id));
            Then(new CustomerIsCheckingOutBasket(id));
        }
    }

###Command

    type ProceedToCheckout = { Id: Guid } with interface ICommand

###Event

    type CustomerIsCheckingOutBasket = { Id: Guid }
        with interface IEvent with member this.Id with get() = this.Id

###Handler
One more method to the basket command handler:

    public IAggregate Handle(ProceedToCheckout command)
    {
        var basket = _domainRepository.GetById<Basket>(command.Id);
        basket.ProceedToCheckout();
        return basket;
    }

###Aggregate
The new method added to the basket aggregate is also trivial.

    public void ProceedToCheckout()
    {
        RaiseEvent(new CustomerIsCheckingOutBasket(Id));
    }


##Checkout
When the user is doing the actual checkout we need to collect the shipping address before we proceed to payment, and the address must be specified.

###Test

    [TestFixture]
    public class CheckoutBasketTests : TestBase
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void WhenTheUserCheckoutWithInvalidAddress_IShouldGetNotified(string street)
        {
            var address = street == null ? null : new Address(street);
            var id = Guid.NewGuid();
            Given(new BasketCreated(id, Guid.NewGuid(), 0));
            WhenThrows<MissingAddressException>(new CheckoutBasket(id, address));
        }

        [Test]
        public void WhenTheUserCheckoutWithAValidAddress_IShouldProceedToTheNextStep()
        {
            var address = new Address("Valid street");
            var id = Guid.NewGuid();
            Given(new BasketCreated(id, Guid.NewGuid(), 0));
            When(new CheckoutBasket(id, address));
            Then(new BasketCheckedOut(id, address));
        }
    }

###Types
To support address I defined a separate F# record for address and put in a "types.fs" file. This must be located above the other files and should include the following: 

    namespace CQRSShop.Contracts.Types

    type Address = { Street: string }


###Command
Now when I have the address type specified the command looks like this:

    type CheckoutBasket = { Id: Guid; ShippingAddress: Address } with interface ICommand

###Event
    type BasketCheckedOut = { Id: Guid; ShippingAddress: Address } 
        with interface IEvent with member this.Id with get() = this.Id

###Handler
Once againt a simple method was added to the command handler class.

    public IAggregate Handle(CheckoutBasket command)
    {
        var basket = _domainRepository.GetById<Basket>(command.Id);
        basket.Checkout(command.ShippingAddress);
        return basket;
    }

###Aggregate
The aggregate is where the logic to validate the input related to what makes a valid checkout should be: 

    public void Checkout(Address shippingAddress)
    {
        if(shippingAddress == null || string.IsNullOrWhiteSpace(shippingAddress.Street))
            throw new MissingAddressException();
        RaiseEvent(new BasketCheckedOut(Id, shippingAddress));
    }

##Make payment
When a payment is made the result will be that an order is created, this order can use the same id as the basket since they will be connected to each other and there can only be one order per basket but many baskets can have no order.

###Tests
The happy path for making a payment is a little bit different than the other tests. Here we're going to create another aggregate from the basket aggregate. The tests will fail if we try to pay an unexpected amount. I haven't covered all cases here either. To get the test running as expected I needed control over the id generation, and to do so I wrote the `IdGenerator` class and put it in the infrastructure project.

    public class IdGenerator
    {
        private static Func<Guid> _generator;

        public static Func<Guid> GenerateGuid
        {
            get
            {
                _generator = _generator ?? Guid.NewGuid;
                return _generator;
            }
            set { _generator = value; }
        }
    }

The tests when you have that class is pretty straightforward:

    [TestFixture]
    public class MakePaymentTests : TestBase
    {
        [TestCase(100, 101)]
        [TestCase(100, 99)]
        [TestCase(100, 91)]
        [TestCase(100, 89)]
        public void WhenNotPayingTheExpectedAmount_IShouldGetNotified(int productPrice, int payment)
        {
            var id = Guid.NewGuid();
            Given(new BasketCreated(id, Guid.NewGuid(), 0),
                new ItemAdded(id, Guid.NewGuid(), "", productPrice, productPrice, 1));
            WhenThrows<UnexpectedPaymentException>(new MakePayment(id, payment));
        }

        [TestCase(100, 101, 101)]
        [TestCase(100, 80, 80)]
        public void WhenPayingTheExpectedAmount_ThenANewOrderShouldBeCreatedFromTheResult(int productPrice, int discountPrice, int payment)
        {
            var id = Guid.NewGuid();
            int dontCare = 0;
            var orderId = Guid.NewGuid();
            IdGenerator.GenerateGuid = () => orderId;
            Given(new BasketCreated(id, Guid.NewGuid(), dontCare),
                new ItemAdded(id, Guid.NewGuid(), "Ball", productPrice, discountPrice, 1));
            When(new MakePayment(id, payment));
            Then(new OrderCreated(orderId, id));
        }
    }

###Command
    type MakePayment = {Id: Guid; Payment: int } with interface ICommand

###Event
The event generated here is order created since that is what we expect when the customer has payed for the basket.

    type OrderCreated ={ Id: Guid; BasketId: Guid }
        with interface IEvent with member this.Id with get() = this.Id

###Handler
This is a slitghtly different handler then the previous one. As you can see from the code it returns an `Order` aggregate and that is what gets return from the handler instead of a `Basket` aggregate.

    public IAggregate Handle(MakePayment command)
    {
        var basket = _domainRepository.GetById<Basket>(command.Id);
        var order = basket.MakePayment(command.Payment);
        return order;
    }

###Aggregate
To the `Basket` aggregate I added this method: 

    public IAggregate MakePayment(int payment)
    {
        var expectedPayment = _items.Sum(y => y.DiscountedPrice);
        if(expectedPayment != payment)
            throw new UnexpectedPaymentException();
        return new Order(Id);
    }

Also, to get this to work I had to create the `Order` aggregate:

    internal class Order : AggregateBase
    {
        public Order()
        {
            RegisterTransition<OrderCreated>(Apply);
        }

        private void Apply(OrderCreated obj)
        {
            Id = obj.Id;
        }

        internal Order(Guid basketId) : this()
        {
            RaiseEvent(new OrderCreated(IdGenerator.GenerateGuid(), basketId));
        }
    }

#Order features
The `Order` aggregates responsibility in my application is to handle everything that has with the order to do, like if it is possible to cancel the order and keep track on if it is shipped. So let's start with last set of features.

To spare myself some time I put all the tests in one file, and implemented all the `Order` features and not step by step as above. The test is also somewhat intertwined between cancelling and shipping so it might feel like the tests are out of order.

There are also some minor changes to the `OrderCreated` event to include a fsharp list for the order items, this makes it much easier to compare two lists. The in-memory eventstore was also updated to use serializtion to verify that I can handle the serialization of the fsharp list.

##Tests
Again I'm not testing every possible edge case, I just want the basic functionality up and running.

    [TestFixture]
    public class AllTheOrderTests : TestBase
    {
        [Test]
        public void WhenStartingShippingProcess_TheShippingShouldBeStarted()
        {
            var id = Guid.NewGuid();
            var orderCreated = BuildOrderCreated(id, basketId:  Guid.NewGuid(), numberOfOrderLines: 1);
            Given(orderCreated);
            When(new StartShippingProcess(id));
            Then(new ShippingProcessStarted(id));
        }

        [Test]
        public void WhenCancellingAnOrderThatHasntBeenStartedShipping_TheOrderShouldBeCancelled()
        {
            var id = Guid.NewGuid();
            var orderCreated = BuildOrderCreated(id, basketId: Guid.NewGuid(), numberOfOrderLines: 1);
            Given(orderCreated);
            When(new CancelOrder(id));
            Then(new OrderCancelled(id));
        }

        [Test]
        public void WhenTryingToStartShippingACancelledOrder_IShouldBeNotified()
        {
            var id = Guid.NewGuid();
            var orderCreated = BuildOrderCreated(id, basketId: Guid.NewGuid(), numberOfOrderLines: 1);
            Given(orderCreated,
                new OrderCancelled(id));
            WhenThrows<OrderCancelledException>(new StartShippingProcess(id));
        }

        [Test]
        public void WhenTryingToCancelAnOrderThatIsAboutToShip_IShouldBeNotified()
        {
            var id = Guid.NewGuid();
            var orderCreated = BuildOrderCreated(id, basketId: Guid.NewGuid(), numberOfOrderLines: 1);
            Given(orderCreated,
                new ShippingProcessStarted(id));
            WhenThrows<ShippingStartedException>(new CancelOrder(id));
        }

        [Test]
        public void WhenShippingAnOrderThatTheShippingProcessIsStarted_ItShouldBeMarkedAsShipped()
        {
            var id = Guid.NewGuid();
            var orderCreated = BuildOrderCreated(id, basketId: Guid.NewGuid(), numberOfOrderLines: 1);
            Given(orderCreated,
                new ShippingProcessStarted(id));
            When(new ShipOrder(id));
            Then(new OrderShipped(id));
        }

        [Test]
        public void WhenShippingAnOrderWhereShippingIsNotStarted_IShouldGetNotified()
        {
            var id = Guid.NewGuid();
            var orderCreated = BuildOrderCreated(id, basketId: Guid.NewGuid(), numberOfOrderLines: 1);
            Given(orderCreated);
            WhenThrows<InvalidOrderState>(new ShipOrder(id));
        }

        [Test]
        public void WhenTheUserCheckoutWithAnAmountLargerThan100000_TheOrderNeedsApproval()
        {
            var address = new Address("Valid street");
            var basketId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            IdGenerator.GenerateGuid = () => orderId;
            var orderLine = new OrderLine(Guid.NewGuid(), "Ball", 100000, 100001, 1);
            Given(new BasketCreated(basketId, Guid.NewGuid(), 0),
                new ItemAdded(basketId, orderLine),
                new BasketCheckedOut(basketId, address));
            When(new MakePayment(basketId, 100001));
            Then(new OrderCreated(orderId, basketId, Helpers.ToFSharpList(new [] {orderLine})),
                new NeedsApproval(orderId));
        }

        [Test]
        public void WhenTheUserCheckoutWithAnAmountLessThan100000_TheOrderIsAutomaticallyApproved()
        {
            var address = new Address("Valid street");
            var basketId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            IdGenerator.GenerateGuid = () => orderId;
            var orderLine = new OrderLine(Guid.NewGuid(), "Ball", 100000, 100000, 1);
            Given(new BasketCreated(basketId, Guid.NewGuid(), 0),
                new ItemAdded(basketId, orderLine),
                new BasketCheckedOut(basketId, address));
            When(new MakePayment(basketId, 100000));
            Then(new OrderCreated(orderId, basketId, Helpers.ToFSharpList(new[] { orderLine })),
                new OrderApproved(orderId));
        }

        [Test]
        public void WhenApprovingAnOrder_ItShouldBeApproved()
        {
            var orderId = Guid.NewGuid();
            Given(new OrderCreated(orderId, Guid.NewGuid(), FSharpList<OrderLine>.Empty));
            When(new ApproveOrder(orderId));
            Then(new OrderApproved(orderId));
        }

        private OrderCreated BuildOrderCreated(Guid orderId, Guid basketId, int numberOfOrderLines, int pricePerProduct = 100)
        {
            var orderLines = FSharpList<OrderLine>.Empty;
            for (var i = 0; i < numberOfOrderLines; i++)
            {
                orderLines = FSharpList<OrderLine>.Cons(new OrderLine(Guid.NewGuid(), "Line " + i, pricePerProduct, pricePerProduct, 1), orderLines);
            }
            return new OrderCreated(orderId, basketId, orderLines);
        }
    }

##Command

    type StartShippingProcess = { Id: Guid } with interface ICommand
    type CancelOrder = { Id: Guid } with interface ICommand
    type ShipOrder = { Id: Guid } with interface ICommand
    type ApproveOrder = { Id: Guid } with interface ICommand

##Event

    type OrderCreated ={ Id: Guid; BasketId: Guid; OrderLines: OrderLine list }
        with interface IEvent with member this.Id with get() = this.Id

    type ShippingProcessStarted = {Id: Guid}
        with interface IEvent with member this.Id with get() = this.Id

    type OrderCancelled = {Id: Guid}
        with interface IEvent with member this.Id with get() = this.Id

    type OrderShipped = {Id: Guid}
        with interface IEvent with member this.Id with get() = this.Id

    type NeedsApproval = {Id: Guid}
        with interface IEvent with member this.Id with get() = this.Id

    type OrderApproved = {Id: Guid}
        with interface IEvent with member this.Id with get() = this.Id


##Handler
    internal class OrderHandler : 
        IHandle<ApproveOrder>, 
        IHandle<StartShippingProcess>, 
        IHandle<CancelOrder>, 
        IHandle<ShipOrder>
    {
        private readonly IDomainRepository _domainRepository;

        public OrderHandler(IDomainRepository domainRepository)
        {
            _domainRepository = domainRepository;
        }

        public IAggregate Handle(ApproveOrder command)
        {
            var order = _domainRepository.GetById<Order>(command.Id);
            order.Approve();
            return order;
        }

        public IAggregate Handle(StartShippingProcess command)
        {
            var order = _domainRepository.GetById<Order>(command.Id);
            order.StartShippingProcess();
            return order;
        }

        public IAggregate Handle(CancelOrder command)
        {
            var order = _domainRepository.GetById<Order>(command.Id);
            order.Cancel();
            return order;
        }

        public IAggregate Handle(ShipOrder command)
        {
            var order = _domainRepository.GetById<Order>(command.Id);
            order.ShipOrder();
            return order;
        }
    }


##Aggregate
    internal class Order : AggregateBase
    {
        private OrderState _orderState;

        private enum OrderState
        {
            ShippingProcessStarted,
            Created,
            Cancelled
        }

        public Order()
        {
            RegisterTransition<OrderCreated>(Apply);
            RegisterTransition<ShippingProcessStarted>(Apply);
            RegisterTransition<OrderCancelled>(Apply);
        }

        private void Apply(OrderCancelled obj)
        {
            _orderState = OrderState.Cancelled;
        }

        private void Apply(ShippingProcessStarted obj)
        {
            _orderState = OrderState.ShippingProcessStarted;
        }

        private void Apply(OrderCreated obj)
        {
            _orderState = OrderState.Created;
            Id = obj.Id;
        }

        internal Order(Guid basketId, FSharpList<OrderLine> orderLines) : this()
        {
            var id = IdGenerator.GenerateGuid();
            RaiseEvent(new OrderCreated(id, basketId, orderLines));
            var totalPrice = orderLines.Sum(y => y.DiscountedPrice);
            if (totalPrice > 100000)
            {
                RaiseEvent(new NeedsApproval(id));
            }
            else
            {
                RaiseEvent(new OrderApproved(id));
            }
        }

        public void Approve()
        {
            RaiseEvent(new OrderApproved(Id));
        }

        public void StartShippingProcess()
        {
            if (_orderState == OrderState.Cancelled)
                throw new OrderCancelledException();

            RaiseEvent(new ShippingProcessStarted(Id));
        }

        public void Cancel()
        {
            if (_orderState == OrderState.Created)
            {
                RaiseEvent(new OrderCancelled(Id));
            }
            else
            {
                throw new ShippingStartedException();
            }
        }

        public void ShipOrder()
        {
            if (_orderState != OrderState.ShippingProcessStarted)
                throw new InvalidOrderState();
            RaiseEvent(new OrderShipped(Id));
        }
    }

And that finished the implementation of all the features.

The part of the series is [Time for reflection](http://blog.tomasjansson.com/cqrs-the-simple-way-with-eventstore-and-elasticsearch-time-for-reflection/).