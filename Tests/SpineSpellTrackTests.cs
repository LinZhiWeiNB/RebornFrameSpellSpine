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

        private sealed class RecordingSkeleton : ISpineSpellSkeleton
        {
            public string AnimationName;
            public bool Loop;
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
        }
    }
}
