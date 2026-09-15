using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Bwork.Authoring.Editor.BridgeAssets;

public sealed class BridgeAssetContractTests
{
    [TestCase(7f, true)]
    [TestCase(8f, true)]
    [TestCase(0f, false)]
    [TestCase(-1f, false)]
    [TestCase(9f, false)]
    public void CarriagewayMustFitInsideStructuralDeck(float width, bool valid)
    {
        string directory = Path.Combine(Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library")), "bridge-width-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string manifestPath = Path.Combine(directory, "manifest.json");
            string placementPath = Path.Combine(directory, "placement.json");
            var manifest = JObject.Parse(@"{
                'schemaVersion':1,'id':'primary','recipeHash':'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
                'generatorVersion':'fixture','units':'meters','coordinateSystem':'unity-y-up-z-forward',
                'deckDatum':0,'totalLength':180,'deckWidth':8,'archSpan':120,'archRise':35,
                'boundsMin':{'x':-5,'y':-42,'z':-90},'boundsMax':{'x':5,'y':1.4,'z':90},
                'lods':[
                    {'level':0,'fbxRelativePath':'lod0.fbx','screenRelativeHeight':0.35,'triangles':30},
                    {'level':1,'fbxRelativePath':'lod1.fbx','screenRelativeHeight':0.12,'triangles':20},
                    {'level':2,'fbxRelativePath':'lod2.fbx','screenRelativeHeight':0.015,'triangles':10}],
                'colliderRelativePath':'collision.fbx',
                'materialSlots':[
                    {'slotName':'BridgeConcrete','materialKey':'concrete'},
                    {'slotName':'BridgeAsphalt','materialKey':'asphalt'},
                    {'slotName':'BridgeMetal','materialKey':'metal'}],
                'materials':[
                    {'key':'concrete','baseColor':[1,1,1,1],'tilingMeters':2},
                    {'key':'asphalt','baseColor':[0.1,0.1,0.1,1],'tilingMeters':2},
                    {'key':'metal','baseColor':[0.2,0.2,0.2,1],'tilingMeters':1}],
                'sources':[{'name':'Test fixture','license':'original','url':''}]
            }");
            // Missing required width takes the default zero and must fail just like explicit zero.
            if (width != 0) manifest["clearCarriageway"] = width;
            File.WriteAllText(manifestPath, manifest.ToString());
            File.WriteAllText(placementPath, "{\"schemaVersion\":1,\"batchId\":\"primary\",\"position\":{\"x\":0,\"y\":46,\"z\":0},\"yawDegrees\":0,\"targetScene\":\"Assets/Scenes/BridgeAuthoringDemo.unity\"}");
            foreach (string file in new[] { "lod0.fbx", "lod1.fbx", "lod2.fbx", "collision.fbx" })
                File.WriteAllText(Path.Combine(directory, file), "Read-only prepare does not import FBX.");
            if (valid)
                Assert.That(BridgeAssetContract.Prepare(manifestPath, placementPath, "primary").Manifest.clearCarriageway, Is.EqualTo(width));
            else
                Assert.Throws<InvalidDataException>(() => BridgeAssetContract.Prepare(manifestPath, placementPath, "primary"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestCase("../outside.fbx")]
    [TestCase("textures/../../outside.png")]
    [TestCase("/tmp/outside.fbx")]
    [TestCase("textures\\outside.png")]
    public void SourcePathsCannotLeaveManifestDirectory(string path)
    {
        Assert.Throws<InvalidDataException>(() => BridgeAssetContract.ResolveRelative("/tmp/bridge-source", path));
    }

    [TestCase("../other")]
    [TestCase("Bridge One")]
    [TestCase("")]
    public void BatchIdentityCannotBecomeAnAssetPath(string batch)
    {
        Assert.Throws<InvalidDataException>(() => BridgeAssetContract.ValidateId(batch, "batchId"));
    }

    [Test]
    public void SourceFingerprintChangesWhenAssetBytesChange()
    {
        string directory = Path.Combine(Path.GetTempPath(), "bridge-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "bridge.fbx"), "first geometry");
            string first = BridgeAssetContract.Fingerprint(directory, "manifest", new[] { "bridge.fbx" });
            File.WriteAllText(Path.Combine(directory, "bridge.fbx"), "second geometry");
            Assert.That(BridgeAssetContract.Fingerprint(directory, "manifest", new[] { "bridge.fbx" }), Is.Not.EqualTo(first));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void PlacementRejectsScalingAndNonfiniteCoordinates()
    {
        Assert.Throws<InvalidDataException>(() => BridgeAssetContract.ReadPlacementJson(
            "{\"schemaVersion\":1,\"batchId\":\"primary\",\"position\":{\"x\":0,\"y\":46,\"z\":0},\"yawDegrees\":0,\"targetScene\":\"Assets/Scenes/CoastalBridgeDemo.unity\",\"scale\":2}", "primary"));
        Assert.Throws<InvalidDataException>(() => BridgeAssetContract.ReadPlacementJson(
            "{\"schemaVersion\":1,\"batchId\":\"primary\",\"position\":{\"x\":0,\"y\":\"NaN\",\"z\":0},\"yawDegrees\":0,\"targetScene\":\"Assets/Scenes/CoastalBridgeDemo.unity\"}", "primary"));
    }
}
