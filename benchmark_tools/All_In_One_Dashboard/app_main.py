import customtkinter as ctk
from src.ui_question_gen import PageQuestionGeneration
from src.ui_evaluation import PageStudentEvaluation
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

class AppMain(ctk.CTk):
    def __init__(self):
        super().__init__()

        self.title("Repo Into Graph - Modern AI Dashboard")
        self.geometry("1400x900")
        ctk.set_appearance_mode("Light")
        
        # Thêm chút padding và màu nền tinh tế
        self.configure(fg_color="#F8FAFC")
        
        # Shared Data
        self.shared_business_id = ""
        self.shared_question = ""
        self.shared_reference = ""

        # --- Sidebar ---
        self.sidebar_frame = ctk.CTkFrame(self, width=260, corner_radius=0, fg_color="#1E293B")
        self.sidebar_frame.grid(row=0, column=0, sticky="nsew")
        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=1)
        
        self.logo_label = ctk.CTkLabel(self.sidebar_frame, text="RIG AI Core", font=ctk.CTkFont(size=26, weight="bold"), text_color="#FFFFFF")
        self.logo_label.grid(row=0, column=0, padx=20, pady=(40, 30))

        # Buttons
        self.btn_gen = ctk.CTkButton(self.sidebar_frame, text="📝 Trí Tuệ Nhân Tạo (Tạo Câu Hỏi)", height=45, 
                                     fg_color="#334155", hover_color="#475569", anchor="w",
                                     font=ctk.CTkFont(size=14, weight="bold"), command=self.show_frame_gen)
        self.btn_gen.grid(row=1, column=0, padx=20, pady=10, sticky="ew")

        self.btn_eval = ctk.CTkButton(self.sidebar_frame, text="🎓 AI Chấm Điểm (Đánh Giá)", height=45, 
                                      fg_color="transparent", hover_color="#475569", anchor="w",
                                      font=ctk.CTkFont(size=14, weight="bold"), command=self.show_frame_eval)
        self.btn_eval.grid(row=2, column=0, padx=20, pady=10, sticky="ew")

        # --- Main Content ---
        self.main_frame = ctk.CTkFrame(self, corner_radius=0, fg_color="transparent")
        self.main_frame.grid(row=0, column=1, sticky="nsew")
        self.main_frame.grid_rowconfigure(0, weight=1)
        self.main_frame.grid_columnconfigure(0, weight=1)

        self.frame_gen = PageQuestionGeneration(self.main_frame, self)
        self.frame_eval = PageStudentEvaluation(self.main_frame, self)

        # Mặc định mở trang 1
        self.show_frame_gen()

    def show_frame_gen(self):
        self.btn_gen.configure(fg_color="#3B82F6") # Active color
        self.btn_eval.configure(fg_color="transparent")
        self.frame_eval.grid_forget()
        self.frame_gen.grid(row=0, column=0, sticky="nsew")

    def show_frame_eval(self):
        self.btn_eval.configure(fg_color="#3B82F6")
        self.btn_gen.configure(fg_color="transparent")
        self.frame_gen.grid_forget()
        self.frame_eval.grid(row=0, column=0, sticky="nsew")
        self.frame_eval.load_shared_data()

if __name__ == "__main__":
    app = AppMain()
    app.mainloop()
