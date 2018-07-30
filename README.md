<p align="center">
    <img src="https://raw.githubusercontent.com/pardeike/Harmony/master/HarmonyLogo.png" alt="Harmony" width="128" />
</p>

<p align="center">
  <a href="https://github.com/pardeike/Harmony/releases/tag/v1.2.0">
    <img src="https://img.shields.io/badge/release-1.2.0.0-4BC51D.svg?style=flat" alt="v1.2.0" />
  </a>
  <a href="../../wiki">
    <img src="https://img.shields.io/badge/documentation-Wiki-4BC51D.svg?style=flat" alt="Documentation" />
  </a>
</p>

<p align="center">
  A library for patching, replacing and decorating .NET and Mono methods during runtime.
</p>

<hr>

Harmony gives you an elegant and high level way to alter the functionality in applications written in C#. It works great in games and is in fact well established in games like  
- Rimworld
- BattleTech
- The Long Dark
- Oxygen Not Included
- Subnautica
- 7 Days To Die
- Cities: Skylines
- Kerbal Space Program
- Besiege
- Stardew Valley
- Staxel
- Total Miner
- Ravenfield
- The Ultimate Nerd Game

It is also used in unit testing Windows Presentation Foundation controls and in many other areas.

If you develop in C# and your code is loaded as a module/plugin into a host application, you can use Harmony to alter the functionality of all the available assemblies of that application. Where other patch libraries simply allow you to replace the original method, Harmony goes one step further and gives you:

* A way to keep the original method intact

* Execute your code before and/or after the original method

* Modify the original with IL code processors

* Multiple Harmony patches co-exist and don't conflict with each other

Installation is usually done by referencing the **0Harmony.dll** (from the zip file) from your project or by using the **[Lib.Harmony](https://www.nuget.org/packages/Lib.Harmony)** nuget package.

Please check out the documentation on the **[GitHub Wiki](../../wiki)** or join us at the official **[discord server](https://discord.gg/xXgghXR)**

Also, an introduction to Transpilers: **[Simple Harmony Transpiler Tutorial](https://gist.github.com/pardeike/c02e29f9e030e6a016422ca8a89eefc9)**

<hr>

**Help by promoting this library** so other developers can find it. One way is to upvote **[this stackoverflow answer](https://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method/42043003#42043003)**. Or spread the word in your developer communities. Thank you!

For more information from me and my other open source projects, follow me on twitter: @pardeike

Hope you enjoy Harmony!

<hr>

<p align="center">
  <a href="mailto:andreas@pardeike.net">
    <img src="https://img.shields.io/badge/email-andreas@pardeike.net-blue.svg?style=flat" alt="Email: andreas@pardeike.net" />
  </a>
  <a href="https://twitter.com/pardeike">
    <img src="https://img.shields.io/badge/twitter-@pardeike-blue.svg?style=flat" alt="Twitter: @pardeike" />
  </a>
  <a href="https://raw.githubusercontent.com/pardeike/Harmony/master/LICENSE">
    <img src="https://img.shields.io/badge/license-MIT-lightgray.svg?style=flat" alt="MIT License" />
  </a>
</p> 
