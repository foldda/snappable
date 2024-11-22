# A Simple and Practical Component-Based Software Development Framework

Foldda Open Components is a simple and practical component-based computing framework where off-the-shelf software components can be made from any third-parties, and be used for assembling versatile data-processing and automation pipelines. Dubbed "breadboard for software", it is for building fun, cheap, and instant software applications.

<div align="center">
<img src="_Resources/foldda-breadboard.png" width="450" align="center">
</div>

## "Breadboard-Like" App-Building Operations 

A Foldda project (called a "solution") consists of a selection of components (called "handlers") that collectively follow a design and perform an application. Unlike the other modular software development framework, where software modules are virtually presented as icons in an IDE environment, Foldda components are packaged as file system folders, which can be physically arranged and connected in a plain Windows environment, using operations such as drag-and-drop. That is why building a Foldda app is more like a breadboard project except the outcome is a software application. This [short video below](https://www.youtube.com/watch?v=l0DjAjVoESo) is a demo of building and running an ETP pipeline with Foldda components.

[![Foldda Demo](https://img.youtube.com/vi/l0DjAjVoESo/0.jpg)](https://www.youtube.com/watch?v=l0DjAjVoESo)

As seen in the video, app-building with Foldda software components does not require a vendor-specific tool such as an IDE, which means you can build or change a "Foldda app" from any _bare_ Windows computer. 

## The Quest of Component-Based Software Engineering 

The ultimate goal of Foldda Automation API is to become the base of an open-sourced software component marketplace, where free and premium components from different vendors are made available for people to assemble apps without much effort. Not only component-based software development is much more productive and easier to maintain as you see in the demo, but a market of software components also has great economic value because it encourages a very high degree of software reuse. Theoretically, when a new component is developed and added to the market's collection, the number of possible apps from these components would multiply and grow exponentially, and, unlike using hardware electronic components, software components can be easily copied and reused in an app without much effort or additional cost. 

However, despite these attractive benefits, one thing that has been stopping component-based computing from being realized is how to define "the boundary" of a component so it can co-exist and collaborate with the other components in an app. We need a standard interface that allows software components to freely and meaningfully exchange data - think of a defining "universal plug" for software components that works like the pins and pin-holes in a physical breadboard project. Also, for a software component operatable like a physical electrical component, it has to be data-model-neutral, meaning the data exchange cannot be bound to a specific data model controlled by a vendor - think "the pin" and "pin-hole" for the breadboard have to be neutral and generic. 

Foldda Automation Framework is an attempt to solve the above problem, that is, it defines and implements such a "universal interface", for software components to exchange data while working together - even if the components are from different vendors, or developed at various times, and have little or no pre-established knowledge of each other[^1]. 

[^1]: This feature Foldda has implemented, in software engineering, is called "late binding".

## About This Repo

To achieve organic growth for the intended software component market, Foldda Automation Framework must allow a user to modify a component, or to create new components, according to his/her specific requirement, rather than trying to provide a large number of components and try to satisfy all users' needs. So the API is designed to be (extremely) simple, flexible, and non-restrictive.

This repo hosts the open-sourced Foldda Automation API as well as the source code of many quality components developp by Foldda according to the API. These components can be used as they are, as you saw in the video, or serve as a boilerplate for you to customize or to start a brand-new component development, to suit your specific requirements. It is hoped these source codes will assist developers in understanding and developing their compatible software components.

The "Developer Kit" project included in this repo is a simple reference runtime. It is also designed to be used for the convenience of custom handler development as you can use it to debug your components' code by following a data processing flow across components.

# The Framework - Technical Details 

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




