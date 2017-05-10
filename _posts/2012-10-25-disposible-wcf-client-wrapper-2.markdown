---
layout: post
title: Disposible WCF client wrapper
date: '2012-10-25 07:11:00'
tags:
- wcf
- net
---

Ok, so you have implemented your WCF service and is about to implement your service client. As usual you want your client to be disposed as it should so you start writing something like: 

	using(var client = new SomeServiceType())
	{
		// Do stuff with your client
	}  // Can throw exception here

Usually this works perfectly fine, but in the case with WCF this won't work as expected. The reason for this is that Microsoft is sort of "violating" their own definition that a call to `Dispose` should never throw an exception, but in case with WCF that is not true. The call to `Dispose` for the client (which appears when leaving the using block above) might throw an exception when `Close` is called in the `Dispose` function due to some communication issues. Why is this so bad you might ask? The problem is if you get an exception inside of your using block, if you get an exception there it would be "swallowed" by the exception caused in the `Dispose` method. If you want to read about it more you can find some information at the following locations: 

* [Avoiding Problems with the Using Statement][1]
* [Blog discussing the issue][2]

One solution I found on the web is presented [here][3], but I don't like that solution since it is somewhat introducing a new word `Use` instead of `Using` that we are used to. So instead of that solution I wrote a generic wrapper that takes the type it is wrapping, creates a static factory that generates clients of that type and exposes the communication channel as a public property. When creating a new instance of the wrapper the factory is not re-created, instead it uses the old one and opens the channel object. The client wrapper implements the `IDisposable` interface and in the `Dispose` method the wrapper closes the channel properly and "swallows" the exception that might occur when `Abort` on the channel is called (which should never occur). The wrapper code is as follows:

    /// <summary>
    /// Wraps a service client so you can use "using" without worrying of getting
    /// your business exception swalloed. Usage:
    /// <example>
    /// <code>
    /// using(var serviceClient = new ServiceClientWrapper&lt;IServiceContract&gt;)
    /// {
    /// serviceClient.Channel.YourServiceCall(request);
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="ServiceType">The type of the ervice type.</typeparam>
    public class ServiceClientWrapper<ServiceType> : IDisposable
    {
        private ServiceType _channel;
        /// <summary>
        /// Gets the channel.
        /// </summary>
        /// <value>The channel.</value>
        public ServiceType Channel
        {
            get { return _channel; }
        }

        private static ChannelFactory<ServiceType> _channelFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceClientWrapper&lt;ServiceType&gt;"/> class.
        /// As a default the endpoint that is used is the one named after the contracts full name.
        /// </summary>
        public ServiceClientWrapper() : this(typeof(ServiceType).FullName)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceClientWrapper&lt;ServiceType&gt;"/> class.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        public ServiceClientWrapper(string endpoint)
        {
            if (_channelFactory == null)
                _channelFactory = new ChannelFactory<ServiceType>(endpoint);
            _channel = _channelFactory.CreateChannel();
            ((IChannel)_channel).Open();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                ((IChannel)_channel).Close();
            }
            catch (CommunicationException e)
            {
                ((IChannel)_channel).Abort();
            }
            catch (TimeoutException e)
            {
                ((IChannel)_channel).Abort();
            }
            catch (Exception e)
            {
                ((IChannel)_channel).Abort();
                // TODO: Insert logging
            }
        }
    }

Now when you have your wrapper all you need to do is use the following code:

	public class ClientWrapperUsage
	{
	    public static void main(string[] args)
	    {
	        using(var clientWrapper = new ServiceClientWrapper<ServiceType>())
	        {
	            var response = clientWrapper.Channel.ServiceCall();
	        }
	    }
	}

I think the solution is quite ok, but I think we should never have been forced to implement our own wrapper to solve the issue. The code is also availble at github in this [gist][4]

[1]:http://msdn.microsoft.com/en-us/library/aa355056.aspx "Avoiding Problems with the Using Statement"
[2]:http://social.msdn.microsoft.com/Forums/en-US/wcf/thread/b95b91c7-d498-446c-b38f-ef132989c154 "Blog discussing the problem"
[3]:http://old.iserviceoriented.com/blog/post/Indisposable+-+WCF+Gotcha+1.aspx "Alternative solution"
[4]:https://gist.github.com/730846 "ServiceClientWrapper gist"