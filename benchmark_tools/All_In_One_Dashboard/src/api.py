"""Gọi Backend API và dịch lỗi sang câu dễ hiểu."""
import json
import requests
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

API_BASE = "https://localhost:55060"


class ApiError(Exception):
    pass


def _explain(e):
    if isinstance(e, requests.exceptions.ConnectionError):
        return ("Không kết nối được Backend (" + API_BASE + "). "
                "Hãy chạy: dotnet run --project Repo_Into_Graph_API rồi thử lại.")
    if isinstance(e, requests.exceptions.Timeout):
        return "Backend phản hồi quá lâu (timeout). Thử lại, hoặc kiểm tra API key của LLM."
    if isinstance(e, requests.exceptions.HTTPError) and e.response is not None:
        r = e.response
        msg = None
        try:
            j = r.json()
            if isinstance(j, dict):
                msg = j.get("message") or j.get("detail") or j.get("title")
                errs = j.get("errors")
                if isinstance(errs, dict) and errs:
                    parts = [f"{k}: {', '.join(v) if isinstance(v, list) else v}" for k, v in errs.items()]
                    msg = ((msg + " — ") if msg else "") + "; ".join(parts)
        except ValueError:
            msg = (r.text or "").strip()[:300] or None
        return f"Lỗi {r.status_code}: {msg or r.reason}"
    return str(e)


def request(method, path, parse_json=True, **kw):
    kw.setdefault("verify", False)
    kw.setdefault("timeout", 30)
    try:
        r = requests.request(method, API_BASE + path, **kw)
        r.raise_for_status()
    except requests.exceptions.RequestException as e:
        raise ApiError(_explain(e)) from e
    if not parse_json:
        return r.text
    if not r.content:
        return None
    try:
        return r.json()
    except ValueError:
        raise ApiError("Backend trả về dữ liệu không phải JSON.")


def evaluate(payload, timeout=180):
    """Chấm 1 câu. LLM đôi khi trả JSON lỗi → vẫn trả về text thô để không mất dữ liệu."""
    text = request("POST", "/api/student-evaluation/evaluate", parse_json=False,
                   json=payload, timeout=timeout)
    try:
        return json.loads(text)
    except ValueError:
        return {"raw_response": text}
