[**English**](README.md) | [**日本語**](README.ja.md) | [**简体中文**](README.zh-CN.md) | [**한국어**](README.ko.md)

# Mochiya Avatar Tools

Unity의 아바타, 의상, 액세서리를 [Mochiya](https://www.mochiya.org/ko)에 공유하거나 호환 앱에서 사용할 파일로 내보내세요. **Upload to Mochiya**를 사용하면 작품을 개인 Avatar Studio 라이브러리에 추가하거나 마켓플레이스에 등록할 수 있습니다. 원본 씬 오브젝트는 변경하지 않습니다.

## 설치

**Unity 2022.3 이상**과 [Git](https://git-scm.com/downloads)이 필요합니다. VRChat SDK Avatars, Modular Avatar, lilToon(2.3.4 이상) 등 모델에 필요한 패키지도 먼저 임포트하세요.

**Window > Package Manager > + > Add package from git URL**을 열고 아래 URL을 **위에서부터 하나씩** 추가하세요. 각 설치가 끝난 뒤 다음 URL을 추가합니다.

![Package Manager에서 Add package from git URL을 선택한 화면.](Documentation~/images/add-unity-package.webp)

1. UniGLTF:
   ```text
   https://github.com/vrm-c/UniVRM.git?path=/Packages/UniGLTF#v0.131.2
   ```
2. UniVRM:
   ```text
   https://github.com/vrm-c/UniVRM.git?path=/Packages/VRM10#v0.131.2
   ```
3. Mochiya Avatar Tools:
   ```text
   https://github.com/mochiya-labs/unity-avatar-tools.git
   ```

호환되는 UniVRM/UniGLTF 0.131.2 이상이 이미 설치되어 있다면 기존 설치를 유지하세요. Unity 컴파일이 끝나면 **Mochiya** 메뉴가 나타납니다. [다른 설치 방법(영어)](Documentation~/installation.md).

## Mochiya에 업로드하기 위한 계정 연결

**API 키는 Upload to Mochiya로 업로드할 때만 필요합니다.** 컴퓨터에서 파일을 변환하거나 내보내려면 [Mochiya Avatar Tools](#local-avatar-tools)로 바로 이동하세요. 계정이나 API 키가 필요하지 않습니다.

1. [Mochiya](https://www.mochiya.org/profile)에 로그인하고 프로필을 엽니다. 계정이 없다면 먼저 가입하세요.
2. 프로필 하단의 **API 키**를 펼쳐 키를 복사합니다. Unity 도구는 이 키를 사용하여 내 Mochiya 계정에 파일을 업로드합니다.
3. Unity에서 **Mochiya > Upload to Mochiya**를 열고 언어를 **한국어**로 선택합니다. **API 키** 필드에 키를 붙여 넣고 **연결**을 누르세요. 연결에 성공한 뒤 업로드합니다. 키를 다른 사람과 공유하지 마세요.

![프로필의 API 키를 펼쳐 복사 버튼이 보이는 화면.](Documentation~/images/api-key.png)

![Unity의 Upload to Mochiya 패널에서 언어 선택, 내용이 가려진 API 키 필드, 연결 버튼이 보이는 화면.](Documentation~/images/connect-to-mochiya.webp)

## Mochiya에 업로드

**편집 모드**에서 모델 프리팹을 **Project** 패널에서 씬의 **Hierarchy**로 드래그하세요. 업로드할 대상에 따라 다음 오브젝트를 선택합니다.

- **아바타 전체:** 아바타의 루트 오브젝트를 드래그하세요. 그 안에 있는 의상과 액세서리도 함께 업로드됩니다.
- **부착물만(의상, 헤어 또는 액세서리):** 제작자의 안내에 따라 호환 아바타에 설정하세요. 원래 Modular Avatar 컴포넌트를 유지하고 아바타 루트 바로 아래에 배치한 다음, 부착물의 루트만 드래그합니다. 먼저 MA Manual Bake를 실행하지 마세요.

```text
Hierarchy
└── My avatar      ← 아바타 전체를 업로드하려면 드래그
    ├── Body
    ├── Armature
    └── My outfit  ← 이 의상만 업로드하려면 드래그
```

부착물만 업로드할 때는 베이스 아바타의 스켈레톤을 참조하지만, 베이스의 몸체와 다른 부착물은 포함되지 않습니다. 베이스 아바타를 먼저 업로드할 필요도 없습니다. 아이템의 사용 요구 사항에 호환 아바타와 버전을 적어주세요. 의상이 다른 체형에 맞게 자동으로 조정되지는 않습니다.

1. **Upload to Mochiya**에서 **Hierarchy**의 모델을 **아바타 또는 부착물** 필드로 드래그합니다. 제목을 확인하고 카테고리와 커버 이미지를 선택한 뒤, **개인 사용** 또는 **마켓플레이스에 등록**을 선택합니다. 필요하면 **추가 아이템 정보**에서 갤러리 이미지나 구매자용 파일을 추가하세요. 유료 판매는 웹사이트에서 판매자 설정을 먼저 완료해야 합니다.
2. 표시된 오류를 해결한 뒤 **개인 사용으로 업로드** 또는 **상품 게시**를 누릅니다. 모델은 자동으로 내보내집니다. 완료 후 **아이템 보기**로 확인하세요. 기존 아이템은 웹사이트에서 편집합니다.

[파일 제한, 연결 및 업로드 복구(영어)](Documentation~/upload.md) · [호환성 및 제한 사항(영어)](Documentation~/reference.md#supported-behavior-and-limits)

<a id="local-avatar-tools"></a>

## Mochiya Avatar Tools

모델을 파일로 보관하거나 다른 사람과 공유하고 싶을 때, 또는 다른 앱에서 사용하려면 **Mochiya > Avatar Tools**를 여세요. 이 패널에서 모델을 호환 앱이 지원하는 VRM 또는 GLB 파일로 저장할 수 있습니다. 내보내기 전에 모델을 확인하거나 조정하려면 Unity 씬에 편집 가능한 VRM 복사본을 만들 수도 있습니다.

동작이나 포즈를 저장하려면 같은 패널에서 Humanoid AnimationClip을 **VRM Animation(.vrma)**으로 내보낼 수 있습니다. 내보낸 애니메이션은 Mochiya Avatar Studio와 해당 형식을 지원하는 앱에서 호환 아바타에 적용할 수 있습니다. 단일 프레임 클립은 포즈를 저장합니다.

변환과 내보내기는 내 컴퓨터에서 처리되므로 Mochiya 계정이나 API 키가 필요하지 않습니다. 모델을 Mochiya에 업로드하기만 하려면 **Upload to Mochiya**를 사용하세요. 모델 내보내기도 자동으로 처리합니다.

[변환 및 애니메이션 가이드(영어)](Documentation~/avatar-tools.md) · [전체 문서(영어)](Documentation~/index.md) · [MIT 라이선스](LICENSE)
