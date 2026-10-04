[**English**](README.md) | [**日本語**](README.ja.md) | [**简体中文**](README.zh-CN.md) | [**한국어**](README.ko.md)

# Mochiya Avatar Tools

将 Unity 中的人物模型、服装和配饰分享到 [Mochiya](https://www.mochiya.org/zh-CN)，或导出为文件供兼容应用使用。通过 **Upload to Mochiya**，你可以将作品加入自己的 Avatar Studio 资源库，也可以上架到市场。原始场景对象不会被修改。

## 安装

需要 **Unity 2022.3 或更新版本**，并已安装 [Git](https://git-scm.com/downloads)。请先导入模型所需的依赖，例如 VRChat SDK Avatars、Modular Avatar 和 lilToon（2.3.4 或更新版本）。

打开 **Window > Package Manager > + > Add package from git URL**，将以下 URL **按顺序逐个添加**，每次等待安装完成后再添加下一个：

![Package Manager 中选中 Add package from git URL 的画面。](Documentation~/images/add-unity-package.webp)

1. UniGLTF：
   ```text
   https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2
   ```
2. UniVRM：
   ```text
   https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2
   ```
3. Mochiya Avatar Tools：
   ```text
   https://github.com/mochiya-labs/unity-avatar-tools.git
   ```

如果已安装兼容的 UniVRM/UniGLTF 0.131.2 或更新版本，可保留现有安装。Unity 编译完成后，会出现 **Mochiya** 菜单。[其他安装方式（英文）](Documentation~/installation.md)。

## 连接账号以上传到 Mochiya

**只有使用 Upload to Mochiya 上传时才需要 API 密钥。** 如果只想在电脑上转换或导出文件，请直接查看 [Mochiya Avatar Tools](#mochiya-avatar-tools)，无需账号或 API 密钥。

1. 登录 [Mochiya](https://www.mochiya.org/profile) 并打开个人主页；如果还没有账号，请先注册。
2. 展开个人主页底部的 **API 密钥**并复制密钥。Unity 工具通过此密钥将文件上传到你的 Mochiya 账号。
3. 在 Unity 中打开 **Mochiya > Upload to Mochiya**，将语言设为**简体中文**，把密钥粘贴到 **API 密钥**字段并点击**连接**。连接成功后再上传。请勿与他人分享密钥。

![个人主页展开 API 密钥后的画面，显示复制按钮。](Documentation~/images/api-key.png)

![Unity 的 Upload to Mochiya 面板，显示语言选择、已隐藏内容的 API 密钥字段和连接按钮。](Documentation~/images/connect-to-mochiya.webp)

## 上传到 Mochiya

1. 在**编辑模式**下，将模型预制体放入场景。依赖人物模型的服装或配饰，应在 Hierarchy 中直接放在该人物模型下。
2. 在 **Upload to Mochiya** 中，从 **Hierarchy** 将模型拖入**人物模型或附属物**字段。检查标题，选择分类和封面图，再选择**个人使用**或**在市场上架**。如有需要，可在**附加物品信息**中添加展示图片或买家下载文件。付费销售需要先在网站完成卖家设置。
3. 解决提示的错误后，点击**上传供个人使用**或**发布商品**。工具会自动导出模型。完成后点击**查看物品**进行确认；已有物品请在网站上编辑。

[文件限制、连接与上传恢复（英文）](Documentation~/upload.md) · [兼容性与限制（英文）](Documentation~/reference.md#supported-behavior-and-limits)

## Mochiya Avatar Tools

如果想将模型保存为文件、分享给他人，或在其他应用中使用，请打开 **Mochiya > Avatar Tools**。这个面板可以将模型保存为兼容应用支持的 VRM 或 GLB 文件。如果想在导出前检查或调整模型，也可以先在 Unity 场景中创建一份可编辑的 VRM 副本。

如果想保存动作或姿势，同一面板还可以将 Humanoid AnimationClip 导出为 **VRM Animation（.vrma）**。导出的动画可在 Mochiya Avatar Studio 和其他支持该格式的应用中用于兼容的人物模型。单帧剪辑保存的是一个姿势。

转换和导出都在你的电脑上完成，无需 Mochiya 账号或 API 密钥。如果只是想将模型上传到 Mochiya，直接使用 **Upload to Mochiya** 即可，它会自动完成模型导出。

[转换与动画导出指南（英文）](Documentation~/avatar-tools.md) · [完整文档（英文）](Documentation~/index.md) · [MIT 许可证](LICENSE)
