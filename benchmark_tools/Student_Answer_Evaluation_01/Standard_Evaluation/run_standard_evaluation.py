import customtkinter as ctk
import requests
import json
import urllib3
import threading

# Tắt cảnh báo InsecureRequestWarning khi gọi https://localhost
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

class StandardEvaluationApp(ctk.CTk):
    def __init__(self):
        super().__init__()

        self.title("Standard Evaluation - Test API")
        self.geometry("1000x800")
        
        # Grid layout
        self.grid_columnconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(0, weight=1)

        # Left Frame (Inputs)
        self.left_frame = ctk.CTkScrollableFrame(self)
        self.left_frame.grid(row=0, column=0, padx=10, pady=10, sticky="nsew")

        # Right Frame (Output)
        self.right_frame = ctk.CTkFrame(self)
        self.right_frame.grid(row=0, column=1, padx=10, pady=10, sticky="nsew")
        self.right_frame.grid_rowconfigure(1, weight=1)
        self.right_frame.grid_columnconfigure(0, weight=1)

        self.build_left_frame()
        self.build_right_frame()

    def build_left_frame(self):
        # API URL
        ctk.CTkLabel(self.left_frame, text="API Base URL:", font=ctk.CTkFont(weight="bold")).pack(anchor="w", pady=(10, 0))
        self.entry_url = ctk.CTkEntry(self.left_frame, width=300)
        self.entry_url.insert(0, "https://localhost:55060")
        self.entry_url.pack(anchor="w", pady=(0, 10))

        # Business ID
        ctk.CTkLabel(self.left_frame, text="Business ID (Guid):", font=ctk.CTkFont(weight="bold")).pack(anchor="w")
        self.entry_biz_id = ctk.CTkEntry(self.left_frame, width=300)
        self.entry_biz_id.pack(anchor="w", pady=(0, 10))

        # Thang điểm (Max Score)
        ctk.CTkLabel(self.left_frame, text="Thang điểm tối đa (Max Score):", font=ctk.CTkFont(weight="bold")).pack(anchor="w")
        self.entry_max_score = ctk.CTkEntry(self.left_frame, width=150)
        self.entry_max_score.insert(0, "10.0")
        self.entry_max_score.pack(anchor="w", pady=(0, 10))

        # Question
        ctk.CTkLabel(self.left_frame, text="Câu hỏi:", font=ctk.CTkFont(weight="bold")).pack(anchor="w")
        self.text_question = ctk.CTkTextbox(self.left_frame, height=80)
        self.text_question.pack(fill="x", pady=(0, 10))

        # Reference Material
        ctk.CTkLabel(self.left_frame, text="Đáp án mẫu / Rubric:", font=ctk.CTkFont(weight="bold")).pack(anchor="w")
        self.text_reference = ctk.CTkTextbox(self.left_frame, height=100)
        self.text_reference.pack(fill="x", pady=(0, 10))

        # Student Answer
        ctk.CTkLabel(self.left_frame, text="Câu trả lời của sinh viên:", font=ctk.CTkFont(weight="bold")).pack(anchor="w")
        self.text_student = ctk.CTkTextbox(self.left_frame, height=120)
        self.text_student.pack(fill="x", pady=(0, 10))

        # Context Mode
        ctk.CTkLabel(self.left_frame, text="Context Mode:", font=ctk.CTkFont(weight="bold")).pack(anchor="w")
        self.mode_var = ctk.StringVar(value="Hybrid")
        mode_frame = ctk.CTkFrame(self.left_frame, fg_color="transparent")
        mode_frame.pack(fill="x", pady=(0, 10))
        ctk.CTkRadioButton(mode_frame, text="Raw", variable=self.mode_var, value="Raw").pack(side="left", padx=(0, 10))
        ctk.CTkRadioButton(mode_frame, text="Graph", variable=self.mode_var, value="Graph").pack(side="left", padx=(0, 10))
        ctk.CTkRadioButton(mode_frame, text="Hybrid", variable=self.mode_var, value="Hybrid").pack(side="left", padx=(0, 10))

        # Feedback Format
        ctk.CTkLabel(self.left_frame, text="Feedback Format:", font=ctk.CTkFont(weight="bold")).pack(anchor="w")
        self.format_var = ctk.StringVar(value="Sandwich")
        format_frame = ctk.CTkFrame(self.left_frame, fg_color="transparent")
        format_frame.pack(fill="x", pady=(0, 20))
        ctk.CTkRadioButton(format_frame, text="Sandwich", variable=self.format_var, value="Sandwich").pack(side="left", padx=(0, 10))
        ctk.CTkRadioButton(format_frame, text="Rubric", variable=self.format_var, value="Rubric").pack(side="left", padx=(0, 10))
        ctk.CTkRadioButton(format_frame, text="Explainable", variable=self.format_var, value="Explainable").pack(side="left", padx=(0, 10))

        # Submit Button
        self.btn_submit = ctk.CTkButton(self.left_frame, text="🚀 CHẤM ĐIỂM (CALL API)", height=40, command=self.on_submit)
        self.btn_submit.pack(fill="x", pady=10)

    def build_right_frame(self):
        ctk.CTkLabel(self.right_frame, text="KẾT QUẢ TỪ API:", font=ctk.CTkFont(weight="bold", size=16)).grid(row=0, column=0, pady=10)
        self.text_result = ctk.CTkTextbox(self.right_frame, font=ctk.CTkFont(family="Consolas", size=13))
        self.text_result.grid(row=1, column=0, sticky="nsew", padx=10, pady=(0, 10))

    def on_submit(self):
        url = f"{self.entry_url.get().strip()}/api/student-evaluation/evaluate"
        
        payload = {
            "businessId": self.entry_biz_id.get().strip(),
            "question": self.text_question.get("1.0", "end-1c").strip(),
            "referenceMaterial": self.text_reference.get("1.0", "end-1c").strip(),
            "studentAnswer": self.text_student.get("1.0", "end-1c").strip(),
            "contextModel": self.mode_var.get(),
            "feedbackFormat": self.format_var.get(),
            "maxScore": float(self.entry_max_score.get().strip() or "10")
        }

        self.btn_submit.configure(state="disabled", text="⏳ Đang xử lý...")
        self.text_result.delete("1.0", "end")
        
        # Chạy request trong luồng riêng để không đơ giao diện
        threading.Thread(target=self.call_api, args=(url, payload), daemon=True).start()

    def call_api(self, url, payload):
        try:
            response = requests.post(url, json=payload, verify=False)
            response.raise_for_status()
            
            # Cố gắng parse JSON để in ra cho đẹp
            try:
                data = response.json()
                pretty_json = json.dumps(data, indent=4, ensure_ascii=False)
                self.update_result(pretty_json)
            except Exception:
                # Nếu không phải JSON hợp lệ thì in text thô
                self.update_result(response.text)

        except requests.exceptions.RequestException as e:
            error_msg = f"LỖI GỌI API:\n{str(e)}"
            if hasattr(e, 'response') and e.response is not None:
                error_msg += f"\n\nChi tiết lỗi từ Server:\n{e.response.text}"
            self.update_result(error_msg)
        finally:
            self.btn_submit.configure(state="normal", text="🚀 CHẤM ĐIỂM (CALL API)")

    def update_result(self, text):
        self.text_result.insert("1.0", text)

if __name__ == "__main__":
    ctk.set_appearance_mode("Light")
    ctk.set_default_color_theme("blue")
    app = StandardEvaluationApp()
    app.mainloop()
