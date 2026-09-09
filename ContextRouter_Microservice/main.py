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
# Toan bo Tang 2 chay tai day, cung tien trinh voi Tang 1: dung chung
# tree-sitter da nap san trong RAM, khong co vong HTTP trung gian nao.
# .NET chi goi mot lan endpoint nay va tra thang ket qua ve Orchestrator.
# =====================================================================
from typing import Optional

from hybrid_context import generate_hybrid_context


class HybridContextMetrics(BaseModel):
    sloc: int = 0
    cyclomaticComplexity: int = 0


class HybridContextRequest(BaseModel):
    moduleId: str = ""
    language: str = ""
    routingDecision: str = "ROUTE_HYBRID"
    rawSourceCode: str = ""
    metrics: Optional[HybridContextMetrics] = None


@app.post("/api/generate-hybrid-context")
async def generate_hybrid_context_endpoint(req: HybridContextRequest):
    """Sinh Ngu canh Lai: CFG skeleton + Critical Snippets + Enriched Metadata."""
    try:
        return generate_hybrid_context(
            code=req.rawSourceCode,
            language=req.language,
            module_id=req.moduleId,
            routing_decision=req.routingDecision,
            sloc=(req.metrics.sloc if req.metrics and req.metrics.sloc else None),
            cyclomatic_complexity=(req.metrics.cyclomaticComplexity
                                   if req.metrics and req.metrics.cyclomaticComplexity else None),
        )
    except Exception as ex:      # tra ve FAILED chu khong de exception thoat ra ngoai
        return {
            "status": "FAILED",
            "route_decision": req.routingDecision,
            "hybrid_prompt": "",
            "metrics": {},
            "moduleId": req.moduleId,
            "language": req.language,
            "parser": "tree-sitter",
            "message": "Loi khi sinh ngu canh lai: " + str(ex),
            "cfgSkeleton": "",
            "criticalSnippets": [],
            "criticalSnippetDetails": [],
            "enrichedMetadata": {},
            "cfgNodes": [],
            "cfgEdges": [],
            "warnings": [],
            "processing_time_ms": 0,
        }


@app.get("/api/generate-hybrid-context/health")
async def hybrid_context_health():
    return {"ok": True, "service": "tang2-hybrid-context", "parser": "tree-sitter"}



if __name__ == "__main__":
    import uvicorn
    # Chạy trên port 8000
    uvicorn.run(app, host="0.0.0.0", port=8000)
