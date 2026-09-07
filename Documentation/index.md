<p align="center"><img src="https://raw.githubusercontent.com/pardeike/Harmony/master/HarmonyLogo.png" alt="Harmony" width="128" /></p>

# Harmony 2

## Introduction

Harmony lets you change .NET methods **at runtime**, without editing DLL files.

It supports **Mono** and **.NET** environments on Windows, Unix and macOS except when Unity uses the stripped down NetStandard profile (.NET 4.x profile works fine). Harmony is used in mainstream Unity games and many other applications.

It combines patches from multiple mods on the same method. [Andreas Pardeike](https://www.patreon.com/pardeike) originally created it for [RimWorld](https://rimworldgame.com) and its modding community.

Enjoy!
/Andreas Pardeike

# Getting Started

Use [Lib.Harmony](https://www.nuget.org/packages/Lib.Harmony) for a single DLL with dependencies merged in. This is the **recommended** package.

Use [Lib.Harmony.Thin](https://www.nuget.org/packages/Lib.Harmony.Thin) if you want to manage dependencies yourself. Make sure they are available at runtime.

# Documentation

Start with the [guide and examples](articles/intro.md), or browse the [API reference](api/index.md).

Found an error or have feedback?

- fork the repository and create a pull request
- file a documentation Issue on the repo
- or write about it on the official discord

**New to modding and C#?** Alongside the language basics, learn about [reflection](https://dotnetcademy.net/Learn/4/Pages/1).

## Community

Questions? Join the [Discord server](https://discord.gg/xXgghXR) or file a [GitHub issue](https://github.com/pardeike/Harmony/issues).

Help by promoting this library so other developers can find it. One way is to upvote [this stackoverflow answer](https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method/42043003#42043003). Or spread the word in your developer communities. Thank you!

# Contact

Andreas Pardeike
andreas@pardeike.net
twitter: @pardeike

## Donations

Donations keep me going:
[https://www.patreon.com/pardeike](https://www.patreon.com/pardeike)
