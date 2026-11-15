using DesktopPet.Core;
using Xunit;

namespace DesktopPet.Core.Tests;

public sealed class PetAnimationLayerTests
{
    private const string Definition = """
        { "formatVersion": 1, "compatibleAppearanceId": "example", "frameSize": 64,
          "frames": { "pose_0": { "image": "frames/pose.png", "anchors": { "grip": { "x": 55, "y": 44 } } } } }
        """;

    [Fact]
    public void ReadsNamedFramesAndPixelAnchors()
    {
        var definition = PetAnimationLayerDefinition.FromJson(Definition);
        Assert.Equal("example", definition.CompatibleAppearanceId);
        Assert.Equal("frames/pose.png", definition.Frames["pose_0"].Image);
        Assert.Equal(55, definition.Frames["pose_0"].Anchors["grip"].X);
        Assert.Equal(44, definition.Frames["pose_0"].Anchors["grip"].Y);
    }

    [Theory]
    [InlineData("../pose.png")]
    [InlineData("/pose.png")]
    [InlineData("C:/pose.png")]
    [InlineData("https://example.test/pose.png")]
    [InlineData("frames\\pose.png")]
    [InlineData("frames//pose.png")]
    [InlineData("frames/./pose.png")]
    [InlineData("pose.jpg")]
    [InlineData("pose\u0000.png")]
    public void RejectsPathsOutsideTheDefinitionDirectory(string path)
    {
        var json = Definition.Replace("\"frames/pose.png\"", System.Text.Json.JsonSerializer.Serialize(path));
        Assert.Throws<ArgumentException>(() => PetAnimationLayerDefinition.FromJson(json));
    }

    [Theory]
    [InlineData("\"formatVersion\": 1", "\"formatVersion\": 2")]
    [InlineData("\"compatibleAppearanceId\": \"example\"", "\"compatibleAppearanceId\": null")]
    [InlineData("\"frameSize\": 64", "\"frameSize\": 7")]
    [InlineData("\"frameSize\": 64", "\"frameSize\": 513")]
    [InlineData("\"x\": 55", "\"x\": -1")]
    [InlineData("\"y\": 44", "\"y\": 65")]
    [InlineData("\"x\": 55", "\"x\": 1e999")]
    [InlineData("\"x\": 55", "\"x\": \"NaN\"")]
    [InlineData("\"pose_0\"", "\" \"")]
    [InlineData("\"grip\"", "\"\"")]
    public void RejectsIncompatibleOrUnusableDefinitions(string source, string replacement)
        => Assert.Throws<ArgumentException>(() => PetAnimationLayerDefinition.FromJson(Definition.Replace(source, replacement)));

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{")]
    public void RejectsMalformedJson(string json)
        => Assert.Throws<ArgumentException>(() => PetAnimationLayerDefinition.FromJson(json));

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public void RejectsEmptyOrExcessiveFrameSets(int count)
    {
        var frames = string.Join(",", Enumerable.Range(0, count).Select(i => $"\"frame{i}\":{{\"image\":\"pose.png\"}}"));
        Assert.Throws<ArgumentException>(() => PetAnimationLayerDefinition.FromJson(
            "{\"formatVersion\":1,\"compatibleAppearanceId\":\"example\",\"frameSize\":64,\"frames\":{" + frames + "}}"));
    }

    [Fact]
    public void AcceptsAnchorsAtImageEdgesAndOptionalEmptyAnchors()
    {
        var edges = PetAnimationLayerDefinition.FromJson(Definition.Replace("\"x\": 55", "\"x\": 0").Replace("\"y\": 44", "\"y\": 64"));
        Assert.Equal(64, edges.Frames["pose_0"].Anchors["grip"].Y);
        var empty = PetAnimationLayerDefinition.FromJson(Definition.Replace(
            ", \"anchors\": { \"grip\": { \"x\": 55, \"y\": 44 } }", ""));
        Assert.Empty(empty.Frames["pose_0"].Anchors);
    }
}
