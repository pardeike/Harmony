<p align="center"><img src="https://raw.githubusercontent.com/pardeike/Harmony/master/HarmonyLogo.png" alt="Harmony" width="128" /></p>

# Harmony 2.0

## Introduction

Harmony gives you an elegant and high level way to **alter functionality** in applications written in C#. It does this at **runtime** by monkey patching methods unlike other solutions that change the content of dll files.

It supports **Mono** and **.NET** environments on Windows, Unix and macOS except when Unity uses the stripped down NetStandard profile (.NET 4.x profile works fine). Harmony is used in mainstream Unity games and many other applications.

Designed to be used by multiple users (usually called Mods) that would otherwise override each others hooks, it was originally created for the game [RimWorld](https://rimworldgame.com) and its large modding community by [Andreas Pardeike](https://www.patreon.com/pardeike).

Enjoy!  
/Andreas Pardeike

# Getting Started

Installation is usually done by copying and referencing [0Harmony.dll](https://github.com/pardeike/Harmony/releases) from your project or by using the [Lib.Harmony](https://www.nuget.org/packages/Lib.Harmony) nuget package.

# Documentation

You can learn more about Harmony by using the top menu links. The main section [[Harmony](articles/intro.html)] brings you to the full documentation that explains everything about Harmony and gives you lots of high level examples. In the second section [[API Documentation](api/index.html)] you can browse the public API and all its methods and classes.

If you find a factual error or if you have feedback about the documentation you are welcome to

- fork the repository and create a pull request
- file a documentation Issue on the repo
- or write about it on the official discord

**New to modding and C#?** Beside the basic language features you need at least a good overview of **Reflection** in C#. Read this short and useful [introduction](https://dotnetcademy.net/Learn/4/Pages/1). 

## Community

If you feel stuck or have questions that this site does not answer, feel free to join the official [Discord Server](https://discord.gg/xXgghXR) or file a [GitHub Issue](https://github.com/pardeike/Harmony/issues).

Help by promoting this library so other developers can find it. One way is to upvote [this stackoverflow answer](https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method/42043003#42043003). Or spread the word in your developer communities. Thank you!

# Contact

Andreas Pardeike  
andreas@pardeike.net  
twitter: @pardeike  

## Donations

Donations keep me going:  
[https://www.patreon.com/pardeike](https://www.patreon.com/pardeike)
