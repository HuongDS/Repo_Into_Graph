# 📖 Tài Liệu Kỹ Thuật: Hệ Thống Repo Into Graph

Tài liệu này bao gồm danh sách chi tiết các API, giải thích các luồng hoạt động (flows), thông tin các công cụ kiểm thử (benchmark tools) đi kèm và hướng dẫn khởi chạy hệ thống.

## 📑 Mục lục
1. [Luồng Hoạt Động Của Hệ Thống (Project Flows)](#1-luồng-hoạt-động-của-hệ-thống-project-flows)
2. [Tài liệu API Chi Tiết](#2-tài-liệu-api-chi-tiết)
3. [Giải Thích Các Công Cụ Kiểm Thử (Benchmark Tools)](#3-giải-thích-các-công-cụ-kiểm-thử-benchmark-tools)
4. [Hướng Dẫn Khởi Chạy Dự Án](#4-hướng-dẫn-khởi-chạy-dự-án)

---

## 1. Luồng Hoạt Động Của Hệ Thống (Project Flows)
Dự án được chia làm 3 tầng xử lý (Layers) chính:
- **Tầng 1 (Router):** Khi có một đoạn code hoặc một nghiệp vụ được yêu cầu, hệ thống sẽ tự động đo lường độ phức tạp (bằng Python Microservice) để quyết định xem đoạn code đó nên được đưa cho AI đọc dạng code thô bình thường (`ROUTE_RAW_CODE`), hay phải dùng đồ thị để đọc (`ROUTE_HYBRID`).
- **Tầng 2 (Hybrid Context Generator):** Nếu Tầng 1 quyết định dùng `ROUTE_HYBRID`, tầng này sẽ kết hợp cấu trúc đồ thị (Edges/Nodes) và nội dung hàm để sinh ra "Ngữ cảnh lai".
- **Tầng 3 (QA & Assessment):** Tầng giao tiếp với LLM (Gemini) để sinh ra câu hỏi dựa vào Hybrid Context. Sau khi câu hỏi sinh ra, tầng này có thêm cơ chế đo lường lại (Assessment) xem câu hỏi có đúng không (Accuracy), phủ hết đồ thị chưa (Coverage) và khó hay dễ (Difficulty).

---

## 2. Tài liệu API Chi Tiết

### 2.1. Analysis API (`/api/analysis`)
Nhóm API chuyên nạp và phân tích mã nguồn ban đầu (Parse Source Code).
* `POST /analyze`: Clone repo, phân tích mã nguồn thành Call Graph, Method Sources, DataFlow và lưu vào DB.

### 2.2. Analysis Runs API (`/api/analysis-runs`)
Nhóm API CRUD quản lý lịch sử phân tích.
* `GET /`: Danh sách lịch sử phân tích (phân trang, bộ lọc).
* `GET /{id}`: Chi tiết một bản ghi phân tích cụ thể.
* `POST /`: Tạo thủ công bản ghi AnalysisRun.
* `PUT /{id}`: Cập nhật thông tin bản ghi.

### 2.3. Business API (`/api/businesses`)
Quản lý các luồng nghiệp vụ kinh doanh trích xuất từ đồ thị.
* `GET /?analysisRunId={id}`: Lấy danh sách luồng nghiệp vụ.
* `GET /{id}`: Lấy chi tiết thông tin (Meta-data) luồng nghiệp vụ.
* `GET /{businessId}/graph`: Trả về dữ liệu gốc của đồ thị (Nodes & Edges).
* `GET /{businessId}/hybrid-context`: Trả về ngữ cảnh lai (Hybrid Context) tối ưu.

### 2.4. Few-Shot API (`/api/fewshot`)
Ngân hàng các mẫu ví dụ để Prompt cho AI. **(Lưu ý: Chức năng này hiện tại đang xây dựng data, chưa được tích hợp trực tiếp vào luồng gọi AI chính thức).**
* `GET /` | `GET /{id}` | `POST /` | `PUT /{id}` | `DELETE /{id}`: Quản lý thêm/sửa/xóa/xem các mẫu Few-Shot.

### 2.5. Question Generator API (`/api/questiongenerator`)
Giao tiếp với AI sinh câu hỏi tự động.
* `POST /generate-traditional`: Sinh câu hỏi bằng Raw Code.
* `POST /generate-graph`: Sinh câu hỏi bằng Graph Context.
* `POST /generate`: Sinh câu hỏi tổng quát (Unified).
* `POST /generate-e2e`: Quy trình sinh câu hỏi End-to-End đa tầng.

### 2.6. Workflow Assessment API (`/api/workflowassessment`)
Kiểm tra câu hỏi được sinh ra.
* `POST /assess-from-response`: Đo lường độ bao phủ (Coverage) của câu hỏi trên đồ thị.
* `POST /assess-accuracy`: Kiểm tra tính chính xác (Accuracy) của thông tin trong câu hỏi.
* `POST /assess-difficulty`: Đánh giá độ khó (Difficulty) áp dụng độ phức tạp McCabe.

### 2.7. Test Router API (`/api/test`)
API hỗ trợ kiểm thử nội bộ tách biệt các tầng (Dùng riêng cho Benchmark Tools).
* `POST /test-router`: Test thuật toán Tầng 1.
* `POST /test-hybrid-context`: Test logic Tầng 2.
* `POST /test-llm-orchestrator`: Test Orchestrator Tầng 3.

---

## 3. Giải Thích Các Công Cụ Kiểm Thử (Benchmark Tools)

Bộ công cụ kiểm thử (nằm trong thư mục `benchmark_tools/`) được thiết kế dạng Desktop App bằng Python để test độc lập các tầng:

* **Công cụ Tầng 1 (`Tang_1_Router`)**: Ứng dụng nạp file Excel chứa tập test-case code đầu vào, bắn xuống backend để so sánh kết quả định tuyến thuật toán với kết quả mong đợi.
* **Công cụ Tầng 2 (`Tang_2_Hybrid_Context`)**: Tương tự tầng 1, công cụ tự động gửi danh sách các test-case xuống API Tầng 2 để nhận về kết quả Hybrid Context thô, nhằm kiểm chứng định dạng đồ thị.
* **Công cụ Tầng 3 (`Tang_3_QA`)**: Đây là ứng dụng giao diện lớn nhất của hệ thống. Nó cho phép người dùng chọn Repository, xem trực quan biểu đồ hàm (**Graph Viewer**) và có tính năng thông minh mô phỏng các bước đi (**Smart Path Tracing**). Nó cung cấp các nút để sinh câu hỏi hàng loạt, sau đó tự động gọi các API Assessment để đánh giá chất lượng và xuất báo cáo ra file Excel.

---

## 4. Hướng Dẫn Khởi Chạy Dự Án

### Bước 1: Chạy Microservice Python (Bắt buộc chạy trước)
* **Yêu cầu:** Python 3.10+
* **Cài đặt thư viện:** `pip install -r ContextRouter_Microservice/requirements.txt`
* **Chạy server:** Bấm đúp vào `ContextRouter_Microservice/run_server.bat` (Port: 8000).

### Bước 2: Chạy Backend .NET API
* **Yêu cầu:** .NET 8.0, PostgreSQL.
* Đảm bảo đã sửa chuỗi kết nối Database trong file `appsettings.json` của `Repo_Into_Graph_API`.
* **Mở Terminal chạy lệnh:**
  ```bash
  dotnet restore Repo_Into_Graph.sln
  dotnet ef database update --project Repo_Into_Graph_DataAccess --startup-project Repo_Into_Graph_API.csproj
  dotnet run --project Repo_Into_Graph_API.csproj
  ```

### Bước 3: Chạy Benchmark Tools
* Cài đặt thư viện: `pip install customtkinter requests openpyxl`
* Tùy thuộc vào việc bạn muốn test tầng nào, vào thư mục tương ứng bên trong `benchmark_tools/` và nhấp đúp vào file `.bat` (Ví dụ: `run_tang_3.bat`).
