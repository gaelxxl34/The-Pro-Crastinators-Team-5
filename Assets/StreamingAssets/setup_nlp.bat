@echo off
echo Setting up NLP virtual environment...

cd /d "%~dp0"

python -m venv nlp_env
if errorlevel 1 (
    echo ERROR: Could not create venv. Make sure Python is installed.
    pause
    exit /b 1
)

nlp_env\Scripts\pip install -r requirements.txt
if errorlevel 1 (
    echo ERROR: pip install failed.
    pause
    exit /b 1
)

nlp_env\Scripts\python -m spacy download en_core_web_sm
if errorlevel 1 (
    echo ERROR: spaCy model download failed.
    pause
    exit /b 1
)

echo.
echo Setup complete. You can now hit Play in Unity.
pause