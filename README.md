# Gallatin.service.exe

Gallatin Proxy is a web content filter for Windows, Linux, and Mac OS. Many other web
filters use URL blacklists. While this will work, these lists always need to be updated and are
often too restrictive and cumbersome.

As expected, Gallatin supports URL blacklists/whitelists; moreover, Gallatin intelligently examines the actual web content in real-time
to determine if a page should be blocked. This read-time filtering examines and blocks
pages well before they can be manually added to a URL blacklist. 

Finally, it is easy for developers to create their own add-ins to further extend Gallatin's functionality. For example,
you could create an add-in to disable web access during specific times of the day. A simple implementation could be easily written in a few lines of code.
There are many possibilities for expansion if you consider Gallatin a development platform rather than a web content filter. I encourage
you to have fun, extend, and explore the possibilities of this platform.

# Developer Platform

To extend Gallatin, inherit from the provided interfaces in the [Gallatin Contracts directory](https://github.com/williamoneill/Gallatin/tree/master/Gallatin.Contracts)
and then place your binaries in "addins" subdirectory. Gallatin will pick up your add-in the next time it is restarted or automatic updates are applied.

Gallatin currently allows developers to extend and filter in three different ways:

* [White list evaluator](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/IWhitelistEvaluator.cs) - Allows third-party extensions to white-list a client connnection, circumventing all filtering for the specific connection.
* [Connection filter](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/IConnectionFilter.cs) - Provides a hook to filter and block the pending HTTP connection and return an HTML message to the client.
* [Response filter](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/IResponseFilter.cs) - Another hook used to interpret the HTTP response and modify or block the response, returning a modified HTML response to the client.
* [Add-in logging](https://github.com/williamoneill/Gallatin/blob/master/Gallatin.Contracts/ILogger.cs) - Simple logging facilities to log messages to the configured Gallatin log output.

Gallatin provides some powerful, default filters that can be used as examples. These default filters are located in the 
[Gallatin.Filter](https://github.com/williamoneill/Gallatin/tree/master/Gallatin.Filter) directory.

More hooks can be extended and added as needed. These are the default hooks.

# Documentation

Visit the [Gallatin Proxy website](http://gallatinproxy.com) for detailed documentation and pre-built binaries.

# License
Copyright (c) 2011 Bill O'Neill (william.w.oneill@gmail.com), under the [Apache License, Version 2.0](http://www.apache.org/licenses/LICENSE-2.0).
