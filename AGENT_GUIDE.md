# AGENT GUIDE – Repo Into Graph (Dự án Nghiên Cứu Hybrid Context)

> **Dành cho:** AI Agent của teammate nhận bàn giao Tầng 2.  
> **Cập nhật lần cuối:** 2026-09-05  
> **Người bàn giao:** Huong DS (SongHuong02 branch)

---

## 1. Tổng Quan Dự Án

Dự án nghiên cứu mô hình biểu diễn **Ngữ cảnh Lai (Hybrid Context)** kết hợp:
- **Control Flow Graph (CFG)** — sơ đồ luồng điều khiển của hàm
- **Mã nguồn gốc (Raw Source Code)** — đoạn code thực tế

Mục tiêu: Tối ưu hóa hiệu năng và độ chính xác khi đưa context vào LLM để sinh câu hỏi kiểm thử.

---

## 2. Kiến Trúc 3 Tầng

```
┌─────────────────────────────────────────────────────┐
│          .NET API  (Repo_Into_Graph_API)             │
│                   Orchestrator                       │
└──────────┬──────────────────────┬───────────────────┘
           │                      │
    ┌──────▼──────┐        ┌──────▼──────┐
    │   TẦNG 1    │        │   TẦNG 2    │  ← Bạn sẽ implement
    │  Router     │──────► │  Hybrid     │
    │  (HOÀN     │ handoff │  Context    │
    │   THÀNH)   │        │  Generator  │
    └─────────────┘        └──────┬──────┘
                                  │
                           ┌──────▼──────┐
                           │   TẦNG 3    │
                           │  QA / LLM   │
                           └─────────────┘

Python Microservice (FastAPI, port 8000) — Service phụ dùng chung
```

---

## 3. Trạng Thái Từng Tầng

| Tầng | Tên | Trạng Thái | Mô Tả |
|------|-----|------------|-------|
| 1 | Adaptive Context Router | ✅ HOÀN THÀNH | Phân tích AST, đếm SLOC & V(G), quyết định ROUTE_RAW_CODE / ROUTE_HYBRID |
| 2 | Hybrid Context Generator | 🔧 CẦN IMPLEMENT | Sinh CFG skeleton, gắn Enriched Metadata, trích Critical Snippets |
| 3 | QA Assessment | ✅ HOÀN THÀNH | Đánh giá câu hỏi sinh ra từ LLM |

---

## 4. ⛔ ĐIỀU KHOẢN CẤM TUYỆT ĐỐI

> **TUYỆT ĐỐI KHÔNG ĐƯỢC thay đổi bất kỳ code, logic, hoặc cấu hình nào liên quan đến Tầng 1.**

Danh sách các file và thư mục KHÔNG ĐƯỢC CHỈNH SỬA:

```
ContextRouter_Microservice/          ← KHÔNG CHỈNH SỬA
  ├── ast_analyzer.py                ← KHÔNG CHỈNH SỬA
  ├── main.py                        ← KHÔNG CHỈNH SỬA
  └── requirements.txt               ← KHÔNG CHỈNH SỬA

Repo_Into_Graph_Application/Services/AdaptiveContextRouter/   ← KHÔNG CHỈNH SỬA
Repo_Into_Graph_Application/Dtos/AdaptiveContextRouter/       ← KHÔNG CHỈNH SỬA
Repo_Into_Graph_API/Controllers/TestRouterController.cs       ← KHÔNG CHỈNH SỬA (endpoint /test-router)
benchmark_tools/Tang_1_Router/                                ← KHÔNG CHỈNH SỬA
```

**Lý do:** Tầng 1 đã được kiểm định đầy đủ (19/19 test cases PASS). Bất kỳ thay đổi nào đều có thể phá vỡ contract với Orchestrator và tool test.

---

## 5. Nhiệm Vụ Cần Triển Khai – Tầng 2

### File cần implement (hiện là Stub):

**`Repo_Into_Graph_Application/Services/HybridContextGenerator/HybridContextGeneratorService.cs`**

```csharp
// Hiện tại chỉ là Stub - cần implement đầy đủ logic sau:
public Task<HybridContextOutputDto> GenerateAsync(HybridContextInputDto input)
{
    // TODO: 
    // 1. Dựa vào input.RawSourceCode và input.Language → xây dựng CFG skeleton
    // 2. Gắn Enriched Metadata: async/await markers, annotation tags, dependency info
    // 3. Trích xuất Critical Snippets: các nhánh if, throw, catch, vòng lặp phức tạp
    // 4. Trả về HybridContextOutputDto với đầy đủ dữ liệu
}
```

