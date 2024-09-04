# Foldda - The 'Breadboard' For Component-based Software Projects

Dubbed "the breadboard for software", Foldda is an easy-to-use component-based software development platform - for fun, cheap, and instant software application projects.

<div align="center">
<img src="_Resources/foldda-breadboard.png" width="650" align="center">
</div>

## "Breadboard-Like" App-Building Operations 

Conceptually similar to a hardware breadboard project, in the software space, a Foldda project (called a "solution") consists of a selection of components (called "handlers") which collectively follow a design and deliver a feature - i.e. an app. What's special about Foldda is that these handler components are physically packaged as file system folders, which can be flexibly arranged on and connected through a provided environment (called a "runtime"). This short video illustrates  an example of building and running a component-based ETP pipeline using Foldda.

video demo here

As in the video, by packaging software components as folders, Foldda's component-based app-building can be done without using any specialised tools. You can design and build apps by arranging and connecting software components using only native OS operationssuch as dragging-and-dropping folders. This contrasts with the other "no-code" app-dev frameworks where the components must exist within a vendor-specific IDE environment. 

## Promoting A Truely Open Software Component Marketplace 

Component-based computing encourges code reuse and modular design. 

Foldda Automation Framework from this repo is the foundation of an open component-based computing eco-system, that has vendor-neutral software component and runtime development in its design. Such visionary design requires two pieces of technology: the first is a universal, vendor independent packaging of software components like we've just discussed and demostrated; the second technology required is a standard interfcae that allow software components to freely and meaningful exchange data, think a "universal plug" for components like the pins and pin-holes on a physical breadboard ...

## Charian - Universal Data Exchange

A Foldda runtime `needs to address the problem of defining and implementing the interface between the components - which can be potentially independently developed and have no assumed knowledge of one other. And that is another key piece of tech from Foldda - the Charian object serialization API.

With Charian, Foldda runtime has this real power which is that it allows plug-n-play of third-party developed handlers that would work with existing handlers without having to recompile the app. It means you can have a handler built to your specific requirements while taking advantage of the existing prebuilt handlers, which means ultimate flexibility and control. And when a newly developed handler combines with the existing handlers, it multiplies the number of possible apps that can be built.

This allows Foldda Runtime to function as "the (software) breadboard", i.e. it powers up, and connects the input and the output of, the handler modules. More technically speaking, it navigates through a Foldda solution's folder hierarchy, executes the instructions in each module's folder, and provides data exchange between connected modules. An example of Foldda runtime is the Foldda Windows app.

## Foldda Automation Framework API

The purpose of this repo is to assist developers to understand and develop Foldda compatible software components (or runtimes). In addition to the open-sourced Foldda Automation Framework API source code, it also hosts the source code of many open-source licensed handlers, that can be used in your projects as they are, but also serve as boilerplate for your further custom development. The "Developer Kit" project included in this repo is a simple reference runtime which can be used for the convenience of custom handler development.

# Foldda's Technical Architecture 

In a Foldda app, each folder encapsulates a specific function of a data-processing step, the parent-children relationship of the stacked folders defines the data flow of the processing.

<<A pic of Foldda program flow>>

When a Foldda app executes in a runtime, each module's logic (a specific data-process step) is turned into a process by the runtime, and the app's intended data-processing is performed sequentially as laid out by the folder's hierarchical structure.

<< foldda app execution with runtime >>

# Foldda Handler Explained - A Design Analogy

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




