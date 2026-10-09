# Khôi phục ROPE

Dự án giữ nguyên Unity **6000.0.69f1**. Mở thư mục gốc bằng Unity Hub,
sau đó mở `Assets/_Game/Scenes/Main/MainMenu.unity` để bắt đầu luồng game.
Build Settings gồm MainMenu, Cinematic, Map1 và MapBoss.

Đã xác minh bằng Editor 6000.0.69f1 ngày **09/10/2026**:

- Biên dịch Editor thành công.
- **29/29 Edit Mode** và **29/29 Play Mode** thành công, không bỏ qua test.
- Bốn scene trong Build Settings không có component bị mất script;
  mesh dùng Outline và NavMesh tại vị trí spawn đều hợp lệ.
- Biên dịch thành công **43 assembly script Windows**, gồm `ProjectMain`,
  `ThirdParty`, `LlamAcademy.ImpactSystem` và `MCPForUnity.Runtime`. Không đưa assembly Editor
  hoặc test vào kết quả runtime. Bước này chưa tạo bản build `.exe`.

Các sửa chữa liên quan đến assembly:

- Script hiệu ứng JMO/WarFX thuộc assembly runtime `ThirdParty`.
- `ThirdParty.Editor.asmdef` nằm trong `WarFX/Editor`; thư mục
  `Cartoon FX Easy Editor/Editor` dùng `.asmref` trỏ tới cùng assembly Editor.
  Assembly này chỉ tham chiếu `ThirdParty`, không phụ thuộc Test Runner.
- Xóa `using UnityEditor` không được sử dụng trong `BoneFixer` và
  `ParticleHandler`, để chúng biên dịch được cho bản game.
- Cập nhật 108 tên assembly được lưu trong Behavior Graph, UnityEvent và
  prefab điều khiển: kiểu gameplay dùng `ProjectMain`; kiểu Starter Assets
  dùng `ThirdParty`. Giữ nguyên GUID và cấu trúc graph tác giả; Unity
  tái tạo dữ liệu graph runtime sau khi giải quyết các kiểu bị mất.
- Test Edit Mode tham chiếu `Unity.Behavior` để kiểm tra việc tải graph.
- Chuyển test sát thương kết liễu quái sang Play Mode, vì `Destroy` chỉ
  hoạt động trong luồng runtime. Test kiểm tra máu về 0, sự kiện chết chỉ
  phát một lần và đối tượng được hủy ở cuối frame.
- Dọn 9 subasset không còn được tham chiếu trong profile URP mặc định,
  gồm component test của Unity và component có script đã mất. Giữ nguyên
  19 component đang được dùng và các giá trị đồ họa của chúng.

Unity không tự chuyển script trong thư mục `Editor` sang assembly Editor
khi một `.asmdef` của thư mục cha đã bao phủ chúng. Cần giới hạn platform
của assembly Editor và dùng `.asmdef` hoặc `.asmref` tại thư mục thích hợp.
Xem [tài liệu Unity về assembly](https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definitions-intro.html).

