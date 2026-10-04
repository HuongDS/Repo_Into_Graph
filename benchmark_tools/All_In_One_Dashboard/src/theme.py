"""Design tokens + widget helpers cho All-In-One Dashboard.

Mọi màu / khoảng cách / bo góc / cỡ chữ đều đi qua file này.
Màu khai báo dạng (light, dark) để customtkinter tự đổi theo chế độ sáng/tối.
"""
import customtkinter as ctk

# ---------------------------------------------------------------------------
# Màu (light, dark) — xây quanh seed #3B5BDB
# ---------------------------------------------------------------------------
C = {
    "bg": ("#F5F6FA", "#121316"),
    "surface": ("#FFFFFF", "#1B1C20"),
    "surface_alt": ("#F0F2F7", "#24262B"),      # input, textbox, thẻ con
    "outline": ("#DFE3EC", "#363941"),
    "on_surface": ("#1A1C22", "#E4E5EA"),
    "on_surface_var": ("#596072", "#A6ABB8"),
    "primary": ("#3B5BDB", "#9DB0FF"),
    "primary_hover": ("#3150C7", "#B4C3FF"),
    "on_primary": ("#FFFFFF", "#0E1A4D"),
    "primary_container": ("#E3E8FF", "#2A3566"),
    "primary_container_hover": ("#D3DBFF", "#34417A"),
    "on_primary_container": ("#18286B", "#DCE2FF"),
    "success": ("#1C7A4C", "#73D3A1"),
    "success_container": ("#DCF3E6", "#1C3A2B"),
    "warning": ("#8A5300", "#F2C063"),
    "warning_container": ("#FFF0D3", "#3D2F12"),
    "error": ("#B3261E", "#FF8A80"),
    "error_container": ("#FCE3E1", "#4A1F1F"),
    "disabled_text": ("#9AA0AE", "#5E626C"),
    "sidebar": ("#1C2231", "#0D0E11"),
    "sidebar_text": ("#C5CBDA", "#A6ABB8"),
    "sidebar_muted": ("#8790A6", "#6B7080"),
    "sidebar_hover": ("#283045", "#1B1C20"),
    "sidebar_active": ("#3B5BDB", "#2A3566"),
    "on_sidebar": ("#FFFFFF", "#FFFFFF"),
}

# Spacing — thang 4pt
SP = {"xs": 4, "sm": 8, "md": 12, "base": 16, "lg": 24, "xl": 32}
# Bo góc — chỉ 3 mức
R = {"sm": 8, "md": 12, "lg": 16}

FAMILY = "Segoe UI"
MONO = "Consolas"

_FONT_SPEC = {
    "display": (30, "bold"),
    "headline": (22, "bold"),
    "title": (16, "bold"),
    "label": (13, "bold"),
    "body": (13, "normal"),
    "body_strong": (14, "bold"),
    "small": (12, "normal"),
    "small_strong": (12, "bold"),
    "mono": (12, "normal"),
}
_font_cache = {}


def font(role):
    """Trả về CTkFont theo vai trò (chỉ gọi sau khi cửa sổ gốc đã tạo)."""
    if role not in _font_cache:
        size, weight = _FONT_SPEC[role]
        family = MONO if role == "mono" else FAMILY
        _font_cache[role] = ctk.CTkFont(family=family, size=size, weight=weight)
    return _font_cache[role]


# ---------------------------------------------------------------------------
# Khối bố cục
# ---------------------------------------------------------------------------
def Card(parent, **kw):
    return ctk.CTkFrame(parent, fg_color=C["surface"], corner_radius=R["lg"],
                        border_width=1, border_color=C["outline"], **kw)


def page_header(parent, title, subtitle):
    f = ctk.CTkFrame(parent, fg_color="transparent")
    ctk.CTkLabel(f, text=title, font=font("headline"), text_color=C["on_surface"],
                 anchor="w").pack(fill="x")
    ctk.CTkLabel(f, text=subtitle, font=font("body"), text_color=C["on_surface_var"],
                 anchor="w", justify="left").pack(fill="x", pady=(SP["xs"], 0))
    return f


def section_title(parent, text, step=None):
    f = ctk.CTkFrame(parent, fg_color="transparent")
    if step is not None:
        ctk.CTkLabel(f, text=str(step), width=24, height=24, corner_radius=12,
                     fg_color=C["primary_container"], text_color=C["on_primary_container"],
                     font=font("small_strong")).pack(side="left", padx=(0, SP["sm"]))
    ctk.CTkLabel(f, text=text, font=font("title"), text_color=C["on_surface"],
                 anchor="w").pack(side="left", fill="x", expand=True)
    return f


