# ROPE — Clean, MCP và kiểm tra gameplay

Hoàn tất ngày **09/10/2026** bằng **Unity 6000.0.69f1** trên máy này.
Editor đã được khôi phục thư viện Mono từ bộ cài chính thức đúng phiên bản.
Mở `Assets/_Game/Scenes/Main/MainMenu.unity`, nhấn Play trong Unity rồi
chọn PLAY trong game để đi qua Cinematic tới Map1.

## Cấu trúc và tên asset

Đã thực hiện **179 thao tác di chuyển/đổi tên**, ghi đầy đủ trong
[ProjectCleanupMoves.csv](ProjectCleanupMoves.csv). Nhóm nội dung chính:

| Nội dung | Vị trí chuẩn |
|---|---|
| Gameplay C# | `Assets/_Game/Scripts/{Characters,Core,Weapons,UI,Audio,Effects,Environment}` |
| Behavior Graph của enemy | `Assets/_Game/Data/AI` |
| Cấu hình súng, đạn, sát thương, hiệu ứng | `Assets/_Game/Data/Weapons` |
| Dữ liệu vật phẩm | `Assets/_Game/Data/Items` |
| Prefab enemy, vật phẩm | `Assets/_Game/Prefabs/{Enemies,Items}` |
| Model/texture | `Assets/_Game/Art/{Weapons,Environment,VFX,Characters}` |
| Shader từ các gói nội dung | `Assets/_Game/Shaders/Packages` |
| RenderTexture inventory | `Assets/_Game/UI/RenderTextures/InventorySlot1..4` |
| Input System | `Assets/Settings/Input/StarterAssets` |
| Tài liệu kiểm thử và công cụ | `Docs/Testing`, `Tools` |

Tên file C# đã được đồng bộ với tên lớp ở các trường hợp lệch nhau,
ví dụ `BossVideoCutscene`, `SpiderAgent`, `TutorialData`, `Checkpoint`.
Các tên lớp đã được serialized từ trước được giữ để tránh làm mất dữ liệu.
Các thư mục sai chính tả như `Forst`, `InvetoryCore`, `Vefects` đã được sửa.
Asset riêng của nhà cung cấp vẫn giữ cấu trúc cần thiết cho package đó.

Đã loại **16 asset thừa**, tổng cộng **48.124.761 byte (~45,90 MiB)**
khỏi `Assets`: 12 bản trùng nội dung được xác minh bằng SHA-256 và không
có dependency, một ZIP mà toàn bộ nội dung đã được giải nén giống hệt,
hai script không hoạt động và một asmdef test rỗng. Archive ZIP test trùng
ở thư mục gốc cũng được bỏ; skill và tài liệu cũ chuyển về `Tools/Skills`
và `Docs/Testing`. Công cụ clean dùng một lần được đưa ra khỏi `Assets`.

Audit cuối kiểm tra **5.891 GUID asset gốc còn lại**, không có asset mất
ngoài kế hoạch, không có GUID bị đổi. Bản sao phục hồi và bằng chứng xóa
nằm trong `Logs/Cleanup`; dung lượng trên chưa trừ các bản sao này.
Không xóa asset chỉ vì không xuất hiện trong dependency của build scene:
LightingData nhị phân và các demo/package vẫn có thể sử dụng chúng.

## MCP for Unity

UPM package và server được ghim **10.3.0**; package dùng tag `v10.3.0`
của [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp/tree/v10.3.0).
Server Python nằm trong `Library/MCPRuntime`, endpoint cục bộ
`http://127.0.0.1:8080/mcp`. Cấu hình Codex trên máy đã có endpoint này.

Từ PowerShell ở thư mục dự án:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Start-UnityMcp.ps1
```

Sau đó chọn **Tools > ROPE > Connect Unity MCP** trong Unity.
Công cụ dùng `uv` trên máy để tạo lại Python 3.12 và server nếu runtime
trong Library chưa tồn tại. Log server/Editor nằm trong `Logs/MCP`.

Có thể kiểm tra kết nối bằng MCP client đi kèm:

```powershell
.\Library\MCPRuntime\Scripts\python.exe .\Tools\Invoke-UnityMcp.py --resource mcpforunity://instances
```

Trong lần xác minh này, MCP đã kết nối đúng instance ROPE, điều khiển
scene/Play Mode, đọc Console, chụp ảnh và chạy Unity Test Framework.

## Kết quả kiểm tra

| Kiểm tra | Kết quả | Bằng chứng trong `Logs/MCP` |
|---|---|---|
| Biên dịch Editor | Không có lỗi biên dịch | `FinalCompileErrors.json` |
| Edit Mode | **29/29**, 0 fail, 0 skip | `FinalEditModeResults.json` |
| Play Mode | **29/29**, 0 fail, 0 skip | `FinalPlayModeResults.json` |
| Build scenes | **4/4**: MainMenu, Cinematic, Map1, MapBoss | `FinalSceneValidation.json` |
| Script Windows player | **43 assembly**, không có Editor/test assembly | `FinalPlayerCompilation.json` |
| Chuyển scene sau sửa LightingData | **1/1**, 0 fail, 0 skip | `SmokeRetestResults.json` |

Bước biên dịch player chỉ kiểm tra script; chưa tạo hoặc chạy bản `.exe`.
Lần kiểm tra cuối qua MCP dùng Editor đang mở. Muốn chạy lại toàn bộ
kiểm tra dòng lệnh, đóng Editor của dự án rồi chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-UnityProject.ps1 -Mode All
```

Kiểm tra cấu trúc scene bao gồm script bị mất, model Outline cần Read/Write,
NavMesh theo đúng loại agent tại spawn, và UI EventSystem bị trùng.
Test chuyển scene kiểm tra menu, video, singleton, player và hồi sinh theo
checkpoint của map hiện tại. Map1 đã bỏ EventSystem dư.

