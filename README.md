# Caro XO

Cờ Caro (gomoku) 2D cho Android, làm bằng Unity 6000.0.76f1 (URP 2D), màn hình dọc.

- Chơi với máy (3 mức Dễ / Vừa / Khó) hoặc 2 người trên một máy
- Bàn cờ 15×15, mở rộng tới 20×20; phóng to, kéo, đi lại
- Hiệu ứng nét bút viết dần khi đánh, tẩy dần khi đi lại; âm thanh bút chì

## Mở dự án

1. Cài Unity **6000.0.76f1** (Unity Hub).
2. Mở thư mục này bằng Unity Hub → Add project from disk.
3. Mở scene `Assets/_Caro/Scenes/Game.unity`.

Giao diện được dựng bằng menu **Caro → Build UI (mockups)** từ ảnh trong `Assets/_Caro/Art/UIv2`.

## Cấu trúc

| Thư mục | Nội dung |
| --- | --- |
| `Assets/_Caro/Scripts/Core` | Luật chơi, bàn cờ, kiểm tra thắng |
| `Assets/_Caro/Scripts/AI` | Máy chơi (tìm kiếm theo mẫu) |
| `Assets/_Caro/Scripts/UI` | Các màn hình, bàn cờ, âm thanh |
| `Assets/_Caro/Editor` | Công cụ dựng UI |
| `Assets/_Caro/Tests` | Test (Unity Test Runner) |
| `Assets/_Caro/Art`, `Audio` | Ảnh, font, âm thanh |

## Bản quyền

Font Pangolin và Liberation Sans theo giấy phép SIL OFL (file kèm theo trong thư mục font).
