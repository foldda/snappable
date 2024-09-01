# Foldda - The 'Breadboard' For Component-based Software Projects

Dubbed "The breadboard for software", Foldda is an easy-to-use component-based software development project environment, for building fun, cheap, and instant computer applications.

<div align="center">
<img src="_Resources/foldda-breadboard.png" width="650" align="center">
</div>

A software component in a Foldda project is called a "handler" that is capable of performing a specific function, and a Foldda app, called a "solution", is a selection of handler components that are connected by Foldda "the breadboard" and perform their intended functions in a certain order and collaboration. Some example handlers are available for app-building from this repo, including Timer, Email-sender, File reader and writer, Dos-command executor, etc.

Physically, a Foldda handler component is packaged as a file system folder which contains the resources and executable pointers required to perform the handler's special function. A config file inside each handler's folder content allows the parameters of the handler's function to be configured. is done by dragging and dropping handler folders and forming a folder hierarchy, where the folders' parent-child relationship defines the data flows between the handlers.

A Foldda Runtime is an application that functions as "the breadboard", i.e. it powers up, and connects the input and the output of, the handler modules. More technically speaking, it navigates through a Foldda solution's folder hierarchy, executes the instructions in each module's folder, and provides data exchange between connected modules. An example of Foldda runtime is the Foldda Windows app.

Although Foldda allows quick and easy toolless and no-code app development using prebuilt handlers and runtime, the real power is that it allows plug-n-play of third-party developed handlers that would work with existing handlers without having to recompile the app. It means you can have a handler built to your specific requirements while taking advantage of the existing prebuilt handlers, which means ultimate flexibility and control. And when a newly developed handler combines with the existing handlers, it multiplies the number of possible apps that can be built.

This repo hosts the open-sourced Foldda Automation Framework API, the reference for building Foldda handlers and runtimes. It also hosts the source code of many open-source licensed handlers, that can be used in your projects as they are, but also serve as boilerplate for your further custom development. The "Developer Kit" project included in this repo is a simple runtime for the convenience of your custom handler development.

# Foldda's Architecture

In a Foldda app, each folder encapsulates a specific function of a data-processing step, the parent-children relationship of the stacked folders defines the data flow of the processing.

<<A pic of Foldda program flow>>

When a Foldda app executes in a runtime, each module's logic (a specific data-process step) is turned into a process by the runtime, and the app's intended data-processing is performed sequentially as laid out by the folder's hierarchical structure.

<< foldda app execution with runtime >>

# An Analogy of Foldda Framework's Design Modeling

The framework is modeled as a factory processing line, where a worker (known as a "handler") takes items from an input bucket, processes them, and places the processed items (or other types of output) into an output bucket.

The Foldda "runtime" is the work environment for the workers, which includes providing the worker its input bucket, and output bucket, and, if applicable, passing the output from a worker to the next worker.

So in a Foldda handler, all it does is take data records from the provided input container, do the intended processing to these records, and then place the produced output to the provided output container. As defined by the framework, a Foldda handler would implement the IDataHandler interface - 

```csharp
  public interface IDataHandler
  {
      /// Setting up the data-handler "worker" with its config, and its input and output storage 
      void Setup(IConfigProvider config, IDataStore inputStorage, IDataStore ouputStorage);

      /// Typically runs a processing loop that processes the input records and saves the output records to the output storage.
      Task ProcessData(CancellationToken cancellationToken);
  }
```

## Framework API Overview

## Handlers

## Runtimes

### Developer Kit




