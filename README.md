# Repo Into Graph API

**Mục đích dự án:** Nghiên cứu mô hình biểu diễn ngữ cảnh lai (Hybrid Context) kết hợp giữa Đồ thị luồng điều khiển (CFG) và Mã nguồn gốc nhằm tối ưu hóa hiệu năng và độ chính xác của LLM.

Đây là hệ thống Backend API của dự án **Repo Into Graph**. Codebase được thiết kế theo cấu trúc 3 lớp (3-layer architecture) để đảm bảo khả năng mở rộng, dễ bảo trì và phân tách trách nhiệm rõ ràng.

## 🏗 Kiến trúc Hệ thống (Architecture)

Dự án bao gồm 3 phân hệ chính:

1. **Repo_Into_Graph_API** (Presentation/API Layer)
   - Các Controllers (`AnalysisController`, `BusinessFlowsController`, `FewShotController`, v.v.)
   - Các file cấu hình DTO, Request/Response
   - Xử lý Exception và cấu hình Global Error
   - Điểm khởi chạy ứng dụng & Dependency Injection (`Program.cs`)

2. **Repo_Into_Graph_Application** (Business Logic/Service Layer)
   - Chứa logic nghiệp vụ cốt lõi (`AnalysisService`, `BusinessFlowService`, `FewShotService`, v.v.)
   - Cấu hình Mapping dữ liệu
   - Các Interfaces để thực hiện Abstraction

3. **Repo_Into_Graph_DataAccess** (Data/Infrastructure Layer)
   - Entity Models (`AnalysisRun`, `CallGraphEdge`, `BusinessFlow`, v.v.)
   - Cấu hình Entity Framework Core DbContext (`AnalysisDbContext`)
   - Triển khai Repositories (`GenericRepository`, `AnalysisRunRepository`, v.v.)
   - Database Migrations

---

## 🚀 Hướng dẫn Chạy Backend API

### Yêu cầu hệ thống (Prerequisites)
- .NET 8.0 SDK
- Cơ sở dữ liệu PostgreSQL
- Docker (Tùy chọn, dùng để chạy Postgres dưới dạng container)

### Cấu hình
1. Đảm bảo bạn đã bật PostgreSQL.
2. Cấu hình chuỗi kết nối (`DefaultConnection`) trong file `appsettings.json` (nằm trong thư mục `Repo_Into_Graph_API`):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=repo_into_graph;Username=postgres;Password=postgres"
  }
}
```

### Build và Chạy API
Chạy các lệnh sau trong terminal:

```bash
# Phục hồi các dependencies
dotnet restore Repo_Into_Graph.sln

# Chạy API
dotnet run --project Repo_Into_Graph_API.csproj
```
Sau khi ứng dụng chạy thành công, bạn có thể truy cập tài liệu Swagger UI tại: `https://localhost:<port>/swagger`

### Database Migrations
Dự án sử dụng Entity Framework Core. Để tự động cập nhật database schema (migrations):

```bash
dotnet ef database update --project Repo_Into_Graph_DataAccess --startup-project Repo_Into_Graph_API.csproj
```

---

## 🛠 Hướng dẫn Chạy Bộ công cụ Kiểm thử (Benchmark Tools)

Dự án có đi kèm một bộ công cụ kiểm thử tự động (được đặt trong thư mục `benchmark_tools/`). Bộ công cụ này cung cấp giao diện trực quan (GUI) để gọi các API đánh giá độ bao phủ (Coverage), độ chính xác (Accuracy), độ phức tạp (Difficulty) và ghi kết quả tự động ra file Excel.

### 1. Yêu cầu cài đặt
- Cài đặt **Python 3.9+** (https://www.python.org/downloads/)
- Cài đặt các thư viện Python cần thiết bằng pip:

```bash
pip install customtkinter requests openpyxl
```

### 2. Cách chạy phần mềm Benchmark (GUI)
1. Hãy chắc chắn rằng **Backend API (.NET) đang được chạy** (Vì công cụ Benchmark sẽ gọi đến `https://localhost:55060`).
2. Mở terminal tại thư mục gốc của dự án và chạy:

```bash
python benchmark_tools/run_gui_benchmark.py
```
3. Giao diện (Dashboard) sẽ hiện ra. Bạn chỉ cần nạp file Excel danh sách nghiệp vụ, chọn phương pháp đánh giá (Traditional / CFG / Cả hai) và bấm "CHẠY BENCHMARK". 
4. Hệ thống sẽ tự động tổng hợp số liệu và tạo ra file `CFG_vs_Traditional_Benchmark_Template.xlsx` cùng các log JSON chi tiết nằm trong thư mục `benchmark_logs/`. Mọi dữ liệu thử nghiệm đều được lưu vết để dễ dàng Audit (Kiểm chứng).
