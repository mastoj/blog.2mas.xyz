---
layout: post
title: F# Suave app on dotnet core on Kubernetes on Google Cloud
date: '2016-11-17 11:25:50'
tags:
- fsharp
- suave
- kubernetes
---

I haven't been doing that much F# dotnet core development, but I think it was time for me to try it out. One of the scenarios that I think it will be used a lot is on [kubernetes](http://kubernetes.io/). I choose to run it on google cloud so I didn't have to set up the infrastructure myself. I could probably have used Azure as well since they now have support for it in preview, but I think that google's implementation looks more mature and easier to use from the command line. So let get this little tutorial started.

## Creating the application

Before we need start we need to install the latest dotnet core bits, which we find here: https://www.microsoft.com/net/download/core. Clicking on `Current` tab we will find the latest bits (1.1.0 as of this moment). With dotnet installed we can get going.

### Create project

The first is to create the project. Just navigate to a folder where you want your project and run this command to create a new F# project:

```
dotnet new -l F#
```

Note that the folder name will be the name of the project, in my case it is `suavecore` and that will also be the name of the dll file created.

### Update references

I made some minor changes to the [`project.json` file](https://github.com/mastoj/suavecore/blob/master/project.json):

```
{
    "version": "1.0.0-*",
    "buildOptions": {
        "debugType": "portable",
        "emitEntryPoint": true,
        "compilerName": "fsc",
        "compile": {
            "includeFiles": [
                "Program.fs"
            ]
        }
    },
    "dependencies": {
        "Microsoft.FSharp.Core.netcore": "1.0.0-alpha-*",
        "Suave": "2.0.0-rc2"
    },
    "tools": {
        "dotnet-compile-fsc": "1.0.0-preview2.1-*"
    },
    "frameworks": {
        "netcoreapp1.0": {
            "dependencies": {
                "Microsoft.NETCore.App": {
                    "type": "platform",
                    "version": "1.1.0"
                },
                "Microsoft.FSharp.Core.netcore": "1.0.0-alpha-161111"
            }
        }
    }
}
```

I basically changed to the latest version of all the packages and added a reference to [Suave](https://suave.io/). 

After the update we need to run

```
dotnet restore
```

to install the dependencies.

### Implementing the application

The application implemented is really simple, it is a basic `Hello World` application that also prints the host name. It is a single file application and it all fits in [`Program.fs`](https://github.com/mastoj/suavecore/blob/master/Program.fs):

```language-fsharp
open Suave
open System.Net

type CmdArgs = { IP: System.Net.IPAddress; Port: Sockets.Port }

[<EntryPoint>]
let main argv = 

    // parse arguments
    let args =
        let parse f str = match f str with (true, i) -> Some i | _ -> None

        let (|Port|_|) = parse System.UInt16.TryParse
        let (|IPAddress|_|) = parse System.Net.IPAddress.TryParse

        //default bind to 127.0.0.1:8083
        let defaultArgs = { IP = System.Net.IPAddress.Loopback; Port = 8083us }

        let rec parseArgs b args =
            match args with
            | [] -> b
            | "--ip" :: IPAddress ip :: xs -> parseArgs { b with IP = ip } xs
            | "--port" :: Port p :: xs -> parseArgs { b with Port = p } xs
            | invalidArgs ->
                printfn "error: invalid arguments %A" invalidArgs
                printfn "Usage:"
                printfn "    --ip ADDRESS   ip address (Default: %O)" defaultArgs.IP
                printfn "    --port PORT    port (Default: %i)" defaultArgs.Port
                exit 1

        argv |> List.ofArray |> parseArgs defaultArgs

    let log x = printfn "%A" x; x

    let getHostName() = 
        Dns.GetHostName()

    // start suave
    startWebServer
        { defaultConfig with
            bindings = [ HttpBinding.create HTTP args.IP args.Port ] }
        (Successful.OK (sprintf "Hello world: %s" (getHostName())))

    0
```

That is all we need to try the application. If you run 

```
dotnet run
```

you will start the application and you can now pay a visit to http://localhost:8083.

### Publishing the application

The last thing we need to do is to publish the application, this will create the bits that we will add to our docker container later on. Run

```
dotnet publish -C Release
```

to publish the application to `bin/Release/netcoreapp1.0/publish`. If you navigate to that folder it is now possible to run the publish commands by executing 

```
dotnet suavecore.dll
```

This will start the web server and you can now navigate to http://localhost:8083 again. Note that `suavecore` is the name of my project, if you have a different name of the project folder your name might differ.

## Building the container

To be able to run this on kubernetes later on we will create a docker container. I have [docker beta for OSX](https://beta.docker.com/) installed to build and try out the container. If you are following along I assume you to have that installed.

### Creating the Dockerfile

The [Dockerfile](https://github.com/mastoj/suavecore/blob/master/Dockerfile) is based on the official dotnet core image from microsoft and looks like this:

```
FROM microsoft/dotnet:core
COPY ./bin/Release/netcoreapp1.0/publish /app
WORKDIR /app
EXPOSE 8083
ENTRYPOINT ["dotnet", "suavecore.dll"]
```

It is quite straightforward what is going on. We base our image on the one from Microsoft as mentioned, then we copy our published app to the `app` folder in the container. We expose port `8083` to be able to access it from the outside and lastly we set the entry point to the command to start the application.

### Building the container

Building a container is as simple as

```
docker build . -t mastoj/suavecore:v1.5
```

The container is tagged with the name of my repo for this image on [docker hub](https://hub.docker.com/r/mastoj/suavecore/) and a version number so we can access the correct version when publishing to kubernetes later on.

### Testing the container

Before we publish the container it might be smart to try it out locally first. So to create a container of the newly created image we run

```
docker run -p=8083:8083 --name suave mastoj/suavecore:v1.5 --ip 0.0.0.0
```

The command above will start a running container of our image and name it `suave`. It will also map port `8083` on our local machine to port `8083` on the container. Lastly we will pass the arguments `--ip 0.0.0.0` to the application to tell it to listen all request no matter what the IP is.

Again you can try http://localhost:8083, but this time you should get a little bit different response since the host name of the container is probably not the same as your machine.

### Publish the container

We are now ready to publish the container. For this tutorial we are using a public repo to keep things simple.

```
docker push mastoj/suavecore:v1.5
```

This will upload the image to docker hub and making it accessible to the public. 

## Setting up google cloud

We are now ready to proceed to the google cloud and kubernetes part. The goal is to host the application with three replicas running behind nginx using https. To be able to try it out on [google cloud](https://cloud.google.com/) you need to sign up and create a project. You also need to install the [sdk](https://cloud.google.com/sdk/).

### Creating the kubernetes cluster

If you have the sdk installed it is really simple to set up a new basic cluster. To create a cluster named `k1` run the following

```
gcloud container clusters create k1
```

When you have the cluster up and running you need to install `kubectl`, which is the CLI tool to work with kubernetes.

```
gcloud components install kubectl
```

Now you should be all set to operate your google cloud container cluster, hopefully. 

### Secrets and configmaps

For nginx to run correctly we need to configure it to use `https` and where our application is in the cluster. 

The first part is generating `cert.pem` and `key.pem` files for tls to work. If you have `openssl` installed you can run: 

```
openssl req -newkey rsa:4096 -nodes -sha512 -x509 -days 3650 -nodes -out cert.pem -keyout key.pem
```

The result from this have I stored in a folder in the repo: https://github.com/mastoj/suavecore/tree/master/kubernetes/tls. You should probably never publish these files, I'm just doing it for demo purpose.

When we have the files we can create a secret that we will be able to mount in our containers when they run in the cluster. To create a secret you use `kubectl`

```
kubectl create secret generic tls-certs --from-file=tls/
``` 

We will see later how we access the secrets.

Next step is to add the nginx configuration. The configuration is a file, that we will mount when the container starts. The file is also in repo with the name [frontend.conf](https://github.com/mastoj/suavecore/blob/master/kubernetes/nginx/frontend.conf):

```
upstream hello {
    server hello.default.svc.cluster.local;
}
server {
    listen 443;
    ssl    on;
    ssl_certificate     /etc/tls/cert.pem;
    ssl_certificate_key /etc/tls/key.pem;
    location / {
        proxy_pass http://hello;
    }
}
```

This configuration file will configure nginx to listen to `443` with `ssl` enabled and route the traffic to `hello.default.svc.cluster.local`, which is where our hello nodes will be. You can also see where `nginx` now expect the secrets to be located.

With this done we can now continue on to creating the deployments and services.

### Creating the deployments

If you want to know exactly what a deployment is you should read this: http://kubernetes.io/docs/user-guide/deployments/#what-is-a-deployment. I want go into details about all these concepts, just show you how to configure it. 

#### The frontend deployment

The [`deployments/frontend.yaml`](https://github.com/mastoj/suavecore/blob/master/kubernetes/deployments/frontend.yaml) defines the deployment for the frontend, which is the nginx part of our application.

```
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: frontend
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: frontend
        track: stable
    spec:
      containers:
        - name: nginx
          image: "nginx:1.9.14"
          lifecycle:
            preStop:
              exec:
                command: ["/usr/sbin/nginx","-s","quit"]
          volumeMounts:
            - name: "nginx-frontend-conf"
              mountPath: "/etc/nginx/conf.d"
            - name: "tls-certs"
              mountPath: "/etc/tls"
      volumes:
        - name: "tls-certs"
          secret:
            secretName: "tls-certs"
        - name: "nginx-frontend-conf"
          configMap:
            name: "nginx-frontend-conf"
            items:
              - key: "frontend.conf"
                path: "frontend.conf"
```

In the file we first define that it is a `deployment` and some metadata. The interesting part is the `containers` part where we define that we will use `nginx` and also add a correct shutdown command when the container is stopped. In the `volumes` section we define that we want access to our `secret` named `tls-certs`, and the `configMap` named `nginx-frontend-conf`. For the `configMap` we are only interested in the key `frontend.conf` and we are going to name that file the same as the key. When we have defined the `volumens` we can reference them in the `volumeMounts` section of the file and define where they should go. That is it for the frontend deployment.

To create our deployment in the cluster run

```
kubectl create -f deployments/frontend.yaml
```

#### Application deployment

The application deployment is defined in [`deployments/hello.yaml`](https://github.com/mastoj/suavecore/blob/master/kubernetes/deployments/hello.yaml)

```
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: hello
spec:
  replicas: 3
  template:
    metadata:
      labels:
        app: hello
        track: stable
    spec:
      containers:
        - name: hello
          image: "mastoj/suavecore:v1.5"
          args: ["--ip", "0.0.0.0"]
          ports:
            - name: http
              containerPort: 8083
```

This is a little bit simple deployment. What interesting here is the number of replicas, `3`, and that we are referencing the docker image we have created earlier. To create the deployment of the application we need to run

```
kubectl create -f deployments/hello.yaml
```

### Creating services

Next up is creating services, which allow us to balance the load requests between our nodes and expose the application to the public.

#### The frontend service

The [`services/frontend.yaml`](https://github.com/mastoj/suavecore/blob/master/kubernetes/services/frontend.yaml) is what defines the service for the frontend. 

```
kind: Service
apiVersion: v1
metadata:
  name: "frontend"
spec:
  selector:
    app: "frontend"
  ports:
    - protocol: "TCP"
      port: 443
      targetPort: 443
  type: LoadBalancer
```

We create the service with

```
kubectl create -f services/frontend.yaml
```

When the service is created you can run the command

```
kubectl get services
```

to check the status. There you will see the public ip when it is available.

#### The hello application service

The definition in [`services/hello.yaml`](https://github.com/mastoj/suavecore/blob/master/kubernetes/services/hello.yaml) is similar to the `frontend` definition

```
kind: Service
apiVersion: v1
metadata:
  name: "hello"
spec:
  selector:
    app: "hello"
  ports:
    - protocol: "TCP"
      port: 80
      targetPort: 8083
```

The difference here is that we don't have the `LoadBalancer` part, which means our app will NOT be accessible from the outside, you have to go through our frontend. We also routes the traffic from the service port `80` to the container port `8083` which our containers use. Creating the service is as easy as for the frontend

```
kubectl create -f services/hello.yaml
```

## Win

Everything should now be configured and up and running. You can find the public IP that you should be able to navigate to be executing

```
kubectl get services
```

Remember that it is `https://<your public ip>`, and the cert we are using is self signed so you will probably get a warning about that as well.

The source code for everything is available here: https://github.com/mastoj/suavecore/

If you have any comments or questions, feel free to post them at the comment section.