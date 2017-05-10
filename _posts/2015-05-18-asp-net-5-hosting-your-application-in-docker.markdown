---
layout: post
title: 'ASP.NET 5: Hosting your application in Docker'
date: '2015-05-18 18:46:57'
tags:
- net
- asp-net-5
- docker
---

A couple of weeks ago I had a presentation about ASP.NET 5 and MVC 6 at NNUG Oslo. The presentation wasn't recorded so I thought I just write some blog posts about it insted. This will be a serie of posts where I plan to go through the features that I demonstrated during the presentation, plus some more features that I didn't have time to cover. I'll start with the basic and show one thing at a time and then add features as we go along. So let's get started.

Post in this serie:

* [The pipeline](http://blog.tomasjansson.com/asp-net-5-the-pipeline/)
* [Adding MVC to an application](http://blog.tomasjansson.com/asp-net-5-adding-mvc-to-an-application)
* [Setting up frontend build (with grunt)](http://blog.tomasjansson.com/asp-net-5-setting-up-frontend-build-with-grunt/)
* [IoC and dependency injection](http://blog.tomasjansson.com/asp-net-5-ioc-and-dependency-injection/)
* [View Components](http://blog.tomasjansson.com/asp-net-5-view-components/)
* [Self-hosting the application](http://blog.tomasjansson.com/asp-net-5-self-hosting-the-application/)
* [Hosting your application in docker](http://blog.tomasjansson.com/asp-net-5-hosting-your-application-in-docker/)

Source code: https://github.com/mastoj/OneManBlog

## The next logical step is container hosting

Microsoft stepping in and partnering up with [Docker](https://www.docker.com/) was great news for the future if you ask me. Hosting applications is a container is a nice mix between light weight hosting and isolation between the application. There exist some security issues with hosting in a container that a someone could take advantage of if the container is running on a host that is not isolated enough, but don't let us go into that discussion.

The goal of this post is to show you one way to host your ASP.NET 5 application inside a docker container. I will not go into details about how you should put this container into production, mainly because I need to figure out a nice way to do it myself first. 

## Docker Machine and Docker

To manage different "hosting machines" I use [Docker Machine](https://docs.docker.com/machine/), not because I have many machines but because it is easy to manage machines with Docker Machine. Installation is straightforward and the documentation is good so I want go into all details about Docker Machine, all we're going to do is create virtual machine that we can use to host our docker containers. To create a machine on VirtualBox you run the following command:

    $ docker-machine create --driver virtualbox dev

Note that it is recommended to use msysgit when running Docker Machine, on Windows I recommend using [ConEmu (install from Chocolatey)](https://chocolatey.org/packages/ConEmu) and run bash inside ConEmu to use Docker Machine and Docker. To target this machine and start using Docker against it you run the following command:

    $ eval "$(docker-machine env dev)"

The last thing before we start defining our docker image is to find the IP of the current machine so we can test it later, execute the following and note down the IP (it is most likely 192.168.99.100):

    $ docker-machine ip

## Updating the project

Before we create the container image definition file we need to update the project so we can host it in a linux container. We need one more dependency in our `project.json` file and one more command so we can start the application: 

    "dependencies": {
        "Microsoft.AspNet.Mvc": "6.0.0-beta4",
        "Microsoft.AspNet.Server.IIS": "1.0.0-beta4",
        "Microsoft.AspNet.StaticFiles": "1.0.0-beta4",
        "Microsoft.AspNet.Server.WebListener": "1.0.0-beta4",
        "Microsoft.AspNet.Hosting": "1.0.0-beta4",
        "Kestrel": "1.0.0-beta4"
    },
    "commands": {
        "web": "Microsoft.AspNet.Hosting --server Microsoft.AspNet.Server.WebListener --server.urls http://localhost:5000",
        "kestrel": "Microsoft.AspNet.Hosting --server Kestrel --server.urls http://localhost:5001"
    },

The new part is the `Kestrel` package is a web server that runs on ASP.NET 5 applications on linux. To start a web server with `Kestrel` we add the `kestrel` command which we can use with dnx on linux.

## Creating the Dockerfile

A docker image is sort of starting point for a container. You define images with `Dockerfile`s, which you can base on other images so you get a hiearchy of images basically. A running instance of an image is what is called a container, so they are almost the same thing but one is passive and one is active. We are going to base our image on the official [ASP.NET image](https://registry.hub.docker.com/u/microsoft/aspnet/), but we will add node, grunt and bower so we can build the application when we create the image. The `Dockerfile` we have define look like:

    FROM microsoft/aspnet:vs-1.0.0-beta4

    COPY . /app
    WORKDIR /app

    RUN apt-get update -y && apt-get install --no-install-recommends -y -q \
        curl \
        python \
        build-essential \
        git \
        ca-certificates

    RUN mkdir /nodejs && \
        curl http://nodejs.org/dist/v0.10.33/node-v0.10.33-linux-x64.tar.gz | \
        tar xvzf - -C /nodejs --strip-components=1

    ENV PATH $PATH:/nodejs/bin

    RUN npm install -g grunt-cli bower

    RUN ["dnu", "restore"]
    RUN ["npm", "install", "."]
    RUN ["grunt", "default"]

    EXPOSE 5001

    ENTRYPOINT ["dnx", "./", "kestrel"]

Let us go through this line by line (almost):

* `COPY . /app`, I have put the `Dockerfile` in the same folder as the `project.json` file this command copies all the code on the host into the `/app` folder in the image.
* `WORKDIR /app`, just sets the current working directory for when we execute the rest of the commands.
* The next two `RUN` commands and the `ENV` command installs and add [nodejs](https://nodejs.org/) to the environment in the image.
* When we have `nodejs` installs we can install `grunt` and `bower` using `npm` with the `RUN` command in the image.
* `RUN ["dnu", "restore"]` installs all the packages.
* `RUN ["npm", "install", "."]` installs all the javascript packages.
* `RUN ["grunt", "default"]` execute the `grunt` build steps that I wrote about [here](http://blog.tomasjansson.com/asp-net-5-setting-up-frontend-build-with-grunt/).
* In our command we specified that we will use port 5001 for `kestrel`, so we use `EXPOSE 5001` to expose that port from the image when we run it.
* The last row, `ENTRYPOINT ["dnx", "./", "kestrel"]`, specifies that when we run this image (making it a container) we will execute `dnx` and in the current folder passing it the `kestrel` command.

No when we have the `Dockerfile` ready all we need to do is to create the image:

    $ docker build -t onemanblog .

and then start a container with:

    $ docker run -i -t -p 5001:5001 onemanblog

The `-i` and `-t` flags make the container run in interactive mode, which means we see if it crashes and it also listens to `stdin`, that is, we can press enter to stop the application. The `-p 5001:5001` redirects the port 5001 from host to the container.

## Summary

If you have followed all the steps above you should now have a running container which you can go to `http://<ip from "docker-machine ip">:5001/` and you should see the same application as we previously ran on Windows.