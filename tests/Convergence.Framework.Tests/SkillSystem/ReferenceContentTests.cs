using System.Text.Json.Nodes;
using Xunit;

namespace Convergence.Framework.Tests.Content;

public sealed class SkillSystemRedesignFixtureTests
{
    [Fact]
    public void SamplePack_EncodesPassiveFusionFodderInvariant()
    {
        JsonObject skillDocument = LoadDocument("skill_system_redesign.skills.sample.json");
        JsonObject entityDocument = LoadDocument("skill_system_redesign.entities.sample.json");
        JsonObject raceDocument = LoadDocument("skill_system_redesign.races.sample.json");
        JsonObject manifest = LoadDocument("skill_system_redesign.manifest.sample.json");

        Assert.Equal(10, RequireInt(manifest, "schemaVersion"));
        Assert.Equal(10, RequireInt(skillDocument, "schemaVersion"));
        Assert.Equal(10, RequireInt(entityDocument, "schemaVersion"));
        Assert.Equal(10, RequireInt(raceDocument, "schemaVersion"));

        JsonObject skill = Assert.IsType<JsonObject>(Assert.Single(RequireArray(skillDocument, "skills")));
        JsonObject entity = Assert.IsType<JsonObject>(Assert.Single(RequireArray(entityDocument, "entities")));
        JsonObject race = Assert.IsType<JsonObject>(Assert.Single(RequireArray(raceDocument, "races")));

        string skillId = RequireString(skill, "id");
        Assert.Equal("passive", RequireString(skill, "activation"));
        Assert.Equal("passive", RequireString(skill, "inheritanceGroupId"));
        Assert.Null(skill["menuGroup"]);

        JsonObject inheritance = skill["inheritance"] as JsonObject
            ?? throw new InvalidOperationException("Sample skill is missing inheritance metadata.");
        Assert.True(RequireBoolean(inheritance, "isInheritable"));

        JsonObject modifier = Assert.IsType<JsonObject>(Assert.Single(RequireArray(skill, "modifiers")));
        JsonObject condition = modifier["when"] as JsonObject
            ?? throw new InvalidOperationException("Sample modifier is missing its when condition.");
        Assert.Equal("effect_element_is", RequireString(condition, "type"));
        Assert.Equal("ice", RequireString(condition, "elementId"));
        Assert.Null(modifier["conditions"]);

        JsonObject groupPolicy = entity["inheritanceRules"]?["groupPolicy"] as JsonObject
            ?? throw new InvalidOperationException("Sample entity is missing inheritanceRules.groupPolicy.");
        Assert.Equal("deny_list", RequireString(groupPolicy, "mode"));
        JsonArray deniedGroups = RequireArray(groupPolicy, "groupIds");
        Assert.Contains(deniedGroups, value => value?.GetValue<string>() == "ice");
        Assert.DoesNotContain(deniedGroups, value => value?.GetValue<string>() == "passive");

        Assert.Equal(skillId, Assert.Single(RequireArray(entity, "baseSkillIds"))?.GetValue<string>());
        Assert.Equal(RequireString(race, "id"), RequireString(entity, "raceId"));

        var expectedDocuments = new Dictionary<string, string>
        {
            ["skills"] = "skill_system_redesign.skills.sample.json",
            ["entities"] = "skill_system_redesign.entities.sample.json",
            ["races"] = "skill_system_redesign.races.sample.json"
        };
        var manifestDocuments = new Dictionary<string, string>();

        foreach (JsonNode? token in RequireArray(manifest, "documents"))
        {
            JsonObject document = Assert.IsType<JsonObject>(token);
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

    private static JsonObject LoadDocument(string fileName)
    {
        string path = TestContentPath.Resolve(Path.Combine(AppContext.BaseDirectory, "Content"), fileName);
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
               ?? throw new InvalidOperationException($"Document '{fileName}' is not a JSON object.");
    }

    private static JsonArray RequireArray(JsonObject document, string propertyName)
    {
        return document[propertyName] as JsonArray
               ?? throw new InvalidOperationException($"Document is missing array '{propertyName}'.");
    }

    private static string RequireString(JsonObject document, string propertyName)
    {
        return document[propertyName]?.GetValue<string>()
               ?? throw new InvalidOperationException($"Document is missing string '{propertyName}'.");
    }

    private static int RequireInt(JsonObject document, string propertyName)
    {
        return document[propertyName]?.GetValue<int>()
               ?? throw new InvalidOperationException($"Document is missing integer '{propertyName}'.");
    }

    private static bool RequireBoolean(JsonObject document, string propertyName)
    {
        return document[propertyName]?.GetValue<bool>()
               ?? throw new InvalidOperationException($"Document is missing boolean '{propertyName}'.");
    }
}
