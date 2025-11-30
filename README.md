# Requirements

* C# latest
* .net sdk 4.71


Copy mod_info.yaml and dll from `projectname/bin/Debug/projectname` to

* Windows - `%HOMEPATH%\Documents\Klei\OxygenNotIncluded\mods\dev\%MOD_NAME%`
* Linux - `~/.config/unity3d/Klei/Oxygen Not Included/mods/Dev/$MOD_NAME`

Check logs 

* Windows - `%HOMEPATH%\Documents\Klei\OxygenNotIncluded\Player.log`
* Linux - `~/.config/unity3d/Klei/Oxygen Not Included/Player.log`


# Usefull mods

* [Mod Preset Manager (Light) [MPML]](https://steamcommunity.com/sharedfiles/filedetails/?id=3281716506)
* [Debug Console](https://steamcommunity.com/sharedfiles/filedetails/?id=2041219184) for windows
* [Debug Buttons](https://steamcommunity.com/sharedfiles/filedetails/?id=3120193648)

## Links
* [Cairath/Oxygen-Not-Included-Modding wiki](https://github.com/Cairath/Oxygen-Not-Included-Modding/wiki)
* [[Tutorial] How to create a basic mod for ONI
  ](https://forums.kleientertainment.com/forums/topic/107833-tutorial-how-to-create-a-basic-mod-for-oni/)
* https://github.com/O-n-y/OxygenNotIncludedModTemplate
* https://gist.github.com/EliteMasterEric/cc9f0271af9410aec32ead637efe7741
* https://harmony.pardeike.net/articles/patching-prefix.html

### Mod examples
* https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods
* https://github.com/aki-art/ONI-Mods
* https://github.com/peterhaneve/ONIMods

### Usefull c# hints 

* [Porting MSBuild Projects To XBuild](https://www.mono-project.com/archived/porting_msbuild_projects_to_xbuild/#prepostbuildevents)
* [MSBuild variables](https://learn.microsoft.com/en-us/cpp/build/reference/common-macros-for-build-commands-and-properties?view=msvc-170&source=recommendations)


# Tips

Q: Fix `error MSB3644: The reference assemblies for framework ".NETFramework,Version=v4.7.1" were not found` \
A: cd packages and execute command from shell

`dotnet add package Microsoft.NETFramework.ReferenceAssemblies.net471 --version 1.0.3`


Q: NuGet has error `Access to the path '/4ab83313-8bfc-4077-846d-a2e9dfa091f2.tmp' is denied.`
A: ```
sudo apt install nuget

dotnet nuget locals all -c
dotnet clean
dotnet restore -v d
dotnet build
```