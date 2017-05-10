---
layout: post
title: How to start IIS Express with PowerShell
date: '2012-11-23 09:29:00'
tags:
- iis-express
- powershell
---

I had a presentation about [SignalR](http://signalr.net/) last week at [BEKK](http://www.bekk.no). In the presentation I wanted to show what a scale out scenario could look like, and to do that I need at least two instances of a web application. Using the script below I was able to fire up two instances of the same application on different ports in IIS Express.

	param( 
		[string] $port = $(throw "Port is required.")
	)
	$iisExpressExe = '"c:\Program Files (x86)\IIS Express\iisexpress.exe"'
	$path = (Resolve-path .)
	Write-Host $path
	Write-host "Starting site on port: $port"
	$params = "/port:$port /path:$path"
	$command = "$iisExpressExe $params"
	cmd /c start cmd /k "$command"
	Start-Sleep -m 1000
	Write-Host "Site started"

That was all I wanted to share today. Hope someone find it useful.