# Foldda Automation Framework
Foldda is a modular app-development framework and runtime service, for assembling apps by stacking modules physically represented as folders. In a Foldda app, each folder encapsulates a specific function of a data-processing step, the parent-children relationship of the stacked folders is used for representing the processing's data flow.

<<A pic of Foldda program flow>>

When a Foldda app executes in a runtime, each module's logic (a specific data-process step) is turned into a process by the runtime, and the app's intended data-processing is performed sequentially as laid out by the folder's hierarchical structure.

<< foldda app execution with runtime >>

The advantage of building apps in such a way is that app-building no longer requires vendor-specific tools ("IDE"), meaning saving money to buy and time to learn these tools. Most significantly, by removing vendor dependency in app-building, it empowers a truly open component-based software architecture that allows modules from different vendors to be placed and collaborate in an app. In other words, as long as a module complies with the Foldda Automation Framework API, immediately it can be (literally) dropped in, and become a functional part of, a Foldda app.



Many so-called 'no-code' platforms allow the creation of modular application software through graphical user interfaces and configuration instead of writing code. But in reality, the developers still need to spend significant effort to learn to use the app-building user interface and configuration, and such "skills" learned are tied to a specific platform vendor. Another problem with 'no-code' platforms is that customers have to live with what is provided. For example, the platform may have certain dependencies that do not meet your organization's privacy and security restriction or the provided modules are limited and fixed to certain functions. So it's unlikely to develop a program exactly what the customers want. 

Foldda app development is not only no-code but also requires "no tool" - there is no need to learn a vendor-specific IDE, so it further reduces the skill required to develop an app. Also, Foldda's modular app architecture has a novel 'loose coupling' design and is purposely designed for **independently developed** modules to work together. This means anyone can build his or her own Foldda module, perhaps for a feature of a specific requirement, and have it work together with the other Foldda modules in an app. Indeed, Foldda is a component-based software framework that is open to third-party developed software components, and potentially and very practically, a customer can make a module and an app to have the features exactly like he or she wants. 

This GitHub repo contains the Foldda framework API which is the base for building and running Foldda modules. It contains open-sourced modules made by Foldda which you are also welcome to check out and play with. You can use these modules as they are, or make changes to them so they become specialized modules of your own - the freedom is yours. 

## Framework Overview



