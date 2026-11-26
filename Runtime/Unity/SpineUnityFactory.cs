using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace RebornFrame.Spell.Spine
{
    /// <summary>
    /// spine-unity 的加载方案。工程定义了 REBORN_SPINE_UNITY 并且安装了 spine-unity 时自动登记。
    /// 换装、缩放和着色器都读 <see cref="SpineLoadSettings"/>。
    /// </summary>
    public sealed class SpineUnityFactory : ISpineSpellObjectFactory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            if (SpineSpellLoading.Factory == null)
                SpineSpellLoading.Factory = new SpineUnityFactory();
        }

        public bool TryCreate(SpineSkeletonBytes source, SpineLoadSettings settings, string setup, out ISpineSpellObject created, out string error)
        {
            created = null;
            var view = new SpineUnityObject();
            try
            {
                error = view.Build(source, settings, setup);
                if (error != null)
                {
                    view.Dispose();
                    return false;
                }

                created = view;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                view.Dispose();
                error = ex.Message != null && ex.Message.IndexOf("atlas page", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "图集里的贴图和文件对不上。"
                    : "骨骼没有读出来。";
                return false;
            }
        }

        sealed class SpineUnityObject : ISpineSpellObject
        {
            readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
            readonly List<string> _animations = new List<string>();
            readonly List<string> _skinNames = new List<string>();
            readonly List<int> _selected = new List<int>();
            List<SpineSkinGroup> _groups = new List<SpineSkinGroup>();
            SkeletonAnimation _animation;
            SpineSkinRule _rule = new SpineSkinRule();
            SpineSetup _setup = new SpineSetup();
            readonly List<string> _worn = new List<string>();
            string _defaultSkin = "";
            string _groupId = "";
            string _triggerId = "";
            string _playing = "";
            int _track;
            bool _loop = true;
            bool _disposed;

            public GameObject GameObject => _animation == null ? null : _animation.gameObject;
            public IReadOnlyList<string> Animations => _animations;
            public IReadOnlyList<string> SkinNames => _skinNames;
            public string DefaultSkinName => _defaultSkin;
            public IReadOnlyList<SpineSkinGroup> SkinGroups => _groups;

            public string Build(SpineSkeletonBytes source, SpineLoadSettings settings, string setup)
            {
                settings = settings ?? new SpineLoadSettings();
                _rule = settings.Skins ?? new SpineSkinRule();
                var scale = settings.Scale > 0f ? settings.Scale : 0.01f;
                var shaderName = string.IsNullOrEmpty(settings.ShaderName) ? "Spine/Skeleton" : settings.ShaderName;
                var shader = Shader.Find(shaderName);
                if (shader == null) return "找不到 Spine 着色器。";

                var premultiplied = settings.PremultipliedAlpha ?? Premultiplied(source.Atlas);
                var textures = new Texture2D[source.Pages.Count];
                for (var i = 0; i < source.Pages.Count; i++)
                {
                    var page = source.Pages[i];
                    var texture = Keep(new Texture2D(2, 2, TextureFormat.RGBA32, false, premultiplied));
                    if (page == null || page.Png == null || !texture.LoadImage(page.Png))
                        return "贴图 " + (page == null ? "" : page.Name) + " 没有读出来。";

                    texture.name = Path.GetFileNameWithoutExtension(page.Name ?? "");
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    textures[i] = texture;
                }

                var atlasText = Keep(new TextAsset(source.Atlas ?? ""));
                atlasText.name = "preview.atlas";
                var materials = new Material[textures.Length];
                for (var i = 0; i < textures.Length; i++)
                {
                    var material = Keep(new Material(shader));
                    material.mainTexture = textures[i];
                    material.name = textures[i].name;
                    MaterialChecks.EnablePMATextureAtMaterial(material, premultiplied);
                    materials[i] = material;
                }

                var atlasAsset = Keep(SpineAtlasAsset.CreateRuntimeInstance(atlasText, materials, true));
                var atlas = atlasAsset.GetAtlas();
                if (atlas == null) return "图集没有读出来。";

                SkeletonData data;
                if (source.Binary)
                {
                    using (var stream = new MemoryStream(source.Skeleton, false))
                        data = new SkeletonBinary(atlas) { Scale = scale }.ReadSkeletonData(stream);
                }
                else
                {
                    using (var reader = new StringReader(TextOf(source.Skeleton)))
                        data = new SkeletonJson(atlas) { Scale = scale }.ReadSkeletonData(reader);
                }

                if (data == null) return "骨骼没有读出来。";

                var dataAsset = Keep(ScriptableObject.CreateInstance<SkeletonDataAsset>());
                dataAsset.atlasAssets = new AtlasAssetBase[] { atlasAsset };
                dataAsset.scale = scale;
                var marker = Keep(new TextAsset(source.Binary ? "skel" : TextOf(source.Skeleton)));
                marker.name = source.Binary ? "preview.skel" : "preview.json";
                dataAsset.skeletonJSON = marker;
                var method = typeof(SkeletonDataAsset).GetMethod("InitializeWithData", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null) return "骨骼没有读出来。";
                method.Invoke(dataAsset, new object[] { data });

                var parts = SkeletonAnimation.NewSkeletonAnimationGameObject(dataAsset);
                _animation = parts.skeletonAnimation;
                _animation.gameObject.name = "SpinePreview";
                _animation.gameObject.hideFlags = HideFlags.HideAndDontSave;
                ReadNames(data);
                RebuildGroups();
                LoadSetup(setup);
                return null;
            }

            public int SelectionOf(int group)
            {
                if (group < 0 || group >= _selected.Count) return 0;
                return _selected[group];
            }

            public void Select(int group, int option)
            {
                if (_disposed || group < 0 || group >= _groups.Count) return;
                if (option < 0 || option >= _groups[group].Options.Count) return;
                _selected[group] = option;
                ApplyEquipment();
            }

            public void Regroup(SpineSkinRule rule)
            {
                if (_disposed) return;
                _rule = rule ?? new SpineSkinRule();
                RebuildGroups();
                ApplyEquipment();
            }

            public void Wear(IReadOnlyList<string> skinNames)
            {
                if (_disposed || _animation == null || _animation.Skeleton == null) return;
                var skeleton = _animation.Skeleton;
                var data = skeleton.Data;
                Skin combined = null;
                if (skinNames != null && data != null)
                {
                    for (var i = 0; i < skinNames.Count; i++)
                    {
                        var name = skinNames[i];
                        if (string.IsNullOrEmpty(name)) continue;
                        if (!string.IsNullOrEmpty(_defaultSkin) && string.Equals(name, _defaultSkin, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var skin = data.FindSkin(name);
                        if (skin == null) continue;
                        if (combined == null) combined = new Skin("preview");
                        combined.AddSkin(skin);
                    }
                }

                skeleton.SetSkin(combined);
                skeleton.SetupPoseSlots();
                var state = _animation.AnimationState;
                if (state != null) state.Apply(skeleton);
                skeleton.UpdateWorldTransform(global::Spine.Physics.Update);
            }

            public void LoadSetup(string setupText)
            {
                if (_disposed) return;
                var next = SpineSetup.Parse(setupText);
                var groupChanged = !string.Equals(next.GroupId, _groupId, StringComparison.Ordinal);
                _setup = next;
                _groupId = next.GroupId;
                _triggerId = next.TriggerId;
                WearResolved();
                PlayResolved(groupChanged);
            }

            public void SetGroup(string groupId)
            {
                if (_disposed || _setup == null || !_setup.HasGroup(groupId)) return;
                _groupId = groupId;
                WearResolved();
                PlayResolved(true);
            }

            public void PlayTrigger(string triggerId, int trackIndex, bool loop)
            {
                if (_disposed) return;
                _triggerId = triggerId ?? "";
                _track = trackIndex;
                _loop = loop;
                PlayResolved(true);
            }

            public void Play(string animationName, int trackIndex, bool loop)
            {
                if (_disposed || _animation == null || string.IsNullOrEmpty(animationName)) return;
                var state = _animation.AnimationState;
                if (state == null) return;
                state.SetAnimation(trackIndex, animationName, loop);
                _animation.Update(0f);
                _playing = animationName;
                _track = trackIndex;
                _loop = loop;
            }

            public void Play(int animationIndex, int trackIndex, bool loop)
            {
                if (animationIndex < 0 || animationIndex >= _animations.Count) return;
                Play(_animations[animationIndex], trackIndex, loop);
            }

            public void Stop(int trackIndex)
            {
                if (_disposed || _animation == null || _animation.AnimationState == null) return;
                _animation.AnimationState.ClearTrack(trackIndex);
                if (trackIndex == _track) _playing = "";
            }

            public bool Measure(string fallbackAnimation, out float x, out float y, out float width, out float height)
            {
                x = 0f;
                y = 0f;
                width = 1f;
                height = 1f;
                var skeleton = _animation == null ? null : _animation.Skeleton;
                if (skeleton == null) return false;

                if (ReadBounds(skeleton, out x, out y, out width, out height)) return true;
                if (!string.IsNullOrEmpty(fallbackAnimation))
                {
                    Play(fallbackAnimation, 0, true);
                    if (ReadBounds(skeleton, out x, out y, out width, out height)) return true;
                }

                var data = skeleton.Data;
                if (data != null)
                {
                    x = data.X;
                    y = data.Y;
                    width = data.Width;
                    height = data.Height;
                }

                if (width < 0.001f || height < 0.001f)
                {
                    width = 1f;
                    height = 1f;
                }

                return true;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_animation != null)
                    UnityEngine.Object.Destroy(_animation.gameObject);
                _animation = null;
                for (var i = 0; i < _owned.Count; i++)
                {
                    if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
                }

                _owned.Clear();
            }

            void ReadNames(SkeletonData data)
            {
                _animations.Clear();
                _skinNames.Clear();
                _defaultSkin = data.DefaultSkin == null ? "" : data.DefaultSkin.Name ?? "";
                var animations = data.Animations;
                if (animations != null)
                {
                    for (var i = 0; i < animations.Count; i++)
                    {
                        var name = animations.Items[i] == null ? null : animations.Items[i].Name;
                        if (!string.IsNullOrEmpty(name)) _animations.Add(name);
                    }
                }

                var skins = data.Skins;
                if (skins == null) return;
                for (var i = 0; i < skins.Count; i++)
                {
                    var skin = skins.Items[i];
                    if (skin != null && !string.IsNullOrEmpty(skin.Name)) _skinNames.Add(skin.Name);
                }
            }

            void WearResolved()
            {
                _setup.CollectSkins(_groupId, _defaultSkin, _worn);
                Wear(_worn);
            }

            void PlayResolved(bool force)
            {
                var clip = _setup.Clip(_triggerId, _groupId);
                if (string.IsNullOrEmpty(clip))
                {
                    Stop(_track);
                    return;
                }

                if (!force && string.Equals(clip, _playing, StringComparison.Ordinal)) return;
                Play(clip, _track, _loop);
            }

            void RebuildGroups()
            {
                _groups = SpineSkins.Group(_skinNames, _defaultSkin, _rule);
                _selected.Clear();
                for (var i = 0; i < _groups.Count; i++)
                {
                    var pick = _rule.SelectFirst && _groups[i].Options.Count > 1 ? 1 : 0;
                    _selected.Add(pick);
                }
            }

            void ApplyEquipment()
            {
                if (_animation == null || _animation.Skeleton == null) return;
                var skeleton = _animation.Skeleton;
                var data = skeleton.Data;
                Skin combined = null;
                for (var i = 0; i < _groups.Count; i++)
                {
                    var option = _groups[i].Options[_selected[i]];
                    if (option == null || string.IsNullOrEmpty(option.SkinName) || data == null) continue;
                    var skin = data.FindSkin(option.SkinName);
                    if (skin == null) continue;
                    if (combined == null) combined = new Skin("preview");
                    combined.AddSkin(skin);
                }

                skeleton.SetSkin(combined);
                skeleton.SetupPoseSlots();
                var state = _animation.AnimationState;
                if (state != null) state.Apply(skeleton);
                skeleton.UpdateWorldTransform(global::Spine.Physics.Update);
            }

            static bool ReadBounds(Skeleton skeleton, out float x, out float y, out float width, out float height)
            {
                float[] buffer = null;
                skeleton.GetBounds(out x, out y, out width, out height, ref buffer);
                return width > 0.001f && height > 0.001f;
            }

            static bool Premultiplied(string atlas)
            {
                if (string.IsNullOrEmpty(atlas)) return true;
                var lines = atlas.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (!line.StartsWith("pma:", StringComparison.OrdinalIgnoreCase)) continue;
                    return line.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                return true;
            }

            static string TextOf(byte[] bytes)
            {
                var text = Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>());
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
                return text;
            }

            T Keep<T>(T obj) where T : UnityEngine.Object
            {
                if (obj != null)
                {
                    obj.hideFlags = HideFlags.HideAndDontSave;
                    _owned.Add(obj);
                }

                return obj;
            }
        }
    }
}
