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

        Assert.Equal(1, RequireInt(manifest, "schemaVersion"));
        Assert.Equal(1, RequireInt(skillDocument, "schemaVersion"));
        Assert.Equal(1, RequireInt(entityDocument, "schemaVersion"));
        Assert.Equal(1, RequireInt(raceDocument, "schemaVersion"));

        JObject skill = Assert.IsType<JObject>(Assert.Single(RequireArray(skillDocument, "skills")));
        JObject entity = Assert.IsType<JObject>(Assert.Single(RequireArray(entityDocument, "entities")));
        JObject race = Assert.IsType<JObject>(Assert.Single(RequireArray(raceDocument, "races")));

        string skillId = RequireString(skill, "id");
        Assert.Equal("passive", RequireString(skill, "activation"));
        Assert.Equal("passive", RequireString(skill, "inheritanceGroupId"));
        Assert.Null(skill["menuGroup"]);

        JObject inheritance = skill["inheritance"] as JObject
            ?? throw new InvalidOperationException("Sample skill is missing inheritance metadata.");
        Assert.True(RequireBoolean(inheritance, "isInheritable"));

        JObject modifier = Assert.IsType<JObject>(Assert.Single(RequireArray(skill, "modifiers")));
        JObject condition = modifier["when"] as JObject
            ?? throw new InvalidOperationException("Sample modifier is missing its when condition.");
        Assert.Equal("effect_element_is", RequireString(condition, "type"));
        Assert.Equal("ice", RequireString(condition, "elementId"));
        Assert.Null(modifier["conditions"]);

        JObject groupPolicy = entity["inheritanceRules"]?["groupPolicy"] as JObject
            ?? throw new InvalidOperationException("Sample entity is missing inheritanceRules.groupPolicy.");
        Assert.Equal("deny_list", RequireString(groupPolicy, "mode"));
        JArray deniedGroups = RequireArray(groupPolicy, "groupIds");
        Assert.Contains(deniedGroups, value => value.Value<string>() == "ice");
        Assert.DoesNotContain(deniedGroups, value => value.Value<string>() == "passive");

        Assert.Equal(skillId, Assert.Single(RequireArray(entity, "baseSkillIds")).Value<string>());
        Assert.Equal(RequireString(race, "id"), RequireString(entity, "raceId"));

        var expectedDocuments = new Dictionary<string, string>
        {
            ["skills"] = "skill_system_redesign.skills.sample.json",
            ["entities"] = "skill_system_redesign.entities.sample.json",
            ["races"] = "skill_system_redesign.races.sample.json"
        };
        var manifestDocuments = new Dictionary<string, string>();

        foreach (JToken token in RequireArray(manifest, "documents"))
        {
            JObject document = Assert.IsType<JObject>(token);
            string type = RequireString(document, "type");
            string path = RequireString(document, "path");

            Assert.True(manifestDocuments.TryAdd(type, path), $"Duplicate manifest document type '{type}'.");
            LoadDocument(path);
        }

        Assert.Equal(expectedDocuments.Count, manifestDocuments.Count);
        foreach ((string type, string expectedPath) in expectedDocuments)
        {
            Assert.True(manifestDocuments.TryGetValue(type, out string? actualPath));
            Assert.Equal(expectedPath, actualPath);
        }
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

    private static int RequireInt(JObject document, string propertyName)
    {
        return document[propertyName]?.Value<int?>()
               ?? throw new InvalidOperationException($"Document is missing integer '{propertyName}'.");
    }

    private static bool RequireBoolean(JObject document, string propertyName)
    {
        return document[propertyName]?.Value<bool?>()
               ?? throw new InvalidOperationException($"Document is missing boolean '{propertyName}'.");
    }
}
