using System;
using System.Collections.Generic;

namespace RebornFrame.Spell.Spine
{
    /// <summary>按规则把皮肤名分成组。不读取具体项目的部位名。</summary>
    public static class SpineSkins
    {
        public static List<SpineSkinGroup> Group(IReadOnlyList<string> skinNames, string defaultSkinName, SpineSkinRule rule)
        {
            var groups = new List<SpineSkinGroup>();
            if (rule == null) rule = new SpineSkinRule();

            var names = new List<string>();
            if (skinNames != null)
            {
                for (var i = 0; i < skinNames.Count; i++)
                {
                    var name = skinNames[i];
                    if (string.IsNullOrEmpty(name)) continue;
                    if (rule.SkipDefault && !string.IsNullOrEmpty(defaultSkinName) &&
                        string.Equals(name, defaultSkinName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    names.Add(name);
                }
            }

            if (names.Count == 0) return groups;
            if (rule.Grouping == SpineSkinGrouping.Manual)
            {
                AppendManual(groups, names, rule);
                return groups;
            }

            var grouping = rule.Grouping;
            var separator = rule.Separator ?? "";
            if (separator.Length == 0 && (grouping == SpineSkinGrouping.FirstSegment || grouping == SpineSkinGrouping.ParentPath))
                grouping = SpineSkinGrouping.Single;

            if (grouping == SpineSkinGrouping.Single)
            {
                var group = NewGroup(Label(rule.LooseLabel, "皮肤"));
                AddOptions(group, names, separator, rule);
                groups.Add(group);
                return groups;
            }

            if (grouping == SpineSkinGrouping.EachSkin)
            {
                var leaves = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var i = 0; i < names.Count; i++)
                {
                    var leaf = Leaf(names[i], separator);
                    leaves.TryGetValue(leaf, out var count);
                    leaves[leaf] = count + 1;
                }

                for (var i = 0; i < names.Count; i++)
                {
                    var leaf = Leaf(names[i], separator);
                    var label = leaves[leaf] > 1 ? names[i] : leaf;
                    var group = NewGroup(label);
                    group.Options.Add(Empty(rule));
                    group.Options.Add(new SpineSkinOption { Label = label, SkinName = names[i] });
                    groups.Add(group);
                }

                groups.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.Ordinal));
                return groups;
            }

            var map = new Dictionary<string, List<SpineSkinOption>>(StringComparer.Ordinal);
            var order = new List<string>();
            for (var i = 0; i < names.Count; i++)
            {
                Split(names[i], separator, grouping, out var folder, out var label);
                if (!map.TryGetValue(folder, out var options))
                {
                    options = new List<SpineSkinOption>();
                    map.Add(folder, options);
                    order.Add(folder);
                }

                options.Add(new SpineSkinOption { Label = label, SkinName = names[i] });
            }

            order.Sort(StringComparer.Ordinal);
            for (var i = 0; i < order.Count; i++)
            {
                var folder = order[i];
                var group = NewGroup(folder.Length == 0 ? Label(rule.LooseLabel, "皮肤") : folder);
                group.Options.Add(Empty(rule));
                var options = map[folder];
                options.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.Ordinal));
                group.Options.AddRange(options);
                groups.Add(group);
            }

            return groups;
        }

        public static int PreferredIndex(IReadOnlyList<string> names, IReadOnlyList<string> preferred)
        {
            if (names == null || names.Count == 0) return -1;
            if (preferred != null)
            {
                for (var pass = 0; pass < 2; pass++)
                {
                    for (var k = 0; k < preferred.Count; k++)
                    {
                        var key = preferred[k];
                        if (string.IsNullOrEmpty(key)) continue;
                        for (var i = 0; i < names.Count; i++)
                        {
                            if (string.IsNullOrEmpty(names[i])) continue;
                            var exact = string.Equals(names[i], key, StringComparison.OrdinalIgnoreCase);
                            var contains = names[i].IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
                            if (pass == 0 ? exact : contains) return i;
                        }
                    }
                }
            }

            return 0;
        }

        static void AppendManual(List<SpineSkinGroup> groups, List<string> names, SpineSkinRule rule)
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < names.Count; i++) known.Add(names[i]);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var buckets = rule.Manual;
            if (buckets == null) return;
            var separator = rule.Separator ?? "";
            for (var i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                if (bucket == null || bucket.Skins == null || bucket.Skins.Count == 0) continue;
                var group = NewGroup(Label(bucket.Label, "组"));
                group.Options.Add(Empty(rule));
                for (var s = 0; s < bucket.Skins.Count; s++)
                {
                    var skin = bucket.Skins[s];
                    if (string.IsNullOrEmpty(skin) || !known.Contains(skin) || !used.Add(skin)) continue;
                    group.Options.Add(new SpineSkinOption { Label = Leaf(skin, separator), SkinName = skin });
                }

                if (group.Options.Count > 1) groups.Add(group);
            }
        }

        static void AddOptions(SpineSkinGroup group, List<string> names, string separator, SpineSkinRule rule)
        {
            var options = new List<SpineSkinOption>(names.Count);
            var leaves = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < names.Count; i++)
            {
                var leaf = Leaf(names[i], separator);
                leaves.TryGetValue(leaf, out var count);
                leaves[leaf] = count + 1;
            }

            for (var i = 0; i < names.Count; i++)
            {
                var leaf = Leaf(names[i], separator);
                options.Add(new SpineSkinOption
                {
                    Label = leaves[leaf] > 1 ? names[i] : leaf,
                    SkinName = names[i],
                });
            }

            options.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.Ordinal));
            group.Options.Add(Empty(rule));
            group.Options.AddRange(options);
        }

        static void Split(string name, string separator, SpineSkinGrouping grouping, out string folder, out string label)
        {
            var parts = string.IsNullOrEmpty(separator)
                ? new[] { name }
                : name.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                folder = "";
                label = name;
                return;
            }

            if (grouping == SpineSkinGrouping.ParentPath)
            {
                label = parts[parts.Length - 1];
                folder = string.Join(separator, parts, 0, parts.Length - 1);
                return;
            }

            folder = parts[0];
            label = string.Join(separator, parts, 1, parts.Length - 1);
        }

        static string Leaf(string name, string separator)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(separator)) return name ?? "";
            var index = name.LastIndexOf(separator, name.Length - 1, StringComparison.Ordinal);
            return index < 0 || index + separator.Length >= name.Length ? name : name.Substring(index + separator.Length);
        }

        static SpineSkinGroup NewGroup(string label)
        {
            return new SpineSkinGroup { Label = label ?? "" };
        }

        static SpineSkinOption Empty(SpineSkinRule rule)
        {
            return new SpineSkinOption { Label = Label(rule.EmptyLabel, "无"), SkinName = "" };
        }

        static string Label(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
