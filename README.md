# Foldda - The 'Breadboard' For Component-based Software Projects

Dubbed "The breadboard for software", Foldda is an easy-to-use component-based software development project environment, for building fun, cheap, and instant computer applications.

<div align="center">
<img src="_Resources/foldda-breadboard.png" width="650" align="center">

**_"Foldda connects pre-built components and functions 'like a breadboard' in a software project."_**
</div>

A software component in a Foldda project is called a "handler" that is packaged as a file system folder containing the resources and executable pointers required for performing the component's specific function. In each handler's folder content, there is a config file that has a set of configurable parameters related to the handler's function, and developing an 'app' (called a "solution") is done by dragging and dropping handler folders and forming a folder hierarchy, where the folders' parent-child relationship defines the data flows between the handlers. Examples of handlers are Timer, Email-sender, File reader and writer, Dos-command executor, etc.

A Foldda Runtime is an application that functions as "the breadboard", i.e. it powers up, and connects the input and the output of, the handler modules. More technically speaking, it navigates through a Foldda solution's folder hierarchy, executes the instructions in each module's folder, and provides data exchange between connected modules. An example of Foldda runtime is the Foldda Windows app.

Although Foldda allows quick and easy toolless and no-code app development using prebuilt handlers and runtime, the real power is that it allows plug-n-play of third-party developed handlers that would work with existing handlers without having to recompile the app. It means you can have a handler built to your specific requirements while taking advantage of the existing prebuilt handlers, which means ultimate flexibility and control. And when a newly developed handler combines with the existing handlers, it multiplies the number of possible apps that can be built.

This repo hosts the open-sourced Foldda Automation Framework API, the reference for building Foldda handlers and runtimes. It also hosts the source code of many open-source licensed handlers, that can be used in your projects as they are, but also serve as boilerplate for your further custom development. The "Developer Kit" project included in this repo is a simple runtime for the convenience of your custom handler development.

# Foldda's Architecture

In a Foldda app, each folder encapsulates a specific function of a data-processing step, the parent-children relationship of the stacked folders defines the data flow of the processing.

<<A pic of Foldda program flow>>

When a Foldda app executes in a runtime, each module's logic (a specific data-process step) is turned into a process by the runtime, and the app's intended data-processing is performed sequentially as laid out by the folder's hierarchical structure.

<< foldda app execution with runtime >>

# Foldda Framework's Modeling

The framework is modeled as a factory processing line, where a worker (the "handler") takes items from an input bucket, processes them, and places the processed items (or other types of output) into an output bucket.

The Foldda "runtime" is the work environment that supplies to the worker, which includes giving the worker its input bucket, taking away the worker's output bucket, and, if applicable, passing the output to the next worker.


## Framework API Overview

## Handlers

## Runtimes

### Developer Kit




