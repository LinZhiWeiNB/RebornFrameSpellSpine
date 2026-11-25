using System;
using System.Collections.Generic;
using UnityEngine;

namespace RebornFrame.Spell.Spine
{
    /// <summary>
    ///     技能包能驱动的 Spine 骨架。游戏用 Spine.Unity 的 SkeletonAnimation 实现它。
    /// </summary>
    public interface ISpineSpellSkeleton
    {
        /// <summary>在指定轨道上播放动画。</summary>
        /// <param name="animationName">动画名。</param>
        /// <param name="trackIndex">Spine 轨道编号。</param>
        /// <param name="loop">是否循环。</param>
        void Play(string animationName, int trackIndex, bool loop);

        /// <summary>清空指定轨道。</summary>
        /// <param name="trackIndex">Spine 轨道编号。</param>
        void Stop(int trackIndex);
    }

    /// <summary>Spine 动画轨道种类，作者 0、本地编号 4。</summary>
    public static class SpineSpellTracks
    {
        /// <summary>注册给技能包的轨道种类。</summary>
        public static readonly SpellPresentationTrackId TypeId = SpellPresentationTrackId.Combine(0, 4);

        private static readonly Dictionary<ulong, ISpineSpellSkeleton> Skeletons = new();
        private static readonly HashSet<string> Prepared = new();

        /// <summary>把单位绑定到已经适配好的 Spine 骨架。</summary>
        /// <param name="stableId">单位稳定 ID。</param>
        /// <param name="skeleton">骨架。null 表示解除绑定。</param>
        public static void Bind(ulong stableId, ISpineSpellSkeleton skeleton)
        {
            if (stableId == 0) throw new ArgumentException("稳定 ID 不能为 0。", nameof(stableId));

            if (skeleton == null) Skeletons.Remove(stableId);
            else Skeletons[stableId] = skeleton;
        }

        /// <summary>向技能包注册 Spine 轨道。重复调用没有效果。</summary>
        public static void EnsureRegistered()
        {
            if (SpellPresentation.IsRegistered(TypeId)) return;

            SpellPresentation.Register(TypeId, new Factory());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad()
        {
            EnsureRegistered();
        }

        private sealed class Factory : ISpellPresentationTrackFactory
        {
            public void Prepare(ReadOnlySpan<byte> parameters)
            {
                var config = SpineSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (string.IsNullOrEmpty(config.AnimationName))
                    throw new ArgumentException("Spine 轨道没有动画名。");

                Prepared.Add(config.AnimationName);
            }

            public ISpellPresentationTrack Create(ReadOnlySpan<byte> parameters, in SpellPresentationContext context)
            {
                var config = SpineSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (!Prepared.Contains(config.AnimationName))
                    throw new InvalidOperationException($"Spine 动画 {config.AnimationName} 还没有预备。");

                if (!Skeletons.TryGetValue(context.OwnerStableId, out var skeleton))
                    throw new InvalidOperationException($"单位 {context.OwnerStableId} 没有绑定 Spine 骨架。");

                return new Track(skeleton, config.AnimationName, config.TrackIndex, config.Loop);
            }
        }

        private sealed class Track : ISpellPresentationTrack
        {
            private readonly string _animationName;
            private readonly bool _loop;
            private readonly ISpineSpellSkeleton _skeleton;
            private readonly int _trackIndex;

            public Track(ISpineSpellSkeleton skeleton, string animationName, int trackIndex, bool loop)
            {
                _skeleton = skeleton;
                _animationName = animationName;
                _trackIndex = trackIndex;
                _loop = loop;
            }

            public void Play()
            {
                _skeleton.Play(_animationName, _trackIndex, _loop);
            }

            public void Stop(SpellEndReason reason)
            {
                _skeleton.Stop(_trackIndex);
            }
        }
    }
}