def field_label(parent, text, hint=None):
    f = ctk.CTkFrame(parent, fg_color="transparent")
    ctk.CTkLabel(f, text=text, font=font("label"), text_color=C["on_surface"],
                 anchor="w").pack(side="left")
    if hint:
        ctk.CTkLabel(f, text=hint, font=font("small"), text_color=C["on_surface_var"],
                     anchor="w").pack(side="left", padx=(SP["sm"], 0))
    return f


def muted(parent, text="", **kw):
    kw.setdefault("anchor", "w")
    kw.setdefault("justify", "left")
    return ctk.CTkLabel(parent, text=text, font=font("small"),
                        text_color=C["on_surface_var"], **kw)


def divider(parent):
    return ctk.CTkFrame(parent, height=1, fg_color=C["outline"], corner_radius=0)


# ---------------------------------------------------------------------------
# Input
# ---------------------------------------------------------------------------
def entry(parent, mono=False, **kw):
    return ctk.CTkEntry(parent, height=38, corner_radius=R["sm"], border_width=1,
                        fg_color=C["surface_alt"], border_color=C["outline"],
                        text_color=C["on_surface"], placeholder_text_color=C["on_surface_var"],
                        font=font("mono" if mono else "body"), **kw)


def textbox(parent, mono=False, **kw):
    kw.setdefault("wrap", "word")
    kw.setdefault("border_width", 1)
    kw.setdefault("border_color", C["outline"])
    kw.setdefault("fg_color", C["surface_alt"])
    return ctk.CTkTextbox(parent, corner_radius=R["sm"],
                          text_color=C["on_surface"], font=font("mono" if mono else "body"),
                          scrollbar_button_color=C["outline"], **kw)


def combobox(parent, values, command=None, **kw):
    cb = ctk.CTkComboBox(parent, values=values, command=command, height=38,
                         corner_radius=R["sm"], border_width=1, state="readonly",
                         fg_color=C["surface_alt"], border_color=C["outline"],
                         button_color=C["outline"], button_hover_color=C["primary"],
                         dropdown_fg_color=C["surface"], dropdown_hover_color=C["primary_container"],
                         dropdown_text_color=C["on_surface"], text_color=C["on_surface"],
                         font=font("body"), dropdown_font=font("body"), **kw)
    if values:
        cb.set(values[0])
    return cb


def segmented(parent, values, command=None, **kw):
    seg = ctk.CTkSegmentedButton(parent, values=values, command=command, height=36,
                                 corner_radius=R["sm"], font=font("label"),
                                 fg_color=C["surface_alt"], selected_color=C["primary_container"],
                                 selected_hover_color=C["primary_container_hover"],
                                 unselected_color=C["surface_alt"], unselected_hover_color=C["outline"],
                                 text_color=C["on_surface"], text_color_disabled=C["disabled_text"],
                                 **kw)
    return seg


def radio(parent, text, variable, value, **kw):
    return ctk.CTkRadioButton(parent, text=text, variable=variable, value=value,
                              font=font("label"), text_color=C["on_surface"],
                              fg_color=C["primary"], hover_color=C["primary_hover"],
                              border_color=C["on_surface_var"], **kw)


# ---------------------------------------------------------------------------
# Nút — 1 primary mỗi màn hình; còn lại secondary / tonal / text
# ---------------------------------------------------------------------------
def primary_button(parent, text, command, **kw):
    kw.setdefault("height", 44)
    return ctk.CTkButton(parent, text=text, command=command, corner_radius=R["md"],
                         fg_color=C["primary"], hover_color=C["primary_hover"],
                         text_color=C["on_primary"], text_color_disabled=C["disabled_text"],
                         font=font("body_strong"), **kw)


def secondary_button(parent, text, command, **kw):
    kw.setdefault("height", 38)
    return ctk.CTkButton(parent, text=text, command=command, corner_radius=R["md"],
                         fg_color="transparent", hover_color=C["primary_container"],
                         border_width=1, border_color=C["outline"],
                         text_color=C["primary"], text_color_disabled=C["disabled_text"],
                         font=font("label"), **kw)


def tonal_button(parent, text, command, **kw):
    kw.setdefault("height", 36)
    return ctk.CTkButton(parent, text=text, command=command, corner_radius=R["md"],
                         fg_color=C["primary_container"], hover_color=C["primary_container_hover"],
                         text_color=C["on_primary_container"], text_color_disabled=C["disabled_text"],
                         font=font("label"), **kw)


