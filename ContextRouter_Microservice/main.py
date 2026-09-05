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

if __name__ == "__main__":
    import uvicorn
    # Chạy trên port 8000
    uvicorn.run(app, host="0.0.0.0", port=8000)
