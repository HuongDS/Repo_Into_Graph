@echo off
cd /d %~dp0

echo Dang kiem tra thu vien...
python -m pip install customtkinter requests openpyxl >nul 2>&1

echo Khoi dong Dashboard...
python app_main.py
pause
