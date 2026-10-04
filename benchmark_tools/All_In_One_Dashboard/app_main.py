import customtkinter as ctk

from src import theme as T
from src.ui_question_gen import PageQuestionGeneration
from src.ui_evaluation import PageStudentEvaluation


class AppMain(ctk.CTk):
    NAV = [
        ("gen", "📝   Tạo câu hỏi"),
        ("eval", "🎓   Chấm điểm"),
    ]

    def __init__(self):
        super().__init__()
        self.title("Repo Into Graph — AI Dashboard")
        self.geometry("1440x900")
        self.minsize(1180, 720)
        self.configure(fg_color=T.C["bg"])

        # Bộ câu hỏi dùng chung giữa 2 trang
        self.question_set = {"business_id": "", "business_name": "", "method": "", "questions": []}

        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=1)
        self._build_sidebar()

        self.content = ctk.CTkFrame(self, fg_color="transparent", corner_radius=0)
        self.content.grid(row=0, column=1, sticky="nsew")
        self.content.grid_rowconfigure(0, weight=1)
        self.content.grid_columnconfigure(0, weight=1)

        self.pages = {
            "gen": PageQuestionGeneration(self.content, self),
            "eval": PageStudentEvaluation(self.content, self),
        }
        self.show("gen")

    # ------------------------------------------------------------------
    def _build_sidebar(self):
        sb = ctk.CTkFrame(self, width=248, corner_radius=0, fg_color=T.C["sidebar"])
        sb.grid(row=0, column=0, sticky="nsew")
        sb.grid_propagate(False)
        sb.grid_columnconfigure(0, weight=1)
        sb.grid_rowconfigure(10, weight=1)

        ctk.CTkLabel(sb, text="RIG AI Core", font=T.font("headline"), text_color=T.C["on_sidebar"],
                     anchor="w").grid(row=0, column=0, sticky="ew", padx=T.SP["lg"], pady=(T.SP["xl"], 0))
        ctk.CTkLabel(sb, text="Sinh câu hỏi & chấm điểm", font=T.font("small"),
                     text_color=T.C["sidebar_muted"], anchor="w").grid(
            row=1, column=0, sticky="ew", padx=T.SP["lg"], pady=(T.SP["xs"], T.SP["xl"]))

        self.nav_buttons = {}
        for i, (key, label) in enumerate(self.NAV):
            b = ctk.CTkButton(sb, text=label, anchor="w", height=44, corner_radius=T.R["md"],
                              fg_color="transparent", hover_color=T.C["sidebar_hover"],
                              text_color=T.C["sidebar_text"], font=T.font("label"),
                              command=lambda k=key: self.show(k))
            b.grid(row=2 + i, column=0, sticky="ew", padx=T.SP["base"], pady=T.SP["xs"])
            self.nav_buttons[key] = b

        bottom = ctk.CTkFrame(sb, fg_color="transparent")
        bottom.grid(row=11, column=0, sticky="ew", padx=T.SP["lg"], pady=T.SP["lg"])
        self.sw_dark = ctk.CTkSwitch(bottom, text="Giao diện tối", font=T.font("small"),
                                     text_color=T.C["sidebar_text"], progress_color=T.C["primary"],
                                     command=self._toggle_dark)
        self.sw_dark.pack(anchor="w")
        ctk.CTkLabel(bottom, text="API: localhost:55060", font=T.font("small"),
                     text_color=T.C["sidebar_muted"], anchor="w").pack(anchor="w", pady=(T.SP["sm"], 0))

    def _toggle_dark(self):
        ctk.set_appearance_mode("Dark" if self.sw_dark.get() else "Light")

    # ------------------------------------------------------------------
    def show(self, key):
        for k, page in self.pages.items():
            page.grid_forget()
            active = k == key
            self.nav_buttons[k].configure(
                fg_color=T.C["sidebar_active"] if active else "transparent",
                text_color=T.C["on_sidebar"] if active else T.C["sidebar_text"])
        page = self.pages[key]
        page.grid(row=0, column=0, sticky="nsew")
        if hasattr(page, "on_show"):
            page.on_show()

    def set_question_set(self, business_id, business_name, method, questions, select=0):
        self.question_set = {"business_id": business_id, "business_name": business_name,
                             "method": method, "questions": list(questions)}
        self.pages["eval"].refresh_question_picker(select=select)


if __name__ == "__main__":
    ctk.set_appearance_mode("Light")
    app = AppMain()
    app.mainloop()
