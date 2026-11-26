using System.Collections.Generic;
using NUnit.Framework;

namespace RebornFrame.Spell.Spine.Tests
{
    public class SpineSkinTests
    {
        static readonly string[] Names =
        {
            "default",
            "attack/no_attack",
            "attack/one_attack",
            "b_cloth/cloth1",
            "solo",
            "form/a/extra",
        };

        [Test]
        public void Group_FirstSegment_UsesSeparator()
        {
            var groups = SpineSkins.Group(Names, "default", new SpineSkinRule());

            Assert.AreEqual(4, groups.Count);
            Assert.AreEqual("皮肤", groups[0].Label);
            Assert.AreEqual("solo", groups[0].Options[1].SkinName);
            Assert.AreEqual("attack", groups[1].Label);
            Assert.AreEqual("无", groups[1].Options[0].Label);
            Assert.AreEqual("no_attack", groups[1].Options[1].Label);
            Assert.AreEqual("one_attack", groups[1].Options[2].Label);
            Assert.AreEqual("b_cloth", groups[2].Label);
            Assert.AreEqual("form", groups[3].Label);
            Assert.AreEqual("a/extra", groups[3].Options[1].Label);
            Assert.AreEqual("form/a/extra", groups[3].Options[1].SkinName);
        }

        [Test]
        public void Group_ParentPath_KeepsLeadingFolders()
        {
            var rule = new SpineSkinRule { Grouping = SpineSkinGrouping.ParentPath };
            var groups = SpineSkins.Group(Names, "default", rule);

            Assert.AreEqual("form/a", groups[3].Label);
            Assert.AreEqual("extra", groups[3].Options[1].Label);
        }

        [Test]
        public void Group_CustomSeparator()
        {
            var rule = new SpineSkinRule { Separator = "." };
            var groups = SpineSkins.Group(new[] { "arm.sword", "arm.bow", "body" }, null, rule);

            Assert.AreEqual("皮肤", groups[0].Label);
            Assert.AreEqual("body", groups[0].Options[1].SkinName);
            Assert.AreEqual("arm", groups[1].Label);
            Assert.AreEqual("bow", groups[1].Options[1].Label);
            Assert.AreEqual("sword", groups[1].Options[2].Label);
        }

        [Test]
        public void Group_EachSkin_OneGroupPerSkin()
        {
            var rule = new SpineSkinRule { Grouping = SpineSkinGrouping.EachSkin };
            var groups = SpineSkins.Group(new[] { "attack/one", "b_cloth/cloth1" }, null, rule);

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("cloth1", groups[0].Label);
            Assert.AreEqual("one", groups[1].Label);
            Assert.AreEqual("", groups[0].Options[0].SkinName);
            Assert.AreEqual("b_cloth/cloth1", groups[0].Options[1].SkinName);
        }

        [Test]
        public void Group_Single_PutsEverySkinInOneGroup()
        {
            var rule = new SpineSkinRule { Grouping = SpineSkinGrouping.Single };
            var groups = SpineSkins.Group(Names, "default", rule);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(6, groups[0].Options.Count);
            Assert.AreEqual("", groups[0].Options[0].SkinName);
        }

        [Test]
        public void Group_EmptySeparator_BecomesSingle()
        {
            var rule = new SpineSkinRule { Separator = "" };
            var groups = SpineSkins.Group(new[] { "a/b", "c" }, null, rule);

            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(3, groups[0].Options.Count);
        }

        [Test]
        public void Group_CanKeepDefaultSkin()
        {
            var rule = new SpineSkinRule { SkipDefault = false, Grouping = SpineSkinGrouping.Single };
            var groups = SpineSkins.Group(new[] { "default", "hat" }, "default", rule);

            Assert.AreEqual("default", groups[0].Options[1].SkinName);
        }

        [Test]
        public void Group_Manual_UsesNamedBuckets()
        {
            var rule = new SpineSkinRule { Grouping = SpineSkinGrouping.Manual, Separator = "/" };
            var weapon = new SpineSkinBucket { Label = "武器" };
            weapon.Skins.Add("attack/one_attack");
            weapon.Skins.Add("missing");
            weapon.Skins.Add("attack/no_attack");
            var cloth = new SpineSkinBucket { Label = "衣服" };
            cloth.Skins.Add("b_cloth/cloth1");
            cloth.Skins.Add("attack/one_attack");
            rule.Manual.Add(weapon);
            rule.Manual.Add(cloth);

            var groups = SpineSkins.Group(Names, "default", rule);

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("武器", groups[0].Label);
            Assert.AreEqual("one_attack", groups[0].Options[1].Label);
            Assert.AreEqual("no_attack", groups[0].Options[2].Label);
            Assert.AreEqual("衣服", groups[1].Label);
            Assert.AreEqual(2, groups[1].Options.Count);
            Assert.AreEqual("b_cloth/cloth1", groups[1].Options[1].SkinName);
        }

        [Test]
        public void PreferredIndex_MatchesExactNameBeforeSubstring()
        {
            var names = new List<string> { "run_idle", "idle", "attack" };
            Assert.AreEqual(1, SpineSkins.PreferredIndex(names, new[] { "idle" }));
            Assert.AreEqual(0, SpineSkins.PreferredIndex(new[] { "run" }, new[] { "missing" }));
            Assert.AreEqual(-1, SpineSkins.PreferredIndex(new string[0], new[] { "idle" }));
        }
    }
}
