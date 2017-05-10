---
layout: post
title: Setting up Event Store with kubernetes on google cloud
date: '2016-11-23 22:40:25'
tags:
- eventstore
- kubernetes
---

To prepare myself for my new job, which will involve some kubernetes stuff, I've played around with it somewhat lately, as you could see in this [post](http://blog.2mas.xyz/fsharp-suave-app-on-dotnet-core-on-kubernetes-on-google-cloud/). This post is taking things one step further without making it that much more advanced. The goal for this post is to set up a [Event Store](http://geteventstore.com/) cluster on google container engine with a simple script. A prerequisite to get any of this working is that you have installed [`gcloud`](https://cloud.google.com/sdk/) and [`kubectl`](https://cloud.google.com/container-engine/docs/quickstart).

If you don't want to read the whole post and just go to the code you can look it up on [github](https://github.com/mastoj/eventstore-kubernetes). The [`naïve`](https://github.com/mastoj/eventstore-kubernetes/tree/naive) and [`master`](https://github.com/mastoj/eventstore-kubernetes) branches will be described in this post.

## Disclaimer

What I will describe here will expose the Event Store cluster to the public and is something you **should not** do, I do it to make it easier to test that it works. I haven't done any performance tests or reliability tests on my setup either, which you should probably do before using it in production. 

## The end goal

