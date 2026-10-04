import customtkinter as ctk
import requests
import json
import threading
from openpyxl import Workbook, load_workbook
from tkinter import filedialog, messagebox

class PageStudentEvaluation(ctk.CTkFrame):
    def __init__(self, parent, controller):
        super().__init__(parent, fg_color="transparent")
        self.controller = controller
        
        self.grid_columnconfigure(0, weight=2) # 20% Cấu hình
        self.grid_columnconfigure(1, weight=3) # 35% Text (Câu hỏi, câu trả lời)
        self.grid_columnconfigure(2, weight=4) # 45% Kết quả
        self.grid_rowconfigure(1, weight=1)

        # Header
        header = ctk.CTkFrame(self, fg_color="transparent")
        header.grid(row=0, column=0, columnspan=3, pady=10, sticky="ew")
        ctk.CTkLabel(header, text="🎓 AI CHẤM ĐIỂM (STUDENT EVALUATION)", font=ctk.CTkFont(size=24, weight="bold")).pack()

        # Left Frame (Configs & Actions)
        self.left_frame = ctk.CTkFrame(self, fg_color="#FFFFFF", corner_radius=12, border_width=1, border_color="#E2E8F0")
        self.left_frame.grid(row=1, column=0, padx=(20, 10), pady=10, sticky="nsew")

        # Middle Frame (Texts)
        self.mid_frame = ctk.CTkFrame(self, fg_color="#FFFFFF", corner_radius=12, border_width=1, border_color="#E2E8F0")
        self.mid_frame.grid(row=1, column=1, padx=10, pady=10, sticky="nsew")

        # Right Frame (Results)
        self.right_frame = ctk.CTkFrame(self, fg_color="#FFFFFF", corner_radius=12, border_width=1, border_color="#E2E8F0")
        self.right_frame.grid(row=1, column=2, padx=(10, 20), pady=10, sticky="nsew")
        self.right_frame.grid_rowconfigure(2, weight=1)
        self.right_frame.grid_columnconfigure(0, weight=1)

        self.build_left()
        self.build_mid()
        self.build_right()

    def build_left(self):
        # Single Eval Form Config
        ctk.CTkLabel(self.left_frame, text="⚙️ CẤU HÌNH", font=ctk.CTkFont(weight="bold", size=16)).pack(pady=(15, 15))
        
        ctk.CTkLabel(self.left_frame, text="📌 MÃ NGHIỆP VỤ (Business ID):", font=ctk.CTkFont(weight="bold")).pack(anchor="w", padx=15)
        self.entry_biz_id = ctk.CTkEntry(self.left_frame, width=220)
        self.entry_biz_id.pack(anchor="w", padx=15, pady=(0, 15))

        ctk.CTkLabel(self.left_frame, text="💯 Thang điểm (Max Score):", font=ctk.CTkFont(weight="bold")).pack(anchor="w", padx=15)
        self.entry_max_score = ctk.CTkEntry(self.left_frame, width=120)
        self.entry_max_score.insert(0, "10.0")
        self.entry_max_score.pack(anchor="w", padx=15, pady=(0, 20))

        # Configs
        ctk.CTkLabel(self.left_frame, text="Context Mode:", font=ctk.CTkFont(weight="bold")).pack(anchor="w", padx=15)
        self.mode_var = ctk.StringVar(value="Hybrid")
        ctk.CTkRadioButton(self.left_frame, text="Raw", variable=self.mode_var, value="Raw").pack(anchor="w", padx=25, pady=5)
        ctk.CTkRadioButton(self.left_frame, text="Graph", variable=self.mode_var, value="Graph").pack(anchor="w", padx=25, pady=5)
        ctk.CTkRadioButton(self.left_frame, text="Hybrid", variable=self.mode_var, value="Hybrid").pack(anchor="w", padx=25, pady=(5, 20))

        ctk.CTkLabel(self.left_frame, text="Feedback Format:", font=ctk.CTkFont(weight="bold")).pack(anchor="w", padx=15)
        self.format_var = ctk.StringVar(value="Sandwich")
        ctk.CTkRadioButton(self.left_frame, text="Sandwich", variable=self.format_var, value="Sandwich").pack(anchor="w", padx=25, pady=5)
        ctk.CTkRadioButton(self.left_frame, text="Rubric", variable=self.format_var, value="Rubric").pack(anchor="w", padx=25, pady=5)
        ctk.CTkRadioButton(self.left_frame, text="Explainable", variable=self.format_var, value="Explainable").pack(anchor="w", padx=25, pady=(5, 30))

        self.btn_submit = ctk.CTkButton(self.left_frame, text="🚀 CHẤM ĐIỂM (SINGLE)", height=45, fg_color="#3B82F6", hover_color="#2563EB", font=ctk.CTkFont(weight="bold", size=14), command=self.on_submit)
        self.btn_submit.pack(fill="x", padx=15, pady=5)

    def build_mid(self):
        self.mid_frame.grid_rowconfigure(1, weight=1)
        self.mid_frame.grid_rowconfigure(3, weight=1)
        self.mid_frame.grid_rowconfigure(5, weight=2)
        self.mid_frame.grid_columnconfigure(0, weight=1)

        ctk.CTkLabel(self.mid_frame, text="❓ Đề bài / Câu hỏi:", font=ctk.CTkFont(weight="bold")).grid(row=0, column=0, sticky="w", padx=10, pady=(10,0))
        self.text_question = ctk.CTkTextbox(self.mid_frame, border_width=1, wrap="word")
        self.text_question.grid(row=1, column=0, sticky="nsew", padx=10, pady=(5, 10))

        ctk.CTkLabel(self.mid_frame, text="✅ Đáp án mẫu (Reference):", font=ctk.CTkFont(weight="bold")).grid(row=2, column=0, sticky="w", padx=10)
        self.text_reference = ctk.CTkTextbox(self.mid_frame, border_width=1, wrap="word")
        self.text_reference.grid(row=3, column=0, sticky="nsew", padx=10, pady=(5, 10))

        ctk.CTkLabel(self.mid_frame, text="✍️ CÂU TRẢ LỜI CỦA SINH VIÊN:", text_color="#2563EB", font=ctk.CTkFont(weight="bold")).grid(row=4, column=0, sticky="w", padx=10)
        self.text_student = ctk.CTkTextbox(self.mid_frame, border_width=2, border_color="#3B82F6", wrap="word")
        self.text_student.grid(row=5, column=0, sticky="nsew", padx=10, pady=(5, 15))

    def build_right(self):
        # Tools row
        tool_frame = ctk.CTkFrame(self.right_frame, fg_color="transparent")
        tool_frame.grid(row=0, column=0, sticky="ew", padx=10, pady=10)
        
        btn_template = ctk.CTkButton(tool_frame, text="📥 Tải Template Excel", width=120, command=self.export_template, fg_color="#64748B")
        btn_template.pack(side="left", padx=5)
        
        btn_import = ctk.CTkButton(tool_frame, text="📂 Import Excel (Chấm Nhiều Câu)", width=150, command=self.batch_evaluate, fg_color="#F59E0B", hover_color="#D97706", text_color="black", font=ctk.CTkFont(weight="bold"))
        btn_import.pack(side="left", padx=5)

        self.btn_export = ctk.CTkButton(tool_frame, text="💾 Lưu Kết Quả (Excel)", width=120, command=self.export_result, fg_color="#10B981")
        self.btn_export.pack(side="right", padx=5)
        
        ctk.CTkLabel(self.right_frame, text="📊 KẾT QUẢ ĐÁNH GIÁ (JSON & Format Đẹp):", font=ctk.CTkFont(weight="bold", size=14)).grid(row=1, column=0, pady=(10,5), padx=10, sticky="w")
        self.text_result = ctk.CTkTextbox(self.right_frame, font=ctk.CTkFont(family="Consolas", size=14), fg_color="#F1F5F9", text_color="#0F172A")
        self.text_result.grid(row=2, column=0, sticky="nsew", padx=10, pady=(0, 10))

        self.last_results = [] # Lưu trữ kết quả batch hoặc single

    def load_shared_data(self):
        if self.controller.shared_business_id:
            self.entry_biz_id.delete(0, "end")
            self.entry_biz_id.insert(0, self.controller.shared_business_id)
        if self.controller.shared_question:
            self.text_question.delete("1.0", "end")
            self.text_question.insert("1.0", self.controller.shared_question)
        if self.controller.shared_reference:
            self.text_reference.delete("1.0", "end")
            self.text_reference.insert("1.0", self.controller.shared_reference)

    def format_json_pretty(self, data):
        """Hàm tự viết để in JSON ra thành dạng văn bản đẹp, dễ đọc"""
        output = ""
        if isinstance(data, dict):
            for k, v in data.items():
                if k.lower() == "score" or k.lower() == "total_score":
                    output += f"🏆 ĐIỂM SỐ: {v}\n"
                elif isinstance(v, list):
                    output += f"📍 {k.upper()}:\n"
                    for item in v:
                        if isinstance(item, dict):
                            output += f"   - {item.get('name', 'Tiêu chí')}: Điểm {item.get('score', '')}\n"
                            output += f"     Nhận xét: {item.get('comment', '')}\n"
                        else:
                            output += f"   - {item}\n"
                else:
                    output += f"📍 {k.upper()}:\n   {v}\n\n"
        else:
            output = str(data)
        return output

    def call_eval_api(self, payload):
        url = "https://localhost:55060/api/student-evaluation/evaluate"
        res = requests.post(url, json=payload, verify=False, timeout=120)
        res.raise_for_status()
        return res.json()

    def on_submit(self):
        payload = {
            "businessId": self.entry_biz_id.get().strip(),
            "question": self.text_question.get("1.0", "end-1c").strip(),
            "referenceMaterial": self.text_reference.get("1.0", "end-1c").strip(),
            "studentAnswer": self.text_student.get("1.0", "end-1c").strip(),
            "contextModel": self.mode_var.get().lower(),
            "feedbackFormat": self.format_var.get().lower(),
            "maxScore": float(self.entry_max_score.get().strip() or "10")
        }

        self.btn_submit.configure(state="disabled", text="⏳ DeepSeek Đang Chấm Điểm...")
        self.text_result.delete("1.0", "end")
        self.last_results = []

        def worker():
            try:
                data = self.call_eval_api(payload)
                self.last_results.append({
                    "BusinessId": payload["businessId"],
                    "StudentAnswer": payload["studentAnswer"],
                    "RawJSON": json.dumps(data, ensure_ascii=False)
                })
                # Hien thi JSON chuan
                self.text_result.insert("end", "=== KẾT QUẢ ĐÃ ĐƯỢC LÀM ĐẸP ===\n")
                self.text_result.insert("end", self.format_json_pretty(data) + "\n\n")
                self.text_result.insert("end", "=== KẾT QUẢ RAW (JSON) ===\n")
                self.text_result.insert("end", json.dumps(data, indent=4, ensure_ascii=False))
            except Exception as e:
                self.text_result.insert("1.0", f"LỖI:\n{e}")
            finally:
                self.btn_submit.configure(state="normal", text="🚀 CHẤM ĐIỂM (SINGLE)")

        threading.Thread(target=worker, daemon=True).start()

    def export_template(self):
        try:
            wb = Workbook()
            ws = wb.active
            ws.title = "Template"
            headers = ["BusinessId", "Question", "ReferenceMaterial", "StudentAnswer", "MaxScore"]
            ws.append(headers)
            ws.append(["00000000-0000-0000-0000-000000000000", "Ví dụ câu hỏi?", "Ví dụ đáp án", "Em trả lời là...", 10])
            
            filepath = filedialog.asksaveasfilename(defaultextension=".xlsx", filetypes=[("Excel", "*.xlsx")], initialfile="Evaluation_Template.xlsx")
            if filepath:
                wb.save(filepath)
                messagebox.showinfo("Success", f"Đã lưu template thành công tại:\n{filepath}")
        except Exception as e:
            messagebox.showerror("Error", str(e))

    def batch_evaluate(self):
        filepath = filedialog.askopenfilename(filetypes=[("Excel files", "*.xlsx *.xls")])
        if not filepath: return

        try:
            wb = load_workbook(filepath)
            ws = wb.active
            rows = list(ws.iter_rows(values_only=True))
            if not rows or len(rows) < 2:
                messagebox.showerror("Error", "File Excel trống hoặc không có dữ liệu!")
                return
            headers = [str(h).strip() for h in rows[0]]
            req_cols = ["BusinessId", "Question", "ReferenceMaterial", "StudentAnswer"]
            if not all(c in headers for c in req_cols):
                messagebox.showerror("Error", "File Excel bị thiếu cột! Vui lòng dùng Template.")
                return
            
            col_idx = {h: i for i, h in enumerate(headers)}
            data_rows = rows[1:]
        except Exception as e:
            messagebox.showerror("Error", str(e))
            return

        self.text_result.delete("1.0", "end")
        self.text_result.insert("end", f"⏳ Bắt đầu chấm {len(data_rows)} câu hỏi từ Excel...\n")
        self.last_results = []
        
        mode = self.mode_var.get()
        fmt = self.format_var.get()

        def worker():
            for idx, row in enumerate(data_rows):
                b_id = str(row[col_idx["BusinessId"]]).strip()
                q = str(row[col_idx["Question"]])
                ref = str(row[col_idx["ReferenceMaterial"]])
                ans = str(row[col_idx["StudentAnswer"]])
                max_sc = 10.0
                if "MaxScore" in col_idx and row[col_idx["MaxScore"]] is not None:
                    try:
                        max_sc = float(row[col_idx["MaxScore"]])
                    except:
                        pass

                self.text_result.insert("end", f"\n▶️ Đang chấm dòng {idx+1} (Business: {b_id[:8]})...\n")
                self.text_result.see("end")

                payload = {
                    "businessId": b_id,
                    "question": q,
                    "referenceMaterial": ref,
                    "studentAnswer": ans,
                    "contextModel": mode.lower(),
                    "feedbackFormat": fmt.lower(),
                    "maxScore": max_sc
                }

                try:
                    data = self.call_eval_api(payload)
                    score = data.get("score") or data.get("total_score") or "?"
                    self.text_result.insert("end", f"   ✅ Chấm xong! Điểm số: {score}\n")
                    self.last_results.append({
                        "BusinessId": b_id,
                        "Question": q,
                        "StudentAnswer": ans,
                        "EvaluationScore": score,
                        "EvaluationJSON": json.dumps(data, ensure_ascii=False)
                    })
                except Exception as e:
                    self.text_result.insert("end", f"   ❌ Lỗi: {e}\n")
                    self.last_results.append({
                        "BusinessId": b_id,
                        "Question": q,
                        "StudentAnswer": ans,
                        "EvaluationScore": "ERROR",
                        "EvaluationJSON": str(e)
                    })
                self.text_result.see("end")

            self.text_result.insert("end", "\n🎉 HOÀN THÀNH CHẤM BÀI HÀNG LOẠT! Bạn có thể bấm 'Lưu Kết Quả' để xuất file Excel.\n")
            self.text_result.see("end")

        threading.Thread(target=worker, daemon=True).start()

    def export_result(self):
        if not self.last_results:
            messagebox.showwarning("Warning", "Chưa có kết quả chấm điểm nào để lưu!")
            return
            
        try:
            filepath = filedialog.asksaveasfilename(defaultextension=".xlsx", filetypes=[("Excel", "*.xlsx")], initialfile="Evaluation_Results.xlsx")
            if filepath:
                wb = Workbook()
                ws = wb.active
                ws.title = "Results"
                
                # Headers from keys of first dict
                headers = list(self.last_results[0].keys())
                ws.append(headers)
                
                for res in self.last_results:
                    ws.append([res.get(h, "") for h in headers])
                    
                wb.save(filepath)
                messagebox.showinfo("Success", f"Đã lưu kết quả thành công tại:\n{filepath}")
        except Exception as e:
            messagebox.showerror("Error", str(e))
