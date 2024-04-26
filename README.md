<p align="center">
	<img src="https://raw.githubusercontent.com/pardeike/Harmony/master/HarmonyLogo.png" alt="Harmony" width="180" /><br>
	<b>Version 2.3</b><br>
	A library for patching, replacing and decorating<br>
	.NET and Mono methods during runtime.
</p>

### About

Harmony gives you an elegant and high level way to alter the functionality in applications written in C#. It works great in games and is well established in titles like  

• **Rust**  
• **Rimworld**  
• **7 Days To Die**  
• **Stardew Valley**  
• **Subnautica**  
• **Oxygen Not Included**  
• **Besiege**  
• **Cities:Skylines**  
• **Kerbal Space Program**  
• **Resonite**  
• **BattleTech**  
• **Slime Rancher**  

and others like Ravenfield, Sheltered, Staxel, The Ultimate Nerd Game, Total Miner, Unturned, SCP: Secret Laboratory ...

It is also used in unit testing WPF controls at Microsoft and Google and in many other areas.

### How it works

If you develop in C# and your code is loaded as a module/plugin into a host application, you can use Harmony to alter the functionality of all the available assemblies of that application. Where other patch libraries simply allow you to replace the original method, Harmony goes one step further and gives you:

• A way to keep the original method intact  
• Execute your code before and/or after the original method  
• Modify the original with IL code processors  
• Multiple Harmony patches co-exist and don't conflict with each other  
• Works at runtime and does not touch any files

### Installation

If you want a single file, dependency-merged assembly, you should use the [Lib.Harmony](https://www.nuget.org/packages/Lib.Harmony) nuget package. This is the **preferred** way.

If you instead want to supply the dependencies yourself, you should use the [Lib.Harmony.Thin](https://www.nuget.org/packages/Lib.Harmony.Thin) nuget package. You get more control but you are responsible to make all references available at runtime.

### Documentation

Please check out the [documentation](https://harmony.pardeike.net) and join the official [discord server](https://discord.gg/xXgghXR).

### Contribute

I put thousands of hours into this project and its support. So every little action helps:

• Become a [GitHub sponsor](https://github.com/sponsors/pardeike) or a [Patreon](https://www.patreon.com/pardeike)  
• Upvote this [stackoverflow answer](https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method/42043003#42043003)  
• Spread the word in your developer communities

This project uses the great [MonoMod.Core](https://github.com/MonoMod) library by [0x0ade](https://github.com/0x0ade) and [nike4613](https://github.com/nike4613).

### Harmony 1

Harmony 1 is deprecated and not under active development anymore. The latest version of it (v1.2.0.1) is stable and contains only minor bugs. Keep using it if you are in an environment that exclusively uses Harmony 1. Currently Harmony 1.x and 2.x are **NOT COMPATIBLE** with each other and **SHOULD NOT BE MIXED**. The old documentation can still be found at the [Wiki](https://github.com/pardeike/Harmony/wiki).

<br>&nbsp;

<p align="center">
	<a href="../../blob/master/LICENSE"><img src="https://img.shields.io/github/license/pardeike/harmony.svg?style=flat-squared&label=License" /></a>
	<a href="../../releases/latest"><img src="https://img.shields.io/github/release/pardeike/harmony.svg?style=flat-squared&label=Release" /></a>
	<a href="https://harmony.pardeike.net"><img src="https://img.shields.io/badge/documentation-%F0%9F%94%8D-9cf?style=flat-squared&label=Documentation" /></a>
</p>
<p align="center">
	<a href="https://github.com/pardeike/Harmony/releases/latest"><img src="https://img.shields.io/github/downloads/pardeike/Harmony/total.svg?style=flat-squared&logo=github&color=009900&label=Release%20Downloads" /></a>
	<a href="https://www.nuget.org/packages/Lib.Harmony"><img src="https://img.shields.io/nuget/dt/Lib.Harmony?style=flat-squared&logo=nuget&label=Nuget%20Downloads&color=009900" /></a>
</p>
<p align="center">
	<a href="https://github.com/pardeike/Harmony/actions/workflows/test.yml"><img src="https://img.shields.io/github/actions/workflow/status/pardeike/Harmony/test.yml?style=flat-squared&logo=github&label=CI%20Tests" /></a>
	<a href="https://discord.gg/xXgghXR"><img src="https://img.shields.io/discord/131466550938042369.svg?style=flat-squared&logo=discord&label=Official%20Discord" /></a>
</p>
<p align="center">
	<a href="mailto:andreas@pardeike.net"><img src="https://img.shields.io/badge/email-andreas@pardeike.net-blue.svg?style=flat-squared&label=Email" /></a>
	<a href="https://twitter.com/pardeike"><img src="https://img.shields.io/badge/twitter-@pardeike-blue.svg?style=flat-squared&logo=twitter&label=Twitter" /></a>
</p>
