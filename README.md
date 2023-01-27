# Toolbox-MessageDispatch
A generic message dispatching system for C#. It can be used in standalone apps or as part of a Unity project and it has specific extensions for Unity GameObject support.

I can't overstate enough how valuable it is to be able to build Unity games that do not require code to link with other specific GameObjects or Components. Using a system like this message dispatcher allows one to build completely independent sub-systems that can automatically communicate with whomever they need to without requiring knowledge of who those recipients are, how they work, or even if they are present. It also avoids the need to inject any interfaces or even hardlink GameObjects or components in the inspector while still being able to communicate important information to anyone that requires such information.

Dependencies:  
[com.postegames.playerloopinjector](https://github.com/Slugronaut/Toolbox-PlayerLoopInjector)  
[com.postegames.autocreatable](https://github.com/Slugronaut/Toolbox-AutoCreatable)  
