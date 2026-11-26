using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebornFrame.Spell.Spine
{
    /// <summary>皮肤名怎么分成可换装的组。</summary>
    public enum SpineSkinGrouping
    {
        /// <summary>分隔符的第一段是组，后面是选项。同组互斥，各组再合成。</summary>
        FirstSegment = 0,

        /// <summary>最后一段是选项，它前面的路径是组。</summary>
        ParentPath = 1,

        /// <summary>每个皮肤单独成组，可以同时穿上。</summary>
        EachSkin = 2,

        /// <summary>全部皮肤放在一组里，只能选一个。</summary>
        Single = 3,

        /// <summary>按 Manual 里写好的分组。皮肤名原样使用，不再切开。</summary>
        Manual = 4,
    }

    /// <summary>手动分组里的一组。Skins 里的皮肤互斥。</summary>
    public sealed class SpineSkinBucket
    {
        public string Label = "";
        public readonly List<string> Skins = new List<string>();
    }

    /// <summary>换装规则。加载时原样使用，不假设某份资源的目录名。</summary>
    public sealed class SpineSkinRule
    {
        /// <summary>切开皮肤名的分隔符。第一段、上级路径会用到它。空字符串时这两种分组改成单选。</summary>
        public string Separator = "/";

        public SpineSkinGrouping Grouping = SpineSkinGrouping.FirstSegment;

        /// <summary>打开时每组选中第一个皮肤。逐个穿上时通常关掉，避免一次叠完全部皮肤。</summary>
        public bool SelectFirst = true;

        /// <summary>跳过骨架的默认皮肤。默认皮肤仍然是找不到附件时的后备。</summary>
        public bool SkipDefault = true;

        public string EmptyLabel = "无";
        public string LooseLabel = "皮肤";

        /// <summary>Grouping 为 Manual 时使用。顺序就是预览里的行顺序。</summary>
        public readonly List<SpineSkinBucket> Manual = new List<SpineSkinBucket>();
    }

    /// <summary>一份骨架的加载参数。换装规则、缩放、着色器和起手动画都在这里。</summary>
    public sealed class SpineLoadSettings
    {
        public float Scale = 0.01f;
        public string ShaderName = "Spine/Skeleton";

        /// <summary>null 时按图集里的 pma 行决定。图集没写时按预乘处理。</summary>
        public bool? PremultipliedAlpha;

        public SpineSkinRule Skins = new SpineSkinRule();

        /// <summary>预览起手时优先匹配的动画名。先整名，再包含。</summary>
        public string[] PreferredAnimations = { "idle", "stand", "wait", "loop", "daiji" };
    }

    /// <summary>已经拆开的骨架、图集和贴图。各种打包方式先转成这个再交给加载方案。</summary>
    public sealed class SpineSkeletonBytes
    {
        public byte[] Skeleton = Array.Empty<byte>();
        public bool Binary = true;
        public string Atlas = "";
        public readonly List<SpineAtlasPage> Pages = new List<SpineAtlasPage>();
    }

    public sealed class SpineAtlasPage
    {
        public string Name = "";
        public byte[] Png = Array.Empty<byte>();
    }

    public sealed class SpineSkinOption
    {
        public string Label = "";
        public string SkinName = "";
    }

    public sealed class SpineSkinGroup
    {
        public string Label = "";
        public readonly List<SpineSkinOption> Options = new List<SpineSkinOption>();
    }

    /// <summary>一具已经创建出来的骨架物体。</summary>
    public interface ISpineSpellObject : IDisposable
    {
        GameObject GameObject { get; }
        IReadOnlyList<string> Animations { get; }
        IReadOnlyList<string> SkinNames { get; }
        string DefaultSkinName { get; }
        IReadOnlyList<SpineSkinGroup> SkinGroups { get; }
        int SelectionOf(int group);
        void Select(int group, int option);
        void Regroup(SpineSkinRule rule);

        /// <summary>按皮肤名合成当前外观。空名单会卸下皮肤，默认皮肤仍作后备。不重播动画。</summary>
        void Wear(IReadOnlyList<string> skinNames);

        /// <summary>换上 setup.txt。按其中的群组重穿外观；当前触发器对应的片段变了才重播。</summary>
        void LoadSetup(string setupText);

        /// <summary>换到这个群组，按该组重穿外观，再播放当前触发器。</summary>
        void SetGroup(string groupId);

        /// <summary>按触发器播放。片段由当前群组和 setup.txt 决定；这一组没有片段就停掉该轨道。</summary>
        void PlayTrigger(string triggerId, int trackIndex, bool loop);

        /// <summary>按动画名播放。</summary>
        void Play(string animationName, int trackIndex, bool loop);

        /// <summary>按动画列表里的序号播放。序号从 0 开始，越界则不播。</summary>
        void Play(int animationIndex, int trackIndex, bool loop);
        void Stop(int trackIndex);
        bool Measure(string fallbackAnimation, out float x, out float y, out float width, out float height);
    }

    /// <summary>按配置把骨架字节做成物体。工程安装了哪种 Spine 运行时，就注册哪种实现。</summary>
    public interface ISpineSpellObjectFactory
    {
        bool TryCreate(SpineSkeletonBytes source, SpineLoadSettings settings, string setup, out ISpineSpellObject created, out string error);
    }

    public static class SpineSpellLoading
    {
        public static ISpineSpellObjectFactory Factory { get; set; }

        public static bool TryCreate(SpineSkeletonBytes source, SpineLoadSettings settings, string setup, out ISpineSpellObject created, out string error)
        {
            created = null;
            if (source == null || source.Skeleton == null || source.Skeleton.Length == 0 || string.IsNullOrEmpty(source.Atlas) || source.Pages == null || source.Pages.Count == 0)
            {
                error = "这份 Spine 没有读出来。";
                return false;
            }

            if (Factory == null)
            {
                error = "还没有设置 Spine 加载方案。";
                return false;
            }

            return Factory.TryCreate(source, settings, setup, out created, out error);
        }
    }
}
