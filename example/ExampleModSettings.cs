namespace OxygenNotIncluded.Mods.Example;

using Newtonsoft.Json;
using PeterHan.PLib.Options;


[JsonObject(MemberSerialization.OptIn)]
[ModInfo("https://www.github.com/peterhaneve/ONIMods")]
public class ExampleModSettings
{
    [Option("Wattage", "How many watts you can use before exploding.")]
    [Limit(1, 50000)]
    [JsonProperty]
    public float Watts { get; set; }

    public ExampleModSettings()
    {
        Watts = 10000f; // defaults to 10000, e.g. if the config doesn't exist
    }
}