def text_button(parent, text, command, **kw):
    kw.setdefault("height", 32)
    return ctk.CTkButton(parent, text=text, command=command, corner_radius=R["sm"],
                         fg_color="transparent", hover_color=C["primary_container"],
                         text_color=C["primary"], font=font("label"), width=0, **kw)


def icon_button(parent, symbol, command, **kw):
    return ctk.CTkButton(parent, text=symbol, command=command, width=38, height=38,
                         corner_radius=R["sm"], fg_color=C["surface_alt"],
                         hover_color=C["outline"], border_width=1, border_color=C["outline"],
                         text_color=C["on_surface"], font=font("title"), **kw)


# ---------------------------------------------------------------------------
# Chip / Banner / StateView
# ---------------------------------------------------------------------------
_KIND = {
    "primary": ("primary_container", "on_primary_container", "ℹ"),
    "info": ("primary_container", "on_primary_container", "ℹ"),
    "success": ("success_container", "success", "✓"),
    "warning": ("warning_container", "warning", "!"),
    "error": ("error_container", "error", "⚠"),
    "neutral": ("surface_alt", "on_surface_var", "•"),
}

DIFFICULTY_KIND = {"easy": "success", "medium": "warning", "hard": "error"}


def chip(parent, text, kind="neutral"):
    bg, fg, _ = _KIND.get(kind, _KIND["neutral"])
    return ctk.CTkLabel(parent, text=text, height=24, corner_radius=12, fg_color=C[bg],
                        text_color=C[fg], font=font("small_strong"), padx=10)


class Banner(ctk.CTkLabel):
    """Thông báo trạng thái trong form. Ẩn/hiện mà không làm xô lệch layout pack."""

    def __init__(self, parent, wraplength=320):
        super().__init__(parent, text="", corner_radius=R["sm"], anchor="w", justify="left",
                         font=font("small"), wraplength=wraplength, padx=SP["md"], pady=SP["sm"])
        self._pack_kw = None
        self._grid_mode = False

    def attach(self, after, **pack_kw):
        """Dùng với cha xếp bằng pack: banner sẽ hiện ngay sau `after`."""
        self._pack_kw = dict(pack_kw, after=after)

    def attach_grid(self, **grid_kw):
        self._grid_mode = True
        self.grid(**grid_kw)
        self.grid_remove()

    def show(self, kind, text):
        bg, fg, icon = _KIND.get(kind, _KIND["info"])
        self.configure(text=f"{icon}  {text}", fg_color=C[bg], text_color=C[fg])
        if self._grid_mode:
            self.grid()
        elif self._pack_kw and not self.winfo_ismapped():
            self.pack(**self._pack_kw)

    def hide(self):
        if self._grid_mode:
            self.grid_remove()
        else:
            self.pack_forget()


class StateView(ctk.CTkFrame):
    """Màn hình trạng thái: empty / loading / error — luôn có hướng dẫn + hành động."""

    def __init__(self, parent):
        super().__init__(parent, fg_color="transparent")
        inner = ctk.CTkFrame(self, fg_color="transparent")
        inner.place(relx=0.5, rely=0.45, anchor="center")
        self.icon = ctk.CTkLabel(inner, text="", font=font("display"), text_color=C["on_surface_var"])
        self.icon.pack()
        self.title = ctk.CTkLabel(inner, text="", font=font("title"), text_color=C["on_surface"])
        self.title.pack(pady=(SP["sm"], SP["xs"]))
        self.body = ctk.CTkLabel(inner, text="", font=font("body"), text_color=C["on_surface_var"],
                                 wraplength=420, justify="center")
        self.body.pack()
        self.progress = ctk.CTkProgressBar(inner, mode="indeterminate", width=240, height=4,
                                           progress_color=C["primary"], fg_color=C["surface_alt"])
        self.action = tonal_button(inner, "", None)
        self.bind("<Configure>", lambda e: self.body.configure(wraplength=max(200, min(460, e.width - 48))))

    def show(self, icon, title, body="", action_text=None, action=None, loading=False):
        self.icon.configure(text=icon)
        self.title.configure(text=title)
        self.body.configure(text=body)
        self.progress.stop()
        self.progress.pack_forget()
        self.action.pack_forget()
        if loading:
            self.progress.pack(pady=(SP["base"], 0))
            self.progress.start()
        if action_text and action:
            self.action.configure(text=action_text, command=action)
            self.action.pack(pady=(SP["base"], 0))
