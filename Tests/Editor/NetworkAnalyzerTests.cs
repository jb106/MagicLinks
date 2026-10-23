using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MagicLinks.Tests
{
    public struct NetTestItem
    {
        public int id;
        public int count;
    }

    public class NetTestShip
    {
        public float oxygen;
        public List<NetTestItem> cargo;
        public GameObject pilot;
        public AudioSource alarm;
        public Action onDamaged;
        public readonly int build = 1;
        [NonSerialized] public float localTimer;
    }

    public class NetworkAnalyzerTests
    {
        private MagicLinksConfiguration _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<MagicLinksConfiguration>();

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);

        private static DynamicVariable Link(string name, string type, int magicType = 0, bool isList = false, bool networked = true) =>
            new DynamicVariable { vName = name, vLabelType = type, magicType = magicType, isList = isList, isNetworked = networked };

        private void ChooseValidShipModes()
        {
            _config.SetFieldMode(typeof(NetTestShip).FullName, "pilot", MagicNetFieldMode.Reference);
            _config.SetFieldMode(typeof(NetTestShip).FullName, "alarm", MagicNetFieldMode.Excluded);
            _config.SetFieldMode(typeof(NetTestShip).FullName, "onDamaged", MagicNetFieldMode.Excluded);
        }

        [Test]
        public void NonNetworkedLinks_AreIgnored()
        {
            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[] { Link("A", "int", networked: false) }, _config);
            Assert.IsEmpty(analysis.links);
        }

        [Test]
        public void Ids_FollowNameOrder()
        {
            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[] { Link("Zeta", "int"), Link("Alpha", "bool") }, _config);

            Assert.AreEqual("Alpha", analysis.links[0].variable.vName);
            Assert.AreEqual(1, analysis.links[0].id);
            Assert.AreEqual(2, analysis.links[1].id);
        }

        [Test]
        public void DictionaryNames_MatchTheManager()
        {
            Assert.AreEqual("INT_LIST_EVENT", MagicLinksNetworkAnalyzer.GetDictionaryName(Link("A", "int", 1, true)));
            Assert.AreEqual("VECTOR3", MagicLinksNetworkAnalyzer.GetDictionaryName(Link("A", "Vector3")));
            Assert.AreEqual("VOID", MagicLinksNetworkAnalyzer.GetDictionaryName(Link("A", "Void", 2)));
        }

        [Test]
        public void CustomType_WithoutChoices_ReportsEveryUntransportableField()
        {
            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[] { Link("Ship", nameof(NetTestShip)) }, _config);
            string errors = string.Join("\n", analysis.Errors);

            Assert.IsTrue(analysis.HasErrors);
            StringAssert.Contains("NetTestShip.pilot", errors);
            StringAssert.Contains("NetTestShip.alarm", errors);
            StringAssert.Contains("NetTestShip.onDamaged", errors);
            StringAssert.DoesNotContain("oxygen", errors);
        }

        [Test]
        public void CustomType_WithChoices_HasNoErrors()
        {
            ChooseValidShipModes();

            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[] { Link("Ship", nameof(NetTestShip)) }, _config);

            Assert.IsFalse(analysis.HasErrors, string.Join("\n", analysis.Errors));
            Assert.AreEqual("MagicNet_MagicLinks_Tests_NetTestShip.Instance", analysis.links[0].serializer);
        }

        [Test]
        public void ReadonlyAndNonSerializedFields_AreExcludedAutomatically()
        {
            ChooseValidShipModes();

            var analyzer = new MagicLinksNetworkAnalyzer(_config);
            var ship = analyzer.DescribeForEditor(typeof(NetTestShip)).First(d => d.type == typeof(NetTestShip));

            Assert.AreEqual(MagicNetFieldMode.Excluded, ship.fields.First(f => f.field.Name == "build").effectiveMode);
            Assert.AreEqual(MagicNetFieldMode.Excluded, ship.fields.First(f => f.field.Name == "localTimer").effectiveMode);
            Assert.AreEqual(MagicNetFieldMode.Value, ship.fields.First(f => f.field.Name == "oxygen").effectiveMode);
        }

        [Test]
        public void ReferenceOnAValueField_IsAnError()
        {
            ChooseValidShipModes();
            _config.SetFieldMode(typeof(NetTestShip).FullName, "oxygen", MagicNetFieldMode.Reference);

            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[] { Link("Ship", nameof(NetTestShip)) }, _config);

            StringAssert.Contains("NetTestShip.oxygen", string.Join("\n", analysis.Errors));
        }

        [Test]
        public void GeneratedScript_RegistersEveryLink()
        {
            ChooseValidShipModes();
            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[]
            {
                Link("Ship", nameof(NetTestShip)), Link("Door", "bool"), Link("Boom", "Vector3", 1), Link("Alarm", "Void", 2)
            }, _config);

            string template = File.ReadAllText(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.MagicNetworkTemplate));
            string script = MagicLinksNetworkScriptsGenerator.BuildNetworkScript(template, analysis);

            StringAssert.Contains("session.AddVariable(4, \"Ship\", manager.NETTESTSHIP", script);
            StringAssert.Contains("session.AddVariable(3, \"Door\", manager.BOOL, MagicNetSerializers.Bool, false);", script);
            StringAssert.Contains("session.AddEvent(2, \"Boom\", manager.VECTOR3_EVENT, MagicNetSerializers.Vector3);", script);
            StringAssert.Contains("session.AddVoidEvent(1, \"Alarm\", manager.VOID);", script);
            StringAssert.Contains("class MagicNet_MagicLinks_Tests_NetTestItem", script);
            StringAssert.DoesNotContain("#SCHEMAHASH", script);
        }

        [Test]
        public void GeneratedScript_ListVariableIsSyncedItemByItem_ListEventAsAWholeList()
        {
            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[]
            {
                Link("Inventory", "int", isList: true), Link("Drops", "int", 1, true)
            }, _config);

            string template = File.ReadAllText(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.MagicNetworkTemplate));
            string script = MagicLinksNetworkScriptsGenerator.BuildNetworkScript(template, analysis);

            StringAssert.Contains("session.AddList(2, \"Inventory\", manager.INT_LIST, MagicNetSerializers.Int, false);", script);
            StringAssert.Contains("session.AddEvent(1, \"Drops\", manager.INT_LIST_EVENT, MagicNetSerializers.ListOf(MagicNetSerializers.Int));", script);
        }

        [Test]
        public void GeneratedScript_WithErrors_RegistersNothing()
        {
            var analysis = MagicLinksNetworkAnalyzer.Analyze(new[] { Link("Ship", nameof(NetTestShip)) }, _config);

            string template = File.ReadAllText(MagicLinksUtilities.GetPackageRelativePath(MagicLinksConst.MagicNetworkTemplate));
            string script = MagicLinksNetworkScriptsGenerator.BuildNetworkScript(template, analysis);

            StringAssert.Contains("MagicLinksNetworkRegistry.Install(SchemaHash, null, new string[]", script);
            StringAssert.DoesNotContain("session.Add", script);
        }
    }
}
