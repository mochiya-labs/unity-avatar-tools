using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Mochiya.AvatarTools.Editor
{
    // Native Editor guidance. Marketplace field labels remain owned by UploadContract.json.
    internal static class MochiyaPanelText
    {
        internal static readonly string[] Locales = { "en", "ja", "zh-CN", "ko" };
        private static readonly string[] Names = { "English", "日本語", "简体中文", "한국어" };

        internal static string LanguageField(string locale) => Locales[EditorGUILayout.Popup(
            Get(locale, "Language"), Math.Max(0, Array.IndexOf(Locales, locale)), Names)];

        internal static string Get(string locale, string text)
        {
            if (text == null) return "";
            int language = Array.IndexOf(Locales, locale);
            if (language <= 0) return text;
            if (Translations.TryGetValue(text, out var values)) return values[language - 1];
            if (text.Contains("\n")) return string.Join("\n", text.Split('\n').Select(line => Get(locale, line.TrimEnd('\r'))));
            // Keep detailed diagnostic text from Unity and other packages intact.
            return text;
        }

        internal const string ToolsIntro = "Prepare avatars, outfits and accessories for Mochiya and compatible apps. Convert or export models, or export animation clips.";
        internal const string UploadIntro = "Upload an avatar, outfit or accessory from your Unity scene to Mochiya for personal use in Avatar Studio or listing on the marketplace.";
        internal const string AnimationIntro = "Export a Humanoid AnimationClip as VRM Animation (.vrma) for avatar playback in Mochiya Avatar Studio and compatible apps. A single-frame clip stores a pose.";

        private static readonly Dictionary<string, string[]> Translations = new Dictionary<string, string[]>
        {
            { "Language", new[] { "言語", "语言", "언어" } },
            { ToolsIntro, new[] {
                "アバター・衣装・アクセサリーをMochiyaや対応アプリ向けに準備します。モデルの変換・エクスポートや、アニメーションクリップのエクスポートができます。",
                "为 Mochiya 和兼容应用准备人物模型、服装与配饰。转换或导出模型，也可以导出动画剪辑。",
                "Mochiya와 호환 앱에서 사용할 아바타, 의상, 액세서리를 준비합니다. 모델을 변환하거나 내보내고, 애니메이션 클립도 내보낼 수 있습니다." } },
            { UploadIntro, new[] {
                "Unityシーンのアバター・衣装・アクセサリーをMochiyaにアップロードし、Avatar Studioで個人利用したり、マーケットプレイスに出品したりできます。",
                "将 Unity 场景中的人物模型、服装或配饰上传到 Mochiya，供自己在 Avatar Studio 中使用，或上架到市场。",
                "Unity 씬의 아바타, 의상 또는 액세서리를 Mochiya에 업로드하여 Avatar Studio에서 개인적으로 사용하거나 마켓플레이스에 등록합니다." } },
            { AnimationIntro, new[] {
                "HumanoidのAnimationClipをVRM Animation（.vrma）としてエクスポートし、Mochiya Avatar Studioや対応アプリでアバターの動きを再生できます。1フレームのクリップはポーズを保存します。",
                "将 Humanoid AnimationClip 导出为 VRM Animation（.vrma），用于在 Mochiya Avatar Studio 和兼容应用中播放人物模型动画。单帧剪辑保存的是一个姿势。",
                "Humanoid AnimationClip을 VRM Animation(.vrma)으로 내보내 Mochiya Avatar Studio와 호환 앱에서 아바타의 동작을 재생할 수 있습니다. 단일 프레임 클립은 포즈를 저장합니다." } },
            { "Convert or export your model", new[] { "モデルの変換・エクスポート", "转换或导出模型", "모델 변환 및 내보내기" } },
            { "Choose an avatar, or an attachment placed directly under its avatar.", new[] { "アバター、またはアバターの直下に配置した衣装・アクセサリーを選択してください。", "选择人物模型，或在 Hierarchy 中直接放在该人物模型下的服装、配饰等附属物。", "아바타 또는 아바타 바로 아래에 배치한 의상·액세서리를 선택하세요." } },
            { "Avatar or attachment", new[] { "アバター／衣装・アクセサリー", "人物模型或附属物", "아바타 또는 부착물" } },
            { "Detected asset", new[] { "検出された種類", "检测到的类型", "감지된 유형" } },
            { "Avatar", new[] { "アバター", "人物模型", "아바타" } },
            { "Attachment", new[] { "衣装・アクセサリー", "附属物", "부착물" } },
            { "Neither", new[] { "該当なし", "未识别", "해당 없음" } },
            { "Reference avatar", new[] { "参照アバター", "参考人物模型", "참조 아바타" } },
            { "Compatibility warnings (optional)", new[] { "互換性の警告（任意）", "兼容性警告（可选）", "호환성 경고(선택 사항)" } },
            { "No compatibility warnings reported.", new[] { "互換性の警告はありません。", "没有兼容性警告。", "호환성 경고가 없습니다." } },
            { "Export settings (optional)", new[] { "エクスポート設定（任意）", "导出设置（可选）", "내보내기 설정(선택 사항)" } },
            { "Convert to VRM GameObject", new[] { "VRM GameObjectに変換", "转换为 VRM GameObject", "VRM GameObject로 변환" } },
            { "Export VRM / GLB…", new[] { "VRM / GLBをエクスポート…", "导出 VRM / GLB…", "VRM / GLB 내보내기…" } },
            { "Export VRM…", new[] { "VRMをエクスポート…", "导出 VRM…", "VRM 내보내기…" } },
            { "Export GLB…", new[] { "GLBをエクスポート…", "导出 GLB…", "GLB 내보내기…" } },
            { "Export animation", new[] { "アニメーションのエクスポート", "导出动画", "애니메이션 내보내기" } },
            { "Animation Clip", new[] { "アニメーションクリップ", "动画剪辑", "애니메이션 클립" } },
            { "Choose a Humanoid clip. Generic and Legacy clips are not supported.", new[] { "Humanoidのクリップを選択してください。GenericとLegacyのクリップは対応していません。", "请选择 Humanoid 剪辑。不支持 Generic 和 Legacy 剪辑。", "Humanoid 클립을 선택하세요. Generic 및 Legacy 클립은 지원하지 않습니다." } },
            { "Export VRM Animation (.vrma)…", new[] { "VRM Animation（.vrma）をエクスポート…", "导出 VRM Animation（.vrma）…", "VRM Animation(.vrma) 내보내기…" } },
            { "Export VRM Animation", new[] { "VRM Animationのエクスポート", "导出 VRM Animation", "VRM Animation 내보내기" } },
            { "Sampling {0}…", new[] { "{0}をサンプリング中…", "正在采样 {0}…", "{0} 샘플링 중…" } },
            { "Exported {0}", new[] { "{0}をエクスポートしました", "已导出 {0}", "{0} 내보내기 완료" } },
            { "Animation export cancelled.", new[] { "アニメーションのエクスポートをキャンセルしました。", "已取消动画导出。", "애니메이션 내보내기를 취소했습니다." } },
            { "Created {0} in the Hierarchy.", new[] { "Hierarchyに{0}を作成しました。", "已在 Hierarchy 中创建 {0}。", "Hierarchy에 {0}을(를) 생성했습니다." } },
            { "Mochiya Export", new[] { "Mochiyaエクスポート", "Mochiya 导出", "Mochiya 내보내기" } },
            { "Exporting {0}…", new[] { "{0}をエクスポート中…", "正在导出 {0}…", "{0} 내보내는 중…" } },
            { "Export {0}", new[] { "{0}のエクスポート", "导出 {0}", "{0} 내보내기" } },
            { "Export profile", new[] { "エクスポートプロファイル", "导出配置", "내보내기 프로필" } },
            { "Inspect profile", new[] { "プロファイルを表示", "查看配置", "프로필 보기" } },
            { "New profile…", new[] { "新規プロファイル…", "新建配置…", "새 프로필…" } },
            { "Create export profile", new[] { "エクスポートプロファイルを作成", "创建导出配置", "내보내기 프로필 만들기" } },
            { "Save reusable export settings.", new[] { "再利用できるエクスポート設定を保存します。", "保存可重复使用的导出设置。", "재사용할 내보내기 설정을 저장합니다." } },
            { "Choose an avatar or attachment from the Hierarchy.", new[] { "Hierarchyからアバターまたは衣装・アクセサリーを選択してください。", "请从 Hierarchy 中选择人物模型或附属物。", "Hierarchy에서 아바타 또는 부착물을 선택하세요." } },
            { "Place the prefab in a scene, then select its root in the Hierarchy.", new[] { "プレハブをシーンに配置し、Hierarchyでルートを選択してください。", "请将预制体放入场景，然后在 Hierarchy 中选择其根对象。", "프리팹을 씬에 배치한 다음 Hierarchy에서 루트 오브젝트를 선택하세요." } },
            { "This object has no avatar or attachment meshes. Select the model's root.", new[] { "アバターや衣装のメッシュがありません。モデルのルートを選択してください。", "此对象没有人物模型或附属物网格。请选择模型的根对象。", "아바타 또는 부착물 메시가 없습니다. 모델의 루트 오브젝트를 선택하세요." } },
            { "Previously converted asset; its saved kind is preserved.", new[] { "変換済みのアセットです。保存された種類を維持します。", "已转换的资源；保留已保存的类型。", "이미 변환된 에셋입니다. 저장된 유형을 유지합니다." } },
            { "Has its own VRM humanoid and no MA dependency outside this asset.", new[] { "独立したVRM Humanoidを持ち、アセット外へのMA依存がありません。", "拥有自己的 VRM Humanoid，且没有指向此资源外部的 MA 依赖。", "자체 VRM Humanoid가 있으며 에셋 외부에 대한 MA 의존성이 없습니다." } },
            { "Needs its direct parent's humanoid for VRM export.", new[] { "VRMエクスポートには直上の親のHumanoidが必要です。", "导出 VRM 需要直接父对象的 Humanoid。", "VRM 내보내기에 바로 위 부모의 Humanoid가 필요합니다." } },
            { "Has its own humanoid, but MA depends on objects or settings outside this asset.", new[] { "独自のHumanoidがありますが、MAがアセット外のオブジェクトや設定に依存しています。", "拥有自己的 Humanoid，但 MA 依赖此资源外部的对象或设置。", "자체 Humanoid가 있지만 MA가 에셋 외부의 오브젝트나 설정에 의존합니다." } },
            { "This asset has an external MA dependency. Its direct parent must be a valid independent VRM avatar.", new[] { "アセット外へのMA依存があります。直上の親に有効な独立したVRMアバターが必要です。", "此资源存在外部 MA 依赖。其直接父对象必须是有效的独立 VRM 人物模型。", "외부 MA 의존성이 있습니다. 바로 위 부모가 유효한 독립 VRM 아바타여야 합니다." } },
            { "This asset has no valid VRM humanoid. Place it directly under a valid independent avatar to convert it as an attachment, or configure its own Humanoid Animator.", new[] { "有効なVRM Humanoidがありません。有効な独立アバターの直下に配置して衣装・アクセサリーとして変換するか、このアセットのHumanoid Animatorを設定してください。", "此资源没有有效的 VRM Humanoid。请将其直接放在有效的独立人物模型下作为附属物转换，或配置它自己的 Humanoid Animator。", "유효한 VRM Humanoid가 없습니다. 유효한 독립 아바타 바로 아래에 배치하여 부착물로 변환하거나 자체 Humanoid Animator를 설정하세요." } },
            { "Export requires Edit Mode.", new[] { "エクスポートは編集モードで行ってください。", "请在编辑模式下导出。", "편집 모드에서 내보내세요." } },
            { "Animation export requires Edit Mode.", new[] { "アニメーションのエクスポートは編集モードで行ってください。", "请在编辑模式下导出动画。", "편집 모드에서 애니메이션을 내보내세요." } },
            { "Choose a Humanoid AnimationClip. Generic and Legacy clips require their original skeleton and are not supported.", new[] { "HumanoidのAnimationClipを選択してください。GenericとLegacyは元のスケルトンが必要なため対応していません。", "请选择 Humanoid AnimationClip。Generic 和 Legacy 剪辑需要原始骨架，因此不受支持。", "Humanoid AnimationClip을 선택하세요. Generic 및 Legacy 클립은 원본 스켈레톤이 필요하므로 지원하지 않습니다." } },
            { "The clip contains object-reference animation, which VRMA cannot export.", new[] { "クリップにVRMAへエクスポートできないオブジェクト参照のアニメーションが含まれています。", "剪辑包含无法导出到 VRMA 的对象引用动画。", "클립에 VRMA로 내보낼 수 없는 오브젝트 참조 애니메이션이 포함되어 있습니다." } },
            { "The clip contains object, transform or blendshape curves. Export a Humanoid-only clip.", new[] { "オブジェクト・Transform・ブレンドシェイプのカーブが含まれています。Humanoidのみのクリップを使用してください。", "剪辑包含对象、Transform 或混合形状曲线。请使用仅含 Humanoid 动画的剪辑。", "오브젝트, Transform 또는 블렌드셰이프 커브가 포함되어 있습니다. Humanoid 전용 클립을 사용하세요." } },
        };
    }
}
