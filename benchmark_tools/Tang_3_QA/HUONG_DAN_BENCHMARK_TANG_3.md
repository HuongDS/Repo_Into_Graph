# TÀI LIỆU HƯỚNG DẪN & GIẢI THÍCH BỘ BENCHMARK ĐÁNH GIÁ (TẦNG 3)

Tài liệu này giải thích chi tiết cách bộ công cụ **Benchmark GUI** (`run_gui_benchmark.py`) hoạt động, cũng như cơ sở lý thuyết và luồng chạy thực tế của hệ thống API Đánh giá chất lượng câu hỏi (`WorkflowAssessmentController`).

---

## PHẦN 1: BỘ CÔNG CỤ BENCHMARK GUI (`run_gui_benchmark.py`)

Bộ công cụ này là một ứng dụng Desktop (viết bằng Python + CustomTkinter) đóng vai trò tự động hóa quá trình sinh câu hỏi và chấm điểm, sau đó xuất ra báo cáo Excel so sánh trực quan giữa 3 phương pháp (Truyền thống, CFG, E2E).

### 1. Luồng hoạt động của Tool (Workflow)

Khi người dùng nhấn nút **"CHẠY BENCHMARK"**, Tool sẽ tự động chạy một "Pipeline" qua 4 bước (gọi 4 API liên tiếp) cho mỗi phương pháp được chọn:

*   **Bước 1: Sinh câu hỏi (Generate)**
    *   **API được gọi:** Tùy chọn phương pháp mà gọi 1 trong 3 API:
        *   `/api/QuestionGenerator/generate-traditional` (Sinh từ Code thô)
        *   `/api/QuestionGenerator/generate-graph` (Sinh từ Đồ thị)
        *   `/api/QuestionGenerator/generate-e2e` (Sinh kết hợp 3 Tầng)
    *   **Xử lý:** Nhận kết quả là một mảng các Câu hỏi kèm theo Đáp án do AI (Gemini) sinh ra, cùng với lượng Token Input/Output đã tiêu thụ.

*   **Bước 2: Chấm điểm Bao phủ (Coverage)**
    *   **API được gọi:** `POST /api/WorkflowAssessment/assess-from-response`
    *   **Xử lý:** Tool truyền nguyên vẹn file JSON của Bước 1 vào API này. Hệ thống sẽ trả về chỉ số Coverage (%) cho từng câu hỏi (Câu hỏi này chạm đến bao nhiêu % số Node trong luồng nghiệp vụ).

*   **Bước 3: Chấm điểm Chính xác (Accuracy)**
    *   **API được gọi:** `POST /api/WorkflowAssessment/assess-accuracy`
    *   **Xử lý:** Tiếp tục truyền JSON từ Bước 1 vào. Trả về phán quyết Câu hỏi đó ĐÚNG LOGIC hay SAI LOGIC, kèm theo lời giải thích (Verdict) của AI Giám khảo.

*   **Bước 4: Tính Độ phức tạp (Difficulty / Cyclomatic)**
    *   **API được gọi:** `POST /api/WorkflowAssessment/assess-difficulty`
    *   **Xử lý:** Trả về độ phức tạp Cyclomatic Complexity (dựa trên số lượng rẽ nhánh mà câu hỏi kích hoạt).

*   **Bước 5: Xuất Báo Cáo**
    *   Tool tổng hợp toàn bộ số liệu, tính trung bình (Average) và xuất ra file Excel `CFG_vs_Traditional_Benchmark_Template.xlsx` (có sheet 3 cột để so sánh Delta).
    *   Đồng thời lưu raw log vào thư mục `benchmark_logs/` để xem chi tiết bằng UI "Xem Logs".

---

## PHẦN 2: CHI TIẾT CÁC API ĐÁNH GIÁ (WORKFLOW ASSESSMENT)

Các API này nằm trong `WorkflowAssessmentController.cs`. Chúng được thiết kế để đo lường tự động chất lượng của câu hỏi mà không cần đến sức người.

