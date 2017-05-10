---
layout: post
title: Parsing json with PowerShell and Json.NET
date: '2013-01-17 09:31:00'
tags:
- powershell
- json
---

I've been working lately on a set of scripts to automate setup of computers (I will write more about those later). With setup I mean from a web-developers perspective, so it includes setting up IIS, adding users to computer, changing hosts-file and changing environment-variables. An important part of this is how to specify variables to the scripts and I choose json. This short post will show you the module I implemented to parse a json file with the result of an `HashTable`. So let's get started. I will just show you the code and then explain it under.

    function ParseItem($jsonItem) {
        if($jsonItem.Type -eq "Array") {
            return ParseJsonArray($jsonItem)
        }
        elseif($jsonItem.Type -eq "Object") {
            return ParseJsonObject($jsonItem)
        }
        else {
            return $jsonItem.ToString()
        }
    }

    function ParseJsonObject($jsonObj) {
        $result = @{}
        $jsonObj.Keys | ForEach-Object {
            $key = $_
            $item = $jsonObj[$key]
            $parsedItem = ParseItem $item
            $result.Add($key,$parsedItem)
        }
        return $result
    }

    function ParseJsonArray($jsonArray) {
        $result = @()
        $jsonArray | ForEach-Object {
            $parsedItem = ParseItem $_
            $result += $parsedItem
        }
        return $result
    }

    function ParseJsonString($json) {
        $config = [Newtonsoft.Json.Linq.JObject]::Parse($json)
        return ParseJsonObject($config)
    }

    function ParseJsonFile($fileName) {
        $json = (Get-Content $FileName | Out-String)
        return ParseJsonString $json
    }

    Export-ModuleMember ParseJsonFile, ParseJsonString

So let's start from the bottom of the file. I export the module members `ParseJSonFile` and `ParseJsonString`, those are the entry points for the script. `ParseJsonFile` is a function that just reads the json from a file and then calls `ParseJsonString` and it is in that function the magic starts. If you look at the first row in `ParseJsonString` I call an external assembly, and that is the Json.NET assembly. To call it you have to load the assembly first. You could, as I have done here, use a module manifest for the module and add the assembly to the required assembly list. If you don't want to do so you could also load the assembly before exuting a method from the module using this line:

    [Reflection.Assembly]::LoadFile("path-to\Newtonsoft.Json.dll”)

When I've parsed ths json string I make one assumption, and that is that what you have in the json is an object, that is the only assumption I had since it fits my requirements. The technique I use for the rest of the script is recursion, so if you have a really long json-file you might get an exception, but I don't think that would be a problem. The `ParseJsonObject` creates a new `HashTable` and then loops over all the keys in the object from Json.NET. For each value I call the most interesting function in the script, the `ParseItem` function. This method looks at the value and then do three different things depending on the type of the value. If it is an object it recursively calls the `ParseJsonObject` function, if it is an array it calls the `ParseJsonArray` function and if it is neither of those it takes the string value and returns the value. The last method is the `ParseJsonArray` and it works the same way as the `ParseJsonObject` excepts it's traversing an array instead of an object.

A small example of what it looks like against one json-file I have:


![Json parsing example][1]

  [1]: https://ykaqzw.bay.livefilestore.com/y1pA2PfyvbBJb1RByqI4tyndvpi3P-wV8gcE_cYBahljq8H7jQfzmvB7QBo5h3HKLAStoH0dB2Whp_qkmT6UDirnkPQmq9yOnli/JsonParseExample.png?psid=1 "Json parsing example"