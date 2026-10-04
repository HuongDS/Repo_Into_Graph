import json
import os
import threading

import customtkinter as ctk

from . import api
from . import theme as T


class PageQuestionGeneration(ctk.CTkFrame):
    METHODS = {"Traditional": "traditional", "CFG": "cfg", "Hybrid": "hybrid"}

    def __init__(self, parent, controller):
        super().__init__(parent, fg_color="transparent", corner_radius=0)
        self.controller = controller
        self.repos, self.repo_labels = [], []
        self.businesses, self.business_labels = [], []
        self.raw_result = None
        self.questions = []
        self.meta = {}
        self._wrap_labels, self._wrap_width = [], 0
        self._build()
        self.reload_repos()

    def ui(self, fn, *args, **kwargs):
        """Cập nhật giao diện từ luồng nền một cách an toàn."""
        self.after(0, lambda: fn(*args, **kwargs))

    # ------------------------------------------------------------------ layout
    def _build(self):
        self.grid_columnconfigure(0, weight=0, minsize=400)
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(1, weight=1)

        T.page_header(self, "Tạo câu hỏi",
                      "Sinh câu hỏi tự luận từ mã nguồn của một nghiệp vụ, kèm đáp án mẫu do AI viết."
                      ).grid(row=0, column=0, columnspan=2, sticky="ew",
                             padx=T.SP["xl"], pady=(T.SP["lg"], T.SP["base"]))

        left = T.Card(self)
        left.grid(row=1, column=0, sticky="nsew", padx=(T.SP["xl"], T.SP["md"]), pady=(0, T.SP["xl"]))
        right = T.Card(self)
        right.grid(row=1, column=1, sticky="nsew", padx=(T.SP["md"], T.SP["xl"]), pady=(0, T.SP["xl"]))
        self._build_left(left)
        self._build_right(right)

    def _build_left(self, card):
        body = ctk.CTkFrame(card, fg_color="transparent")
        body.pack(fill="both", expand=True, padx=T.SP["lg"], pady=T.SP["lg"])

        # Hành động chính ghim ở đáy
        self.btn_run = T.primary_button(body, "Tạo câu hỏi", self.generate)
        self.btn_run.pack(side="bottom", fill="x")

        # 1. Kho mã nguồn
        T.section_title(body, "Kho mã nguồn", step=1).pack(fill="x")
        row = ctk.CTkFrame(body, fg_color="transparent")
        row.pack(fill="x", pady=(T.SP["sm"], 0))
        self.cb_repo = T.combobox(row, ["Đang tải…"], command=self.on_repo_selected)
        self.cb_repo.pack(side="left", fill="x", expand=True)
        T.icon_button(row, "↻", self.reload_repos).pack(side="left", padx=(T.SP["sm"], 0))

        T.field_label(body, "Hoặc phân tích repo mới", "đường dẫn / URL Git").pack(
            fill="x", pady=(T.SP["base"], T.SP["xs"] + 2))
        row2 = ctk.CTkFrame(body, fg_color="transparent")
        row2.pack(fill="x")
        self.ent_repo = T.entry(row2, placeholder_text="D:\\du-an  hoặc  https://github.com/…")
        self.ent_repo.pack(side="left", fill="x", expand=True)
        self.btn_analyze = T.secondary_button(row2, "Phân tích", self.analyze_repository, width=104)
        self.btn_analyze.pack(side="left", padx=(T.SP["sm"], 0))

        self.banner = T.Banner(body)
        self.banner.attach(after=row2, fill="x", pady=(T.SP["md"], 0))

        # 2. Nghiệp vụ
        T.section_title(body, "Nghiệp vụ", step=2).pack(fill="x", pady=(T.SP["lg"], T.SP["sm"]))
        self.cb_business = T.combobox(body, ["Chọn kho mã nguồn trước"])
        self.cb_business.pack(fill="x")
        self.lbl_biz_count = T.muted(body)
        self.lbl_biz_count.pack(fill="x", pady=(T.SP["xs"] + 2, 0))

        # 3. Cấu hình
        T.section_title(body, "Cấu hình sinh", step=3).pack(fill="x", pady=(T.SP["lg"], T.SP["sm"]))
        T.field_label(body, "Phương pháp").pack(fill="x")
        self.seg_method = T.segmented(body, list(self.METHODS))
        self.seg_method.set("Hybrid")
        self.seg_method.pack(fill="x", pady=(T.SP["xs"] + 2, T.SP["md"]))
        T.field_label(body, "Số câu hỏi").pack(fill="x")
        self.seg_num = T.segmented(body, ["1", "3", "5", "10"])
        self.seg_num.set("5")
        self.seg_num.pack(fill="x", pady=(T.SP["xs"] + 2, 0))

    def _build_right(self, card):
        card.grid_columnconfigure(0, weight=1)
        card.grid_rowconfigure(1, weight=1)

        head = ctk.CTkFrame(card, fg_color="transparent")
        head.grid(row=0, column=0, sticky="ew", padx=T.SP["lg"], pady=(T.SP["lg"] - 4, T.SP["md"]))
        head.grid_columnconfigure(0, weight=1)
        ctk.CTkLabel(head, text="Câu hỏi đã sinh", font=T.font("title"),
                     text_color=T.C["on_surface"], anchor="w").grid(row=0, column=0, sticky="w")
        self.lbl_meta = T.muted(head, "Chưa có kết quả")
        self.lbl_meta.grid(row=1, column=0, sticky="w")
        self.seg_view = T.segmented(head, ["Thẻ", "JSON"], command=self._switch_view, width=140)
        self.seg_view.set("Thẻ")
        self.seg_view.grid(row=0, column=1, rowspan=2, padx=(T.SP["md"], 0))
        self.btn_send_all = T.tonal_button(head, "Chuyển sang Chấm điểm  →",
                                           lambda: self.send_to_eval(0), state="disabled")
        self.btn_send_all.grid(row=0, column=2, rowspan=2, padx=(T.SP["md"], 0))

        area = ctk.CTkFrame(card, fg_color="transparent")
        area.grid(row=1, column=0, sticky="nsew", padx=T.SP["lg"], pady=(0, T.SP["lg"]))
        area.grid_columnconfigure(0, weight=1)
        area.grid_rowconfigure(0, weight=1)

        self.cards_holder = ctk.CTkFrame(area, fg_color="transparent")
        self.cards = ctk.CTkScrollableFrame(self.cards_holder, fg_color="transparent",
                                            scrollbar_button_color=T.C["outline"])
        self.cards.pack(fill="both", expand=True)
        self.cards_holder.bind("<Configure>", self._on_cards_resize)

        self.json_box = T.textbox(area, mono=True, wrap="none")
        self.state_view = T.StateView(area)
        for w in (self.cards_holder, self.json_box, self.state_view):
            w.grid(row=0, column=0, sticky="nsew")
            w.grid_remove()

        self._show_state("📝", "Chưa có câu hỏi nào",
                         "Chọn kho mã nguồn và nghiệp vụ ở cột bên trái, rồi bấm “Tạo câu hỏi”.")

    # ------------------------------------------------------------- view state
    def _show_area(self, widget):
        for w in (self.cards_holder, self.json_box, self.state_view):
            if w is not widget:
                w.grid_remove()
        widget.grid()

    def _show_state(self, *args, **kw):
        self.state_view.show(*args, **kw)
        self._show_area(self.state_view)

    def _switch_view(self, _value=None):
        if not self.questions:
            return
        self._show_area(self.json_box if self.seg_view.get() == "JSON" else self.cards_holder)

    def _on_cards_resize(self, event):
        width = event.width - 56
        if width > 200 and abs(width - self._wrap_width) > 8:
            self._wrap_width = width
            for lbl in self._wrap_labels:
                lbl.configure(wraplength=width)

    # ---------------------------------------------------------------- repos
    def reload_repos(self):
        self.cb_repo.configure(values=["Đang tải…"])
        self.cb_repo.set("Đang tải…")
        threading.Thread(target=self._load_repos_worker, daemon=True).start()

    def _load_repos_worker(self, select_id=None):
        try:
            data = api.request("GET", "/api/analysis-runs", params={"page": 1, "pageSize": 50}, timeout=10)
            items = (data or {}).get("items", []) if isinstance(data, dict) else (data or [])
            self.ui(self._on_repos_loaded, [r for r in items if "id" in r], select_id)
        except api.ApiError as e:
            self.ui(self._on_repos_failed, str(e))

    def _on_repos_loaded(self, repos, select_id):
        self.repos = repos
        self.repo_labels = []
        for r in repos:
            path = str(r.get("repositoryPath") or "")
            name = os.path.basename(path.rstrip("\\/")) or path
            self.repo_labels.append(f"{name}  ·  {str(r['id'])[:8]}")
        if not repos:
            self.cb_repo.configure(values=["Chưa có kho nào"])
            self.cb_repo.set("Chưa có kho nào")
            self.banner.show("info", "Chưa có kho mã nguồn nào. Nhập đường dẫn repo rồi bấm “Phân tích”.")
            return
        self.cb_repo.configure(values=self.repo_labels)
        idx = 0
        if select_id:
            idx = next((i for i, r in enumerate(repos) if str(r["id"]) == str(select_id)), 0)
        self.cb_repo.set(self.repo_labels[idx])
        self.on_repo_selected(self.repo_labels[idx])

    def _on_repos_failed(self, msg):
        self.cb_repo.configure(values=["Không tải được"])
        self.cb_repo.set("Không tải được")
        self.banner.show("error", msg)

    def on_repo_selected(self, label):
        if label not in self.repo_labels:
            return
        run_id = self.repos[self.repo_labels.index(label)]["id"]
        self.cb_business.configure(values=["Đang tải…"])
        self.cb_business.set("Đang tải…")
        self.lbl_biz_count.configure(text="")
        threading.Thread(target=self._load_businesses_worker, args=(run_id,), daemon=True).start()

    def _load_businesses_worker(self, run_id):
        try:
            data = api.request("GET", "/api/businesses", params={"analysisRunId": run_id}, timeout=15)
            self.ui(self._on_businesses_loaded, [b for b in (data or []) if "id" in b])
        except api.ApiError as e:
            self.ui(self.banner.show, "error", str(e))

    def _on_businesses_loaded(self, items):
        self.businesses = items
        names = [str(b.get("businessName") or "(không tên)") for b in items]
        self.business_labels = [
            f"{n}  ·  {str(b['id'])[:8]}" if names.count(n) > 1 else n
            for n, b in zip(names, items)
        ]
        if not items:
            self.cb_business.configure(values=["Kho này chưa có nghiệp vụ"])
            self.cb_business.set("Kho này chưa có nghiệp vụ")
            self.lbl_biz_count.configure(text="")
            return
        self.banner.hide()
        self.cb_business.configure(values=self.business_labels)
        self.cb_business.set(self.business_labels[0])
        self.lbl_biz_count.configure(text=f"{len(items)} nghiệp vụ trong kho này")

    def selected_business(self):
        label = self.cb_business.get()
        if label in self.business_labels:
            return self.businesses[self.business_labels.index(label)]
        return None

    def analyze_repository(self):
        path = self.ent_repo.get().strip()
        if not path:
            self.banner.show("warning", "Nhập đường dẫn thư mục hoặc URL Git của repo cần phân tích.")
            return
        self.btn_analyze.configure(state="disabled", text="Đang chạy…")
        self.banner.show("info", "Đang phân tích repo — có thể mất vài phút với repo lớn.")

        def work():
            try:
                res = api.request("POST", "/api/analysis/analyze", json={"repositoryPath": path}, timeout=900)
                run_id = (res or {}).get("analysisRunId")
                self.ui(self.banner.show, "success", "Phân tích xong.")
                self._load_repos_worker(select_id=run_id)
            except api.ApiError as e:
                self.ui(self.banner.show, "error", str(e))
            finally:
                self.ui(self.btn_analyze.configure, state="normal", text="Phân tích")

        threading.Thread(target=work, daemon=True).start()

    # ------------------------------------------------------------- generate
    def generate(self):
        biz = self.selected_business()
        if not biz:
            self.banner.show("warning", "Hãy chọn một nghiệp vụ trước khi tạo câu hỏi.")
            return
        self.banner.hide()
        method_label = self.seg_method.get()
        payload = {"businessId": biz["id"], "numQuestions": int(self.seg_num.get()),
                   "generatorType": self.METHODS.get(method_label, "hybrid")}

        self.btn_run.configure(state="disabled", text="Đang sinh câu hỏi…")
        self.btn_send_all.configure(state="disabled")
        self._show_state("⏳", "Đang sinh câu hỏi…",
                         f"{biz.get('businessName', '')} · {method_label} · {payload['numQuestions']} câu. "
                         "Thường mất 10–60 giây.", loading=True)

        def work():
            try:
                raw = api.request("POST", "/api/questiongenerator/generate", json=payload, timeout=300)
                self.ui(self._on_generated, raw, biz, method_label)
            except api.ApiError as e:
                self.ui(self._show_state, "⚠", "Không sinh được câu hỏi", str(e), "Thử lại", self.generate)
            finally:
                self.ui(self.btn_run.configure, state="normal", text="Tạo câu hỏi")

        threading.Thread(target=work, daemon=True).start()

    @staticmethod
    def _normalize(raw):
        items = raw if isinstance(raw, list) else [raw]
        qs = []
        meta = {"business_id": "", "business_name": "", "in": 0, "out": 0}
        for it in items:
            if not isinstance(it, dict):
                continue
            meta["business_id"] = meta["business_id"] or str(it.get("businessId") or "")
            meta["business_name"] = meta["business_name"] or str(it.get("businessName") or "")
            meta["in"] += it.get("inputTokens") or 0
            meta["out"] += it.get("outputTokens") or 0
            for q in it.get("generatedQuestionDtos") or it.get("GeneratedQuestionDtos") or []:
                qs.append({
                    "question": q.get("question") or "",
                    "suggestedAnswer": q.get("suggestedAnswer") or "",
                    "difficulty": q.get("difficulty") or "",
                    "targetedEntryPoints": q.get("targetedEntryPoints") or [],
                })
        return qs, meta

    def _on_generated(self, raw, biz, method_label):
        self.raw_result = raw
        self.questions, self.meta = self._normalize(raw)
        self.meta["business_id"] = self.meta["business_id"] or str(biz["id"])
        self.meta["business_name"] = self.meta["business_name"] or str(biz.get("businessName") or "")
        self.meta["method"] = method_label

        self.json_box.delete("1.0", "end")
        self.json_box.insert("1.0", json.dumps(raw, indent=2, ensure_ascii=False))

        if not self.questions:
            self.lbl_meta.configure(text=self.meta["business_name"])
            self._show_state("🤔", "AI không trả về câu hỏi nào",
                             "Thử lại, đổi phương pháp sinh, hoặc xem tab JSON để biết phản hồi gốc.",
                             "Thử lại", self.generate)
            return

        self.lbl_meta.configure(
            text=f"{self.meta['business_name']}  ·  {method_label}  ·  {len(self.questions)} câu  ·  "
                 f"token {self.meta['in']} vào / {self.meta['out']} ra")
        self.btn_send_all.configure(state="normal")
        self._render_cards()
        self._switch_view()

    def _render_cards(self):
        for w in self.cards.winfo_children():
            w.destroy()
        self._wrap_labels = []
        wrap = self._wrap_width or 640

        for i, q in enumerate(self.questions):
            card = ctk.CTkFrame(self.cards, fg_color=T.C["surface_alt"], corner_radius=T.R["md"])
            card.pack(fill="x", pady=(0, T.SP["md"]), padx=(0, T.SP["xs"]))

            top = ctk.CTkFrame(card, fg_color="transparent")
            top.pack(fill="x", padx=T.SP["base"], pady=(T.SP["md"] + 2, T.SP["sm"]))
            T.chip(top, f"Câu {i + 1}", "primary").pack(side="left")
            diff = str(q["difficulty"] or "—")
            T.chip(top, diff, T.DIFFICULTY_KIND.get(diff.lower(), "neutral")).pack(side="left", padx=(T.SP["sm"], 0))
            T.tonal_button(top, "Chấm câu này  →", lambda i=i: self.send_to_eval(i), width=136).pack(side="right")

            q_lbl = ctk.CTkLabel(card, text=q["question"], font=T.font("body_strong"),
                                 text_color=T.C["on_surface"], justify="left", anchor="w", wraplength=wrap)
            q_lbl.pack(fill="x", padx=T.SP["base"])

            ctk.CTkLabel(card, text="ĐÁP ÁN MẪU (AI)", font=T.font("small_strong"),
                         text_color=T.C["on_surface_var"], anchor="w").pack(
                fill="x", padx=T.SP["base"], pady=(T.SP["md"], 2))
            a_lbl = ctk.CTkLabel(card, text=q["suggestedAnswer"] or "(không có)", font=T.font("body"),
                                 text_color=T.C["on_surface"], justify="left", anchor="w", wraplength=wrap)
            a_lbl.pack(fill="x", padx=T.SP["base"])
            self._wrap_labels += [q_lbl, a_lbl]

            if q["targetedEntryPoints"]:
                ep = T.muted(card, "Entry points:  " + "  ·  ".join(q["targetedEntryPoints"]), wraplength=wrap)
                ep.pack(fill="x", padx=T.SP["base"], pady=(T.SP["sm"], 0))
                self._wrap_labels.append(ep)
            ctk.CTkFrame(card, height=T.SP["md"], fg_color="transparent").pack()

        self.cards._parent_canvas.yview_moveto(0)

    # ------------------------------------------------------------- handoff
    def send_to_eval(self, index):
        if not self.questions:
            return
        self.controller.set_question_set(self.meta["business_id"], self.meta["business_name"],
                                         self.meta.get("method", ""), self.questions, select=index)
        self.controller.show("eval")
