using System;
using System.Reflection;
using Harmony;
using UnityEngine;
using KMod;


namespace OxygenNotIncluded.Mods.Example
{
    public class ExampleMod: UserMod2
    {
        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            // the assembly of this UserMod
            Console.WriteLine(assembly.GetName());

            // path to your mod's folder
            // path; 

            // the `Mod` instance for your mod
            Console.WriteLine(mod);
            
            Console.WriteLine($"Mod <{mod.title}> loaded: {mod.staticID}");
        }
    }
}
