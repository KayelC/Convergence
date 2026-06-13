using Newtonsoft.Json.Linq;
using Xunit;

namespace Convergence.Tests;

public sealed class SkillSystemRedesignFixtureTests
{
    [Fact]
    public void SamplePack_EncodesPassiveFusionFodderInvariant()
    {
        JObject skillDocument = LoadDocument("skill_system_redesign.skills.sample.json");
        JObject entityDocument = LoadDocument("skill_system_redesign.entities.sample.json");
        JObject raceDocument = LoadDocument("skill_system_redesign.races.sample.json");
        JObject manifest = LoadDocument("skill_system_redesign.manifest.sample.json");

        JObject skill = Assert.IsType<JObject>(Assert.Single(RequireArray(skillDocument, "skills")));
        JObject entity = Assert.IsType<JObject>(Assert.Single(RequireArray(entityDocument, "entities")));
        JObject race = Assert.IsType<JObject>(Assert.Single(RequireArray(raceDocument, "races")));

        string skillId = RequireString(skill, "id");
        Assert.Equal("passive", RequireString(skill, "activation"));
        Assert.Equal("passive", RequireString(skill, "inheritanceGroupId"));

        JObject modifier = Assert.IsType<JObject>(Assert.Single(RequireArray(skill, "modifiers")));
        JObject condition = Assert.IsType<JObject>(Assert.Single(RequireArray(modifier, "conditions")));
        Assert.Equal("ice", RequireString(condition, "elementId"));

        JObject groupPolicy = entity["inheritanceRules"]?["groupPolicy"] as JObject
            ?? throw new InvalidOperationException("Sample entity is missing inheritanceRules.groupPolicy.");
        JArray deniedGroups = RequireArray(groupPolicy, "groupIds");
        Assert.Contains(deniedGroups, value => value.Value<string>() == "ice");
        Assert.DoesNotContain(deniedGroups, value => value.Value<string>() == "passive");

        Assert.Contains(RequireArray(entity, "baseSkillIds"), value => value.Value<string>() == skillId);
        Assert.Equal(RequireString(race, "id"), RequireString(entity, "raceId"));

        string[] manifestPaths = RequireArray(manifest, "documents")
            .Select(document => document["path"]?.Value<string>())
            .OfType<string>()
            .ToArray();

        Assert.Contains("skill_system_redesign.skills.sample.json", manifestPaths);
        Assert.Contains("skill_system_redesign.entities.sample.json", manifestPaths);
        Assert.Contains("skill_system_redesign.races.sample.json", manifestPaths);
    }

    private static JObject LoadDocument(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "Jsons", fileName);
        return JObject.Parse(File.ReadAllText(path));
    }

    private static JArray RequireArray(JObject document, string propertyName)
    {
        return document[propertyName] as JArray
               ?? throw new InvalidOperationException($"Document is missing array '{propertyName}'.");
    }

    private static string RequireString(JObject document, string propertyName)
    {
        return document[propertyName]?.Value<string>()
               ?? throw new InvalidOperationException($"Document is missing string '{propertyName}'.");
    }
}
