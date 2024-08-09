# What Is Foldda

Foldda is the breadboard for computer software. It's a simple component-based software project environment, for building fun, cheap, and instant applications.

The software components, or modules, that work in a Foldda project are called "handlers". Each handler is packaged as a file system folder containing the resources and executable pointers required for performing a specific function that can have various configurable parameters, and app-building is done by dragging and dropping handler folders and forming a folder hierarchy (called a "solution") that defines the data flows between the handlers. Examples of handlers are Timer, Email-sender, File reader and writer, Dos-command executor, etc.

A Foldda Runtime is an application that functions as "the breadboard", i.e. it powers up, and connects the input and the output of, the handler modules. More technically speaking, it navigates through a Foldda solution's folder hierarchy, executes the instructions in each module's folder, and provides data exchange between connected modules. An example of Foldda runtime is the Foldda Windows app.

Although Foldda allows quick and easy toolless and no-code app development using prebuilt modules and runtime, it would give much greater power if people could have the power to build custom modules and even runtime, so he can leverage the benefits of the existing modules and features, while also have the ultimate flexibility and control, and that is the purpose of this repo.

# Foldda Automation Framework API

Foldda is a unique modular app development framework where an app is assembled with modules that are packaged as file system folders, and app-building is done by stacking such folders into a hierarchy (i.e. via drag-n-drop). Developing apps in such a way is quick and easy because it does not require using sophisticated vendor-specific tools (i.e. the "IDEs"). Not only does it save money and time, by removing vendor dependency in the app-building process, it also means Foldda allows modules from different vendors to be freely joined and collaborate in an app, without the modules being compiled or linked inside an IDE. In other words, a Foldda app has a *truly open* component-based architecture where any module can connect to any module regardless of the vendor.

This repo contains the open-sourced Foldda Framework API and many free, ready-to-use modules for building Foldda apps out of the box, and if these modules do not have all the features you want, the available source code could be served as boilerplate templates, which can be used to develop your own modules that are customized to your requirement. The only restriction is that, for your customized module to collaborate with the other Foldda modules out there, it needs to comply with the Foldda Framework API that is specified in this repo.

# How does Foldda Work

In a Foldda app, each folder encapsulates a specific function of a data-processing step, the parent-children relationship of the stacked folders defines the data flow of the processing.

<<A pic of Foldda program flow>>

When a Foldda app executes in a runtime, each module's logic (a specific data-process step) is turned into a process by the runtime, and the app's intended data-processing is performed sequentially as laid out by the folder's hierarchical structure.

<< foldda app execution with runtime >>



## Framework API Overview

## Handlers

## Runtimes

### Developer Kit

Many so-called 'no-code' platforms allow the creation of modular application software through graphical user interfaces and configuration instead of writing code. But in reality, the developers still need to spend significant effort to learn to use the app-building user interface and configuration, and such "skills" learned are tied to a specific platform vendor. Another problem with 'no-code' platforms is that customers have to live with what is provided. For example, the platform may have certain dependencies that do not meet your organization's privacy and security restriction or the provided modules are limited and fixed to certain functions. So it's unlikely to develop a program exactly what the customers want. 

Foldda app development is not only no-code but also requires "no tool" - there is no need to learn a vendor-specific IDE, so it further reduces the skill required to develop an app. Also, Foldda's modular app architecture has a novel 'loose coupling' design and is purposely designed for **independently developed** modules to work together. This means anyone can build his or her own Foldda module, perhaps for a feature of a specific requirement, and have it work together with the other Foldda modules in an app. Indeed, Foldda is a component-based software framework that is open to third-party developed software components, and potentially and very practically, a customer can make a module and an app to have the features exactly like he or she wants. 

This GitHub repo contains the Foldda framework API which is the base for building and running Foldda modules. It contains open-sourced modules made by Foldda which you are also welcome to check out and play with. You can use these modules as they are, or make changes to them so they become specialized modules of your own - the freedom is yours. 





