# 技能 Spine 轨道

`com.reborn.spell.spine` 只负责技能表现轨道，不包含 Spine 运行时。游戏自行安装与导出数据匹配的 [spine-unity](https://github.com/EsotericSoftware/spine-runtimes)（含 `spine-csharp`）。本包的程序集不引用它，所以工程里没有运行时时也能编译。

轨道种类是作者 0、本地编号 4，进场景时自动向技能包注册。`data_key` 为空时播放单位上已经绑定的骨架；有值时，开局预备把预先登记的字节交给加载入口，同一份数据只解析一次。

使用 Spine 运行时需要遵守 [Spine Runtimes License](https://esotericsoftware.com/spine-runtimes-license)。

## 播放已有骨架

单位上已经有 `SkeletonAnimation` 时，适配成 `ISpineSpellSkeleton`，按单位稳定 ID 绑定。轨道配置不要填 `data_key`。

```csharp
using Spine.Unity;

public sealed class BoundSpineSkeleton : ISpineSpellSkeleton
{
    private readonly SkeletonAnimation _animation;

    public BoundSpineSkeleton(SkeletonAnimation animation)
    {
        _animation = animation;
    }

    public void Play(string animationName, int trackIndex, bool loop)
    {
        _animation.AnimationState.SetAnimation(trackIndex, animationName, loop);
    }

    public void Stop(int trackIndex)
    {
        _animation.AnimationState.ClearTrack(trackIndex);
    }
}
```

在 `SpellStartup.Start` 之前：

```csharp
SpineSpellTracks.Bind(unitStableId, new BoundSpineSkeleton(skeletonAnimation));
```

## 按配置从字节加载

轨道配置写上 `data_key`。游戏在 `SpellStartup.Start` 之前，用同一个键登记字节并设置 `ISpineSpellLoader`。字节里同时放骨架和贴图，具体怎么切分由游戏规定。

`Load` 里用 `SkeletonBinary.ReadSkeletonData` 读骨架字节，用 `Atlas` 读图集文本和贴图。`CreateSkeleton` 再按当前 spine-unity 的做法，把得到的 `SkeletonData` 放进 `SkeletonDataAsset`，创建 `SkeletonAnimation`，并包成上面的 `BoundSpineSkeleton`。

```csharp
using System.IO;
using Spine;

public sealed class BytesSpineLoader : ISpineSpellLoader
{
    public ISpineSpellData Load(byte[] data)
    {
        using var atlasText = new StringReader(TakeAtlasText(data));
        var atlas = new Atlas(atlasText, "", TakeTextureLoader(data));
        var binary = new SkeletonBinary(new AtlasAttachmentLoader(atlas));
        using var input = new MemoryStream(data, 0, data.Length, false);
        var skeletonData = binary.ReadSkeletonData(input);
        return new BytesSpineData(skeletonData);
    }

    private static string TakeAtlasText(byte[] data)
    {
        // 按游戏自己的字节布局取出图集文本。
        return "";
    }

    private static TextureLoader TakeTextureLoader(byte[] data)
    {
        // 按游戏自己的字节布局把贴图交给 Spine。
        return null;
    }
}
```

`ReadAtlas` 里的 `atlasText` 和 `textureLoader` 来自游戏自己的字节布局。`spine-unity` 创建组件时使用 `SkeletonDataAsset.CreateRuntimeInstance`，它要一份 `TextAsset` 和图集资源；二进制骨架继续走上面的 `SkeletonBinary`。

登记入口：

```csharp
SpineSpellTracks.RegisterData("hero", spineBytes);
SpineSpellTracks.SetLoader(new BytesSpineLoader());
```

轨道参数：

```text
animation_name: "cast"
loop: false
track_index: 0
data_key: "hero"
```

没有登记字节，或还没设置加载入口，预备会失败。
