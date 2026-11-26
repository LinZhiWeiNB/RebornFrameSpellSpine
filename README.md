# 技能 Spine 轨道

`com.reborn.spell.spine` 负责技能表现轨道，以及按配置把一份骨架做成可换装的物体。主程序集不包含 Spine 运行时，也不引用它，所以工程里没有运行时时也能编译。游戏自行安装与导出数据匹配的 [spine-unity](https://github.com/EsotericSoftware/spine-runtimes)（含 `spine-csharp`）。需要插件直接创建物体时，再打开脚本定义 `REBORN_SPINE_UNITY`，可选程序集 `reborn.spell.spine.unity` 才会参与编译。

轨道种类是作者 0、本地编号 4，进场景时自动向技能包注册。`data_key` 为空时播放单位上已经绑定的骨架；有值时，开局预备把预先登记的字节交给加载入口，同一份数据只解析一次。

使用 Spine 运行时需要遵守 [Spine Runtimes License](https://esotericsoftware.com/spine-runtimes-license)。

## 按配置创建可换装物体

各种打包方式先转成 `SpineSkeletonBytes`：骨架字节、是否二进制、图集文本、每页 PNG。换装和加载参数放在 `SpineLoadSettings` 里，不写死某份资源的皮肤目录。

`SpineSkinRule` 决定皮肤怎么分组：

- `FirstSegment`：分隔符的第一段是组，后面是选项
- `ParentPath`：最后一段是选项，前面的路径是组
- `EachSkin`：每个皮肤单独成组
- `Single`：全部皮肤放在一组里，只能选一个

分隔符默认是 `/`。空分隔符时，前两种分组改成单选。同组互斥，各组合成一张皮肤。默认皮肤不进选项，只在找不到附件时兜底。`SelectFirst` 决定打开时是否穿上每组的第一个皮肤。

分组也可以不切开名字。把 `Grouping` 设为 `Manual`，在 `Manual` 里按顺序写入每一组的标题和皮肤全名。没写进去的皮肤不参与换装。

安装了 spine-unity 并定义 `REBORN_SPINE_UNITY` 后，`SpineUnityFactory` 会在启动时登记。之后：

```csharp
SpineLoadSettings settings = new SpineLoadSettings();
settings.Skins.Separator = "/";
settings.Skins.Grouping = SpineSkinGrouping.FirstSegment;

ISpineSpellObject view;
string error;
if (SpineSpellLoading.TryCreate(source, settings, out view, out error))
{
    view.Play(view.Animations[0], 0, true);
    view.Select(0, 2);
}
```

换一套加载方案时，实现 `ISpineSpellObjectFactory`，赋给 `SpineSpellLoading.Factory`。工厂按同一份 `SpineLoadSettings` 解释换装，不必改调用方。

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
