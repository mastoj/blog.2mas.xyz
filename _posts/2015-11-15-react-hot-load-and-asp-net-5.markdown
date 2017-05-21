---
layout: post
title: React hot load and ASP.NET 5
date: '2015-11-15 22:09:39'
tags:
- asp-net-5
- react
- react-hot-load
- webpack
---

One or two months ago I saw a [presentation](https://www.youtube.com/watch?v=xsSnOQynTHs) by [Dan Abramov](https://twitter.com/dan_abramov) about hot loading and react. I have't done much react and I haven't done much webpack which is used to do this, but I know I wanted a sample for ASP.NET 5 and now I finally had the time to try it out. Even though the titles says ASP.NET 5 the solution can be used for basically any web project running on Windows.

## TL;DR

I've put together a small sample with react hot load and ASP.NET 5. You can find the code on [github](https://github.com/mastoj/ReactHotLoadAspNet/).

## What is it?

[React hot load](http://gaearon.github.io/react-hot-loader/) let you change the style files (css/less/scss) and javascript files on the server and see the result directly in the browser while keeping the browser state. This make it really easy to try things out and see the result directly instead of reloading the browser after every minor change. An example is when working with styling, instead of tweaking around in the dev tools you can just change the style file directly. Another example is if you're working with a javascript wizard, then it might be annoying to go through every step when you find an error, hot loading the javascript lets you continue where you are after you change the javascript. 

Below is a demo showing the result. I use atom for changing less and jsx files since the support for jsx+ES6 in Visual Studio isn't the best yet.

![Demo of react hot load]({{ site.url }}/assets/images/migrated/Demo.gif)

## How it sort of works (my version)

I won't cover the details of how hot loading works but the short version of how it works is something like this. Instead of reading the javascript files you read them from a node server, the node server "injects" a "wrapper" between the physical file on disc and the one you pointed too from your web page that routes your javascript command to the file that is currently up to date. If you make a change to a file the wrapper will see this and make a rebuild swapping out what has changed while keeping the state. Keeping the state is possible because of the architecture of react.

## Gotchas

My goal was to get a sample up and running with both javascript and less since that is what I most likely will use in a real project, I also wanted everything to run with the latest version of everything. This caused some problems during the setup and here are a list of things that you need to do to your dev environment to get things working:

* I wanted to run regular npm tasks and to do so in Visual Studio you need the extension [NPM Scripts Task Runner](https://visualstudiogallery.msdn.microsoft.com/8f2f2cbc-4da5-43ba-9de2-c9d08ade4941)
* For some reason `node 5` was needed, so install that.
* To get `node 5` to play nice in Visual Studio you need to add the path for External Web Tools. You find that setting under `Tools->Options->Projects and Solutions->External Web Tools`. Just add the path to `node 5` above the line that points to external tools. It looks like `node 5` installs node modules directly under modules folder and not in a hierarchy as before. This makes Visual Studio to think that a lot of the packages are extraneous, but you can just ignore that.
* One of the packages I used got some weird error if python wasn't installed, so python needs to be installed. I couldn't use version 3 of python so I installed version 2 from [chocolatey](https://chocolatey.org/packages/python2).
* This last part is a little bit tricky and only works because I have both VS2013 and VS2015 I think. I got some error which I found the answer on at [stackoverflow](http://stackoverflow.com/questions/33183161/node-gyp-error-tracker-error-trk0005-failed-to-locate-cl-exe-the-system-c). `Npm` needed to run `cl.exe` for some task, but it didn't find it for VS2015 so changing `Npm` to use VS2013 helped as the SO answer says. The command to do so is `npm config set msvs_version 2013 --global`.

## The setup

I won't cover the whole application and what it does, I'll just cover the pieces to get this up and running.

### The package.json file

The file I ended up with looks like this

```language-json
{
  "version": "0.0.0",
  "name": "",
  "scripts": {
    "start": "node server.js",
    "build": "set NODE_ENV=production && webpack -p --progress --colors"
  },
  "devDependencies": {
    "babel-core": "^6.1.2",
    "babel-loader": "^6.0.1",
    "babel-preset-es2015": "^6.1.2",
    "babel-preset-react": "^6.1.2",
    "css-loader": "^0.22.0",
    "less": "^2.5.3",
    "less-loader": "^2.2.1",
    "react-hot-loader": "^1.3.0",
    "style-loader": "^0.13.0",
    "webpack": "^1.12.2",
    "webpack-dev-server": "^1.12.1"
  },
  "dependencies": {
    "jquery": "2.1.4",
    "marked": "^0.3.5",
    "react": "^0.14.0",
    "react-dom": "^0.14.0"
  }
}
```

I've added to `npm` scripts to the file, `build` that should be used when packaging for production on a build server and `start` that is used to start the server for development. This only works with [webpack](https://webpack.github.io/) so I installed that and all the loaders I needed to build `jsx` and `less` files.

### The server.js

The `server.js` is a small node server that will host the javascript for us during development. It is started with the `start` script task defined in `package.json`. 

```language-javascript
var webpack = require('webpack');
var WebpackDevServer = require('webpack-dev-server');
var config = require('./webpack.config');

new WebpackDevServer(webpack(config), {
    publicPath: config.output.publicPath,
    hot: true,
    historyApiFallback: true,
    headers: { 'Access-Control-Allow-Origin': '*' }
}).listen(3000, 'localhost', function (err, result) {
    if (err) {
        console.log(err);
    }

    console.log('Listening at localhost:3000');
});
```

The server is based on this [boilerplate code](https://github.com/gaearon/react-hot-boilerplate/blob/master/server.js) but I added `headers: { 'Access-Control-Allow-Origin': '*' }`. When I upgraded everything to the latest bits `CORS` was required.

## Webpack configuration

You can run `webpack` directly from the command line, but you usually use a configuration file to do so. This is my first time using `webpack` so it is most likely a guide to how you should do it, more a sample of how I got it to do what I wanted it to do. The `webpack.config.js` I ended up with looks like this:

```language-javascript
var webpack = require('webpack');
var path = require('path');
var outFolder = path.resolve(__dirname, "./wwwroot/app");
var isProduction = process.env.NODE_ENV === 'production ';
var jsxLoaders = isProduction ?
    ['babel?presets[]=es2015,presets[]=react'] :
    ['react-hot', 'babel?presets[]=es2015,presets[]=react']; // only react hot load in debug build
var entryPoint = './content/app.jsx';
var app = isProduction ? [entryPoint] : [
    'webpack-dev-server/client?http://0.0.0.0:3000', // WebpackDevServer host and port
    'webpack/hot/only-dev-server', // "only" prevents reload on syntax errors
    entryPoint
];

module.exports = {
    entry: {
        app: app
    },
    output: {
        path: outFolder,
        filename: "[name].js",
        publicPath: 'http://localhost:3000/static/'
    },
    devtool: "source-map",
    minimize: true,
    module: {
        loaders: [{
            test: /\.(js|jsx)$/,
            loaders: jsxLoaders,
            exclude: /node_modules/
        },
        {
            test: /\.(css|less)$/,
            loaders: ['style','css','less']
        }]
    },
    plugins: [
      new webpack.HotModuleReplacementPlugin()
    ],
    resolve: {
        extensions: ["", ".webpack.js", ".web.js", ".js", ".jsx"]
    },
    devServer: {
        headers: { "Access-Control-Allow-Origin": "*" }
    }
};
```

First I define some settings that differs depending on environment. The environment is set as an environment variable, see the `build` script task in the `package.json` file. One important part is this one: 

```language-javascript
var jsxLoaders = isProduction ?
    ['babel?presets[]=es2015,presets[]=react'] :
    ['react-hot', 'babel?presets[]=es2015,presets[]=react']; // only react hot load in debug build
var entryPoint = './content/app.jsx';
var app = isProduction ? [entryPoint] : [
    'webpack-dev-server/client?http://0.0.0.0:3000', // WebpackDevServer host and port
    'webpack/hot/only-dev-server', // "only" prevents reload on syntax errors
    entryPoint
];
```

This is what make the actual server running, I used to port 3000 to host the files. I also needed to specify the `publicPath` under `output`, that's because the files are not served from the same application as the consumer of the files. As you can see, if we are doing a production build, by running `npm rum build`, we will only use the actual `app.jsx` as entry point. Also, we won't add `react-hot` (alias for `react-hot-loader`) to the list of `jsxLoaders` since I don't want hot loading enabled in production.

I won't try to cover `webpack` in more depth since all this is sort of new to me. 

### The ASP.NET part

If you haven't figured it out by now, the ASP.NET solution stays mainly the same to get this working. The trick is actually just to fire up a node server to host your static content and then point the `script` tags in your solution to that server. So my simple index page looks like this:

```language-aspnet
@{
    // ViewBag.Title = "Home Page";
}
<html>
<head>
    <title>Sample hot load demo</title>
    <link href="/static/"/>
</head>
<body>
    <div id="content"></div>
    @*<script src="/static/app.js"></script>*@
    <script src="http://localhost:3000/static/app.js"></script>
</body>
</html>
```

As you can see I'm pointing to `localhost:3000` instead of directly to disk, this is what makes everything above work. In production probably want to point to the file to disk, and that could probably be solved by tag helpers in ASP.NET 5, or using server side variables based on environment in any other version of ASP.NET.

## Running everything

If you have cloned the [repository](https://github.com/mastoj/ReactHotLoadAspNet) and want to try it out you can now either run start from the `Task Runner Explorer` if you have the `NPM Scripts Task Runner` installed, or you can run `npm run start` from the command line in the root of the web project. 

![Task Runner Explorer]({{ site.url }}/assets/images/migrated/TaskRunner.PNG)

This will start the node server for you. When the node server is up and running you can start the ASP.NET application. Now you can start to interact with the application in the browser and then try to change the `jsx` or `less` files, save and see the changes appear in the browser with no refresh of the page.

## Summary

React hot load looks to me like an awesome way to get fast feedback while doing web development with react. There was a little bit of hazzle to get it up and running on Windows but it is doable, and you probably only need to feel that pain once :). Let me know if you have any questions.