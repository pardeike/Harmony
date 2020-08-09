<p align="center">
	<img src="https://raw.githubusercontent.com/pardeike/Harmony/master/HarmonyLogo.png" alt="Harmony" width="128" /><br>
	<b>Version 2</b><br>
	A library for patching, replacing and decorating<br>
	.NET and Mono methods during runtime.
</p>

### About

Harmony gives you an elegant and high level way to alter the functionality in applications written in C#. It works great in games and is well established in titles like **7 Days To Die**, **BattleTech**, **Besiege**, **Cities:Skylines**, **Kerbal Space Program**, **Oxygen Not Included**, Ravenfield, **Rimworld**, Sheltered, **Stardew Valley**, Staxel, **Subnautica**, The Ultimate Nerd Game, Total Miner, Unturned and many more.

It is also used in unit testing WFP controls and in many other areas.

### How it works

If you develop in C# and your code is loaded as a module/plugin into a host application, you can use Harmony to alter the functionality of all the available assemblies of that application. Where other patch libraries simply allow you to replace the original method, Harmony goes one step further and gives you:

• A way to keep the original method intact  
• Execute your code before and/or after the original method  
• Modify the original with IL code processors  
• Multiple Harmony patches co-exist and don't conflict with each other  
• Works at runtime and does not touch any files

### Installation

Installation is done by using [0Harmony.dll](https://github.com/pardeike/Harmony/releases) in your project or by using the [Lib.Harmony](https://www.nuget.org/packages/Lib.Harmony) nuget package.

### Documentation

Please check out the [documentation](https://harmony.pardeike.net) and join the official [discord server](https://discord.gg/xXgghXR).

### Contribute

I put thousands of hours into this project and its support. So every little action helps:

• Upvote this [stackoverflow answer](https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method/42043003#42043003)  
• Spread the word in your developer communities  
• Become a [GitHub sponsor](https://github.com/sponsors/pardeike) or a [Patreon](https://www.patreon.com/pardeike)

This project uses the great [MonoMod.Common](https://github.com/MonoMod/MonoMod.Common) library by [0x0ade](https://github.com/orgs/MonoMod/people/0x0ade).

### Harmony 1

Harmony 1 is deprecated and not under active development anymore. The latest version of it (v1.2.0.1) is stable and contains only minor bugs. Keep using it if you are in an environment that exclusively uses Harmony 1. Currently Harmony 1.x and 2.x are **NOT COMPATIBLE** with each other and **SHOULD NOT BE MIXED**. The old documentation can still be found at the [Wiki](https://github.com/pardeike/Harmony/wiki).

<br>&nbsp;

<p align="center">
	<a href="../../releases/latest">
		<img src="https://img.shields.io/github/release/pardeike/harmony.svg?style=flat" />
	</a>
	<a href="https://www.nuget.org/packages/lib.harmony">
		<img src="https://img.shields.io/nuget/v/lib.harmony.svg?style=flat" />
	</a>
	<a href="https://harmony.pardeike.net">
		<img src="https://img.shields.io/badge/documentation-%F0%9F%94%8D-blue?style=flat" />
	</a>
</p>
<p align="center">
	<a href="https://dev.azure.com/pardeike/Harmony/_build">
		<img src="https://pardeike.visualstudio.com/Harmony/_apis/build/status/Build%20and%20test" />
	</a>
	<a href="https://discord.gg/xXgghXR">
		<img src="https://img.shields.io/discord/131466550938042369.svg?style=flat&logo=discord&label=discord" />
	</a>
	<a href="../../blob/master/LICENSE">
		<img src="https://img.shields.io/github/license/pardeike/harmony.svg?style=flat" />
	</a>
</p>
<p align="center">
	<a href="mailto:andreas@pardeike.net">
		<img src="https://img.shields.io/badge/email-andreas@pardeike.net-blue.svg?style=flat" />
	</a>
	<a href="https://twitter.com/pardeike">
		<img src="https://img.shields.io/badge/twitter-@pardeike-blue.svg?style=flat&logo=twitter" />
	</a>
</p>
