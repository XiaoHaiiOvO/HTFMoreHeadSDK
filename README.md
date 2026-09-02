# HTF MoreHead SDK

[中文](#中文教程) · [English](#english-guide)

Unity Editor SDK for creating single-file `.htfhhh` cosmetics for [HTF MoreHead](https://github.com/XiaoHaiiOvO/HTFMoreHead). It supports the game's three native appearance categories: Hat, Accessory, and Outfit.

## 中文教程

### 环境要求

- Unity `6000.4.4f1`
- Universal Render Pipeline `17.4.0`
- Windows Standalone 64-bit 内容包
- 已安装 [HTF MoreHead Core](https://github.com/XiaoHaiiOvO/HTFMoreHead)

### 通过 Git URL 安装

1. 在 Unity 打开 `Window > Package Management > Package Manager`。
2. 点击左上角 `+`。
3. 选择 `Install package from git URL...`。
4. 粘贴：

   ```text
   https://github.com/XiaoHaiiOvO/HTFMoreHeadSDK.git#v0.6.0
   ```

5. 安装完成后，在该包右侧的 `Samples` 区域导入 `MoreHead Authoring Reference`。
6. 打开导入到 `Assets/Samples/HTF MoreHead SDK/0.6.0/MoreHead Authoring Reference/` 的 `HTFMoreHead_Authoring.unity`。

如果 Git URL 安装失败，请先确认电脑已安装 Git，并确认 URL 可以在浏览器中访问。

### 制作装扮

1. 把模型、材质和贴图导入当前 Unity 工程。
2. 把模型拖入 `HTFMoreHead_Authoring` 场景。
3. 根据用途选择并校对到参考角色：

   | 分类 | Builder Category | 校对挂点 | 游戏内入口 |
   |---|---|---|---|
   | 帽子/发型/头部物品 | `Hat` | `Armature/Body/Head/HTFMoreHead_HeadAnchor` | 原版“帽子”页左右箭头 |
   | 眼镜/背包/身体配饰 | `Accessory` | 参考角色根节点下的 `Accessory` | 原版“配饰”页左右箭头 |
   | 刚性衣服/外套 | `Outfit` | 参考角色根节点下的 `Outfit` | 原版“外观/衣服”页左右箭头 |

4. 在 Scene 视图调整模型的位置、旋转和缩放。不要移动参考角色或挂点。
5. 打开 `Tools > HTF MoreHead > Open .htfhhh Builder`。
6. 选择与模型相同的 `Category`。
7. 点击 `Auto Find Correct Category Anchor`，或手动拖入上表对应挂点。
8. 将场景中的模型根对象拖入 `Cosmetic Object`。
9. 填写：

   - `Display Name`：游戏 UI 显示名，也会成为文件名。
   - `Author Name`：游戏 UI 显示的作者名，支持中文和其他 Unicode 文字。
   - `Output Folder`：必须位于 `Assets` 目录之外。

10. 点击 `Validate and Build .htfhhh`。

普通作者无需填写 Pack ID 或 Cosmetic ID。它们是联机和存档需要的稳定内部身份，由 Builder 自动生成；只有迁移旧项目或维护已发布物品时才展开 `Advanced Internal IDs`。

### 输出与安装

每次构建只输出一个文件：

```text
{DisplayName}.htfhhh
```

将它放到游戏的任意插件子目录，例如：

```text
How to Fish/BepInEx/plugins/HTFMoreHead/Content/你的装扮.htfhhh
```

Core 会递归扫描 `BepInEx/plugins/**/*.htfhhh`。进入角色自定义界面后，在对应的帽子、配饰或衣服页面继续使用原版左右箭头，即可从原版物品切换到自定义物品。

### 制作限制

- 当前只支持刚性静态模型：`MeshFilter + MeshRenderer`。
- 不支持 `SkinnedMeshRenderer` 蒙皮重定向。
- 不要包含脚本、Collider、Rigidbody、Joint、Camera 或 AudioListener。
- 建议每件装扮不超过 20,000 三角面、2 个材质、1024 贴图。
- 联机双方必须安装相同版本的 Core。内容包可不同；缺包的一方只是不显示对应装扮。
- 发布后不要修改 Advanced Internal IDs，否则旧存档和联机身份会把它视为新物品。

## English guide

### Requirements

- Unity `6000.4.4f1`
- Universal Render Pipeline `17.4.0`
- Windows Standalone 64-bit bundles
- [HTF MoreHead Core](https://github.com/XiaoHaiiOvO/HTFMoreHead)

### Install from a Git URL

1. Open `Window > Package Management > Package Manager` in Unity.
2. Click `+`, then choose `Install package from git URL...`.
3. Paste:

   ```text
   https://github.com/XiaoHaiiOvO/HTFMoreHeadSDK.git#v0.6.0
   ```

4. Import the `MoreHead Authoring Reference` sample from the package details.
5. Open `HTFMoreHead_Authoring.unity` from the imported Samples folder.

### Build a cosmetic

1. Import your model, materials, and textures.
2. Put the model in the authoring scene and align it against the clean player reference.
3. Use the matching category and anchor:

   | Category | Authoring anchor | Native selector |
   |---|---|---|
   | `Hat` | `Armature/Body/Head/HTFMoreHead_HeadAnchor` | Hat arrows |
   | `Accessory` | root `Accessory` child | Accessory arrows |
   | `Outfit` | root `Outfit` child | Outfit arrows |

4. Open `Tools > HTF MoreHead > Open .htfhhh Builder`.
5. Select the category, its anchor, and the scene cosmetic object.
6. Enter a display name, an author name, and an output folder outside `Assets`.
7. Click `Validate and Build .htfhhh`.

The builder automatically creates stable Pack/Cosmetic IDs and bakes the model's anchor-relative position, rotation, and scale into the package. One build produces exactly one `{DisplayName}.htfhhh` file.

Install that file anywhere below `BepInEx/plugins/`. In the game's character customization screen, use the original arrows on the matching Hat, Accessory, or Outfit tab.

### Current limits

- Rigid `MeshFilter + MeshRenderer` cosmetics only.
- No `SkinnedMeshRenderer` bone retargeting yet.
- Scripts, physics, Camera, and AudioListener components are rejected.
- Recommended budget: 20k triangles, 2 materials, 1024px textures.
- All players need the same Core protocol. Missing content packages are tolerated locally.

## Credits

HTF MoreHead was designed with permission from **Masaicker** and references the architecture of the R.E.P.O. mod [MoreHead](https://github.com/Masaicker/repo-MoreHead). Thank you to Masaicker for the authorization and open-source contribution.
