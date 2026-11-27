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

        /// <summary>
        ///     把动画摆到片段内第 <paramref name="frame" /> 帧。
        ///     <paramref name="play" /> 为 false 时停在这一帧，为 true 时从这一帧继续播放。
        ///     这一帧的时间是 <paramref name="frame" /> 乘以 <see cref="SpellPresentation.FrameDuration" />。
        /// </summary>
        /// <param name="animationName">动画名。</param>
        /// <param name="trackIndex">Spine 轨道编号。</param>
        /// <param name="loop">是否循环。</param>
        /// <param name="frame">相对片段起点的帧数。</param>
        /// <param name="play">摆完后是否继续播放。</param>
        void Show(string animationName, int trackIndex, bool loop, int frame, bool play);
    }

    /// <summary>一份已经解析的 Spine 数据，可以反复创建骨架。</summary>
    public interface ISpineSpellData
    {
        /// <summary>创建一具新的可播放骨架。</summary>
        /// <returns>骨架。不能为 null。</returns>
        ISpineSpellSkeleton CreateSkeleton();
    }

    /// <summary>
    ///     Spine 数据的加载入口。游戏用自己的 Spine 运行时实现它。
    ///     入参是一整份数据的字节，由实现决定如何解析。
    /// </summary>
    public interface ISpineSpellLoader
    {
        /// <summary>解析一份 Spine 数据。</summary>
        /// <param name="data">数据字节。不要修改数组内容；需要自行保存时先复制。</param>
        /// <returns>可反复创建骨架的数据。</returns>
        ISpineSpellData Load(byte[] data);
    }

    /// <summary>Spine 动画轨道种类，作者 0、本地编号 4。</summary>
    public static class SpineSpellTracks
    {
        /// <summary>注册给技能包的轨道种类。</summary>
        public static readonly SpellPresentationTrackId TypeId = SpellPresentationTrackId.Combine(0, 4);

        private static readonly Dictionary<string, byte[]> Data = new();
        private static readonly Dictionary<string, ISpineSpellData> Loaded = new();
        private static readonly Dictionary<ulong, ISpineSpellSkeleton> Skeletons = new();
        private static readonly HashSet<string> Prepared = new();
        private static ISpineSpellLoader _loader;

        /// <summary>把单位绑定到已经适配好的 Spine 骨架。</summary>
        /// <param name="stableId">单位稳定 ID。</param>
        /// <param name="skeleton">骨架。null 表示解除绑定。</param>
        public static void Bind(ulong stableId, ISpineSpellSkeleton skeleton)
        {
            if (stableId == 0) throw new ArgumentException("稳定 ID 不能为 0。", nameof(stableId));

            if (skeleton == null) Skeletons.Remove(stableId);
            else Skeletons[stableId] = skeleton;
        }

        /// <summary>设置加载入口。更换或清空后，已解析的数据会在下次预备时重新加载。</summary>
        /// <param name="loader">加载入口。null 表示不再根据配置加载数据。</param>
        public static void SetLoader(ISpineSpellLoader loader)
        {
            _loader = loader;
            Loaded.Clear();
        }

        /// <summary>
        ///     按配置里的 data_key 登记一份 Spine 数据。预备时把这份字节交给加载入口。
        /// </summary>
        /// <param name="key">与轨道配置 data_key 相同的键。</param>
        /// <param name="data">Spine 数据字节。</param>
        public static void RegisterData(string key, byte[] data)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Spine 数据键不能为空。", nameof(key));

            if (data == null || data.Length == 0) throw new ArgumentException("Spine 数据不能为空。", nameof(data));

            var copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            Data[key] = copy;
            Loaded.Remove(key);
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
                if (string.IsNullOrEmpty(config.DataKey)) return;

                LoadData(config.DataKey);
            }

            public ISpellPresentationTrack Create(ReadOnlySpan<byte> parameters, in SpellPresentationContext context)
            {
                var config = SpineSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (!Prepared.Contains(config.AnimationName))
                    throw new InvalidOperationException($"Spine 动画 {config.AnimationName} 还没有预备。");

                if (!string.IsNullOrEmpty(config.DataKey))
                {
                    if (!Loaded.TryGetValue(config.DataKey, out var data))
                        throw new InvalidOperationException($"Spine 数据 {config.DataKey} 还没有加载。");

                    var created = data.CreateSkeleton();
                    if (created == null)
                        throw new InvalidOperationException($"Spine 数据 {config.DataKey} 没有创建出骨架。");

                    return new Track(created, config.AnimationName, config.TrackIndex, config.Loop);
                }

                if (!Skeletons.TryGetValue(context.OwnerStableId, out var skeleton))
                    throw new InvalidOperationException($"单位 {context.OwnerStableId} 没有绑定 Spine 骨架。");

                return new Track(skeleton, config.AnimationName, config.TrackIndex, config.Loop);
            }
        }

        private static void LoadData(string key)
        {
            if (Loaded.ContainsKey(key)) return;

            if (!Data.TryGetValue(key, out var bytes))
                throw new InvalidOperationException($"Spine 数据 {key} 还没有登记。");

            if (_loader == null) throw new InvalidOperationException("还没有设置 Spine 数据加载入口。");

            var loaded = _loader.Load(bytes);
            if (loaded == null) throw new InvalidOperationException($"Spine 数据 {key} 加载结果为空。");

            Loaded[key] = loaded;
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

            public void Show(int frame, bool play)
            {
                _skeleton.Show(_animationName, _trackIndex, _loop, frame, play);
            }
        }
    }
}
