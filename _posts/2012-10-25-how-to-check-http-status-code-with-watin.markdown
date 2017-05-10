---
layout: post
title: How to check http status code with watin
date: '2012-10-25 07:15:00'
tags:
- net
- watin
---

I'm about to write my own blog, which will replace this one when it's done. When doing so there is no excuse to not do it as it should be done. So I'm using [SpecFlow](http://specflow.org/) to define my features and [WatiN](http://watin.org/) to drive my browser tests. One thing I found was that there is no direct way to check the response code for request. A colleague of mine has written this [post](http://www.kongsli.net/nblog/2008/07/08/internet-explorer-automationwatin-catching-navigation-error-codes/) that covers how you catch navigation erros. But I would like to catch 200 codes as well so I extended it to allow for this. I don't think it is 100 % accurate, since you can only assume that if you don't get an error it is a 200 code, which is not true all the time. Alos, it only extends the IE browser, but that should be ok for this scenarios. My guess is that any redirect code doesn't cause an error which will not work in my scenario. However, here is the code to do the job.

First we have the `NavigationObserver` from the blog that will act as an observer of the browser and will catch errors and also when the navigate complete.

<script src="https://gist.github.com/1207490.js?file=NavigationObserver.cs"></script>

The thoughts behind this is that when an error occurs, we catch it in the error handler and set the flag that an error has occured. In the complete event we check if the flag is set, if the flag is set we won't change the status code, and if it isn't checked we assumes that we have an 200 status code. To make it easier to use this observer I have implemented the following extension to the WatiN `IE` browser.

<script src="https://gist.github.com/1207490.js?file=ObservableBrowser.cs"></script>

If you use this custom browser instead of the standard `IE` you should be able to check some status codes. I might do some work with the code to get it to cover more scenarios. 