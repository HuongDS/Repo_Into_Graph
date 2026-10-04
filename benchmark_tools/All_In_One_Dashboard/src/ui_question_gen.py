import customtkinter as ctk
import requests
import json
import threading
from tkinter import messagebox
import os
import webbrowser

class PageQuestionGeneration(ctk.CTkFrame):
    def __init__(self, parent, controller):
        super().__init__(parent, fg_color="transparent")
        self.controller = controller

        self.loaded_businesses = []
        self.loaded_repos = []
        self.current_json = None

        self.setup_ui()
        threading.Thread(target=self.fetch_existing_repos, daemon=True).start()

    def setup_ui(self):
        self.grid_columnconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(1, weight=1)

        # Header
        header = ctk.CTkFrame(self, fg_color="transparent")
        header.grid(row=0, column=0, columnspan=2, pady=10, sticky="ew")
        ctk.CTkLabel(header, text="⚙️ GENERATE QUESTIONS", font=ctk.CTkFont(size=24, weight="bold")).pack()

        # Left Panel (Inputs)
        self.left_panel = ctk.CTkScrollableFrame(self, fg_color="#FFFFFF", corner_radius=12, border_width=1, border_color="#E2E8F0")
        self.left_panel.grid(row=1, column=0, padx=20, pady=10, sticky="nsew")

        # Repository Selection
        ctk.CTkLabel(self.left_panel, text="📁 1. Load Repository", font=ctk.CTkFont(weight="bold", size=16)).pack(anchor="w", pady=(10,10))
        self.combo_repo = ctk.CTkComboBox(self.left_panel, values=["(Loading...)"], width=400, command=self.on_repo_selected)
        self.combo_repo.pack(anchor="w", padx=10, pady=(0,10))

        ctk.CTkLabel(self.left_panel, text="Or Analyze New Repo (Path/URL):", font=ctk.CTkFont(weight="bold")).pack(anchor="w", padx=10)
        repo_frame = ctk.CTkFrame(self.left_panel, fg_color="transparent")
        repo_frame.pack(fill="x", padx=10, pady=(0,10))
        self.entry_repo = ctk.CTkEntry(repo_frame, width=280)
        self.entry_repo.pack(side="left", padx=(0,10))
        self.btn_analyze = ctk.CTkButton(repo_frame, text="Analyze", width=100, command=self.analyze_repository)
        self.btn_analyze.pack(side="left")

        self.lbl_loaded_status = ctk.CTkLabel(self.left_panel, text="⚠ No repository selected", text_color="red")
        self.lbl_loaded_status.pack(anchor="w", padx=10, pady=(0,20))

        # Business Selection
        ctk.CTkLabel(self.left_panel, text="📌 2. Select Business", font=ctk.CTkFont(weight="bold", size=16)).pack(anchor="w", pady=(0,10))
        self.combo_business = ctk.CTkComboBox(self.left_panel, values=["(Select repo first)"], width=400)
        self.combo_business.pack(anchor="w", padx=10, pady=(0,20))

        # Config
        ctk.CTkLabel(self.left_panel, text="⚙️ 3. Configuration", font=ctk.CTkFont(weight="bold", size=16)).pack(anchor="w", pady=(0,10))
        
        cfg_frame = ctk.CTkFrame(self.left_panel, fg_color="transparent")
        cfg_frame.pack(fill="x", padx=10)

        ctk.CTkLabel(cfg_frame, text="Method:").grid(row=0, column=0, sticky="w", pady=5)
        self.combo_method = ctk.CTkComboBox(cfg_frame, values=["Traditional", "CFG", "Hybrid (E2E)"], width=150)
        self.combo_method.set("Hybrid (E2E)")
        self.combo_method.grid(row=0, column=1, sticky="w", padx=10, pady=5)

        ctk.CTkLabel(cfg_frame, text="Num Questions:").grid(row=1, column=0, sticky="w", pady=5)
        self.combo_num = ctk.CTkComboBox(cfg_frame, values=["1", "5", "10"], width=150)
        self.combo_num.set("1")
        self.combo_num.grid(row=1, column=1, sticky="w", padx=10, pady=5)

        self.btn_run = ctk.CTkButton(self.left_panel, text="🚀 TẠO CÂU HỎI", height=45, command=self.generate, fg_color="#10B981", hover_color="#059669", font=ctk.CTkFont(weight="bold", size=14))
        self.btn_run.pack(fill="x", padx=10, pady=30)


        # Right Panel (Output)
        self.right_panel = ctk.CTkFrame(self, fg_color="#FFFFFF", corner_radius=12, border_width=1, border_color="#E2E8F0")
        self.right_panel.grid(row=1, column=1, padx=(0, 20), pady=10, sticky="nsew")
        self.right_panel.grid_rowconfigure(1, weight=1)
        self.right_panel.grid_columnconfigure(0, weight=1)

        ctk.CTkLabel(self.right_panel, text="📝 KẾT QUẢ:", font=ctk.CTkFont(weight="bold", size=16)).grid(row=0, column=0, pady=10, sticky="w", padx=10)
        
        self.txt_result = ctk.CTkTextbox(self.right_panel, font=ctk.CTkFont(family="Consolas", size=13), fg_color="#F1F5F9")
        self.txt_result.grid(row=1, column=0, sticky="nsew", padx=10, pady=5)

        btn_transfer = ctk.CTkButton(self.right_panel, text="➡️ Đẩy sang Chấm Điểm", height=45, fg_color="#3B82F6", command=self.transfer_data, font=ctk.CTkFont(weight="bold", size=14))
        btn_transfer.grid(row=2, column=0, sticky="ew", padx=10, pady=10)

    def log(self, msg):
        self.txt_result.insert("end", msg + "\n")
        self.txt_result.see("end")

    # [Network fetching logic here similar to Tang 3 QA]
    def fetch_existing_repos(self):
        try:
            url = "https://localhost:55060/api/analysis-runs?page=1&pageSize=50"
            resp = requests.get(url, verify=False, timeout=10)
            if resp.status_code == 200:
                items = resp.json().get("items", [])
                self.loaded_repos = [{"id": r["id"], "path": r["repositoryPath"]} for r in items if "id" in r]
                c_vals = [f"{r['path']} ({r['id'][:8]})" for r in self.loaded_repos]
                self.combo_repo.configure(values=c_vals if c_vals else ["No repos found"])
                if c_vals: self.combo_repo.set(c_vals[0])
        except Exception as e:
            print("Err fetch repos:", e)

    def on_repo_selected(self, text):
        r_id = next((r["id"] for r in self.loaded_repos if r["path"] in text), None)
        if r_id:
            self.lbl_loaded_status.configure(text=f"Loading businesses...", text_color="blue")
            threading.Thread(target=self._load_businesses, args=(r_id,), daemon=True).start()

    def _load_businesses(self, run_id):
        try:
            url = f"https://localhost:55060/api/businesses?analysisRunId={run_id}"
            resp = requests.get(url, verify=False, timeout=10)
            if resp.status_code == 200:
                b_list = resp.json()
                self.loaded_businesses = [{"id": b["id"], "name": b["businessName"]} for b in b_list if "id" in b]
                c_vals = [f"{b['name']} ({b['id'][:8]})" for b in self.loaded_businesses]
                self.combo_business.configure(values=c_vals if c_vals else ["No businesses"])
                if c_vals: self.combo_business.set(c_vals[0])
                self.lbl_loaded_status.configure(text="✅ Ready", text_color="green")
        except:
            pass

    def analyze_repository(self):
        path = self.entry_repo.get().strip()
        if not path: return
        self.lbl_loaded_status.configure(text="⏳ Analyzing...", text_color="orange")
        def work():
            try:
                resp = requests.post("https://localhost:55060/api/analysis/analyze", json={"repositoryPath": path}, verify=False)
                if resp.status_code == 200:
                    run_id = resp.json().get("analysisRunId")
                    self.fetch_existing_repos()
                    self._load_businesses(run_id)
            except Exception as e:
                self.lbl_loaded_status.configure(text=f"Error: {e}", text_color="red")
        threading.Thread(target=work, daemon=True).start()

    def get_selected_business_id(self):
        txt = self.combo_business.get()
        return next((b["id"] for b in self.loaded_businesses if b["name"] in txt), None)

    def format_json_pretty(self, data):
        if not isinstance(data, dict):
            return str(data)
            
        b_name = data.get("businessName", "Unknown")
        b_id = data.get("businessId", "Unknown")
        in_tok = data.get("inputTokens", 0)
        out_tok = data.get("outputTokens", 0)
        
        output = f"📋 NGHIỆP VỤ: {b_name}\n"
        output += f"🔑 MÃ (ID): {b_id}\n"
        output += f"⚙️ TOKENS TIÊU THỤ: Input ({in_tok}) | Output ({out_tok})\n\n"
        
        qs = data.get("generatedQuestionDtos", [])
        if not qs:
            qs = data.get("GeneratedQuestionDtos", [])
            
        if not qs:
            output += "⚠ KHÔNG CÓ CÂU HỎI NÀO ĐƯỢC SINH RA.\n"
            return output
            
        for i, q in enumerate(qs, 1):
            diff = q.get("difficulty", "Unknown")
            q_text = q.get("question", "")
            a_text = q.get("suggestedAnswer", "")
            
            output += f"{'='*60}\n"
            output += f"📌 CÂU HỎI {i} (Độ khó: {diff})\n"
            output += f"{'-'*60}\n"
            output += f"❓ ĐỀ BÀI:\n{q_text}\n\n"
            output += f"✅ ĐÁP ÁN MẪU:\n{a_text}\n\n"
            
            targets = q.get("targetedEntryPoints", [])
            if targets:
                output += "🎯 MỤC TIÊU KIỂM TRA (Entry points):\n"
                for t in targets:
                    output += f"   - {t}\n"
            output += "\n"
            
        return output

    def generate(self):
        b_id = self.get_selected_business_id()
        if not b_id: return
        method_map = {"Traditional": "traditional", "CFG": "cfg", "Hybrid (E2E)": "hybrid"}
        gen_type = method_map.get(self.combo_method.get(), "hybrid")
        num = int(self.combo_num.get())

        self.btn_run.configure(state="disabled", text="⏳ Generating...")
        self.txt_result.delete("1.0", "end")
        
        def work():
            try:
                url = "https://localhost:55060/api/questiongenerator/generate"
                payload = {"businessId": b_id, "numQuestions": num, "generatorType": gen_type}
                res = requests.post(url, json=payload, verify=False, timeout=60)
                res.raise_for_status()
                
                # Check for array vs object
                raw_data = res.json()
                if isinstance(raw_data, list) and len(raw_data) > 0:
                    self.current_json = raw_data
                    # If it's a list, just format the first item or iterate
                    pretty = "=== KẾT QUẢ ĐÃ LÀM SẠCH ===\n\n"
                    for item in raw_data:
                        pretty += self.format_json_pretty(item)
                else:
                    self.current_json = [raw_data] # Store as list for transfer consistency
                    pretty = "=== KẾT QUẢ ĐÃ LÀM SẠCH ===\n\n" + self.format_json_pretty(raw_data)
                
                pretty += "\n\n=== RAW JSON ===\n" + json.dumps(raw_data, indent=4, ensure_ascii=False)
                
                self.txt_result.insert("1.0", pretty)
            except Exception as e:
                self.txt_result.insert("1.0", f"Error:\n{e}")
            finally:
                self.btn_run.configure(state="normal", text="🚀 TẠO CÂU HỎI")
        threading.Thread(target=work, daemon=True).start()

    def transfer_data(self):
        b_id = self.get_selected_business_id()
        if not b_id: return
        
        q_text, ref_text = "", ""
        if self.current_json and isinstance(self.current_json, list) and len(self.current_json) > 0:
            first = self.current_json[0]
            qs = first.get("generatedQuestionDtos", [])
            if not qs:
                qs = first.get("GeneratedQuestionDtos", [])
            
            if qs and len(qs) > 0:
                first_q = qs[0]
                q_text = first_q.get("question", "")
                ref_text = first_q.get("suggestedAnswer", "")
            
        self.controller.shared_business_id = b_id
        self.controller.shared_question = q_text
        self.controller.shared_reference = ref_text
        self.controller.show_frame_eval()
