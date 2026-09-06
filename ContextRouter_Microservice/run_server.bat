@echo off
chcp 65001 >nul
echo Khởi chạy Python Server (Dùng chung cho cả Tầng 1 và Tầng 2)
"C:\Users\ASUS\AppData\Local\Programs\Python\Python313\python.exe" -m uvicorn main:app --reload --port 8000
pause