### 1. API Đo Độ Bao Phủ (Coverage Assessment)
*   **Endpoint:** `/api/WorkflowAssessment/assess-from-response`
*   **Cơ sở lý thuyết:** Sử dụng kỹ thuật **Semantic Similarity (Độ tương đồng ngữ nghĩa)** kết hợp với **Vector Embeddings**. 
*   **Luồng xử lý:**
    1. Trích xuất văn bản Câu hỏi do AI sinh ra.
    2. Gọi Gemini Embedding Model để biến Câu hỏi thành một Vector số.
    3. Biến toàn bộ các Node (các hàm/bước) trong đồ thị nghiệp vụ thành Vector.
    4. Tính khoảng cách Cosine Similarity giữa Vector Câu Hỏi và các Vector Node. Những Node nào có điểm Similarity > Threshold sẽ được coi là "Active" (bị câu hỏi chạm đến).
    5. Công thức: `Coverage = (Số Node Active / Tổng số Node trong luồng) * 100%`.

### 2. API Chấm Độ Chính Xác (Accuracy Assessment)
*   **Endpoint:** `/api/WorkflowAssessment/assess-accuracy`
*   **Cơ sở lý thuyết:** Sử dụng mô hình **LLM as a Judge (AI làm Giám khảo)**. Thay vì dùng AI sinh câu hỏi (Gemini) để tự chấm, chúng ta dùng một AI khác thông minh và khách quan hơn là **DeepSeek Chat** để chấm điểm chéo.
*   **Luồng xử lý:**
    1. Hệ thống lắp ráp một Prompt Giám khảo bao gồm: *Mã nguồn gốc của nghiệp vụ* + *Câu hỏi vừa sinh ra* + *Tiêu chí chấm điểm Rubric (Correctness, Faithfulness, Clarity...)*.
    2. Gửi Prompt này cho mô hình DeepSeek (thông qua API).
    3. DeepSeek đọc code, đọc câu hỏi và đưa ra phán quyết (Verdict) dạng JSON ép buộc: `IsAccurate = true/false` kèm lời giải thích.
    4. Đồng thời, API này cũng bóc tách ra một **Chuỗi đường đi liên thông (Extracted Path)** (ví dụ: `Node A -> Node B -> Node C`) mà câu hỏi đang ám chỉ. Nếu chuỗi này bị đứt gãy (không có đường nối vật lý trong đồ thị thật), câu hỏi sẽ bị đánh Fail (Sai logic).

### 3. API Tính Độ Phức Tạp (Difficulty Assessment)
*   **Endpoint:** `/api/WorkflowAssessment/assess-difficulty`
*   **Cơ sở lý thuyết:** Dựa trên **McCabe's Cyclomatic Complexity** nhưng được áp dụng ở cấp độ Đồ thị Quy trình (Workflow Graph) thay vì cấp độ dòng code.
*   **Luồng xử lý:**
    1. API này tái sử dụng lại `Extracted Path` (các Node Active) được trích xuất từ Bước Accuracy ở trên.
    2. Nó đếm số lượng Cạnh (Edges) và số lượng Nút (Nodes) tạo thành đồ thị con (Subgraph) mà câu hỏi kích hoạt.
    3. Dựa vào thuật toán tính toán cấu trúc rẽ nhánh, nó quy ra điểm Cyclomatic (Số lượng nhánh thực thi mà sinh viên phải suy nghĩ để giải được câu hỏi đó).

---

## TỔNG KẾT
Sự kết hợp giữa công cụ tự động `run_gui_benchmark.py` và 3 API Assessment tạo ra một chu trình **Kép kín (Closed-loop)**:
- **Generation (Sinh):** Tầng 1, 2, 3 tối ưu hóa Prompt để đẻ ra câu hỏi.
- **Evaluation (Đánh giá):** Assessment APIs dùng AI Giám khảo (DeepSeek) và Toán học (Vector, Đồ thị) để tự động cân đo đong đếm chất lượng.

Chính nhờ quy trình này, bạn hoàn toàn có cơ sở khoa học để chứng minh rằng phương pháp E2E (3 Tầng) hiệu quả hơn phương pháp Truyền thống bằng các con số định lượng (Quantitative) cụ thể trên file Excel!
