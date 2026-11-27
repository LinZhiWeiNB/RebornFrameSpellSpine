using System;
using System.Collections.Generic;
using Google.Protobuf;
using NUnit.Framework;
using Unity.Entities;

namespace RebornFrame.Spell.Spine.Tests
{
    public class SpineSpellTrackTests
    {
        private SpellStartup _startup;
        private World _world;

        [TearDown]
        public void TearDown()
        {
            _startup?.Dispose();
            if (_world is { IsCreated: true }) _world.Dispose();
            SpellPresentation.Reset();
            SpineSpellTracks.SetLoader(null);
        }

        [Test]
        public void Play_DrivesBoundSkeleton()
        {
            var skeleton = new RecordingSkeleton();
            SpineSpellTracks.Bind(1, skeleton);
            SpineSpellTracks.EnsureRegistered();
            SpellPresentation.SetFrameDuration(0.1f);

            _world = new World(nameof(Play_DrivesBoundSkeleton));
            _startup = new SpellStartup();
            var spell = SpellId.Combine(1, 1);
            var data = new SpellModuleData
            {
                Spells =
                {
                    new SpellData
                    {
                        Id = spell.Value,
                        Activation = (int)SpellActivation.Passive,
                        PresentationTracks =
                        {
                            new SpellPresentationTrack
                            {
                                TypeId = SpineSpellTracks.TypeId.Value,
                                Parameters = new SpineSpellTrackConfig
                                {
                                    AnimationName = "cast",
                                    Loop = true,
                                    TrackIndex = 2
                                }.ToByteString()
                            }
                        }
                    }
                }
            };
            _startup.AddModuleData(data);
            _startup.Start(_world);
            _startup.InitializeUnit(_world.EntityManager.CreateEntity(), 1);
            Assert.IsTrue(SpellAccess.TryGrant(_world.EntityManager, spell, 1));
            _startup.Tick(0);

            Assert.AreEqual("cast", skeleton.AnimationName);
            Assert.AreEqual(2, skeleton.TrackIndex);
            Assert.IsTrue(skeleton.Loop);
            Assert.AreEqual(0, skeleton.StopCount);

            Assert.IsTrue(SpellAccess.TryRevoke(_world.EntityManager, spell, 1));
            _startup.Tick(1);
            Assert.AreEqual(1, skeleton.StopCount);
        }

        [Test]
        public void Prepare_LoadsRegisteredBytesOnce()
        {
            var source = new byte[] { 1, 2, 3, 4 };
            var loader = new RecordingLoader();
            SpineSpellTracks.RegisterData("hero", source);
            source[0] = 9;
            SpineSpellTracks.SetLoader(loader);
            SpineSpellTracks.EnsureRegistered();
            SpellPresentation.SetFrameDuration(0.1f);

            _world = new World(nameof(Prepare_LoadsRegisteredBytesOnce));
            _startup = new SpellStartup();
            var spell = SpellId.Combine(1, 2);
            var data = new SpellModuleData
            {
                Spells =
                {
                    new SpellData
                    {
                        Id = spell.Value,
                        Activation = (int)SpellActivation.Passive,
                        PresentationTracks =
                        {
                            Track("cast", "hero"),
                            Track("idle", "hero")
                        }
                    }
                }
            };
            _startup.AddModuleData(data);
            _startup.Start(_world);
            _startup.InitializeUnit(_world.EntityManager.CreateEntity(), 1);
            Assert.IsTrue(SpellAccess.TryGrant(_world.EntityManager, spell, 1));
            _startup.Tick(0);

            Assert.AreEqual(1, loader.Calls);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, loader.Data);
            Assert.AreEqual(2, loader.Result.Created.Count);
            Assert.AreEqual("cast", loader.Result.Created[0].AnimationName);
            Assert.AreEqual("idle", loader.Result.Created[1].AnimationName);
        }

        [Test]
        public void Prepare_RejectsMissingData()
        {
            SpineSpellTracks.SetLoader(new RecordingLoader());
            SpineSpellTracks.EnsureRegistered();
            SpellPresentation.SetFrameDuration(0.1f);
            _world = new World(nameof(Prepare_RejectsMissingData));
            _startup = new SpellStartup();
            _startup.AddModuleData(Module(SpellId.Combine(1, 3), "missing"));

            var error = Assert.Throws<InvalidOperationException>(() => _startup.Start(_world));
            StringAssert.Contains("missing", error.Message);
        }

        private static SpellPresentationTrack Track(string animationName, string dataKey)
        {
            return new SpellPresentationTrack
            {
                TypeId = SpineSpellTracks.TypeId.Value,
                Parameters = new SpineSpellTrackConfig
                {
                    AnimationName = animationName,
                    DataKey = dataKey
                }.ToByteString()
            };
        }

        private static SpellModuleData Module(SpellId spell, string dataKey)
        {
            return new SpellModuleData
            {
                Spells =
                {
                    new SpellData
                    {
                        Id = spell.Value,
                        Activation = (int)SpellActivation.Passive,
                        PresentationTracks = { Track("cast", dataKey) }
                    }
                }
            };
        }

        private sealed class RecordingLoader : ISpineSpellLoader
        {
            public byte[] Data;
            public int Calls;
            public readonly RecordingData Result = new();

            public ISpineSpellData Load(byte[] data)
            {
                Calls++;
                Data = data;
                return Result;
            }
        }

        private sealed class RecordingData : ISpineSpellData
        {
            public readonly List<RecordingSkeleton> Created = new();

            public ISpineSpellSkeleton CreateSkeleton()
            {
                var skeleton = new RecordingSkeleton();
                Created.Add(skeleton);
                return skeleton;
            }
        }

        private sealed class RecordingSkeleton : ISpineSpellSkeleton
        {
            public string AnimationName;
            public float Time = -1f;
            public bool Loop;
            public bool Playing;
            public int StopCount;
            public int TrackIndex;

            public void Play(string animationName, int trackIndex, bool loop)
            {
                AnimationName = animationName;
                TrackIndex = trackIndex;
                Loop = loop;
            }

            public void Stop(int trackIndex)
            {
                TrackIndex = trackIndex;
                StopCount++;
            }

            public void Show(string animationName, int trackIndex, bool loop, float time, bool play)
            {
                AnimationName = animationName;
                TrackIndex = trackIndex;
                Loop = loop;
                Time = time;
                Playing = play;
            }
        }
    }
}
