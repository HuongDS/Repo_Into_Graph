from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from ast_analyzer import analyze_ast
from contextlib import asynccontextmanager

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Warm-up server by loading parsers and compiling sample code to avoid cold start
    try:
        analyze_ast("public class Dummy {}", "java")
        analyze_ast("public class Dummy {}", "cs")
    except Exception as e:
        print(f"Warm-up failed: {e}")
    yield

app = FastAPI(title="ADAPTIVE CONTEXT ROUTER", lifespan=lifespan)

class AnalyzeRequest(BaseModel):
    code: str
    language: str

class AnalyzeResponse(BaseModel):
    isValid: bool
    sloc: int
    vg: int
    rootNodeType: str
    hasError: bool

@app.post("/api/analyze-context", response_model=AnalyzeResponse)
async def analyze_context(req: AnalyzeRequest):
    lang = req.language.lower()
    
    SUPPORTED_LANGUAGES = {"java", "csharp", "c#", "dotnet"}
    if lang not in SUPPORTED_LANGUAGES:
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported language: {req.language}. Only Java and C# are supported."
        )
        
    code = req.code
    
    # Map common languages to extensions
    ext_map = {
        "csharp": "cs",
        "c#": "cs",
        "dotnet": "cs",
        "java": "java",
    }
    ext = ext_map.get(lang, "cs")
    
    is_valid, vg, sloc, root_node_type = analyze_ast(code, ext)
    
    return AnalyzeResponse(
        isValid=is_valid,
        sloc=sloc,
        vg=vg,
        rootNodeType=root_node_type,
        hasError=not is_valid
    )

# =====================================================================
# TANG 2 - HYBRID CONTEXT GENERATOR
# Endpoint phan tich cau truc ma nguon bang tree-sitter, tra ve cay cau lenh
# chuan hoa cho Tang 2 dung de dung CFG.
# Phan Tang 1 phia tren KHONG bi sua doi.
# =====================================================================
from cfg_structure import parse_structure

MAX_STRUCTURE_CODE_LENGTH = 200_000


class StructureRequest(BaseModel):
    code: str
    language: str


@app.post("/api/parse-structure")
async def parse_structure_endpoint(req: StructureRequest):
    """Tra ve cay cau lenh (methods -> statements) do tree-sitter phan tich."""
    code = req.code or ""
    if len(code) > MAX_STRUCTURE_CODE_LENGTH:
        code = code[:MAX_STRUCTURE_CODE_LENGTH]

    if not code.strip():
        return {"ok": False, "parser": "tree-sitter", "error": "code rong",
                "methods": [], "warnings": [], "hasError": False,
                "language": req.language}

    try:
        return parse_structure(code, req.language)
    except Exception as ex:      # tra loi ve de .NET tu quyet dinh fallback
        return {"ok": False, "parser": "tree-sitter", "error": str(ex),
                "methods": [], "warnings": [], "hasError": True,
                "language": req.language}


@app.get("/api/parse-structure/health")
async def parse_structure_health():
    return {"ok": True, "service": "tang2-structure", "parser": "tree-sitter"}



if __name__ == "__main__":
    import uvicorn
    # Chạy trên port 8000
    uvicorn.run(app, host="0.0.0.0", port=8000)
