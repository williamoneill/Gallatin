# Gallatin.service.exe

Gallatin Proxy is a web content filter for Windows, Linux, and Mac OS. Many other content
filters use URL blacklists. While this will work, these lists always need to be updated.
Gallatin does support URL blacklists, and it also examines the web content in real-time
to determine if a page should be blocked. Finally, it is easy for developers to create their
own add-ins to further extend Gallatin's functionality. 

# Developer Platform

To extend Gallatin, inherit from the provided interfaces in the [Gallatin Contracts directory](https://github.com/williamoneill/Gallatin/tree/master/Gallatin.Contracts)
and then place your binaries in "addins" subdirectory. Gallatin will pick up your add-in the next time it is restarted or automatic updates are applied.

Gallatin currently allows developers to extend and filter in three different ways:
* [White list evaluator](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/IWhitelistEvaluator.cs) - Allows third-party extensions to white-list a client connnection, circumventing all filtering for the specific connection.
* [Connection filter](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/IConnectionFilter.cs) - Provides a hook to filter and block the pending HTTP connection and return an HTML message to the client.
* [Response filter](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/IResponseFilter.cs) - Another hook used to interpret the HTTP response and modify or block the response, returning a modifie HTML response to the client.
* [Add-in logging](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/ILogger.cs) - Simple logging facilities to log messages to the configured Gallatin log output

Gallatin provides some powerful, default loggers that can be used as examples. These default services are located in the 
[Gallatin.Filter}(https://github.com/williamoneill/Gallatin/tree/master/Gallatin.Filter) directory.

# Documentation

Visit the [Gallatin Proxy website](http://gallatinproxy.com) for detailed documentation and pre-built binaries.

# License
Copyright (c) 2011 Bill O'Neill, under the Apache license. See LICENSE file.