## Enemy và chức năng cơ bản

| Đối tượng/chức năng | Đã kiểm tra |
|---|---|
| Arathrox | Phát hiện player, graph cập nhật, root motion di chuyển; hitbox cắn gây sát thương |
| CrustaspikanLarvae | Phát hiện/di chuyển; sự kiện nổ gây sát thương |
| Droid Oil | Phát hiện player, graph tự tạo bullet burst, prefab và fire point hợp lệ |
| Boss Crustaspikan | NavMesh riêng đúng loại agent; phát hiện và tiếp tục di chuyển sau chuỗi kỹ năng mở đầu |
| Boss summon | Sự kiện animation tạo đủ 4 minion; từng agent ở trên NavMesh phù hợp |
| Weapon trong Map1 | Pickup qua trigger, bắn trừ đạn, chặn bắn khi reload, nạp đạn bảo toàn tổng đạn |
| Inventory trong Map1 | Nhặt item thật, số lượng/trọng lượng/giá trị, bật/tắt physics và lực ném khi thả |
| Player | Di chuyển trực tiếp trong Play Mode: khoảng **1,47 m** trên mặt phẳng sau input một giây |

Các lỗi được sửa khi kiểm tra:

- Animator của enemy dùng `AlwaysAnimate` để root motion và sự kiện tấn
  công tiếp tục chạy khi enemy ở ngoài camera. Đây là hành vi cập nhật
  được mô tả trong [Unity AnimatorCullingMode](https://docs.unity3d.com/6000.0/ScriptReference/AnimatorCullingMode.AlwaysAnimate.html).
- Bốn transition kết thúc kỹ năng boss quay về Locomotion; trước đó chúng
  thoát Base Layer và khiến boss mắc kẹt trong pose tấn công.
- Rock được giữ ở trạng thái kinematic trước khi ném và dùng MeshCollider
  convex khi chuyển thành Rigidbody động. Xem
  [quy tắc collider Rigidbody của Unity](https://docs.unity3d.com/2023.2/Manual/rigidbody-configure-colliders.html).
- DroidOilBullet khởi tạo SphereCollider trong Awake; các kiểm tra tag
  `Enemy` không tồn tại được thay bằng layer/component phù hợp.
- Bỏ log health UI của boss phát mỗi frame.
- MapBoss có bản LightingData riêng trong thư mục scene chính, sửa
  `m_Scene` bị mất và giữ nguyên 23 lightmap; không cần bake lại để sửa
  tham chiếu này. Mở lại scene và test chuyển map đã đạt.

Các test cắn, nổ và summon gọi trực tiếp sự kiện combat/animation để kiểm
tra wiring và hiệu ứng sát thương. Đây là kiểm thử sơ bộ, không phải một
vòng chơi hoàn chỉnh hay kiểm thử tất cả tình huống AI/độ khó.

## Hình ảnh và trạng thái bàn giao

Đã xem hình ảnh ở Game View **Full HD 1920×1080**:

- `Logs/MCP/Captures/MainMenu/MainMenuFinal.png`
- `Logs/MCP/Captures/Cinematic/CinematicDesktop.png` — video giải mã
  1920×1080, tạm dừng ở frame 45 để kiểm tra hình ảnh.
- `Logs/MCP/Captures/Map1/Map1Desktop.png`
- `Logs/MCP/Captures/MapBoss/MapBossFinal.png`

Video tự chuyển sang Map1 cũng đã được quan sát. MainMenu và MapBoss ở
lần chụp cuối không có error/warning; Map1 không có error runtime nhưng
còn các warning dưới đây. Unity được trả về **MainMenu, Edit Mode**,
không pause, không compile/import, MCP vẫn kết nối. Các asset sky,
RenderTexture và ammo bị runtime làm thay đổi được khôi phục từ baseline
đã lưu; không reset các thay đổi có sẵn của người dùng bằng Git.

Khi mới tải MapBoss, một số shader tạm hiện cyan trong lúc Unity biên
dịch variant. Chụp lại sau khi `ShaderUtil.anythingCompiling == false`
cho thấy hiển thị bình thường. Đây là
[placeholder của asynchronous shader compilation](https://docs.unity3d.com/6000.0/Documentation/Manual/AsynchronousShaderCompilation-introduction.html).
Công cụ chụp inline Game View của MCP dùng `EditorApplication.Step` và
đã gây assert native `dt > 0` trong một lần chuyển scene; các ảnh cuối
dùng capture bất đồng bộ hoặc camera trực tiếp và không tái hiện assert.

## Những cảnh báo cũ còn lại

- Map1: BoxCollider của item Mecha có scale âm; Unity ép kích thước
  collider về dương. Kiểm thử inventory vẫn đạt.
- Map1: cấu hình số item cần spawn vượt số vị trí sẵn có; LevelManager
  giới hạn số item theo vị trí và tính quota từ số item thực tế.
- Video StartGame.mp4: Unity tự hiệu chỉnh timestamp H.264.
- Animator ThirdPersonShooter: transition `Player_Death -> Exit` thiếu
  điều kiện/Exit Time nên bị Unity bỏ qua. Test hồi sinh vẫn đạt.
- Audit script ban đầu còn 22 GUID không tìm thấy trong 19 asset demo,
  profile HDRP hoặc nội dung Amplify Shader Editor cũ. Danh sách ở
  `Logs/Validation/ScriptReferences.json`. Các demo ngoài bốn scene
  build chưa được chạy; không coi kết quả scene chính là chứng nhận
  toàn bộ nội dung Asset Store.
