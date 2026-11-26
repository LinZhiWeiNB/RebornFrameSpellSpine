using System;
using System.Collections.Generic;
using System.Text;

namespace RebornFrame.Spell.Spine
{
    /// <summary>
    /// setup.txt。群组、触发器、外观槽位，以及当前群组和触发器。
    /// 触发器乘群组得到片段名，槽位的皮肤乘群组得到皮肤名。
    /// </summary>
    public sealed class SpineSetup
    {
        readonly List<SpineSetupItem> _groups = new List<SpineSetupItem>();
        readonly List<SpineSetupItem> _triggers = new List<SpineSetupItem>();
        readonly List<SpineSetupSlot> _slots = new List<SpineSetupSlot>();
        readonly Dictionary<string, Dictionary<string, string>> _clips = new Dictionary<string, Dictionary<string, string>>();
        readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _textures =
            new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

        public string GroupId { get; private set; } = "";
        public string TriggerId { get; private set; } = "";

        public static SpineSetup Parse(string text)
        {
            var setup = new SpineSetup();
            if (string.IsNullOrEmpty(text)) return setup;
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0 || line == "v1") continue;
                var parts = line.Split('\t');
                for (var p = 0; p < parts.Length; p++) parts[p] = Unesc(parts[p]);
                if (parts.Length < 2) continue;
                switch (parts[0])
                {
                    case "play":
                        setup.GroupId = parts[1];
                        setup.TriggerId = parts.Length > 2 ? parts[2] : "";
                        break;
                    case "group":
                        if (parts.Length < 3 || parts[1].Length == 0) break;
                        setup._groups.Add(new SpineSetupItem(parts[1], parts[2]));
                        break;
                    case "trigger":
                        if (parts.Length < 3 || parts[1].Length == 0) break;
                        setup._triggers.Add(new SpineSetupItem(parts[1], parts[2]));
                        break;
                    case "slot":
                        if (parts.Length < 3 || parts[1].Length == 0) break;
                        setup._slots.Add(new SpineSetupSlot(parts[1], parts[2], parts.Length > 3 ? parts[3] : ""));
                        break;
                    case "skin":
                        if (parts.Length < 4 || parts[2].Length == 0) break;
                        var slot = setup.FindSlot(parts[1]);
                        if (slot == null) break;
                        slot.Skins.Add(new SpineSetupItem(parts[2], parts[3]));
                        break;
                    case "clip":
                        if (parts.Length < 3) break;
                        setup.SetClip(parts[1], parts[2], parts.Length > 3 ? parts[3] : "");
                        break;
                    case "tex":
                        if (parts.Length < 4) break;
                        setup.SetTexture(parts[1], parts[2], parts[3], parts.Length > 4 ? parts[4] : "");
                        break;
                }
            }

            if (!setup.HasGroup(setup.GroupId))
                setup.GroupId = setup._groups.Count > 0 ? setup._groups[0].Id : "";
            if (!setup.HasTrigger(setup.TriggerId))
                setup.TriggerId = setup._triggers.Count > 0 ? setup._triggers[0].Id : "";
            return setup;
        }

        public bool HasGroup(string id)
        {
            return Contains(_groups, id);
        }

        public bool HasTrigger(string id)
        {
            return Contains(_triggers, id);
        }

        public string Clip(string triggerId, string groupId)
        {
            if (string.IsNullOrEmpty(triggerId) || string.IsNullOrEmpty(groupId)) return "";
            if (!_clips.TryGetValue(triggerId, out var row) || row == null) return "";
            return row.TryGetValue(groupId, out var value) ? value ?? "" : "";
        }

        public void CollectSkins(string groupId, string defaultSkin, List<string> into)
        {
            if (into == null) return;
            into.Clear();
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var skin = Chosen(slot);
                if (skin == null) continue;
                var name = Texture(slot.Id, skin.Id, groupId);
                if (string.IsNullOrEmpty(name)) continue;
                if (!string.IsNullOrEmpty(defaultSkin) && string.Equals(name, defaultSkin, StringComparison.OrdinalIgnoreCase))
                    continue;
                into.Add(name);
            }
        }

        string Texture(string slotId, string skinId, string groupId)
        {
            if (string.IsNullOrEmpty(slotId) || string.IsNullOrEmpty(skinId) || string.IsNullOrEmpty(groupId)) return "";
            if (!_textures.TryGetValue(slotId, out var skins) || skins == null) return "";
            if (!skins.TryGetValue(skinId, out var row) || row == null) return "";
            return row.TryGetValue(groupId, out var value) ? value ?? "" : "";
        }

        void SetClip(string triggerId, string groupId, string value)
        {
            if (string.IsNullOrEmpty(triggerId) || string.IsNullOrEmpty(groupId)) return;
            if (!_clips.TryGetValue(triggerId, out var row) || row == null)
            {
                row = new Dictionary<string, string>();
                _clips[triggerId] = row;
            }

            row[groupId] = value ?? "";
        }

        void SetTexture(string slotId, string skinId, string groupId, string value)
        {
            if (string.IsNullOrEmpty(slotId) || string.IsNullOrEmpty(skinId) || string.IsNullOrEmpty(groupId)) return;
            if (!_textures.TryGetValue(slotId, out var skins) || skins == null)
            {
                skins = new Dictionary<string, Dictionary<string, string>>();
                _textures[slotId] = skins;
            }

            if (!skins.TryGetValue(skinId, out var row) || row == null)
            {
                row = new Dictionary<string, string>();
                skins[skinId] = row;
            }

            row[groupId] = value ?? "";
        }

        SpineSetupSlot FindSlot(string id)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Id == id) return _slots[i];
            }

            return null;
        }

        static SpineSetupItem Chosen(SpineSetupSlot slot)
        {
            for (var i = 0; i < slot.Skins.Count; i++)
            {
                if (slot.Skins[i].Id == slot.Chosen) return slot.Skins[i];
            }

            return slot.Skins.Count > 0 ? slot.Skins[0] : null;
        }

        static bool Contains(List<SpineSetupItem> items, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id) return true;
            }

            return false;
        }

        static string Unesc(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0) return value ?? "";
            var text = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch != '\\' || i + 1 >= value.Length)
                {
                    text.Append(ch);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case '\\': text.Append('\\'); break;
                    case 't': text.Append('\t'); break;
                    case 'r': text.Append('\r'); break;
                    case 'n': text.Append('\n'); break;
                    default: text.Append(value[i]); break;
                }
            }

            return text.ToString();
        }

        sealed class SpineSetupItem
        {
            public readonly string Id;
            public readonly string Name;

            public SpineSetupItem(string id, string name)
            {
                Id = id ?? "";
                Name = name ?? "";
            }
        }

        sealed class SpineSetupSlot
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Chosen;
            public readonly List<SpineSetupItem> Skins = new List<SpineSetupItem>();

            public SpineSetupSlot(string id, string name, string chosen)
            {
                Id = id ?? "";
                Name = name ?? "";
                Chosen = chosen ?? "";
            }
        }
    }
}