### Input nhận được (HybridContextInputDto):
```json
{
  "moduleId": "MOD_001",
  "language": "java",
  "routingDecision": "ROUTE_HYBRID",
  "metrics": {
    "sloc": 32,
    "cyclomaticComplexity": 6
  },
  "rawSourceCode": "...",
  "astPayload": {
    "parserType": "tree-sitter",
    "rootNodeType": "class_declaration",
    "hasError": false
  }
}
```

### Output cần trả về (HybridContextOutputDto — cần mở rộng):
```json
{
  "moduleId": "MOD_001",
  "status": "SUCCESS",
  "cfgSkeleton": "graph TD\n  A[Start] --> B{req == null?}...",
  "criticalSnippets": ["if (req == null) throw new BadRequest();"],
  "enrichedMetadata": { ... },
  "message": "..."
}
```

> **Lưu ý:** File `HybridContextOutputDto.cs` cần được mở rộng thêm các trường `cfgSkeleton`, `criticalSnippets`, `enrichedMetadata`. Đây là file bạn ĐƯỢC PHÉP chỉnh sửa.

---

## 6. Hướng Dẫn Chạy Môi Trường Dev

### Bước 1: Khởi động Python Microservice
```bash
cd ContextRouter_Microservice
run_server.bat          # hoặc: python main.py
# → Server chạy tại http://localhost:8000
```

### Bước 2: Khởi động .NET API
```bash
dotnet restore Repo_Into_Graph.sln
dotnet run --project Repo_Into_Graph_API
# → API chạy tại https://localhost:55060
```

### Bước 3: Kiểm tra kết nối
Gọi thử endpoint Tầng 2:
```bash
curl -k -X POST https://localhost:55060/api/test/test-hybrid-context \
  -H "Content-Type: application/json" \
  -d '{
    "moduleId": "TEST_001",
    "language": "java",
    "routingDecision": "ROUTE_HYBRID",
    "metrics": {"sloc": 30, "cyclomaticComplexity": 5},
    "rawSourceCode": "public void test() {}",
    "astPayload": {"parserType": "tree-sitter", "rootNodeType": "class_declaration", "hasError": false}
  }'
```

### Bước 4: Chạy Tool Test Tầng 2
```bash
cd benchmark_tools\Tang_2_Hybrid_Context
run_tang_2.bat
# Nạp file: Checklist_Test_Tang_2_Handover.xlsx
```

---

## 7. Cấu Trúc Thư Mục Quan Trọng

```
Repo_Into_Graph_Solutions/
├── ContextRouter_Microservice/          [Python FastAPI - Service phụ dùng chung]
├── Repo_Into_Graph_API/
│   ├── Controllers/
│   │   └── TestRouterController.cs      [Endpoint /test-router và /test-hybrid-context]
│   └── Extensions/
│       └── DependencyInjectionExtensions.cs
├── Repo_Into_Graph_Application/
│   ├── Dtos/
│   │   ├── AdaptiveContextRouter/       [DTOs Tầng 1 - KHÔNG CHỈNH SỬA]
│   │   └── HybridContextGenerator/     [DTOs Tầng 2 - CÓ THỂ MỞ RỘNG]
│   └── Services/
│       ├── AdaptiveContextRouter/       [Service Tầng 1 - KHÔNG CHỈNH SỬA]
│       └── HybridContextGenerator/     [Service Tầng 2 - CẦN IMPLEMENT ĐÂY]
└── benchmark_tools/
    ├── Tang_1_Router/                   [Tool test Tầng 1 - KHÔNG CHỈNH SỬA]
    ├── Tang_2_Hybrid_Context/           [Tool test Tầng 2 - CÓ THỂ MỞ RỘNG]
    └── Tang_3_QA/                       [Tool test Tầng 3]
```

---

## 8. Git Workflow

- **Branch hiện tại:** `SongHuong02`  
- Tất cả commits của Tầng 2 nên theo convention: `feat(hybrid): ...`
- Không merge vào `main` khi chưa có sự đồng ý của cả team.

---

## 9. Liên Hệ

Mọi thắc mắc về Tầng 1 hoặc kiến trúc chung, liên hệ: **Huong DS (SongHuong02 branch)**.
