# A 'Breadboard' System For Software Projects

Dubbed "breadboard for software", Foldda Automation is a simple, easy-to-use component-based software development framework for building fun, cheap, and instant software applications.

<div align="center">
<img src="_Resources/foldda-breadboard.png" width="450" align="center">
</div>

## "Breadboard-Like" App-Building Operations 

A Foldda project (called a "solution") consists of a selection of components (called "handlers") that collectively follow a design and perform an application. Unlike the other modular software development framework, where software modules are virtually presented as icons in an IDE environment, Foldda components are packaged as file system folders, which can be physically arranged and connected in a plain Windows environment, using operations such as drag-and-drop. That is why building a Foldda app is more like a breadboard project except the outcome is a software application. This [short video below](https://www.youtube.com/watch?v=l0DjAjVoESo) is a demo of building and running an ETP pipeline with Foldda components.

[![Foldda Demo](https://img.youtube.com/vi/l0DjAjVoESo/0.jpg)](https://www.youtube.com/watch?v=l0DjAjVoESo)

As seen in the video, app-building with Foldda software components does not require a vendor-specific tool such as an IDE, which means you can build or change a "Foldda app" from any _bare_ Windows computer. 

## The Quest for An Open Software Component Marketplace 

The ultimate goal of Foldda Automation API is to become the base of an open-sourced software component marketplace, where free and premium components from different vendors are made available for people to assemble apps without much effort. Not only component-based software development is much more productive and easier to maintain like you see in the demo, a market of software components has great economical potential because, theoretically, when a new component is developped and added to the market, the number of possible apps multiplies and would grow exponentially, and, unlike using hardware electronic components, software components can be easily copied and reused in an app without much effort or additional cost. 

However, despite these attractive benefits, one thing has been stopping component-based computing from being realised is how to define "the boundary" of a component so it can co-exist and collaborate with the other components in an app. That is, we need a standard interface that allows software components to freely and meaningful exchange data - think a defining "universal plug" for software components that works like the pins and pin-holes in a physical breadboard project. Also, if there is such a solution, it has to be data-model-neutral, meaning the data exchange cannot be bound to a specific data model controled a vendor, which will limit its application. 

## About This Repo

Foldda Automation Framework is an attempt to solve the above problem, that is, it defines and implements such a "universal interface", for software components to exchange data while working together - even if the components are from different vendors at different times, and have little or no pre-established knowledge of each other[^1]. 

[^1]: In software engineering, this feature Foldda has implemented is called "late binding".

This repo hosts the source code of the Foldda Automation API, which is the base of the components and the component-hosting runtime you saw in the video. It also hosts the source code of many quality components and a reference runtime, which can be used as they are, or to be further customized to suit your specific requirements.

By making the API's source code publically available, this repo would assist developers in understanding and developing compatible software components (or runtimes). Also, many components like the ones you saw in the demo video are open-sourced here, which can be used in your projects as they are, but also serve as boilerplate for your further custom development. Being able to modify a component according to your specific requirement is also key because everyone's requirements are different, and it encourages you to make changes and take ownership of the development of a custom component that caters to your requirements.

The "Developer Kit" project included in this repo is a simple reference runtime that can be used for the convenience of custom handler development as you'll be able to debug your code between components following a data processing flow.

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

## Charian - Universal Data Exchange

A Foldda runtime needs to address the problem of defining and implementing the interface between the components - which can be potentially independently developed and have no assumed knowledge of one other. And that is another key piece of tech from Foldda - the Charian object serialization API.

With Charian, Foldda runtime has this real power which is that it allows plug-n-play of third-party developed handlers that would work with existing handlers without having to recompile the app. It means you can have a handler built to your specific requirements while taking advantage of the existing prebuilt handlers, which means ultimate flexibility and control. And when a newly developed handler combines with the existing handlers, it multiplies the number of possible apps that can be built.

This allows Foldda Runtime to function as "the (software) breadboard", i.e. it powers up, and connects the input and the output of, the handler modules. More technically speaking, it navigates through a Foldda solution's folder hierarchy, executes the instructions in each module's folder, and provides data exchange between connected modules. An example of Foldda runtime is the Foldda Windows app.


## Handlers

## Runtimes

### Developer Kit




