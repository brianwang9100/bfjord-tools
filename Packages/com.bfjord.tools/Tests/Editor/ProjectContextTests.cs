using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Bwork.Authoring.Editor.Tests
{
    public sealed class ProjectContextTests
    {
        string directory;

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Directory.Exists("/private/tmp") ? "/private/tmp" : Path.GetTempPath(), "bfjord-context-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown] public void TearDown() => Directory.Delete(directory, true);

        static JObject Config() => JObject.Parse("{\"schemaVersion\":1,\"enabled\":true,\"bridgeSourceRoot\":\"SourceArt\",\"captureRoot\":\"Evidence\"}");

        [Test] public void ConfigurationResolvesAtAnUnrelatedProjectLocation()
        {
            var config = ProjectContext.Parse(Config().ToString(), directory);
            Assert.That(config.bridgeSourceRoot, Is.EqualTo(Path.Combine(directory, "SourceArt")));
            Assert.That(config.captureRoot, Is.EqualTo(Path.Combine(directory, "Evidence")));
            Assert.That(config.sandboxScene, Is.EqualTo("Assets/Scenes/WorldAuthoringTools.unity"));
        }

        [Test] public void ConfigurationRequiresExplicitOptIn()
        {
            var config = Config(); config.Remove("enabled");
            Assert.Throws<InvalidDataException>(() => ProjectContext.Parse(config.ToString(), directory));
        }

        [TestCase("../Elsewhere/Generated")]
        [TestCase("Assets/../ProjectSettings")]
        [TestCase("Assets")]
        public void GeneratedRootCannotEscapeAssets(string root)
        {
            var config = Config(); config["sandboxGeneratedRoot"] = root;
            Assert.Throws<InvalidDataException>(() => ProjectContext.Parse(config.ToString(), directory));
        }

        [Test] public void GeneratedRootsCannotOverlap()
        {
            var config = Config(); config["bridgeDemoGeneratedRoot"] = "Assets/Generated/AuthoringTools/Nested";
            Assert.Throws<InvalidDataException>(() => ProjectContext.Parse(config.ToString(), directory));
        }

        [Test] public void CaptureOutputCannotOverwriteProjectConfiguration()
        {
            var config = Config(); config["captureRoot"] = "ProjectSettings";
            Assert.Throws<InvalidDataException>(() => ProjectContext.Parse(config.ToString(), directory));
        }

        [Test] public void UnknownConfigurationFieldsRejectTypos()
        {
            var config = Config(); config["capturRoot"] = "Wrong";
            Assert.Throws<InvalidDataException>(() => ProjectContext.Parse(config.ToString(), directory));
        }

        [Test] public void DispatcherRejectsUnknownMethodsAndArgumentsBeforeExecution()
        {
            Assert.Throws<InvalidDataException>(() => AuthoringBatch.ValidateRequest("{\"schemaVersion\":1,\"command\":\"System.IO.File.Delete\",\"arguments\":{}}"));
            Assert.Throws<InvalidDataException>(() => AuthoringBatch.ValidateRequest("{\"schemaVersion\":1,\"command\":\"bwork_roads\",\"arguments\":{\"actoin\":\"apply\"}}"));
            Assert.Throws<InvalidDataException>(() => AuthoringBatch.ValidateRequest("{\"schemaVersion\":1,\"command\":\"bwork_roads\",\"arguments\":{\"fourArms\":\"false\"}}"));
        }

        [Test] public void DispatcherAcceptsTypedPublicCommandRequest()
        {
            Assert.DoesNotThrow(() => AuthoringBatch.ValidateRequest("{\"schemaVersion\":1,\"command\":\"bwork_roads\",\"arguments\":{\"action\":\"prepare\",\"fourArms\":false}}"));
        }
    }
}