I did two different approaches, which both will be covered in this post, that had the same end goal. I wanted to have a cluster of Event Store nodes running behind a [headless kubernetes service](http://kubernetes.io/docs/user-guide/services/#headless-services) and nginx on top of that to add access to the public. Using a headless kubernetes service will result in a service registered with a dns registration that resolves to all the IPs for the associated containers, and this is exactly what EventStore needs to to discovery through dns.

## Configuring nginx

I put this section first since it is the same for both approaches. 

### Nginx configuraion

The configuration of nginx will be stored in a `configmap` and looks like this, https://github.com/mastoj/eventstore-kubernetes/blob/master/nginx/frontend.conf:

```
upstream es {
    server es.default.svc.cluster.local:2113;
}
server {
    listen 2113;
    location / {
        proxy_set_header    X-Real-IP $remote_addr;
        proxy_set_header    Host      $http_host;
        proxy_pass          http://es;
    }
}
```

If you know nginx, this is just basic configuration. First we create an `upstream` that can be references later on by our `proxy_pass` when someone is visiting the path `/`. The url `es.default.svc.cluster.local` is the dns registration that our Event Store node will get when we get to that point. The `server` section just defines that we should listen on port `80` and proxy the traffic to the `upstream ` defined.

To create the `configmap` we can execute this command

```
kubectl create configmap nginx-es-frontend-conf --from-file=nginx/frontend.conf
```

### The nginx deployment

This is basically the same as I used in the previous post, if you read that one. The [specification](https://github.com/mastoj/eventstore-kubernetes/blob/master/deployments/frontend-es.yaml) looks like this:

```
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: frontend-es
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: frontend-es
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
            - name: "nginx-es-frontend-conf"
              mountPath: "/etc/nginx/conf.d"
      volumes:
        - name: "nginx-es-frontend-conf"
          configMap:
            name: "nginx-es-frontend-conf"
            items:
              - key: "frontend.conf"
                path: "frontend.conf"
```

It only contains one container, the `nginx` one, which uses the `configmap` created above for configuration. We only need one replica at the moment.

To create it we run the following command

```
kubectl create -f deployments/frontend-es.yaml
```

### The nginx service

To create a public IP we need to create a service on top of this deployment. The [specification](https://github.com/mastoj/eventstore-kubernetes/blob/master/services/frontend-es.yaml) for the service looks like this:

```
kind: Service
apiVersion: v1
metadata:
  name: "frontend-es"
spec:
  selector:
    app: "frontend-es"
  ports:
    - protocol: "TCP"
      port: 2113
      targetPort: 2113
  type: LoadBalancer
```

To create the service and finish up the nginx part of the post we run the following command

```
kubectl create -f services/frontend-es.yaml
```

## First approach - the naïve one

The first thing I wanted to do was to get a cluster up and running without persisting the data to disk, that is, only keep the data in the container. A cluster like that might work for development, but not in production. My grand masterplan was to just add persistent to that cluster after the cluster was up and running, which did not work. How to get persistent will be covered under [*Second approach*](#secondapproachaddingpersistentdata). Before we get into the persistent part, let's get this cluster up and running.

### Creating the deployment

The [deployment file](https://github.com/mastoj/eventstore-kubernetes/blob/naive/deployments/eventstore.yaml) is really simple:

```
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: es
spec:
  replicas: 3
  template:
    metadata:
      labels:
        app: es
    spec:
      containers:
        - name: es
          image: "eventstore/eventstore"
          env: 
            - name: EVENTSTORE_INT_IP
              valueFrom:
                fieldRef:
                  fieldPath: status.podIP
            - name: EVENTSTORE_EXT_IP
              valueFrom:
                fieldRef:
                  fieldPath: status.podIP
            - name: EVENTSTORE_INT_TCP_PORT
              value: "1111"
            - name: EVENTSTORE_EXT_TCP_PORT
              value: "1112"
            - name: EVENTSTORE_INT_HTTP_PORT
              value: "2114"
            - name: EVENTSTORE_EXT_HTTP_PORT
              value: "2113"
            - name: EVENTSTORE_CLUSTER_SIZE
              value: "3"
            - name: EVENTSTORE_CLUSTER_DNS
              value: "es.default.svc.cluster.local"
            - name: EVENTSTORE_CLUSTER_GOSSIP_PORT
              value: "2114"
            - name: EVENTSTORE_GOSSIP_ALLOWED_DIFFERENCE_MS
              value: "600000"
            - name: EVENTSTORE_INT_HTTP_PREFIXES
              value: "http://*:2114/"
            - name: EVENTSTORE_EXT_HTTP_PREFIXES
              value: "http://*:2113/"
          ports:
            - containerPort: 2113
            - containerPort: 2114
            - containerPort: 1111
            - containerPort: 1112
```

We use the Event Store docker image from [docker hub](https://hub.docker.com/r/eventstore/eventstore/). This image doesn't allow command line arguments, so we need to use environment variables to configure it. You can read about Event Store configuration [here](http://docs.geteventstore.com/server/3.9.0/command-line-arguments/). Every container (we are running three here) need to use their own IP during configuration, and with kubernetes we can access that data through `status.podIP` when we configure the environment variables.

Creating is as simple as before:

```
kubectl create -f deployments/eventstore.yaml
```

That should create a three nodes, but as of this moment they will fail to find each other, and that is why we need the service.

### Creating the service

Creating the service is even easier than the deployment. The [specification file](https://github.com/mastoj/eventstore-kubernetes/blob/naive/services/eventstore.yaml) looks like this:

```
kind: Service
apiVersion: v1
metadata:
  name: "es"
spec:
  selector:
    app: "es"
  ports:
    - protocol: "TCP"
      port: 2113
      targetPort: 2113
  clusterIP: None
```

We only expose the 2113 port, which means that we will only be able to talk over http to the event store cluster. To the service we name all the containers that has the label `app: "es"`. The last thing to know is that we set the `clusterIP` to `None`, this will not create one single IP in the DNS for this service, instead it will resolve to all the IPs of the containers and that is exactly what Event Store needs to be able to configure itself.

Again we are using `kubectl` to create the service:

```
kubectl create -f services/eventstore.yaml
```

When this is created we should now be ready to test it.

### Test

To test that it works run the following command: 

```
kubectl get services
```

In the result from that command you will find an `External IP`. To access Event Store, open the browser and go to `<external ip>:2113`. If everything works as expected you should now have access to Event Store.

### Challenges with approach one

The major challenge is persistent data, how do you map one persistent volume to each node in the cluster? Leave a comment on the post if you have any idea. 

Let us say that you solve the first problem, how do you make sure the nodes get the same persistent volume the next time you restart the cluster?

Both those two problems is what got me started working on the second approach.

There is a third problem, which we won't fix, and that is increasing the number of replicas won't work. The reason be that Event Store doesn't support elastic scaling, so increasing the number of replicas will only add "clones" to the cluster, not increase its size.

## Second approach - adding persistent data

We are still going to use deployments, but instead of letting the number of replicas define the number of nodes we will create one deployment with one replica per node. This way we can force each node to get access to the same persistent data volume when it is restarted, and using deployments will also handle restart for us.

### Generating deployment

The difference from the first approach here is that we will generate the deployment from a template instead of creating one. For this to work we need both a template and a simple script that generates the deployments. The [template file](https://github.com/mastoj/eventstore-kubernetes/blob/master/templates/es_deployment_template.yaml) looks like this: 

```
apiVersion: extensions/v1beta1
kind: Deployment
metadata:
  name: es-${nodenumber}
spec:
  replicas: 1
  template:
    metadata:
      labels:
        app: es-${nodenumber}
        escluster: es
    spec:
      containers:
        - name: es-${nodenumber}
          image: "eventstore/eventstore"
          env: 
            - name: EVENTSTORE_INT_IP
              valueFrom:
                fieldRef:
                  fieldPath: status.podIP
            - name: EVENTSTORE_EXT_IP
              valueFrom:
                fieldRef:
                  fieldPath: status.podIP
            - name: EVENTSTORE_INT_TCP_PORT
              value: "1111"
            - name: EVENTSTORE_EXT_TCP_PORT
              value: "1112"
            - name: EVENTSTORE_INT_HTTP_PORT
              value: "2114"
            - name: EVENTSTORE_EXT_HTTP_PORT
              value: "2113"
            - name: EVENTSTORE_CLUSTER_SIZE
              value: "${nodecount}"
            - name: EVENTSTORE_CLUSTER_DNS
              value: "es.default.svc.cluster.local"
            - name: EVENTSTORE_CLUSTER_GOSSIP_PORT
              value: "2114"
            - name: EVENTSTORE_GOSSIP_ALLOWED_DIFFERENCE_MS
              value: "600000"
            - name: EVENTSTORE_INT_HTTP_PREFIXES
              value: "http://*:2114/"
            - name: EVENTSTORE_EXT_HTTP_PREFIXES
              value: "http://*:2113/"
            - name: EVENTSTORE_DB
              value: "/usr/data/eventstore/data"
            - name: EVENTSTORE_LOG
              value: "/usr/data/eventstore/log"
          ports:
            - containerPort: 2113
            - containerPort: 2114
            - containerPort: 1111
            - containerPort: 1112
          volumeMounts:
            - mountPath: "/usr/data/eventstore"
              name: espd
      volumes:
        - name: espd
          gcePersistentDisk:
            pdName: esdisk-${nodenumber}
            fsType: ext4
```

The template looks almost the same as in the first case, but we have now added two; `nodenumber` and `nodecount`. We have also added a `gcePersistentDisk` which we mount to `usr/data/eventstore`, and that is the parent folder we use for logs and data in the configuration of Event Store, `EVENTSTORE_DB` and `EVENTSTORE_LOGS`. We have also added a new label `escluster` which will be used for the service to identify which nodes that should be included in the service. 

To generate the actual deploy files we run a bash script that has the following content: 

```
    for ((c=1; c<=$count; c++ ))
    do
        cat ../templates/es_deployment_template.yaml | sed -e "s/\${nodenumber}/$c/" | sed -e "s/\${nodecount}/$count/" > .tmp/es_deployment_$c.yaml
    done
```

Running that will generate one file for each node, and each file has its own volume mounted. The script as a whole can be found [here](https://github.com/mastoj/eventstore-kubernetes/blob/master/scripts/create_cluster.sh).

When the files has been generated we can then run:

```
    for ((c=1; c<=$count; c++ ))
    do
        kubectl apply -f .tmp/es_deployment_$c.yaml
    done
```

This code will create one deployment per file. Since we are using deployments our pods will be restarted if they crashes.

### Creating the service

This is exactly the same as in the first approach, but with a minor change. We will use the `escluster` label to identify the pods to add to the service. The file is [here](https://github.com/mastoj/eventstore-kubernetes/blob/master/services/eventstore.yaml)

### The create cluster script

It is not reasonable to execute any of this by hand, so I created this [script](https://github.com/mastoj/eventstore-kubernetes/blob/master/scripts/create_cluster.sh). I will dissect it here:

```
#!/bin/bash

function init {
    rm -rf .tmp
    mkdir -p .tmp
}

function validateInput {
    count=$1
    re='^[0-9]+$'
    if ! [[ $count =~ $re ]] ; then
        echo "error: Not a number" >&2; exit 1
    fi
}
```

The first part is just some house keeping and validating input arguments. The plan is that you should be able to create a cluster of any size by running: `./create_cluster.sh <size>`. 

``` 
function createSpecs {
    local count=$1
    for ((c=1; c<=$count; c++ ))
    do
        cat ../templates/es_deployment_template.yaml | sed -e "s/\${nodenumber}/$c/" | sed -e "s/\${nodecount}/$count/" > .tmp/es_deployment_$c.yaml
    done
}

function createDeployments {
    local count=$1
    for ((c=1; c<=$count; c++ ))
    do
        kubectl apply -f .tmp/es_deployment_$c.yaml
    done
}
```

The next part defines functions to generate the deployment files and how they should be executed.

```
function createEsService {
    kubectl create -f ../services/eventstore.yaml
}
```

This finish of the Event Store part of the script by creating a service on top of the nodes created by deployment.

```
function addNginxConfig {
    kubectl create configmap nginx-es-frontend-conf --from-file=../nginx/frontend.conf
}

function createFrontendDeployment {
    kubectl create -f ../deployments/frontend-es.yaml
}

function createFrontendService {
    kubectl create -f ../services/frontend-es.yaml
}
```

The next part is basically what we described in the section about `nginx` setup.

```
function createDisks {
    local count=$1
    for ((c=1; c<=$count; c++ ))
    do
        if gcloud compute disks list esdisk-$c | grep esdisk-$c; then
            echo "creating disk: esdisk-$c" 
            gcloud compute disks create --size=10GB esdisk-$c
        else
            echo "disk already exists: esdisk-$c"
        fi
    done
}
```

A simple helper function to create disks on google cloud.

```
function createEsCluster {
    local count=$1
    createSpecs $count
    createDeployments $count
    createEsService
}

function createFrontEnd {
    addNginxConfig
    createFrontendDeployment
    createFrontendService
}
```

The last two functions is just to make it easier to read what is going on.

```
init
validateInput $1 #sets the variable $count
createDisks $count
createEsCluster $count
createFrontEnd
```

With all the functions defined it is quite clear what is going on in this script.

### Test

The easier way to test it is to create the cluster:

```
./create_cluster.sh 5
```

Add some data to the cluster. You do that by finding the external IP of the nginx service and then follow the [getting started instructions](http://docs.geteventstore.com/introduction/3.9.0/) for Event Store to add some data.

With that in place you can kill pods as you like to simulate failures with:

```
kubectl delete pod --now <pod id>
```

When you kill a pod a new should be created, but this time it should use the same persistent disk as the old one.

You can even delete the whole cluster and rebuild it again. I have added [delete cluster script](https://github.com/mastoj/eventstore-kubernetes/blob/master/scripts/delete_cluster.sh) that will delete everything but the disks. If you then create the cluster again the data you added should still be there, since we are using the same disks for the cluster.

If you want to change the size of the cluster you can actually do that as well, just delete the cluster and create it again with a larger cluster size and that should work.

Note that if you delete and create the cluster you might end up with a new IP.

## Summary

I wouldn't say that this is perfect, but it definitely a start. One drawback is that it doesn't allow for zero downtime increase of cluster size. It could probably be added, but it is out of scope for this post. I haven't tested the performance either, and that is probably something you should do before actually using it. As I mentioned earlier, in a production environment you shouldn't expose Event Store to the public. 

There is probably a lot more comments one can have about this setup, but I leave that up to you. Feel free to come with both positive and negative comments :).

