---
layout: post
title: Debugging your Windows Service in Visual Studio
date: '2012-10-25 07:22:00'
tags:
- wcf
- net
---

It could be hard to debug your windows services if you play by the book. If you do play by the book you need to install the windows service and then attach a debugger to the process to be able to debug the service, of course you might need to do that in some cases maybe to make sure that the service acts as it should under the account it is running. However, to debug the actual functionality it is always easier to just hit F5 in Visual Studio and start debugging and here is how you do that. First, create a _Service_ project for your reusable code. The first file you should add is a windows service class, `CustomServiceBase`, that extends the `ServiceBase` class. 

	namespace Service
	{
		public class CustomServiceBase : ServiceBase
		{
			public void StartService(string[] args)
			{
				OnStart(args);
			}

			public void StopService()
			{
				OnStop();
			}
		}
	}

This will be your new `ServiceBase` class instead of `ServiceBase`. Basically all it do is adding two methods that is used later on so we don't have to use reflection to access the `OnStart` and the `OnStop` methods. The next file you need to add to your _Service_ is the generic `ServiceManager` class that will acts as your _runner_. 

	namespace Service
	{
		public delegate void ServiceManagerHandler(object sender, EventArgs args);

		public class ServiceManager<T> where T : CustomServiceBase, new() 
		{
			public event ServiceManagerHandler ServiceDefaultModeStarting;
			public event ServiceManagerHandler ServiceInteractiveModeStarting;

			public void Run()
			{
				if (!Debugger.IsAttached)
				{
					StartServiceDefault();
				}
				else
				{
					StartServiceInteractive();
				}
			}

			private void StartServiceDefault()
			{
				if (ServiceDefaultModeStarting != null)
					ServiceDefaultModeStarting(this, null);
				ServiceBase[] ServicesToRun;
				ServicesToRun = new ServiceBase[] 
					{ 
						new T() 
					};
				ServiceBase.Run(ServicesToRun);
			}

			private void StartServiceInteractive()
			{
				if (ServiceInteractiveModeStarting != null)
					ServiceInteractiveModeStarting(this, null);
				Console.Title = Assembly.GetEntryAssembly().FullName;
				Console.WriteLine(Assembly.GetEntryAssembly().FullName);
				T service = new T();

				Console.WriteLine();
				try
				{
					Console.WriteLine();
					Console.WriteLine("Calling StartService()");
					Console.WriteLine("-----------------------------------------------");

					service.StartService(null);

					Console.WriteLine();
					Console.WriteLine("Service running in foreground, press enter to exit...");
					Console.ReadLine();

					Console.WriteLine("Calling StopService()");
					Console.WriteLine("-----------------------------------------------");

					service.StopService();
				}
				catch (Exception ex)
				{
					throw;
				}
			}
		}
	}

This is the most simplest version doesn't take any arguments when starting the service. It's straight forward to add that functionality but we don't need it in the project I am in. So what does the `ServiceManager` do? It takes a `CustomServiceBase` type as the generic input, the it checks to see if a debugger is attached or not. What do we achieve with this? If you start it in debug mode in Visual Studio a debugger will be attach and we can take control over the execution, so in that case we call the `StartServiceInteractiveMode` which calls the `StartService` method in the `CustomServiceBase` class and then waits for some input before it calls the `StopService` method. Now you might ask how I know I have a console available? The answer is I don't, I'm using my own convention that the _ServiceImplementation_ project will be console application. To use this new simple library you created above your regular _ServiceImplementation_ project will look something like this:

	namespace ServiceImplementation
	{
		public partial class YourService : CustomServiceBase
		{
			public void OnStart(string[] args)
			{
				// Code to run on start
			}
			
			public void OnStop()
			{
				// Code to run on stop
			}
		}
	}

This is no different compared to a regular service implementation class. The `Program` class will look something like this

	namespace ServiceImplementation
	{
		public static void Main()
		{
			var serviceManager = new ServiceManager<ConversionService>();
			serviceManager.ServiceDefaultModeStarting += new ServiceManagerHandler(serviceManager_ServiceDefaultModeStarting);
			serviceManager.ServiceInteractiveModeStarting += new ServiceManagerHandler(serviceManager_ServiceInteractiveModeStarting);
			serviceManager.Run();
		}

		static void serviceManager_ServiceInteractiveModeStarting(object sender, EventArgs args)
		{
			// if you want to do something when the service is starting in interactive/debug mode
		}

		static void serviceManager_ServiceDefaultModeStarting(object sender, EventArgs args)
		{
			// if you want to do something when the service is starting in default mode
		}
	}

You don't have to implement the two event methods if you don't need to but it could be good in logging purpose. 

**Note:** Remeber that the _ServiceImplementation_ project should be a console application and nothing else. 

So that's basically all you need to make your windows services debuggable unde visual studio. If you have any problem or find it useful let me know.