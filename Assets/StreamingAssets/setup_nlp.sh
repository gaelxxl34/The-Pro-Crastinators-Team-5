#!/bin/bash
echo "Setting up NLP virtual environment..."

cd "$(dirname "$0")"

python3 -m venv nlp_env
if [ $? -ne 0 ]; then
    echo "ERROR: Could not create venv. Make sure Python 3 is installed."
    exit 1
fi

nlp_env/bin/pip install -r requirements.txt
if [ $? -ne 0 ]; then
    echo "ERROR: pip install failed."
    exit 1
fi

nlp_env/bin/python -m spacy download en_core_web_sm
if [ $? -ne 0 ]; then
    echo "ERROR: spaCy model download failed."
    exit 1
fi

echo ""
echo "Setup complete. You can now hit Play in Unity."