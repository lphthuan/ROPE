# Kiểm tra và sửa menu khởi động

Ngày **09/10/2026**, Unity **6000.0.69f1**.

Nút Play/Quit trong MainMenu đã có đúng tham chiếu tới MenuManager,
InputSystemUIInputModule hoạt động và raycast tại vị trí hai nút không
bị graphic khác chặn. Play có thể tải Cinematic qua listener serialized.
Quit trước đây chỉ gọi Application.Quit nên không dừng phiên chơi trong
Unity Editor.

`MenuManager.Start` hiện reset Time.timeScale và áp dụng trạng thái cursor
của scene ngay lúc khởi động, kể cả khi trạng thái pause/khóa chuột còn
từ lần chạy trước. OnDestroy dọn singleton để các lần chạy/chuyển scene
không giữ manager đã bị hủy. QuitGame dừng Play Mode với UNITY_EDITOR;
bản game Windows vẫn dùng Application.Quit.

`MenuInteractionTests` bổ sung ba Play Mode test:

- Click Play bằng mouse state qua InputSystemUIInputModule và listener
  gốc của Button, kiểm tra Cinematic được tải.
- Click Quit qua cùng đường input, kiểm tra listener QuitGame được gán
  đúng và sự kiện click đến Button. Trong test này handler thoát được
  tạm tắt để không đóng phiên Play Mode của Test Runner.
- Khởi động MenuManager sau khi khóa/ẩn cursor và pause, kiểm tra cursor
  được mở/hiện và Time.timeScale trở về 1.

Test dùng bản sao InputSettings/InputActionAsset và virtual Mouse riêng,
khôi phục dữ liệu gốc khi kết thúc. Việc bỏ qua focus chỉ áp dụng cho test
vì Unity có thể ở sau MCP client; setting input của game không bị đổi.
PlayModeTests.asmdef bổ sung tham chiếu Unity.InputSystem.

Kết quả lần sửa cursor/Quit:

- **32/32 Play Mode**, 0 fail, 0 skip; bao gồm ba test menu và toàn bộ
  29 test gameplay trước đó. `Logs/MCP/Menu/PlayModeResults.json`.
- **43 assembly Windows** biên dịch được với nhánh UNITY_EDITOR đã loại
  khỏi player. `Logs/MCP/Menu/PlayerCompilation.json`.
- Click Quit với **listener gốc vẫn bật** ở một phiên Play Mode riêng đã
  trả Unity về MainMenu, `isPlaying=false`, `isPaused=false`.
  `Logs/MCP/Menu/ExitClickBefore.json` và `ExitClickAfter.json`.
- Kiểm tra thoát lại với InputSettings gốc của dự án: MainMenu ở Edit
  Mode, không có lỗi Console. `Logs/MCP/Menu/ExitNormalSettingsVerification.json`.
- Bộ Edit Mode trước đó vẫn có **29/29** test đạt; không đổi test Edit Mode.

Unity được trả về MainMenu ở Edit Mode. Để chơi, nhấn Play ở toolbar Unity
và thao tác trong tab Game; nút Quit của game sẽ kết thúc phiên Play Mode.

## Bổ sung: chuột thật không hoạt động trong Game View

Phản hồi tiếp theo phát hiện menu chỉ nhận click trong Simulator. Đã tái
hiện trên Unity 6000.0.69f1 / Input System 1.19.0: mở Simulator làm native
Mouse chuyển từ enabled=true sang false và thêm Device Simulator Touchscreen.
Khi focus lại Game View, Simulator có trạng thái Disabled nhưng native
Mouse vẫn bị tắt. Đóng cửa sổ Simulator mới bật lại native Mouse.

Nguyên nhân nằm ở Input System Editor/DeviceSimulator/InputSystemPlugin.cs:
OnCreate gọi DisableDevice với mouse/pen native để tránh xung đột với touch;
OnDestroy mới EnableDevice lại. Các test trước dùng virtual Mouse nên không
bị nhánh này tắt và đã bỏ sót lỗi input của Editor. Game View còn được lưu
với chế độ PlayUnfocused trong layout cũ.

Simulator là công cụ tích hợp trong Unity Editor để xem trước màn hình và
touch của thiết bị mobile. Nó không đổi build target của dự án. ROPE vẫn
đang dùng StandaloneWindows64; Simulator không được đóng gói vào game PC.
Tham khảo [Unity Device Simulator](https://docs.unity3d.com/6000.0/Documentation/Manual/device-simulator-introduction.html).

`Assets/_Game/Editor/PcGameView.cs` sửa cấu hình Editor cho build target
Standalone: đóng Simulator khi khôi phục layout sau reload hoặc trước khi
vào Play Mode, sau đó chọn Game View và đặt PlayFocused. Đóng Simulator
cho phép chính plugin của Unity khôi phục thiết bị; không bật ép input
từng frame và không thay đổi InputSettings hoặc script gameplay. Có thể
áp dụng lại bằng **Tools > ROPE > Use PC Game View**. Cấu hình này không
chạy trong batch mode hay khi target là mobile.

Kiểm tra sau sửa:

- Mở lại Simulator, xác nhận chuột thật bị tắt; vào Play Mode tự đóng
  Simulator, native Mouse enabled=true, không còn simulator touchscreen,
  Game View PlayFocused, InputSettings vẫn là asset gốc.
- Gửi mouse state qua thiết bị Mouse native đang có trong Editor bằng MCP,
  với listener gốc và InputSettings gốc: Play dẫn tới gameplay Map1 sau
  cinematic; Quit trả về MainMenu Edit Mode. Không dùng virtual Mouse hay
  đổi focus policy trong hai kiểm tra này.
- Chạy lại ba MenuInteractionTests: **3/3 đạt**, 0 fail, 0 skip.
- Console không có error sau kiểm tra native Play/Quit.

Evidence: `Logs/MCP/PCGameView/SimulatorCreationBeforeFix.json`,
`GameViewBeforeFix.json`, `GameViewAfterFix.json`, `NativeMousePlayResult.json`,
`NativeMouseQuitResult.json` và `MenuTests.json` trong cùng thư mục.
