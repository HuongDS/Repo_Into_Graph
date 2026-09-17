import os
import time
import json
import threading
import datetime
import urllib3
import requests
import json
import openpyxl
import webbrowser
import customtkinter as ctk
from tkinter import filedialog, messagebox

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# ==========================================================================
# CAU HINH CHONG LOI API (Gemini / DeepSeek co the loi tam thoi: 429, 503, timeout)
# ==========================================================================
REQUEST_TIMEOUT = 300        # giay - cho toi da cho 1 lan goi API
MAX_RETRIES     = 4          # tong so lan thu cho MOI buoc (1 lan dau + 3 lan thu lai)
RETRY_WAIT      = [20, 45, 90]   # giay - nghi bao lau truoc moi lan thu lai
RATE_LIMIT_WAIT = 65         # giay - nghi lau hon khi bi HTTP 429 (rate limit)


def _validate_generate(data):
    """Kiem tra ket qua buoc sinh cau hoi. Tra ve None neu OK, hoac chuoi mo ta loi."""
    qs = data.get("generatedQuestionDtos", data.get("GeneratedQuestionDtos", []))
    if not qs:
        return "API khong tra ve cau hoi nao (0 questions)"
    if not data.get("inputTokens"):
        return "API khong tra ve inputTokens (= 0) -> ket qua khong hop le"
    return None


def _validate_assessment(data):
    """Kiem tra ket qua cac buoc cham diem."""
    if not data.get("questionResults"):
        return "API khong tra ve questionResults"
    return None


# --- THEME CONFIGURATION ---
ctk.set_appearance_mode("Light")  # The user explicitly requested Light mode
ctk.set_default_color_theme("blue")

