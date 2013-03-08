owin-connect
============

Implement node.js express handlers and connect middleware in .NET using OWIN

## The lay of the land

This module has a package.json dependency on the `owin@0.5.0` module which provides basic .NET to node.js interop mechanisms. 

The src\Owin.Connect directory contains a C# library project that implements an OWIN HTTP server. The server implements two functions: `Configure` and `Invoke`. The `Configure` is responsible for loading and configuring an OWIN .NET application. This is where all the patterns of OWIN OM would need to be implemented. The `Invoke` is responsible for processing a single HTTP request using one of the previously loaded OWIN .NET applications, normalized to `Func<IDictionary<string,object>,Task>` form.

Both `Configure` and `Invoke` are exposed to node.js using the fundational capabilities of `owin`. The lib\owin-connect.js calls these functions from node.js. The lib\owin-connect.js exports a function that is a factory of connect middleware. The function takes the name of the OWIN .NET assembly file as a parameter, and passes it (along with other optional parameters) to the `Configure` method of the OWIN HTTP server. 

## Building

The Owin.Connect library needs to be built first. Once we arrive in a stable place, a binary will be checked into git to make consuming owin-connect easier. 

From a VS 2012 command prompt, go to the root of the project and 

```
msbuild src\Owin.Connect\Owin.Connect.sln
set OWIN_CONNECT_NATIVE=C:\projects\owin-connect\src\Owin.Connect\bin\Debug\Owin.Connect.dll
```

The OWIN_CONNECT_NATIVE must be set to the location of the built Owin.Connect.dll. The owin-connect.js will use that location if specified. This is helpful during development and required until a stable binary is checked into the repo.

## Running the sample

You need Windows, node.js 0.8.x (tested with 0.8.19), and .NET 4.5 along with VS 2012 toolset for building.

Assuming the project was git-cloned and built, run this from the VS 2012 command prompt from the root of the project:

```
npm install
cs samples\hello
npm install express
build.bat
node server.js
```

The go to http://localhost:3000/node. This should display a message from an express handler in node.js. 

If you go to http://localhost:3000/net, you should see a similar message from the .NET OWIN application in Owin.Samples.dll plugged in as a handler to the express pipeline.
