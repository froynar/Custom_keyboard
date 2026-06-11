# Legacy SQL Scripts (mô hình cũ — trước refactor)

Các script trong thư mục này thuộc **mô hình cũ** (case/PCB/plate riêng, `compatibility_rules`, build lưu component trực tiếp). Chúng được giữ lại chỉ để **tham khảo lịch sử**, **không** dùng cho schema refactor.

Schema/seed mới (mô hình kit-based, 17 bảng) sẽ nằm ở thư mục cha `Database/SqlServer/`:

- `CreateSchema_Refactor.sql` (Phase 1 — sẽ tạo)
- `VerifyRefactor.sql` (Phase 1 — sẽ tạo)
- Seed dùng `Documents_Refactor/SeedData_Refactor.sql`

> Không chạy các script legacy này lên database refactor.
