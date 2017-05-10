---
layout: post
title: Puppet & Windows
date: '2014-10-13 14:25:45'
tags:
- devops
- puppet
- vagrant
---

I've spent some time the last couple of weeks playing around with [Puppet](http://puppetlabs.com/) (and [Vagrant](http://www.vagrantup.com/)) on Windows after reading through Puppet Cookbook and Beginner's guide from [John Arundel](http://bitfieldconsulting.com/). Even though the books doesn't cover Windows that much they are a good introduction to Puppet.

I'll post a longer post later on describing what I've done in more detail, but as of know I just thought I would point you to the repos and links where you can see me playing with this stuff.

 * [WinPuppet](https://github.com/mastoj/winpuppet) - this is where I keep the puppet code for this experiment
 * [NirvanaService](https://github.com/mastoj/nirvanaservice) - a service wrapper that allow you to basically wrap any executable as a service in a really flexible manner
 * [My nuget feed](https://www.myget.org/feed/Packages/crazy-choco) - this is a where I keep random [Chocolatey packages](https://chocolatey.org/), try `nuget list -source https://www.myget.org/F/crazy-choco/`
 
To get started you should just be able to clone the WinPuppet repo and run `vagrant up` (if you have vagrant installed and virtualbox installed). That will download a vagrant box, create a vm from the box, install puppet, install chocolatey and provision the virtual machine.

So far I think everything works really smooth. Everything above is work in progress, but I think the `NirvanaService` is sort of version 1, at least for my needs. The way I've done it is to create chocolatey packages for things I would like to install, use the chocolatey provider in puppet to install them and then run executable with `NirvanaService`. Please come with feedback or suggestion if you have any.