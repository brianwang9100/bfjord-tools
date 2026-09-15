using System;
using System.IO;
using NUnit.Framework;
using Bwork.Authoring.Editor.BridgeAssets;

public sealed class BridgeSurfaceContractTests
{
    string directory;
    [SetUp] public void SetUp() { directory = Path.Combine(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library")), "bfjord-surface-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
    [TearDown] public void TearDown() { Directory.Delete(directory, true); }
    string Write(string materials)
    {
        string path = Path.Combine(directory, "profile.json");
        File.WriteAllText(path, "{\"schemaVersion\":1,\"id\":\"weathered\",\"materials\":" + materials + ",\"sources\":[{\"name\":\"Original test tint\",\"license\":\"CC0-1.0\"}]}");
        return path;
    }
    [Test] public void SurfaceChangesRequireNoGeometrySources()
    {
        var prepared = BridgeSurfaceContract.Prepare(Write("[{\"key\":\"concrete\",\"baseColor\":[0.7,0.6,0.5,1]}]"), new[] { "concrete" });
        Assert.That(prepared.Files, Is.Empty);
        Assert.That(prepared.Profile.materials[0].key, Is.EqualTo("concrete"));
    }
    [Test] public void ProfileRejectsUnsupportedPhysicalSlot()
    {
        Assert.Throws<InvalidDataException>(() => BridgeSurfaceContract.Prepare(Write("[{\"key\":\"timber\",\"baseColor\":[1,1,1,1]}]"), new[] { "concrete" }));
    }
    [Test] public void MissingTextureFailsBeforeMutation()
    {
        Assert.Throws<InvalidDataException>(() => BridgeSurfaceContract.Prepare(Write("[{\"key\":\"concrete\",\"baseColorPath\":\"missing.png\"}]"), new[] { "concrete" }));
    }
    [Test] public void TextureCannotEscapeSourceDirectory()
    {
        Assert.Throws<InvalidDataException>(() => BridgeSurfaceContract.Prepare(Write("[{\"key\":\"concrete\",\"baseColorPath\":\"../outside.png\"}]"), new[] { "concrete" }));
    }
    [Test] public void TextureCannotServeColorAndNormalRoles()
    {
        File.WriteAllText(Path.Combine(directory, "color.png"), "test input");
        Assert.Throws<InvalidDataException>(() => BridgeSurfaceContract.Prepare(Write("[{\"key\":\"concrete\",\"baseColorPath\":\"color.png\",\"normalPath\":\"color.png\"}]"), new[] { "concrete" }));
    }
    [Test] public void TintAndTextureBytesBothAffectFingerprint()
    {
        File.WriteAllText(Path.Combine(directory, "color.png"), "first texture");
        string path = Write("[{\"key\":\"concrete\",\"baseColorPath\":\"color.png\"}]");
        string first = BridgeSurfaceContract.Prepare(path, new[] { "concrete" }).Hash;
        File.WriteAllText(Path.Combine(directory, "color.png"), "second texture");
        Assert.That(BridgeSurfaceContract.Prepare(path, new[] { "concrete" }).Hash, Is.Not.EqualTo(first));
        path = Write("[{\"key\":\"concrete\",\"baseColor\":[0.7,0.6,0.5,1]}]");
        string tint = BridgeSurfaceContract.Prepare(path, new[] { "concrete" }).Hash;
        path = Write("[{\"key\":\"concrete\",\"baseColor\":[0.8,0.6,0.5,1]}]");
        Assert.That(BridgeSurfaceContract.Prepare(path, new[] { "concrete" }).Hash, Is.Not.EqualTo(tint));
    }
}
