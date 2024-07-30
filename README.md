# Foldda - Building Apps Like No Other!

Foldda is an unique modular app development framework where an app is assembled by stacking (i.e. drag-n-drop) modules that are physically represented as folders. App-building in such a way saves money and time, as it does not require purchasing and training using vendor-specific tools (i.e. the "IDEs"). More significantly though, it means in a Foldda app it allows modules from different vendors to be freely joined and collaborate, in other words, a Foldda app has a *truly open* component-based architecture where any module can connect to any module regardless of their vendor[1].

[1] A Foldda module needs to comply with the open-sourced Foldda Framework API specified in this repo.

This repo contains the open-sourced Foldda Framework API, as well as many free, ready-to-use modules for building Foldda apps out of the box. If the features of these modules haven't already met your requirements, the available source code can serve as boilerplate templates for your further (unrestricted) customization.

This repo is anticipated to benefit from being part of the OS community, including being transparent and having continuous improvement.

# How does Foldda Work

In a Foldda app, each folder encapsulates a specific function of a data-processing step, the parent-children relationship of the stacked folders is used for representing the processing's data flow.

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





