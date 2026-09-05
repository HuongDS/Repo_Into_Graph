# Repo Into Graph API

**Mục đích dự án:** Nghiên cứu mô hình biểu diễn ngữ cảnh lai (Hybrid Context) kết hợp giữa Đồ thị luồng điều khiển (CFG) và Mã nguồn gốc nhằm tối ưu hóa hiệu năng và độ chính xác của LLM.

Đây là hệ thống Backend API của dự án **Repo Into Graph**. Codebase được thiết kế theo cấu trúc 3 lớp (3-layer architecture) để đảm bảo khả năng mở rộng, dễ bảo trì và phân tách trách nhiệm rõ ràng.

## Kiến trúc Hệ thống (Architecture)

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

## Hướng dẫn Chạy Backend API

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

## Hướng dẫn Chạy Python Microservice (Service phụ toàn dự án)

Dự án sử dụng một **Python FastAPI Microservice** làm service phân tích mã nguồn (AST, SLOC, V(G)) dùng chung cho các tầng trong kiến trúc. Service này **phải được khởi động trước khi chạy .NET API**.

### Yêu cầu
- Python 3.10+
- Các thư viện: `fastapi`, `uvicorn`, `tree-sitter`, `tree-sitter-java`, `tree-sitter-c-sharp`, `tree-sitter-python`

```bash
cd ContextRouter_Microservice
pip install -r requirements.txt
```

### Khởi động Server
```bash
# Windows — chạy bằng file bat có sẵn:
ContextRouter_Microservice\run_server.bat

# Hoặc chạy thủ công:
python main.py
```
Server sẽ khởi động tại `http://localhost:8000`. Endpoint phân tích: `POST /api/analyze-context`.

---

## Hướng dẫn Chạy Bộ công cụ Kiểm thử (Benchmark Tools)

Toàn bộ công cụ kiểm thử nằm trong thư mục `benchmark_tools/`, được chia theo từng tầng:

```
benchmark_tools/
├── Tang_1_Router/          ← Kiểm định Tầng 1 (Adaptive Context Router)
├── Tang_2_Hybrid_Context/  ← Kiểm định Tầng 2 (Hybrid Context Generator)
└── Tang_3_QA/              ← Kiểm định Tầng 3 (Question Generation & Assessment)
```

### Yêu cầu cài đặt chung
```bash
pip install customtkinter requests openpyxl
```

### Tầng 1 – Adaptive Context Router Benchmark
> Cần: **Python Microservice đang chạy** + **.NET API đang chạy**

```bash
# Nhấp đúp vào file:
benchmark_tools\Tang_1_Router\run_tang_1.bat

# Hoặc chạy thủ công:
cd benchmark_tools\Tang_1_Router
python run_router_benchmark.py
```
Nạp file `Checklist_Kiem_Dinh_Tang_1_Router.xlsx` vào giao diện và nhấn **CHẠY BENCHMARK**.

### Tầng 2 – Hybrid Context Generator Benchmark
> Cần: **.NET API đang chạy**

```bash
# Nhấp đúp vào file:
benchmark_tools\Tang_2_Hybrid_Context\run_tang_2.bat

# Hoặc chạy thủ công:
cd benchmark_tools\Tang_2_Hybrid_Context
python run_hybrid_benchmark.py
```
Nạp file `Checklist_Test_Tang_2_Handover.xlsx` vào giao diện và nhấn **CHẠY BENCHMARK**.  
Nhấp đúp vào bất kỳ hàng kết quả nào để xem payload đã gửi & response nhận về chi tiết.

### Tầng 3 – QA Assessment Benchmark
> Cần: **.NET API đang chạy**

```bash
# Nhấp đúp vào file:
benchmark_tools\Tang_3_QA\run_tang_3.bat

# Hoặc chạy thủ công:
cd benchmark_tools\Tang_3_QA
python run_gui_benchmark.py
```

