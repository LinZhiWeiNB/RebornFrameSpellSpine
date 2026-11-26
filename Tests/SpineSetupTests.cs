using System.Collections.Generic;
using NUnit.Framework;

namespace RebornFrame.Spell.Spine.Tests
{
    public class SpineSetupTests
    {
        const string Text =
            "v1\n" +
            "seq\t2\n" +
            "play\tg2\tt1\n" +
            "group\tg1\t正面\n" +
            "group\tg2\t背面\n" +
            "trigger\tt1\t攻击\n" +
            "slot\tslot1\t衣服\tskin1\n" +
            "skin\tslot1\tskin1\t布衣\n" +
            "clip\tt1\tg1\tf_attack\n" +
            "clip\tt1\tg2\tb_attack\n" +
            "tex\tslot1\tskin1\tg1\tf_cloth/cloth1\n" +
            "tex\tslot1\tskin1\tg2\tb_cloth/cloth1\n";

        [Test]
        public void Parse_UsesGroupToPickClipAndSkin()
        {
            var setup = SpineSetup.Parse(Text);
            Assert.AreEqual("g2", setup.GroupId);
            Assert.AreEqual("t1", setup.TriggerId);
            Assert.AreEqual("b_attack", setup.Clip("t1", "g2"));
            Assert.AreEqual("f_attack", setup.Clip("t1", "g1"));
            Assert.AreEqual("", setup.Clip("t1", "missing"));

            var skins = new List<string>();
            setup.CollectSkins("g2", "default", skins);
            CollectionAssert.AreEqual(new[] { "b_cloth/cloth1" }, skins);
            setup.CollectSkins("g2", "b_cloth/cloth1", skins);
            Assert.AreEqual(0, skins.Count);
        }

        [Test]
        public void Parse_DropsMissingPlayIds()
        {
            var setup = SpineSetup.Parse("v1\nplay\tgone\ttgone\ngroup\tg1\t正面\ntrigger\tt1\t攻击\n");
            Assert.AreEqual("g1", setup.GroupId);
            Assert.AreEqual("t1", setup.TriggerId);
            Assert.IsFalse(setup.HasGroup("gone"));
        }
    }
}
