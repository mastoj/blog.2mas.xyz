---
layout: post
title: NServiceBus course summary
date: '2012-12-03 09:30:00'
tags:
- net
- nservicebus
- cqrs
---

Last week I've been attending the course [Enterprise Development with NServiceBus](http://www.programutvikling.no/kurskalenderoversikt.aspx?id=1528011) authored by [Udi Dahan](http://www.udidahan.com/) and held by [Andreas Öhlund](http://andreasohlund.net/) at [Programutvikling](http://www.programutvikling.no/). The course was well structured and the course material, which you get a copy of, is really great. Andreas made all of the topics easy to understand, disregarding the complexity of the topics. This is a course I highly recommend to everyone that wants to learn NServiceBus and messaging in general. Along the way you will also learn some DDD and CQRS.

## Key takeaways
There were a lot of really good takeaways from this course, but the one I think was the most useful one is the [store and forward](http://en.wikipedia.org/wiki/Store_and_forward) pattern. Store and forward is a fundamental pattern to build highly scalable distributed messaging system which NServiceBus is built up on. The other two fundamental patterns, I consider NServiceBus to have three fundamental patterns, are one-way messaging and queues.

### Queues
To store messages NServiceBus uses queues. The default queue is MSMQ, but other queues are supported as well.

### One-way messaging
When NServiceBus services communicate with each other they are sending messages to each other by putting them in each others queues. Since the message is put on the queue the service won't get a direct response, or more exactly, it will not get a response at all. If a response is to be extected that has to be done through a new one-way message back to the originator.

### Store and forward
This is a pattern based on queues and one-way messaging and will make the services highly scalable. This is best explained by an example. Imagine a distributed system that has an order service and an billing service. When a customer makes an order the order service will process it and if it is ok it will send an order accepted message to the billing service. In the process of sending the order accepted message the order will first store the message in it's own queue and when it succeed to connect to the billing service it will send the message. This allows the system as a whole to keep accepting orders even though the billing service is down, which is really good from a business perspective.

## General comments
 
 * NServiceBus is 100 % transactional, that's why it uses RavendDB (as default) and not mongoDB for example to store its own state. That means when a message is processed NServiceBus will make sure it is processed otherwise it will be returned to the queue and NServiceBus will try process the message later. Also, if you interact with a sql server or some other service that supports transaction that could be used while processing a message.
 * The [saga](http://www.udidahan.com/2009/04/20/saga-persistence-and-event-driven-architectures/) implementation in NServiceBus is really powerful and might for example help you [kill your batch jobs](http://skillsmatter.com/podcast/home/death-batch-job).