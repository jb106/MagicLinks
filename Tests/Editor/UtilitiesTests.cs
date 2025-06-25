using NUnit.Framework;
using MagicLinks;

namespace MagicLinks.Tests
{
    public class UtilitiesTests
    {
        [Test]
        public void GetStringWithFirstUpperCaseLetter_UppercasesFirst()
        {
            string result = MagicLinksUtilities.GetStringWithFirstUpperCaseLetter("test");
            Assert.AreEqual("Test", result);
        }

        [Test]
        public void GetRuntimeField_ReturnsCorrectPath()
        {
            string path = MagicLinksConst.GetRuntimeField("Bool");
            Assert.AreEqual("Runtime/UI/Fields/bool.uxml", path);
        }

        [Test]
        public void GetTrueType_ReturnsSystemTypeForBaseType()
        {
            string type = MagicLinksUtilities.GetTrueType("int");
            Assert.AreEqual(typeof(int).ToString(), type);
        }

        [Test]
        public void GetTrueType_ReturnsInputForUnknownType()
        {
            string type = MagicLinksUtilities.GetTrueType("MyCustomType");
            Assert.AreEqual("MyCustomType", type);
        }
    }
}
