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

Kết quả cuối:

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
