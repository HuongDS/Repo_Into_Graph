@echo off
echo Khoi chay Python Server (Tang 1 + endpoint /api/parse-structure cua Tang 2)
"C:\Users\ASUS\AppData\Local\Programs\Python\Python313\python.exe" -m uvicorn main:app --reload --port 8000
pause
