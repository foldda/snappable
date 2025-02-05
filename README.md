# Enflow Components

Enflow is a simple component-based computing framework that allows vendor-neutral, standardized components to be assembled into data-processing pipelines for integration and data automation. 

Enflow employs a unique, schemaless data format, called RDA, for connected components to exchange arbitrarily complex data, effectively allowing any Enflow-compliant components, from any person or company, to join and work together without pre-setting, and being constrained by, a data model.

> In an analogy, Enflow is "the joint" for coupling software components to become a bigger module or an app, just like the LEGO joint allows connecting arbitrarily shaped pieces to function as a more complex unit.

This repo contains files and resources for Enflow component development, including -

1. The API of the component standard, for any third party to make Enflow-compatible components with arbitrary features that can collaborate and interact with Enflow components made by the other vendors.
2. Source code of many Enflow components for various modularized tasks and functions, which can be used as they are or as a spoilerplate for further customization tailored to your specific requirement.
3. An easy-to-use "Developer Kit" with GUI that allows debugging and tracing in Visual Studio when developing customized Enflow components.

## Background: Component-Based Computing and Software Component Marketplace 

The ultimate goal of Enflow Component API is to become the base of an open-sourced software component marketplace, where free and premium components from different vendors are made available for people to assemble apps without programming. Not only component-based software development is much more productive and easier to maintain as you see in the demo, but a market of software components also has great economic value because it encourages a very high degree of software reuse. Theoretically, when a new component is developed and added to the market's collection, the number of possible apps from these components would multiply and grow exponentially, and, unlike using hardware electronic components, software components can be easily copied and reused in an app without much effort or additional cost. 

In the past, despite these attractive benefits, one thing that stopped realizing component-based computing was how to define "a component's boundary" so it could co-exist and collaborate with the other components in an app. We need a standard interface that allows software components to freely and meaningfully exchange data.

Foldda Enflow is an attempt to solve the above problem, that is, it defines and implements such a "universal interface", for software components to exchange data while working together - even if the components are from different vendors, or developed at various times, and have little or no pre-established knowledge of each other[^1]. 

[^1]: This feature is called "late binding" in software engineering.

## Enflow: Defining A Simple Component-Based Computing Framework

In an analogy, Enflow Components works much like "the breadboard for software" which essentially is a framework for simple and practical component-based computing. That is, like a breadboard defining the intended electric signals between electrical components and how they are connected, the API defines a generic data package standard for exchanging between software components and defines the interface of how each component can be connected and collaboratively function together in a runtime environment like Enflow. Think of a defining "universal plug" for software components that works like the pins and pin-holes in a physical breadboard project. Also, for a software component operatable like a physical electrical component, it has to be data-model-neutral, meaning the data exchange cannot be bound to a specific data model controlled by a vendor - think "the pin" and "pin-hole" for the breadboard have to be neutral and generic. 

<div align="center">
<img src="_Resources/foldda-breadboard.png" width="450" align="center">
</div>

Based on the "pins" and "plugs" defined by the API. "Off-the-shelf" Enflow components can be made by any third party, giving you unrestricted choices of vendors for software components which you can use to create custom data processing and automation pipelines. And of course, you can also create components yourself without depending on a vendor. 

## "Breadboard-Like" App-Building Operations 

An Enflow project (called a "solution") consists of a selection of components (called "handlers") that collectively work together to perform an application. Unlike the other modular software development frameworks, where software modules only exist in a proprietary IDE environment, Enflow components are packaged as file system folders, which can be physically carried in a USB, and be built into an app using plain Windows desktop operations such as drag-and-drop - i.e. without the need of an IDE. That is why building a Foldda app is more like a breadboard project except the outcome is a software application. This [short video below](https://www.youtube.com/watch?v=l0DjAjVoESo) is a demo of building and running an ETP pipeline with Foldda components.

[![Foldda Demo](https://img.youtube.com/vi/l0DjAjVoESo/0.jpg)](https://www.youtube.com/watch?v=l0DjAjVoESo)

As seen in the video, app-building with Enflow components does not require any vendor-specific tool, which means you can build or change an "Enflow app" from any _bare_ Windows computer. 

## Using This Repo

To achieve organic growth for the intended software component market, Enflow Framework must allow a user to modify a component, or to create new components, according to his/her specific requirement, rather than trying to provide a large number of components and try to satisfy all users' needs. So the API is designed to be (extremely) simple, flexible, and non-restrictive.

This repo hosts the open-sourced Foldda Automation API as well as the source code of many quality components developed by Foldda according to the API. These components can be used as they are, as you saw in the video, or serve as a boilerplate for you to customize or to start a brand-new component development, to suit your specific requirements. It is hoped these source codes will assist developers in understanding and developing their compatible software components.

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




