# Dự án Repo Into Graph

**Mục đích dự án:** Nghiên cứu mô hình biểu diễn ngữ cảnh lai (Hybrid Context) kết hợp giữa Đồ thị luồng điều khiển (CFG) và Mã nguồn gốc nhằm tối ưu hóa hiệu năng và độ chính xác của Mô hình ngôn ngữ lớn (LLM).

Hệ thống cung cấp một giải pháp Backend toàn diện giúp tự động phân tích mã nguồn từ repository, trích xuất cấu trúc đồ thị luồng gọi hàm, xây dựng ngữ cảnh lai, hỗ trợ sinh câu hỏi tự động và kiểm tra/đánh giá các câu hỏi được sinh ra.

##  Kiến trúc Hệ thống (Architecture)

Dự án được thiết kế theo cấu trúc 3 lớp (3-layer architecture) để đảm bảo khả năng mở rộng và dễ bảo trì:

1. **Repo_Into_Graph_API** (Presentation/API Layer)
   - Tiếp nhận Request từ người dùng hoặc các Tool kiểm thử bên ngoài.
   - Các Controllers quản lý luồng điều hướng (Analysis, Business, Router, v.v.).

2. **Repo_Into_Graph_Application** (Business Logic/Service Layer)
   - Chứa logic nghiệp vụ cốt lõi (Trích xuất đồ thị, đánh giá câu hỏi, tạo Hybrid Context).
   - Giao tiếp với các dịch vụ AI và các Microservice vệ tinh.

3. **Repo_Into_Graph_DataAccess** (Data/Infrastructure Layer)
   - Tương tác với cơ sở dữ liệu PostgreSQL thông qua Entity Framework Core.
   - Quản lý các cấu trúc dữ liệu lưu trữ (AnalysisRun, CallGraphEdge, MethodSource).

---
> **Xem Tài Liệu Chi Tiết**: Để xem danh sách API đầy đủ, giải thích về các luồng xử lý và công cụ (benchmark tools), vui lòng đọc file [DOCUMENTATION.md](DOCUMENTATION.md).