class BenchmarkApp(ctk.CTk):
    def __init__(self):
        super().__init__()
        
        self.title("Source Code Question Generator")
        self.geometry("900x850")
        self.resizable(True, True)
        self.configure(fg_color="#F8FAFC")  # Crisp and modern background

        
        self.loaded_businesses = []  # List of dicts: {"name": "...", "id": "..."}
        self.loaded_repos = []       # List of dicts: {"id": "...", "path": "..."}
        self.last_call_ms = 0        # thoi gian (ms) cua lan goi API thanh cong gan nhat
        
        # --- UI SETUP ---
        self.setup_ui()
        
        # Fetch initial repos
        threading.Thread(target=self.fetch_existing_repos, daemon=True).start()
        
    def setup_ui(self):
        # 1. HEADER
        header_frame = ctk.CTkFrame(self, fg_color="transparent")
        header_frame.pack(pady=(25, 15), fill="x")
        header_label = ctk.CTkLabel(header_frame, text="📊 SOURCE CODE QUESTION GENERATOR", font=ctk.CTkFont(size=28, weight="bold", family="Inter"))
        header_label.pack()
        sub_label = ctk.CTkLabel(header_frame, text="Repo Into Graph Solutions", font=ctk.CTkFont(size=14, family="Inter"), text_color="#64748B")
        sub_label.pack(pady=(2,0))
        
        # 2. FRAME 1: Data Loading
        frame_data = ctk.CTkFrame(self, corner_radius=12, fg_color="#FFFFFF", border_width=1, border_color="#E2E8F0")
        frame_data.pack(padx=35, pady=10, fill="x")
        
        lbl_data = ctk.CTkLabel(frame_data, text="📁 STEP 1: LOAD REPOSITORY", font=ctk.CTkFont(size=16, weight="bold", family="Inter"), text_color="#1E293B")
        lbl_data.grid(row=0, column=0, padx=25, pady=(15, 10), sticky="w", columnspan=3)
        
        # Select existing repo
        ctk.CTkLabel(frame_data, text="Load from DB:", font=ctk.CTkFont(weight="bold", family="Inter"), text_color="#334155").grid(row=1, column=0, padx=25, pady=(0, 10), sticky="w")
        self.combo_repo = ctk.CTkComboBox(frame_data, values=["(Loading existing repositories...)"], width=450, state="readonly", fg_color="#F1F5F9", border_color="#CBD5E1", dropdown_fg_color="#FFFFFF", dropdown_hover_color="#E2E8F0", command=self.on_repo_selected)
        self.combo_repo.grid(row=1, column=1, padx=10, pady=(0, 10), sticky="w")
        self.combo_repo.set("(Loading existing repositories...)")
        
        # OR Analyze new repo
        ctk.CTkLabel(frame_data, text="Or Analyze New:", font=ctk.CTkFont(weight="bold", family="Inter"), text_color="#334155").grid(row=2, column=0, padx=25, pady=(0, 15), sticky="w")
        self.entry_repo = ctk.CTkEntry(frame_data, placeholder_text="Enter GitHub URL or Local Folder Path...", width=450, height=35, font=ctk.CTkFont(family="Inter"))
        self.entry_repo.grid(row=2, column=1, padx=10, pady=(0, 15), sticky="w")
        
        # Analyze button
        self.btn_analyze = ctk.CTkButton(frame_data, text="Analyze Repository", command=self.analyze_repository, fg_color="#3B82F6", hover_color="#2563EB", font=ctk.CTkFont(weight="bold", family="Inter"), corner_radius=6, width=160, height=35)
        self.btn_analyze.grid(row=2, column=2, padx=10, pady=(0, 15), sticky="w")
        
        self.lbl_loaded_status = ctk.CTkLabel(frame_data, text="⚠ No repository selected yet.", text_color="#EF4444", font=ctk.CTkFont(slant="italic", family="Inter"))
        self.lbl_loaded_status.grid(row=3, column=0, columnspan=3, padx=25, pady=(0, 15), sticky="w")
        
        # 3. FRAME 2: Configuration
        frame_config = ctk.CTkFrame(self, corner_radius=12, fg_color="#FFFFFF", border_width=1, border_color="#E2E8F0")
        frame_config.pack(padx=35, pady=10, fill="x")
        
        lbl_config = ctk.CTkLabel(frame_config, text="⚙️ STEP 2: TEST CONFIGURATION", font=ctk.CTkFont(size=16, weight="bold", family="Inter"), text_color="#1E293B")
        lbl_config.grid(row=0, column=0, padx=25, pady=20, sticky="w", columnspan=2)
        
        # - Chọn nghiệp vụ
        ctk.CTkLabel(frame_config, text="📌 Select Business:", font=ctk.CTkFont(weight="bold", family="Inter"), text_color="#334155").grid(row=1, column=0, padx=25, pady=(5,15), sticky="w")
        self.combo_business = ctk.CTkComboBox(frame_config, values=["(Please analyze a repository first)"], width=480, state="readonly", fg_color="#F1F5F9", border_color="#CBD5E1", dropdown_fg_color="#FFFFFF", dropdown_hover_color="#E2E8F0")
        self.combo_business.grid(row=1, column=1, padx=10, pady=(5,15), sticky="w", columnspan=3)
        self.combo_business.set("(Please analyze a repository first)")
        
        # - Phương pháp
        ctk.CTkLabel(frame_config, text="🔬 Test Method:", font=ctk.CTkFont(weight="bold", family="Inter"), text_color="#334155").grid(row=2, column=0, padx=25, pady=15, sticky="w")
        self.radio_var = ctk.StringVar(value="All")
        r_all = ctk.CTkRadioButton(frame_config, text="Run All (All 3)", variable=self.radio_var, value="All", text_color="#475569", border_color="#94A3B8")
        r_trad = ctk.CTkRadioButton(frame_config, text="Traditional", variable=self.radio_var, value="Traditional", text_color="#475569", border_color="#94A3B8")
        r_cfg = ctk.CTkRadioButton(frame_config, text="CFG (Graph)", variable=self.radio_var, value="CFG", text_color="#475569", border_color="#94A3B8")
        r_e2e = ctk.CTkRadioButton(frame_config, text="E2E (Hybrid 3-Tier)", variable=self.radio_var, value="E2E", text_color="#475569", border_color="#94A3B8")
        
        r_all.grid(row=2, column=1, padx=10, pady=15, sticky="w")
        r_trad.grid(row=2, column=2, padx=10, pady=15, sticky="w")
        r_cfg.grid(row=2, column=3, padx=10, pady=15, sticky="w")
        r_e2e.grid(row=2, column=4, padx=10, pady=15, sticky="w")
        
        # - Số câu hỏi & Độ khó
        ctk.CTkLabel(frame_config, text="🔢 Number of Questions:", font=ctk.CTkFont(weight="bold", family="Inter"), text_color="#334155").grid(row=3, column=0, padx=25, pady=(15,20), sticky="w")
        self.combo_num = ctk.CTkComboBox(frame_config, values=["5", "10", "15", "20"], width=130, state="readonly", fg_color="#F1F5F9", border_color="#CBD5E1", dropdown_fg_color="#FFFFFF", dropdown_hover_color="#E2E8F0")
        self.combo_num.grid(row=3, column=1, padx=10, pady=(15,20), sticky="w")
        self.combo_num.set("5")
        
        ctk.CTkLabel(frame_config, text="🔥 Difficulty:", font=ctk.CTkFont(weight="bold", family="Inter"), text_color="#334155").grid(row=3, column=2, padx=10, pady=(15,20), sticky="e")
        self.combo_difficulty = ctk.CTkComboBox(frame_config, values=["Easy", "Medium", "Hard"], width=130, state="readonly", fg_color="#F1F5F9", border_color="#CBD5E1", dropdown_fg_color="#FFFFFF", dropdown_hover_color="#E2E8F0")
        self.combo_difficulty.set("Medium")
        self.combo_difficulty.grid(row=3, column=3, padx=10, pady=(15,20), sticky="w")
        
        # 4. FRAME 3: Execution & Logs
        frame_exec = ctk.CTkFrame(self, corner_radius=12, fg_color="#FFFFFF", border_width=1, border_color="#E2E8F0")
        frame_exec.pack(padx=35, pady=10, fill="both", expand=True)
        
        button_frame = ctk.CTkFrame(frame_exec, fg_color="transparent")
        button_frame.pack(fill="x", padx=25, pady=20)
        
        self.btn_run = ctk.CTkButton(
            button_frame, 
            text="🚀 RUN BENCHMARK", 
            font=ctk.CTkFont(size=16, weight="bold", family="Inter"), 
            height=45, 
            corner_radius=8,
            fg_color="#3B82F6", 
            hover_color="#2563EB",
            command=self.start_benchmark_thread
        )
        self.btn_run.pack(side="left", fill="x", expand=True, padx=(0, 10))
        
        self.btn_view_graph = ctk.CTkButton(
            button_frame, 
            text="👁 VIEW GRAPH", 
            font=ctk.CTkFont(size=16, weight="bold", family="Inter"), 
            height=45, 
            corner_radius=8,
            fg_color="#F59E0B", 
            hover_color="#D97706",
            command=self.open_graph_viewer
        )
        self.btn_view_graph.pack(side="left", fill="x", expand=True, padx=(5, 5))
        
        self.btn_view_logs = ctk.CTkButton(
            button_frame, 
            text="📂 VIEW LOGS", 
            font=ctk.CTkFont(size=16, weight="bold", family="Inter"), 
            height=45, 
            corner_radius=8,
            fg_color="#64748B", 
            hover_color="#475569",
            command=self.open_logs_viewer
        )
        self.btn_view_logs.pack(side="right", fill="x", expand=True, padx=(10, 0))
        
        self.lbl_status = ctk.CTkLabel(frame_exec, text="Status: Ready", font=ctk.CTkFont(weight="bold", slant="italic", family="Inter"), text_color="#3B82F6")
        self.lbl_status.pack(anchor="w", padx=25)

        self.progress_bar = ctk.CTkProgressBar(frame_exec, height=8, corner_radius=4, progress_color="#10B981", fg_color="#E2E8F0")
        self.progress_bar.pack(fill="x", padx=25, pady=(10, 20))
        self.progress_bar.set(0)
        
        self.textbox_log = ctk.CTkTextbox(frame_exec, height=220, state="disabled", font=ctk.CTkFont(family="Consolas", size=13), fg_color="#F8FAFC", text_color="#1E293B", border_width=1, border_color="#CBD5E1")
        self.textbox_log.pack(padx=25, pady=(0, 25), fill="both", expand=True)
    # --- ACTIONS ---
    
    def set_status(self, text):
        self.lbl_status.configure(text=text)
        self.update_idletasks()
        
    def set_progress(self, value):
        self.progress_bar.set(value)
        self.update_idletasks()
        
    def log(self, message):
        """Helper to append log messages thread-safely."""
        self.textbox_log.configure(state="normal")
        self.textbox_log.insert("end", message + "\n")
        self.textbox_log.see("end")
        self.textbox_log.configure(state="disabled")
        self.update_idletasks()

    def fetch_existing_repos(self):
        try:
            url = "https://localhost:55060/api/analysis-runs?page=1&pageSize=50"
            resp = requests.get(url, verify=False, timeout=10)
            if resp.status_code == 200:
                data = resp.json()
                items = data.get("items", []) or data.get("Items", [])
                
                self.loaded_repos = []
                combo_values = []
                
                for item in items:
                    r_id = item.get("id") or item.get("Id")
                    r_path = item.get("repositoryPath") or item.get("RepositoryPath")
                    if r_id and r_path:
                        self.loaded_repos.append({"id": r_id, "path": r_path})
                        combo_values.append(f"{r_path} ({r_id[:8]}...)")
                
                if combo_values:
                    # Update combo from main thread
                    self.after(0, lambda: self.combo_repo.configure(values=combo_values))
                    self.after(0, lambda: self.combo_repo.set("(Select an existing repository)"))
                else:
                    self.after(0, lambda: self.combo_repo.set("(No existing repos found)"))
        except Exception as e:
            self.log(f"[WARNING] Could not fetch existing repos: {e}")
            self.after(0, lambda: self.combo_repo.set("(Failed to load DB repos)"))

    def on_repo_selected(self, selected_text):
        if not selected_text or selected_text.startswith("("):
            return
            
        r_id = None
        for r in self.loaded_repos:
            # We displayed it as f"{path} ({id[:8]}...)"
            if r["path"] in selected_text:
                r_id = r["id"]
                break
                
        if r_id:
            self.lbl_loaded_status.configure(text=f"⏳ Loading businesses for {r_id}...", text_color="#F59E0B")
            threading.Thread(target=self._fetch_businesses_thread, args=(r_id,), daemon=True).start()

    def _fetch_businesses_thread(self, analysis_run_id):
        self._load_businesses_for_run(analysis_run_id)

    def _load_businesses_for_run(self, analysis_run_id):
        try:
            self.log(f"[*] Fetching businesses for Run ID: {analysis_run_id}...")
            url_businesses = f"https://localhost:55060/api/businesses?analysisRunId={analysis_run_id}"
            b_resp = requests.get(url_businesses, verify=False, timeout=60)
            
            if b_resp.status_code != 200:
                self.log(f"[ERROR] Fail to fetch businesses. Status {b_resp.status_code}")
                self.update_ui_after_analyze(success=False, msg="Failed to fetch businesses.")
                return
                
            businesses = b_resp.json()
            if not businesses:
                self.log("[WARNING] No businesses found for this repository.")
                self.update_ui_after_analyze(success=False, msg="No businesses found.")
                return
                
            # 3. Cập nhật giao diện
            self.loaded_businesses = []
            combo_values = []
            for b in businesses:
                b_id = b.get("id") or b.get("Id")
                b_name = b.get("businessName") or b.get("BusinessName")
                if b_id and b_name:
                    self.loaded_businesses.append({"name": b_name, "id": b_id})
                    combo_values.append(f"{b_name} ({b_id})")
                    
            if not combo_values:
                self.update_ui_after_analyze(success=False, msg="No valid businesses parsed.")
                return
                
            self.combo_business.configure(values=combo_values)
            self.combo_business.set(combo_values[0])
            self.update_ui_after_analyze(success=True, msg=f"✅ Successfully loaded {len(combo_values)} businesses.")
            
        except Exception as e:
            self.log(f"[ERROR] Exception during fetching businesses: {e}")
            self.update_ui_after_analyze(success=False, msg="Exception fetching businesses. See logs.")

    def analyze_repository(self):
        repo_path = self.entry_repo.get().strip()
        if not repo_path:
            messagebox.showwarning("Warning", "Please enter a GitHub URL or local folder path.")
            return
            
        self.btn_analyze.configure(state="disabled")
        self.entry_repo.configure(state="disabled")
        self.combo_repo.configure(state="disabled")
        self.set_status("Status: Analyzing Repository... (This may take a while)")
        self.lbl_loaded_status.configure(text="⏳ Analyzing Repository, extracting businesses...", text_color="#F59E0B")
        
        thread = threading.Thread(target=self._analyze_repo_thread, args=(repo_path,))
        thread.start()

    def _analyze_repo_thread(self, repo_path):
        try:
            # 1. Gọi API phân tích (clone, parse code, build CFG, lưu DB)
            url_analyze = "https://localhost:55060/api/analysis/analyze"
            payload = {"repositoryPath": repo_path}
            
            self.log(f"[*] Gửi yêu cầu phân tích repository: {repo_path}")
            resp = requests.post(url_analyze, json=payload, verify=False, timeout=600)
            
            if resp.status_code != 200:
                self.log(f"[ERROR] Fail to analyze repo. Status {resp.status_code}: {resp.text}")
                self.update_ui_after_analyze(success=False, msg="Failed to analyze repository.")
                return
                
            data = resp.json()
            analysis_run_id = data.get("analysisRunId") or data.get("AnalysisRunId")
            if not analysis_run_id:
                self.log("[ERROR] API returned 200 but missing AnalysisRunId.")
                self.update_ui_after_analyze(success=False, msg="Failed to get AnalysisRunId.")
                return
                
            self.log(f"[*] Phân tích thành công! AnalysisRunId: {analysis_run_id}")
            self.log(f"[*] Edges: {data.get('edgesCount', 0)}, Methods: {data.get('methodsCount', 0)}")
            
            # Fetch and update repo dropdown so the new repo appears
            self.fetch_existing_repos()
            
            # 2. Gọi API lấy danh sách Businesses
            self._load_businesses_for_run(analysis_run_id)
            
        except Exception as e:
            self.log(f"[ERROR] Exception during analysis: {e}")
            self.update_ui_after_analyze(success=False, msg="Exception during analysis. See logs.")

    def update_ui_after_analyze(self, success, msg):
        self.btn_analyze.configure(state="normal")
        self.entry_repo.configure(state="normal")
        self.combo_repo.configure(state="readonly")
        if success:
            self.set_status("Status: Ready")
            self.lbl_loaded_status.configure(text=msg, text_color="#10B981")
        else:
            self.set_status("Status: Analysis Failed")
            self.lbl_loaded_status.configure(text=f"⚠ {msg}", text_color="#EF4444")

    def get_selected_business_id(self):
        selected_text = self.combo_business.get()
        for b in self.loaded_businesses:
            if b["id"] in selected_text:
                return b["id"], b["name"]
        return None, None

    def start_benchmark_thread(self):
        if not self.loaded_businesses:
            messagebox.showwarning("Warning", "Please load business list first!")
            return
            
        b_id, b_name = self.get_selected_business_id()
        if not b_id:
            messagebox.showwarning("Warning", "Invalid selected business!")
            return
            
        answer = messagebox.askyesnocancel("Write Excel Data", "Do you want to APPEND to existing data (Yes)\nOr CLEAR ALL old data (No)?\n\n(Press Cancel to abort)")
        if answer is None:
            return
            
        clear_old_data = not answer  # True if No (Clear), False if Yes (Append)
            
        self.btn_run.configure(state="disabled")
        self.textbox_log.configure(state="normal")
        self.textbox_log.delete("1.0", "end")
        self.textbox_log.configure(state="disabled")
        
        # Start thread
        thread = threading.Thread(target=self.run_benchmark_workflow, args=(b_id, b_name, clear_old_data))
        thread.start()

    # --- GRAPH VIEWER LOGIC ---
    def open_graph_viewer(self):
        b_id, b_name = self.get_selected_business_id()
        if not b_id:
            messagebox.showwarning("Warning", "Please select a business first!")
            return
            
        mode = self.radio_var.get()
        if mode == "Traditional":
            messagebox.showinfo("Information", "Phương pháp Traditional (Code Base) không sử dụng Đồ Thị.")
            return
            
        self.set_status("Fetching Graph data from Backend...")
        thread = threading.Thread(target=self._generate_and_open_graph, args=(b_id, mode))
        thread.start()
        
    def _generate_and_open_graph(self, business_id, mode):
        try:
            nodes_json = "[]"
            edges_json = "[]"
            snippets_json = "[]"
            
            if mode == "CFG":
                # Lấy Đồ thị tổng quan (Macro Graph)
                url = f"https://localhost:55060/api/businesses/{business_id}/graph"
                try:
                    resp = requests.get(url, verify=False, timeout=30)
                    if resp.status_code != 200:
                        self.log(f"[ERROR] Failed to fetch Graph. Status: {resp.status_code}")
                        self.set_status("Error lấy Đồ thị")
                        return
                        
                    data = resp.json()
                    nodes = data.get("nodes", [])
                    edges = data.get("edges", [])
                    
                    if not nodes:
                        self.log("[WARNING] No Nodes in Graph.")
                        self.set_status("Graph is empty!")
                        return
                        
                    vis_nodes = []
                    for n in nodes:
                        ntype = n.get("type", "")
                        if ntype == "StartEvent":
                            color = {"background": "#10B981", "border": "#059669"} # Emerald
                            size = 20
                        elif ntype == "EndEvent":
                            color = {"background": "#EF4444", "border": "#B91C1C"} # Red
                            size = 20
                        else:
                            color = {"background": "#6366F1", "border": "#4F46E5"} # Indigo (Tasks)
                            size = 15
                        vis_nodes.append({
                            "id": n["id"],
                            "label": n.get("name") or ntype,
                            "title": n.get("description", ""),
                            "color": color,
                            "shape": "box",
                            "margin": 10,
                            "font": {"color": "#1e293b", "face": "Inter, sans-serif", "size": 16}
                        })
                        
                    vis_edges = []
                    for e in edges:
                        vis_edges.append({
                            "from": e["fromNodeId"],
                            "to": e["toNodeId"],
                            "label": e.get("condition", ""),
                            "arrows": {"to": {"enabled": True, "scaleFactor": 1.2}},
                            "color": {"color": "#94a3b8", "highlight": "#3b82f6", "hover": "#3b82f6"},
                            "font": {"size": 12, "color": "#475569", "face": "Inter, sans-serif", "align": "horizontal", "background": "#ffffff"},
                            "smooth": {"type": "cubicBezier", "forceDirection": "vertical", "roundness": 0.4}
                        })
                    
                    nodes_json = json.dumps(vis_nodes)
                    edges_json = json.dumps(vis_edges)
                except Exception as e:
                    self.log(f"[LỖI] Exception when calling graph API: {e}")
                    return
            else:
                # Mode E2E: Lấy Micro CFG và Critical Snippets từ Tầng 2
                url = f"https://localhost:55060/api/businesses/{business_id}/hybrid-context"
                try:
                    resp = requests.get(url, verify=False, timeout=60)
                    if resp.status_code != 200:
                        self.log(f"[ERROR] Failed to fetch Hybrid Context. Status: {resp.status_code}")
                        self.log(f"Details: {resp.text}")
                        self.set_status("Error lấy Hybrid Context")
                        return
                        
                    data = resp.json()
                    cfg_nodes = data.get("cfgNodes", [])
                    cfg_edges = data.get("cfgEdges", [])
                    snippets = data.get("criticalSnippetDetails", [])
                    
                    if not cfg_nodes:
                        self.log("[WARNING] No Nodes in Hybrid CFG.")
                        self.set_status("Graph is empty!")
                        return
                        
                    vis_nodes = []
                    for n in cfg_nodes:
                        nkind = n.get("kind", "")
                        if nkind == "START":
                            color = {"background": "#10B981", "border": "#059669"}
                            size = 20
                        elif nkind == "END" or nkind == "THROW" or nkind == "RETURN":
                            color = {"background": "#EF4444", "border": "#B91C1C"}
                            size = 20
                        elif nkind == "DECISION":
                            color = {"background": "#F59E0B", "border": "#D97706"} # Amber cho rẻ nhánh
                            size = 18
                        else:
                            color = {"background": "#6366F1", "border": "#4F46E5"} # Indigo
                            size = 15
                            
                        vis_nodes.append({
                            "id": n["id"],
                            "label": n.get("label") or nkind,
                            "title": n.get("code", ""),
                            "color": color,
                            "shape": "dot",
                            "size": size,
                            "font": {"color": "#1e293b", "face": "Inter, sans-serif", "size": 14}
                        })
                        
                    vis_edges = []
                    for e in cfg_edges:
                        vis_edges.append({
                            "from": e.get("from", ""),
                            "to": e.get("to", ""),
                            "label": e.get("label", ""),
                            "arrows": {"to": {"enabled": True, "scaleFactor": 0.5}},
                            "color": {"color": "#94a3b8", "highlight": "#64748b"},
                            "font": {"size": 11, "color": "#64748b", "face": "Inter, sans-serif", "align": "middle"},
                            "smooth": {"type": "continuous"}
                        })
                        
                    nodes_json = json.dumps(vis_nodes)
                    edges_json = json.dumps(vis_edges)
                    snippets_json = json.dumps(snippets)
                except Exception as e:
                    self.log(f"[LỖI] Exception when calling hybrid-context API: {e}")
                    return

            # HTML Template sử dụng Vis.js CDN
            title_text = "Layer 1 Graph (Pure CFG - Macro)" if mode == "CFG" else "Layer 2 Graph (Hybrid Micro CFG)"
            html_content = f"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>Graph Viewer</title>
                <script type="text/javascript" src="https://unpkg.com/vis-network/standalone/umd/vis-network.min.js"></script>
                <style type="text/css">
                    body {{ margin: 0; padding: 0; display: flex; height: 100vh; font-family: sans-serif; }}
                    #graph-container {{ flex: 1; position: relative; border-right: 2px solid #ccc; background-color: #ffffff; }}
                    #mynetwork {{ width: 100%; height: 100%; }}
                    #codeview {{ flex: 1; padding: 20px; overflow-y: auto; background-color: #f8f9fa; display: {'block' if mode == "E2E" else 'none'}; }}
                    pre {{ background: #272822; color: #f8f8f2; padding: 15px; border-radius: 5px; font-family: Consolas, monospace; overflow-x: auto; white-space: pre-wrap; }}
                    .title-box {{ position: absolute; top: 10px; left: 10px; z-index: 1000; background: white; padding: 5px 10px; border-radius: 5px; box-shadow: 0 0 5px rgba(0,0,0,0.2); font-weight: bold; }}
                    .empty-state {{ color: #9ca3af; font-style: italic; margin-top: 20px; }}
                </style>
            </head>
            <body>
                <div id="graph-container">
                    <div id="mynetwork"></div>
                    <div class="title-box">{title_text}</div>
                    <div id="tour-controls" style="position: absolute; top: 10px; left: 50%; transform: translateX(-50%); z-index: 1000; background: white; padding: 10px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); display: flex; gap: 10px; align-items: center;">
                        <select id="pathSelector" onchange="selectPath()" style="padding: 5px; border-radius: 4px; border: 1px solid #ccc; font-weight: bold; cursor: pointer;"></select>
                        <button id="btnPrev" onclick="tourPrev()" style="padding: 5px 10px; cursor: pointer; border: 1px solid #ccc; border-radius: 4px; background: #f8f9fa;">⬅ Prev Node</button>
                        <span id="tourStatus" style="font-weight: bold; font-family: sans-serif; color: #334155; min-width: 100px; text-align: center;">Overview</span>
                        <button id="btnNext" onclick="tourNext()" style="padding: 5px 10px; cursor: pointer; border: 1px solid #ccc; border-radius: 4px; background: #f8f9fa;">Next Node ➡</button>
                    </div>
                </div>
                <div id="codeview">
                    <h3>Critical Snippets Included</h3>
                    <div id="code-content">
                        <div class="empty-state">👉 Click on a Node in the graph to view the attached critical snippet (if any).</div>
                    </div>
                </div>
                
                <script type="text/javascript">
                    var nodes = new vis.DataSet({nodes_json});
                    var edges = new vis.DataSet({edges_json});
                    var snippets = {snippets_json};
                    
                    var container = document.getElementById('mynetwork');
                    var data = {{ nodes: nodes, edges: edges }};
                    var options = {{
                        layout: {{
                            hierarchical: {{
                                direction: 'UD',          // Up-Down
                                sortMethod: 'directed',   // Follow edge direction
                                shakeTowards: 'roots',    // Giữ layout không dẹt
                                levelSeparation: 150,     // Vertical space between nodes
                                nodeSpacing: 250,         // Horizontal space
                                treeSpacing: 250
                            }}
                        }},
                        interaction: {{
                            hover: true,
                            tooltipDelay: 200,
                            zoomView: true,
                            dragView: true,
                            hoverConnectedEdges: true,
                            selectConnectedEdges: true
                        }},
                        physics: {{
                            enabled: false // Tắt physics để các Node không bị chạy lộn xộn
                        }}
                    }};
                    var network = new vis.Network(container, data, options);
                    
                    // Tính năng Smart Path Tracing
                    var allPaths = [];
                    var currentPathIndex = 0;
                    var currentStepIndex = -1;
                    
                    network.once("afterDrawing", function() {{
                        var adj = {{}};
                        var inDegree = {{}};
                        var allNodeIds = nodes.getIds();
                        var rawEdges = edges.get();
                        
                        allNodeIds.forEach(id => {{
                            adj[id] = [];
                            inDegree[id] = 0;
                        }});
                        
                        rawEdges.forEach(e => {{
                            if (adj[e.from]) {{
                                adj[e.from].push(e.to);
                                if (inDegree[e.to] !== undefined) inDegree[e.to]++;
                            }}
                        }});
                        
                        var startNodes = allNodeIds.filter(id => inDegree[id] === 0);
                        if (startNodes.length === 0 && allNodeIds.length > 0) startNodes = [allNodeIds[0]];
                        
                        function dfs(currentNode, currentPath, visited) {{
                            if (allPaths.length > 30) return; // Limit paths to avoid UI freeze
                            
                            currentPath.push(currentNode);
                            visited.add(currentNode);
                            
                            var neighbors = adj[currentNode] || [];
                            if (neighbors.length === 0) {{
                                allPaths.push([...currentPath]);
                            }} else {{
                                for (var i = 0; i < neighbors.length; i++) {{
                                    var nextNode = neighbors[i];
                                    if (!visited.has(nextNode)) {{
                                        dfs(nextNode, [...currentPath], new Set(visited));
                                    }} else {{
                                        var cyclePath = [...currentPath, nextNode];
                                        allPaths.push(cyclePath);
                                    }}
                                }}
                            }}
                        }}
                        
                        startNodes.forEach(startNode => {{
                            dfs(startNode, [], new Set());
                        }});
                        
                        if (allPaths.length === 0) allPaths.push(allNodeIds); // Fallback
                        
                        var selector = document.getElementById("pathSelector");
                        selector.innerHTML = "";
                        allPaths.forEach((p, idx) => {{
                            var opt = document.createElement("option");
                            opt.value = idx;
                            opt.innerHTML = "Luồng " + (idx + 1) + " (" + p.length + " nodes)";
                            selector.appendChild(opt);
                        }});
                    }});
                    
                    window.selectPath = function() {{
                        var selector = document.getElementById("pathSelector");
                        currentPathIndex = parseInt(selector.value);
                        currentStepIndex = -1;
                        updateTourView();
                    }};
                    
                    window.updateTourView = function() {{
                        if (allPaths.length === 0) return;
                        var currentPathNodes = allPaths[currentPathIndex];
                        
                        if (currentStepIndex === -1) {{
                            document.getElementById("tourStatus").innerText = "Overview";
                            network.fit({{animation: {{duration: 800}}}});
                            network.unselectAll();
                        }} else {{
                            document.getElementById("tourStatus").innerText = "Node " + (currentStepIndex + 1) + " / " + currentPathNodes.length;
                            var activeNodeId = currentPathNodes[currentStepIndex];
                            
                            network.focus(activeNodeId, {{
                                scale: 1.2,
                                animation: {{ duration: 500 }}
                            }});
                            network.selectNodes([activeNodeId]);
                        }}
                    }};
                    
                    window.tourNext = function() {{
                        if (allPaths.length === 0) return;
                        var currentPathNodes = allPaths[currentPathIndex];
                        if (currentStepIndex < currentPathNodes.length - 1) {{
                            currentStepIndex++;
                            updateTourView();
                        }}
                    }};
                    
                    window.tourPrev = function() {{
                        if (currentStepIndex > -1) {{
                            currentStepIndex--;
                            updateTourView();
                        }}
                    }};
                    // Xử lý sự kiện click trên Node
                    network.on("click", function (params) {{
                        if (params.nodes.length > 0) {{
                            var nodeId = params.nodes[0];
                            var codeContent = document.getElementById('code-content');
                            
                            // Tìm snippet tương ứng với Node Id
                            var matchingSnippets = snippets.filter(s => s.nodeId === nodeId || s.NodeId === nodeId);
                            
                            if (matchingSnippets.length > 0) {{
                                var html = '';
                                matchingSnippets.forEach(function(s) {{
                                    var kind = s.kind || s.Kind || 'Code';
                                    var reason = s.reason || s.Reason || '';
                                    var code = s.code || s.Code || '';
                                    
                                    code = code.replace(/</g, '&lt;').replace(/>/g, '&gt;');
                                    
                                    html += `<div style="margin-bottom: 20px;">
                                                <div style="background: #e2e8f0; padding: 5px 10px; border-radius: 5px 5px 0 0; font-weight: bold; color: #334155; font-size: 13px;">
                                                    Type: ${{kind}} | Reason: ${{reason}}
                                                </div>
                                                <pre style="margin-top: 0; border-radius: 0 0 5px 5px;"><code>${{code}}</code></pre>
                                             </div>`;
                                }});
                                codeContent.innerHTML = html;
                            }} else {{
                                codeContent.innerHTML = '<div class="empty-state">No critical snippet attached to this Node.</div>';
                            }}
                        }}
                    }});
                </script>
            </body>
            </html>
            """
            
            import time
            file_name = f"graph_viewer_{business_id}_{int(time.time())}.html"
            with open(file_name, "w", encoding="utf-8") as f:
                f.write(html_content)
                
            webbrowser.open(f"file://{os.path.abspath(file_name)}")
            self.set_status("Graph opened in browser!")
            self.log(f"Created and opened graph file: {file_name}")
            
        except Exception as e:
            self.log(f"[ERROR] Could not generate graph: {str(e)}")
            self.set_status("Error tạo đồ thị!")

    # --- BENCHMARK WORKFLOW LOGIC ---

    def post_with_retry(self, url, payload, step_name, mode, validate=None):
        """
        Goi POST co kiem tra loi day du + TU DONG CHAY LAI khi API loi tam thoi.

        Khac biet so voi ban cu:
          - Co timeout (truoc day khong co -> treo vo han).
          - Kiem tra HTTP status (truoc day HTTP 500/429 van bi coi la thanh cong).
          - Kiem tra noi dung tra ve (truoc day response rong -> am tham ghi so 0).
          - Het so lan thu thi NEM LOI, KHONG BAO GIO tra ve du lieu rong.

        Thoi gian cua lan goi thanh cong duoc luu vao self.last_call_ms
        (khong tinh thoi gian nghi giua cac lan thu).
        """
        last_error = "khong ro"

        for attempt in range(1, MAX_RETRIES + 1):
            try:
                t0 = time.time()
                resp = requests.post(url, json=payload, verify=False, timeout=REQUEST_TIMEOUT)
                elapsed_ms = int((time.time() - t0) * 1000)

                # --- 1. Kiem tra ma HTTP ---
                if resp.status_code != 200:
                    last_error = "HTTP %d: %s" % (resp.status_code, resp.text[:300])

                    if resp.status_code == 429:
                        retry_after = resp.headers.get("Retry-After", "")
                        wait = int(retry_after) if retry_after.isdigit() else RATE_LIMIT_WAIT
                        self.log("   [!] %s (%s) bi RATE LIMIT (429) - lan %d/%d."
                                 % (step_name, mode, attempt, MAX_RETRIES))
                        if attempt >= MAX_RETRIES:
                            break
                        self.log("       -> Nghi %ds roi CHAY LAI buoc nay..." % wait)
                        self.set_status("Trang thai: Bi rate limit, cho %ds roi thu lai..." % wait)
                        time.sleep(wait)
                        continue

                    if resp.status_code in (500, 502, 503, 504):
                        raise requests.exceptions.RequestException(last_error)

                    # 4xx khac (400 sai payload, 404 sai endpoint...) -> thu lai vo ich
                    raise RuntimeError("[%s] %s that bai VINH VIEN - %s" % (mode, step_name, last_error))

                # --- 2. Parse JSON ---
                data = resp.json()

                # --- 3. Kiem tra noi dung ---
                if validate is not None:
                    problem = validate(data)
                    if problem:
                        last_error = "%s | Response: %s" % (problem, str(data)[:300])
                        raise requests.exceptions.RequestException(last_error)

                if attempt > 1:
                    self.log("   [OK] %s (%s) da thanh cong o lan thu %d." % (step_name, mode, attempt))
                self.last_call_ms = elapsed_ms
                return data

            except RuntimeError:
                raise  # loi vinh vien -> khong thu lai nua
            except Exception as e:
                last_error = str(e)[:300]
                if attempt >= MAX_RETRIES:
                    break
                wait = RETRY_WAIT[min(attempt - 1, len(RETRY_WAIT) - 1)]
                self.log("   [!] %s (%s) LOI lan %d/%d: %s"
                         % (step_name, mode, attempt, MAX_RETRIES, last_error))
                self.log("       -> Nghi %ds roi CHAY LAI buoc nay..." % wait)
                self.set_status("Trang thai: Loi API, dang thu lai lan %d..." % (attempt + 1))
                time.sleep(wait)

        raise RuntimeError(
            "[%s] %s that bai sau %d lan thu. Loi cuoi: %s"
            % (mode, step_name, MAX_RETRIES, last_error)
        )

    def run_pipeline(self, api_url, business_id, num_questions, difficulty, mode):
        self.log(f"\n--- Bắt đầu Pipeline {mode} ---")
        self.set_status(f"Status: Khởi động Pipeline {mode}...")
        self.set_progress(0.1)
        
        if mode == "Traditional":
            generate_endpoint = f"{api_url}/api/QuestionGenerator/generate-traditional"
        elif mode == "E2E":
            generate_endpoint = f"{api_url}/api/QuestionGenerator/generate-e2e"
        else:
            generate_endpoint = f"{api_url}/api/QuestionGenerator/generate-graph"
        
        self.set_status(f"Status: Đang sinh {num_questions} câu hỏi ({mode})...")
        self.log(f"1. Calling QuestionGenerator ({mode})...")
        start_time = time.time()
        gen_payload = {
            "businessId": business_id,
            "numberOfQuestions": num_questions,
            "difficulty": difficulty,
            "mode": "Traditional" if mode == "Traditional" else ("E2E" if mode == "E2E" else "Graph")
        }
        
        # Buoc 1: sinh cau hoi (tu dong chay lai neu Gemini loi)
        gen_res = self.post_with_retry(
            generate_endpoint, gen_payload,
            "Buoc 1 - Sinh cau hoi", mode, validate=_validate_generate
        )
        gen_time = self.last_call_ms

        questions = gen_res.get("generatedQuestionDtos", gen_res.get("GeneratedQuestionDtos", []))
        self.log(f"   -> Đã sinh thành công {len(questions)} câu hỏi trong {gen_time}ms.")
        self.set_progress(0.3)

        self.set_status(f"Status: Đang chấm điểm Coverage ({mode})...")
        self.log(f"2. Calling Coverage Assessment ({mode})...")
        cov_res = self.post_with_retry(
            f"{api_url}/api/WorkflowAssessment/assess-from-response", gen_res,
            "Buoc 2 - Coverage", mode, validate=_validate_assessment
        )
        self.log(f"   -> Hoàn thành Coverage Assessment.")
        self.set_progress(0.5)

        self.set_status(f"Status: Đang chấm điểm Accuracy ({mode})...")
        self.log(f"3. Calling Accuracy Assessment ({mode})...")
        acc_res = self.post_with_retry(
            f"{api_url}/api/WorkflowAssessment/assess-accuracy", gen_res,
            "Buoc 3 - Accuracy", mode, validate=_validate_assessment
        )
        self.log(f"   -> Hoàn thành Accuracy Assessment.")
        self.set_progress(0.7)

        self.set_status(f"Status: Đang đánh giá Độ khó ({mode})...")
        self.log(f"4. Calling Difficulty Assessment ({mode})...")
        diff_res = self.post_with_retry(
            f"{api_url}/api/WorkflowAssessment/assess-difficulty", gen_res,
            "Buoc 4 - Difficulty", mode, validate=_validate_assessment
        )
        self.log(f"   -> Hoàn thành Difficulty Assessment.")
        self.set_progress(0.9)
        
        self.log(f"Hoàn tất Pipeline {mode}.")
        self.set_status(f"Status: Đã hoàn tất Pipeline {mode}.")
        return gen_time, gen_res, cov_res, acc_res, diff_res

    def assemble_results(self, gen_time, gen_res, cov_res, acc_res, diff_res):
        questions_list = gen_res.get("generatedQuestionDtos", gen_res.get("GeneratedQuestionDtos", []))
        details = []
        
        for q in questions_list:
            q_text = q.get("question", q.get("Question", ""))
            cov = next((c for c in cov_res.get("questionResults", []) if c.get("question") == q_text), {})
            acc = next((a for a in acc_res.get("questionResults", []) if a.get("question") == q_text), {})
            acc_result = acc.get("accuracyResult", {})
            diff = next((d for d in diff_res.get("questionResults", []) if d.get("question") == q_text), {})
            diff_result = diff.get("difficultyResult", {})
            
            details.append({
                "question": q_text,
                "coverage": cov.get("coverage", 0),
                "activeNodes": cov.get("activeNodeCount", 0),
                "isAccurate": acc_result.get("isAccurate", False),
                "cyclomatic": diff_result.get("cyclomaticComplexity", 0),
                "evaluationNotes": acc_result.get("finalVerdict", "")
            })
            
        avg_coverage = cov_res.get("averageTotalCoverage", 0)
        avg_active_nodes = sum(d["activeNodes"] for d in details) / len(details) if details else 0
        accuracy_rate = sum(1 for d in details if d["isAccurate"]) / len(details) if details else 0
        avg_complexity = diff_res.get("averageCyclomaticComplexity", sum(diff.get("difficultyResult", {}).get("cyclomaticComplexity", 0) for diff in diff_res.get("questionResults", [])) / len(details) if details else 0)

        return {
            "time": gen_time,
            "inputTokens": gen_res.get("inputTokens", 0),
            "outputTokens": gen_res.get("outputTokens", 0),
            "coverage": avg_coverage,
            "activeNodes": int(avg_active_nodes),
            "accuracy": accuracy_rate,
            "complexity": avg_complexity,
            "details": details,
            "businessName": gen_res.get("businessName", "")
        }

    def run_benchmark_workflow(self, business_id, business_name, clear_old_data=False):
        method = self.radio_var.get()
        num_questions = int(self.combo_num.get())
        difficulty = self.combo_difficulty.get()
        api_url = "https://localhost:55060"
        
        # Auto-generate Run ID based on timestamp
        run_id = datetime.datetime.now().strftime("RUN_%d%m_%H%M")
        
        self.log(f"=== BẮT ĐẦU BENCHMARK ===")
        self.log(f"Nghiệp vụ: {business_name}")
        self.log(f"Phương pháp: {method}")
        self.log(f"Mã Run ID tự động: {run_id}")
        if clear_old_data:
            self.log(f"[CHÚ Ý] Sẽ XÓA TOÀN BỘ dữ liệu cũ trước khi ghi.")
            
        self.set_progress(0.0)
        self.set_status("Status: Chuẩn bị chạy...")
        
        results = {
            "runId": run_id,
            "businessName": business_name,
            "difficulty": difficulty
        }
        
        raw_logs = {
            "runId": run_id,
            "businessName": business_name,
            "timestamp": datetime.datetime.now().isoformat(),
            "method": method,
            "difficulty": difficulty,
            "requestedQuestions": num_questions,
            "traditional": None,
            "cfg": None,
            "e2e": None
        }
        
        try:
            if method in ["All", "Traditional"]:
                t_time, t_gen, t_cov, t_acc, t_diff = self.run_pipeline(api_url, business_id, num_questions, difficulty, "Traditional")
                t_data = self.assemble_results(t_time, t_gen, t_cov, t_acc, t_diff)
                
                results["tradTime"] = t_data["time"]
                results["tradInputTokens"] = t_data["inputTokens"]
                results["tradOutputTokens"] = t_data["outputTokens"]
                results["tradCoverage"] = t_data["coverage"]
                results["tradActiveNodes"] = t_data["activeNodes"]
                results["tradAccuracy"] = t_data["accuracy"]
                results["tradComplexity"] = t_data["complexity"]
                results["tradDetails"] = t_data["details"]
                
                raw_logs["traditional"] = {
                    "generate": t_gen,
                    "coverage": t_cov,
                    "accuracy": t_acc,
                    "difficulty": t_diff
                }

            if method in ["All", "CFG"]:
                c_time, c_gen, c_cov, c_acc, c_diff = self.run_pipeline(api_url, business_id, num_questions, difficulty, "Graph-based (CFG)")
                c_data = self.assemble_results(c_time, c_gen, c_cov, c_acc, c_diff)
                
                results["cfgTime"] = c_data["time"]
                results["cfgInputTokens"] = c_data["inputTokens"]
                results["cfgOutputTokens"] = c_data["outputTokens"]
                results["cfgCoverage"] = c_data["coverage"]
                results["cfgActiveNodes"] = c_data["activeNodes"]
                results["cfgAccuracy"] = c_data["accuracy"]
                results["cfgComplexity"] = c_data["complexity"]
                results["cfgDetails"] = c_data["details"]
                
                raw_logs["cfg"] = {
                    "generate": c_gen,
                    "coverage": c_cov,
                    "accuracy": c_acc,
                    "difficulty": c_diff
                }

            if method in ["All", "E2E"]:
                e_time, e_gen, e_cov, e_acc, e_diff = self.run_pipeline(api_url, business_id, num_questions, difficulty, "E2E")
                e_data = self.assemble_results(e_time, e_gen, e_cov, e_acc, e_diff)
                
                results["e2eTime"] = e_data["time"]
                results["e2eInputTokens"] = e_data["inputTokens"]
                results["e2eOutputTokens"] = e_data["outputTokens"]
                results["e2eCoverage"] = e_data["coverage"]
                results["e2eActiveNodes"] = e_data["activeNodes"]
                results["e2eAccuracy"] = e_data["accuracy"]
                results["e2eComplexity"] = e_data["complexity"]
                results["e2eDetails"] = e_data["details"]
                
                raw_logs["e2e"] = {
                    "generate": e_gen,
                    "coverage": e_cov,
                    "accuracy": e_acc,
                    "difficulty": e_diff
                }
                
            # DUMP RAW LOGS TO JSON
            log_dir = "benchmark_logs"
            if not os.path.exists(log_dir):
                os.makedirs(log_dir)
            log_filename = os.path.join(log_dir, f"{run_id}_Details.json")
            with open(log_filename, "w", encoding="utf-8") as f:
                json.dump(raw_logs, f, ensure_ascii=False, indent=4)
            self.log(f"\nĐã lưu log chi tiết vào: {log_filename}")
                
        except Exception as e:
            self.log(f"\n[LỖI API] Đã có lỗi xảy ra: {str(e)}")
            self.set_status("Status: LỖI")
            self.btn_run.configure(state="normal")
            return

        self.set_progress(1.0)
        self.set_status("Status: Đang ghi dữ liệu vào Excel...")
        self.log("\nTiến hành ghi dữ liệu vào Excel...")
        self.save_to_excel(results, method, clear_old_data)
        
        self.btn_run.configure(state="normal")
        self.set_status("Status: HOÀN TẤT!")
        self.log("\n✅ BENCHMARK HOÀN TẤT THÀNH CÔNG!")
        
    def save_to_excel(self, results, method, clear_old_data=False):
        excel_path = "CFG_vs_Traditional_Benchmark_Template.xlsx"
        
        # Try finding it in parent directories just in case
        if not os.path.exists(excel_path):
            current = os.path.abspath(os.path.curdir)
            while current and current != os.path.dirname(current):
                candidate = os.path.join(current, "CFG_vs_Traditional_Benchmark_Template.xlsx")
                if os.path.exists(candidate):
                    excel_path = candidate
                    break
                current = os.path.dirname(current)

        try:
            if not os.path.exists(excel_path):
                self.log(f"[THÔNG BÁO] Không tìm thấy file excel {excel_path}. Đang tự động tạo file mới...")
                wb = openpyxl.Workbook()
            else:
                wb = openpyxl.load_workbook(excel_path)
            
            # --- RENAME SHEETS IF NECESSARY ---
            if "Báo cáo So sánh (Dashboard)" in wb.sheetnames:
                wb["Báo cáo So sánh (Dashboard)"].title = "Báo cáo"
            if "Thử nghiệm" in wb.sheetnames:
                wb["Thử nghiệm"].title = "Run"

            # --- ALWAYS REBUILD BÁO CÁO SHEET ---
            if "Báo cáo" not in wb.sheetnames:
                ws_report = wb.create_sheet("Báo cáo", 0)
            else:
                ws_report = wb["Báo cáo"]
                ws_report.delete_rows(1, ws_report.max_row) # Clear it completely
                
            ws_report.append(['BÁO CÁO KẾT QUẢ THỬ NGHIỆM SO SÁNH 3 PHƯƠNG PHÁP: TRADITIONAL VS CFG VS E2E'])
            ws_report.append([])
            ws_report.append(['Hạng mục Đánh giá (Metrics)', 'Đơn vị', 'Code Thô (Traditional)', 'Đồ thị (CFG)', 'E2E (Hybrid 3-Tier)', 'E2E vs Trad (Delta)', 'E2E vs CFG (Delta)', 'Ghi chú & Nhận xét'])
            ws_report.append(['Độ bao phủ trung bình (Average Total Coverage)', '%', '', '', '', '=E4-C4', '=E4-D4', 'Chỉ số từ API assess-from-response'])
            ws_report.append(['Độ bao phủ theo Workflow (Coverage Workflow/Global)', '%', '', '', '', '=E5-C5', '=E5-D5', 'Tỷ lệ nút workflow được chạm đến'])
            ws_report.append(['Độ chính xác trung bình (Average Accuracy Rate)', '%', '', '', '', '=E6-C6', '=E6-D6', 'Chỉ số từ API assess-accuracy'])
            ws_report.append(['Thời gian sinh câu hỏi trung bình (Avg Gen Time)', 'ms', '', '', '', '=E7-C7', '=E7-D7', 'Thời gian Postman nhận Response'])
            ws_report.append(['Tổng Token đầu vào (Total Input Tokens)', 'tokens', '', '', '', '=E8-C8', '=E8-D8', 'Token truyền vào Gemini AI'])
            ws_report.append(['Tổng Token đầu ra (Total Output Tokens)', 'tokens', '', '', '', '=E9-C9', '=E9-D9', 'Token Gemini AI phản hồi'])
            ws_report.append(['Số câu hỏi hợp lệ / Đúng logic', 'câu', '', '', '', '=E10-C10', '=E10-D10', 'Số câu không vi phạm logic code'])
            ws_report.append(['Độ phức tạp Cyclomatic Avg (Số cạnh active)', 'cạnh', '', '', '', '=E11-C11', '=E11-D11', 'Chỉ số từ API assess-difficulty'])
                
            # --- UPDATE BÁO CÁO FORMULAS ---
            if "Báo cáo" in wb.sheetnames:
                ws_report = wb["Báo cáo"]
                # Traditional (Column C)
                ws_report["C4"] = '=AVERAGEIFS(Run!H:H, Run!D:D, "Traditional")'
                ws_report["C5"] = '=AVERAGEIFS(Run!H:H, Run!D:D, "Traditional")' # Coverage Workflow (Same as above)
                ws_report["C6"] = '=AVERAGEIFS(Run!K:K, Run!D:D, "Traditional")'
                ws_report["C7"] = '=AVERAGEIFS(Run!E:E, Run!D:D, "Traditional")'
                ws_report["C8"] = '=SUMIFS(Run!F:F, Run!D:D, "Traditional")'
                ws_report["C9"] = '=SUMIFS(Run!G:G, Run!D:D, "Traditional")'
                ws_report["C10"] = '=COUNTIFS(Run!D:D, "Traditional", Run!J:J, "Đúng")'
                ws_report["C11"] = '=AVERAGEIFS(Run!L:L, Run!D:D, "Traditional")'
                
                # CFG (Column D)
                ws_report["D4"] = '=AVERAGEIFS(Run!H:H, Run!D:D, "Graph-based (CFG)")'
                ws_report["D5"] = '=AVERAGEIFS(Run!H:H, Run!D:D, "Graph-based (CFG)")'
                ws_report["D6"] = '=AVERAGEIFS(Run!K:K, Run!D:D, "Graph-based (CFG)")'
                ws_report["D7"] = '=AVERAGEIFS(Run!E:E, Run!D:D, "Graph-based (CFG)")'
                ws_report["D8"] = '=SUMIFS(Run!F:F, Run!D:D, "Graph-based (CFG)")'
                ws_report["D9"] = '=SUMIFS(Run!G:G, Run!D:D, "Graph-based (CFG)")'
                ws_report["D10"] = '=COUNTIFS(Run!D:D, "Graph-based (CFG)", Run!J:J, "Đúng")'
                ws_report["D11"] = '=AVERAGEIFS(Run!L:L, Run!D:D, "Graph-based (CFG)")'
                
                # E2E (Column E)
                ws_report["E4"] = '=AVERAGEIFS(Run!H:H, Run!D:D, "E2E (Kết hợp 3 Tầng)")'
                ws_report["E5"] = '=AVERAGEIFS(Run!H:H, Run!D:D, "E2E (Kết hợp 3 Tầng)")'
                ws_report["E6"] = '=AVERAGEIFS(Run!K:K, Run!D:D, "E2E (Kết hợp 3 Tầng)")'
                ws_report["E7"] = '=AVERAGEIFS(Run!E:E, Run!D:D, "E2E (Kết hợp 3 Tầng)")'
                ws_report["E8"] = '=SUMIFS(Run!F:F, Run!D:D, "E2E (Kết hợp 3 Tầng)")'
                ws_report["E9"] = '=SUMIFS(Run!G:G, Run!D:D, "E2E (Kết hợp 3 Tầng)")'
                ws_report["E10"] = '=COUNTIFS(Run!D:D, "E2E (Kết hợp 3 Tầng)", Run!J:J, "Đúng")'
                ws_report["E11"] = '=AVERAGEIFS(Run!L:L, Run!D:D, "E2E (Kết hợp 3 Tầng)")'

            # --- SUMMARY / RUN SHEET ---
            if "Run" not in wb.sheetnames:
                ws_run = wb.create_sheet("Run")
            else:
                ws_run = wb["Run"]
                
            # Always force correct headers on row 3
            run_headers = [
                "STT Câu", "RUN_ID", "Tên Business", "Phương pháp", 
                "Thời gian gen", "Input Tokens", "Output Tokens", 
                "Coverage (%)", "Active Node Count", "Accuracy", 
                "Accuracy Rate", "Cyclomatic"
            ]
            for col_idx, header in enumerate(run_headers, 1):
                ws_run.cell(row=3, column=col_idx, value=header)
            
            # Clear old extra columns if any
            for col_idx in range(len(run_headers) + 1, ws_run.max_column + 1):
                ws_run.cell(row=3, column=col_idx, value="")
                
            if clear_old_data and ws_run.max_row >= 4:
                ws_run.delete_rows(4, ws_run.max_row - 3)
                
            def write_run_sheet(mode, results_dict, details_list):
                run_row = -1
                for r in range(4, max(5, ws_run.max_row + 5)):
                    if not ws_run.cell(row=r, column=1).value:
                        run_row = r
                        break
                if run_row == -1: run_row = ws_run.max_row + 1
                
                stt = 1
                for q in details_list:
                    ws_run.cell(row=run_row, column=1, value=stt)
                    mode_label = "TRAD" if "Trad" in mode else ("E2E" if "E2E" in mode else "CFG")
                    ws_run.cell(row=run_row, column=2, value=f"{results['runId']}_{mode_label}")
                    ws_run.cell(row=run_row, column=3, value=results['businessName'])
                    ws_run.cell(row=run_row, column=4, value=mode)
                    
                    prefix = "trad" if "Trad" in mode else ("e2e" if "E2E" in mode else "cfg")
                    ws_run.cell(row=run_row, column=5, value=results_dict.get(f"{prefix}Time", 0))
                    ws_run.cell(row=run_row, column=6, value=results_dict.get(f"{prefix}InputTokens", 0))
                    ws_run.cell(row=run_row, column=7, value=results_dict.get(f"{prefix}OutputTokens", 0))
                    ws_run.cell(row=run_row, column=8, value=q.get("coverage", 0))
                    ws_run.cell(row=run_row, column=9, value=q.get("activeNodes", 0))
                    ws_run.cell(row=run_row, column=10, value="Đúng" if q.get("isAccurate", False) else "Sai")
                    ws_run.cell(row=run_row, column=11, value=results_dict.get(f"{prefix}Accuracy", 0))
                    ws_run.cell(row=run_row, column=12, value=q.get("cyclomatic", 0))
                    
                    run_row += 1
                    stt += 1

            if method in ["All", "Traditional"]:
                write_run_sheet("Traditional", results, results.get("tradDetails", []))
            if method in ["All", "CFG"]:
                write_run_sheet("Graph-based (CFG)", results, results.get("cfgDetails", []))
            if method in ["All", "E2E"]:
                write_run_sheet("E2E (Kết hợp 3 Tầng)", results, results.get("e2eDetails", []))

            # --- DETAILS SHEET ---
            if "Chi tiết Câu hỏi" not in wb.sheetnames:
                ws_details = wb.create_sheet("Chi tiết Câu hỏi")
            else:
                ws_details = wb["Chi tiết Câu hỏi"]
                
            # Always force correct headers on row 3
            details_headers = ["RUN_ID", "Phương Pháp", "STT Câu", "Nội dung câu hỏi"]
            for col_idx, header in enumerate(details_headers, 1):
                ws_details.cell(row=3, column=col_idx, value=header)
                
            # Clear old extra columns if any (like Gateways, etc)
            for col_idx in range(len(details_headers) + 1, ws_details.max_column + 1):
                ws_details.cell(row=3, column=col_idx, value="")
                # Clear entire column data to be safe
                for r in range(4, ws_details.max_row + 1):
                    ws_details.cell(row=r, column=col_idx, value="")

            if clear_old_data and ws_details.max_row >= 4:
                ws_details.delete_rows(4, ws_details.max_row - 3)
                
            def write_details(mode, details_list):
                d_row = -1
                for r in range(4, max(5, ws_details.max_row + 5)):
                    if not ws_details.cell(row=r, column=1).value:
                        d_row = r
                        break
                if d_row == -1: d_row = ws_details.max_row + 1
                
                stt = 1
                for q in details_list:
                    mode_label = "TRAD" if "Trad" in mode else ("E2E" if "E2E" in mode else "CFG")
                    ws_details.cell(row=d_row, column=1, value=f"{results['runId']}_{mode_label}")
                    ws_details.cell(row=d_row, column=2, value=mode)
                    ws_details.cell(row=d_row, column=3, value=stt)
                    ws_details.cell(row=d_row, column=4, value=q.get("question", ""))
                    d_row += 1
                    stt += 1

            if method in ["All", "Traditional"]:
                write_details("Traditional", results.get("tradDetails", []))
            if method in ["All", "CFG"]:
                write_details("Graph-based (CFG)", results.get("cfgDetails", []))
            if method in ["All", "E2E"]:
                write_details("E2E (Kết hợp 3 Tầng)", results.get("e2eDetails", []))

            wb.save(excel_path)
            self.log(f"Đã lưu thành công vào file Excel!")
        except Exception as e:
            self.log(f"[LỖI EXCEL] Không thể ghi file: {str(e)}")
            
    def open_logs_viewer(self):
        log_dir = "benchmark_logs"
        if not os.path.exists(log_dir):
            messagebox.showinfo("Information", "Chưa có dữ liệu log nào được ghi nhận.")
            return
            
        json_files = [f for f in os.listdir(log_dir) if f.endswith(".json")]
        if not json_files:
            messagebox.showinfo("Information", "Chưa có file log JSON nào trong thư mục.")
            return
            
        json_files.sort(reverse=True) # Newest first
        
        viewer = ctk.CTkToplevel(self)
        viewer.title("Trình xem chi tiết Câu Hỏi (Log Viewer)")
        viewer.geometry("1100x700")
        viewer.transient(self)
        viewer.configure(fg_color="#F0F4F8")
        
        # --- TOP FRAME: Select file ---
        top_frame = ctk.CTkFrame(viewer, fg_color="#FFFFFF", corner_radius=8)
        top_frame.pack(fill="x", padx=20, pady=15)
        
        ctk.CTkLabel(top_frame, text="Chọn file log:", font=ctk.CTkFont(weight="bold")).pack(side="left", padx=10, pady=10)
        combo_logs = ctk.CTkComboBox(top_frame, values=json_files, width=300, state="readonly")
        combo_logs.pack(side="left", padx=10, pady=10)
        combo_logs.set(json_files[0])
        
        # --- MAIN FRAME ---
        main_frame = ctk.CTkFrame(viewer, fg_color="transparent")
        main_frame.pack(fill="both", expand=True, padx=20, pady=(0, 20))
        
        # Left Panel (Questions List)
        left_panel = ctk.CTkScrollableFrame(main_frame, width=250, fg_color="#FFFFFF", corner_radius=8, border_width=1, border_color="#E1E5EB")
        left_panel.pack(side="left", fill="y", padx=(0, 10))
        
        # Right Panel (Details)
        right_panel = ctk.CTkFrame(main_frame, fg_color="#FFFFFF", corner_radius=8, border_width=1, border_color="#E1E5EB")
        right_panel.pack(side="right", fill="both", expand=True)
        
        txt_details = ctk.CTkTextbox(right_panel, font=ctk.CTkFont(family="Consolas", size=14), fg_color="transparent", text_color="#333333", wrap="word")
        txt_details.pack(fill="both", expand=True, padx=15, pady=15)
        
        current_data = {}
        
        def show_question_details(method, idx, q_text):
            txt_details.configure(state="normal")
            txt_details.delete("1.0", "end")
            
            if not current_data or method not in current_data or not current_data[method]:
                txt_details.insert("end", "Không có dữ liệu.")
                txt_details.configure(state="disabled")
                return
                
            data = current_data[method]
            
            # Find data across the 4 APIs
            gen_list = data["generate"].get("generatedQuestionDtos", data["generate"].get("GeneratedQuestionDtos", []))
            cov_list = data["coverage"].get("questionResults", [])
            acc_list = data["accuracy"].get("questionResults", [])
            diff_list = data["difficulty"].get("questionResults", [])
            
            # Match
            gen_match = next((q for q in gen_list if q.get("question", q.get("Question", "")) == q_text), {})
            cov_match = next((q for q in cov_list if q.get("question") == q_text), {})
            acc_match = next((q for q in acc_list if q.get("question") == q_text), {})
            diff_match = next((q for q in diff_list if q.get("question") == q_text), {})
            
            # Formatting output
            out = f"=== CHI TIẾT CÂU HỎI {idx} ({'Code Thô' if method == 'traditional' else 'Đồ thị CFG'}) ===\n\n"
            out += f"❓ CÂU HỎI:\n{q_text}\n\n"
            
            out += f"💡 ĐÁP ÁN (Dự kiến):\n"
            out += f"{gen_match.get('answer', gen_match.get('Answer', 'N/A'))}\n\n"
            
            out += "-" * 50 + "\n\n"
            out += f"📊 1. ĐÁNH GIÁ ĐỘ BAO PHỦ (COVERAGE)\n"
            out += f"   - Coverage Score: {cov_match.get('coverage', 0) * 100:.2f}%\n"
            out += f"   - Active Nodes Count: {cov_match.get('activeNodeCount', 0)}\n"
            out += f"   - Chi tiết Nodes được kích hoạt:\n"
            for node in cov_match.get('activeNodes', []):
                out += f"      + [{node.get('nodeId')}] {node.get('nodeName')}\n"
            
            out += "\n" + "-" * 50 + "\n\n"
            acc_res = acc_match.get("accuracyResult", {})
            out += f"🎯 2. ĐÁNH GIÁ TÍNH ĐÚNG ĐẮN (ACCURACY)\n"
            out += f"   - Kết quả: {'✅ ĐÚNG LOGIC' if acc_res.get('isAccurate') else '❌ SAI LOGIC'}\n"
            out += f"   - Lập luận của AI (Final Verdict):\n"
            verdict = acc_res.get('finalVerdict', 'N/A')
            import textwrap
            for line in textwrap.wrap(verdict, width=80):
                out += f"      {line}\n"
                
            out += "\n" + "-" * 50 + "\n\n"
            diff_res = diff_match.get("difficultyResult", {})
            out += f"🔥 3. ĐÁNH GIÁ ĐỘ PHỨC TẠP (DIFFICULTY)\n"
            out += f"   - Điểm Cyclomatic (Số cạnh Active): {diff_res.get('cyclomaticComplexity', 0)}\n"
            out += f"   - Mức độ đánh giá: {diff_res.get('difficultyLevel', 'N/A')}\n"
            
            txt_details.insert("end", out)
            txt_details.configure(state="disabled")

        def load_selected_log(*args):
            selected = combo_logs.get()
            file_path = os.path.join(log_dir, selected)
            
            # Clear old buttons
            for widget in left_panel.winfo_children():
                widget.destroy()
                
            txt_details.configure(state="normal")
            txt_details.delete("1.0", "end")
            txt_details.configure(state="disabled")
            
            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    nonlocal current_data
                    current_data = json.load(f)
                    
                # Add buttons for Traditional
                if current_data.get("traditional"):
                    lbl_t = ctk.CTkLabel(left_panel, text="Code Thô (Traditional)", font=ctk.CTkFont(weight="bold"), text_color="#D83B01")
                    lbl_t.pack(pady=(10, 5), anchor="w", padx=10)
                    
                    t_gen = current_data["traditional"]["generate"].get("generatedQuestionDtos", current_data["traditional"]["generate"].get("GeneratedQuestionDtos", []))
                    for i, q in enumerate(t_gen, 1):
                        q_text = q.get("question", q.get("Question", ""))
                        # Using default arg in lambda to capture loop variables correctly
                        btn = ctk.CTkButton(left_panel, text=f"Câu {i}", fg_color="transparent", text_color="#333333", hover_color="#E1E5EB", anchor="w",
                                            command=lambda m="traditional", idx=i, qt=q_text: show_question_details(m, idx, qt))
                        btn.pack(fill="x", padx=5, pady=2)
                        
                # Add buttons for CFG
                if current_data.get("cfg"):
                    lbl_c = ctk.CTkLabel(left_panel, text="Đồ thị (CFG)", font=ctk.CTkFont(weight="bold"), text_color="#107C41")
                    lbl_c.pack(pady=(15, 5), anchor="w", padx=10)
                    
                    c_gen = current_data["cfg"]["generate"].get("generatedQuestionDtos", current_data["cfg"]["generate"].get("GeneratedQuestionDtos", []))
                    for i, q in enumerate(c_gen, 1):
                        q_text = q.get("question", q.get("Question", ""))
                        btn = ctk.CTkButton(left_panel, text=f"Câu {i}", fg_color="transparent", text_color="#333333", hover_color="#E1E5EB", anchor="w",
                                            command=lambda m="cfg", idx=i, qt=q_text: show_question_details(m, idx, qt))
                        btn.pack(fill="x", padx=5, pady=2)
                        
                # Add buttons for E2E
                if current_data.get("e2e"):
                    lbl_e = ctk.CTkLabel(left_panel, text="E2E (Hybrid 3-Tier)", font=ctk.CTkFont(weight="bold"), text_color="#0066CC")
                    lbl_e.pack(pady=(15, 5), anchor="w", padx=10)
                    
                    e_gen = current_data["e2e"]["generate"].get("generatedQuestionDtos", current_data["e2e"]["generate"].get("GeneratedQuestionDtos", []))
                    for i, q in enumerate(e_gen, 1):
                        q_text = q.get("question", q.get("Question", ""))
                        btn = ctk.CTkButton(left_panel, text=f"Câu {i}", fg_color="transparent", text_color="#333333", hover_color="#E1E5EB", anchor="w",
                                            command=lambda m="e2e", idx=i, qt=q_text: show_question_details(m, idx, qt))
                        btn.pack(fill="x", padx=5, pady=2)
                        
            except Exception as e:
                txt_details.configure(state="normal")
                txt_details.insert("end", f"Error đọc file: {str(e)}")
                txt_details.configure(state="disabled")
                
        btn_load = ctk.CTkButton(top_frame, text="Mở File & Phân Tích", command=load_selected_log, fg_color="#0066CC")
        btn_load.pack(side="left", padx=10, pady=10)
        
        load_selected_log()

if __name__ == "__main__":
    app = BenchmarkApp()
    app.mainloop()