Chạy toàn bộ kiểm tra từ thư mục gốc khi Editor của dự án đã đóng:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-UnityProject.ps1 -Mode All
```

Có thể chạy riêng `Compile`, `EditMode`, `PlayMode`, `Scenes`, hoặc
`PlayerScripts`, và truyền `-EditorPath` nếu Unity được cài ở nơi khác.

Lần xác minh cuối được chạy qua MCP trong Editor đang mở. Kết quả chính
là `Logs/MCP/FinalEditModeResults.json`, `FinalPlayModeResults.json`,
`FinalSceneValidation.json` và `FinalPlayerCompilation.json`;
`Logs/Validation/CurrentResults.json` tổng hợp các bằng chứng này.
Kết quả biên dịch player nằm trong `Logs/PlayerScriptAssemblies`.
Khi chạy công cụ dòng lệnh ở trên, log và XML mới được ghi vào
`Logs/Validation`. Công cụ yêu cầu XML có test được thực thi và không có
test thất bại. Log/XML của các lần sửa chữa trước được lưu riêng trong
`Logs/Cleanup/TestHistory`.

Các test bổ sung kiểm tra Behavior Graph sau khi chuyển assembly và khởi
động luồng MainMenu → Cinematic → Map1 → MapBoss → MainMenu, gồm lệnh
PlayGame từ menu và tham chiếu video mở đầu. Các bộ test health,
hitbox, inventory, weapon và vision hiện có tiếp tục được chạy.

GameManager dùng vòng đời của từng scene để player, checkpoint và UI
luôn thuộc map hiện tại. Bỏ `DontDestroyOnLoad` vốn được gọi trên đối
tượng con và dọn singleton khi scene bị hủy. Test chuyển map kiểm tra
hồi sinh theo checkpoint trên cả Map1 và MapBoss.

Bật Read/Write cho 18 model được ItemController hoặc Outline sử dụng
trong các scene chính. Quick Outline cần đọc vertices, normals và cập
nhật UV/submesh ở runtime; cấu hình cũ gây lỗi ngay khi mở map.
Bước `Scenes` cũng kiểm tra điều kiện này để phát hiện nếu cấu hình
import bị thay đổi về sau.

MapBoss được bổ sung NavMeshSurface và dữ liệu bake cho đúng loại agent
Crustaspikan (`-334000983`). Các surface cũ chỉ phục vụ Arathrox và
CrustaspikanLarvae, khiến boss không thể tìm đường. Script di chuyển và
node MoveToPosition kiểm tra agent còn hoạt động và đã nằm trên NavMesh
trước khi dùng API tìm đường. Test khởi động scene yêu cầu mọi agent
đang hoạt động phải nằm trên NavMesh.

Có thể bake lại dữ liệu boss bằng menu `Tools > ROPE > Rebuild Boss
Navigation` trong Unity. Công cụ giữ GUID của dữ liệu bake khi cập nhật.

Rà soát toàn bộ asset YAML ban đầu gồm 1.224 scene, prefab và asset dữ
liệu. Sau khi sửa, còn 22 GUID script chưa tìm thấy trong 19 asset cũ,
chủ yếu profile HDRP, hàm của Amplify Shader Editor và scene demo từ
Asset Store. Danh sách nằm trong `Logs/Validation/ScriptReferences.json`.
Các scene chính đã qua cả kiểm tra cấu trúc và khởi động Play Mode.
Cảnh báo asset cũ cần được phân biệt với lỗi biên dịch hoặc lỗi runtime
của game; việc mở và khởi động các demo này chưa được xác minh.

Scene video mở đầu đã khởi động trong test; Unity có cảnh báo tự hiệu chỉnh
timestamp H.264 của `StartGame.mp4`. Map1 còn cảnh báo collider Mecha có
scale âm và số item muốn spawn lớn hơn số vị trí có sẵn. Animator player
cũng có một transition chết không có điều kiện nên Unity bỏ qua nó.
Package Validation Suite không được dùng đã được gỡ để giải quyết
cảnh báo thư viện `log4net` trùng phiên bản.

Đợt clean tiếp theo đã thực hiện 179 thao tác di chuyển/đổi tên và loại
16 asset thừa khỏi `Assets`, giữ GUID của mọi asset còn lại. Đã cài và
kết nối MCP for Unity **10.3.0**. Enemy được kiểm tra phát hiện player,
di chuyển, sát thương cắn/nổ, bắn và triệu hồi minion; weapon và inventory
được kiểm tra bằng đối tượng thật của Map1. Đã sửa animation culling,
transition thoát kỹ năng của boss, collider rock, tham chiếu collider
bullet và các phép kiểm tra tag không tồn tại.

LightingData của MapBoss có bản riêng tại thư mục của scene chính,
gán lại tham chiếu scene bị mất và giữ nguyên 23 lightmap. Kiểm tra bốn
scene và test chuyển scene đã được chạy lại thành công sau sửa chữa này.
Unity hiện mở MainMenu ở Edit Mode, Game View dùng preset Full HD.

Xem [báo cáo clean, MCP và QA](Docs/CleanupAndQA.md) để biết cấu trúc mới,
cách kết nối lại MCP, ảnh scene và phạm vi kiểm thử.

Trong lần kiểm tra ban đầu, Editor trên máy bị thiếu các thư viện Mono,
bao gồm `mscorlib.dll`, và không khởi động được cả với dự án trống.
Bộ cài chính thức đúng phiên bản đã được tải và xác minh chữ ký Unity để
khôi phục cài đặt. Nếu lỗi tương tự tái diễn, kiểm tra cài đặt Editor trước
khi xóa cache hoặc thay đổi phiên bản dự án.
