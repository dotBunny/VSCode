# VSCode
> Seamless Visual Studio Code Integration in Unity

### Important for Visual Studio Code 0.7.2 and BELOW Users
In version 0.8.0, a directory structure change was made that moved where configuration files were stored by Visual Studio Code. Thusly, the plugin was updated to handle the new structure, however this means that people using versions of Visual Studio Code below 0.8.0 need to go back to a previous release of the [plugin] (https://github.com/dotBunny/VSCode/releases/tag/1.6.5), or they can update Visual Studio Code.

## Installation
It is important to make sure that the `VSCode.cs` file is placed under the `Assets/Plugins/Editor` folder in the project. If you already have `SimpleJSON.cs` already in the project you do not need to worry about copying it over, however if you don't, you are going to need to make sure that it finds its way over to somewhere under the `Assets/Plugins` folder.

> The dependency on [SimpleJSON](http://wiki.unity3d.com/index.php/SimpleJSON "SimpleJSON @ wiki.unity3d.com") will be removed in an upcoming version of Unity

## Usage
Once the VSCode files are in place, simply navigate your way to the `Assets | VS Code` menu and select the `Enable Integration` option. A check mark will appear beside the option, indicating that its active.

That's it! Your ready to go!

## Platform Support
I use the plugin every day on a Mac (so it's battle tested there), and occasionally test it on a Windows VM. As for the recently announced Linux support, it should work just like the Mac version. I'll get around to installing the Linux editor sometime in the near future.

The Windows version of Visual Studio Code currently does not support debugging Mono, and will just throw a warning if you try to do it. The "Code" team is aware of this limitation, and we'll leave it at that.
