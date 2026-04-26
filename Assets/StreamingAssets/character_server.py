"""
Character Name Extraction Server
---------------------------------
Returns each character's name and how many times they are mentioned
in each segment of the document (split into 10 equal chunks).

Requirements:
    pip install flask spacy
    python -m spacy download en_core_web_sm
"""

import spacy
from flask import Flask, request, jsonify

app = Flask(__name__)

print("Loading spaCy model...", flush=True)
nlp = spacy.load("en_core_web_sm")
print("Model ready.", flush=True)

NUM_SEGMENTS = 10


@app.route("/extract_characters", methods=["POST"])
def extract_characters():
    data = request.get_json()
    if not data or "text" not in data:
        return jsonify({"error": "Missing 'text' field"}), 400

    text = data["text"].strip()
    if not text:
        return jsonify({"characters": []})

    print(f"[Server] Received {len(text)} chars.", flush=True)

    # Pass 1 — find unique names from full document
    print("[Server] Running NLP on full document...", flush=True)
    full_doc = nlp(text)
    seen, names = set(), []
    for ent in full_doc.ents:
        if ent.label_ == "PERSON":
            name = ent.text.strip()
            if name and name not in seen:
                seen.add(name)
                names.append(name)
    print(f"[Server] Found {len(names)} unique names.", flush=True)

    # Pass 2 — run NLP once per segment (not once per character per segment)
    seg_size = max(1, len(text) // NUM_SEGMENTS)
    segments = [text[i * seg_size:(i + 1) * seg_size] for i in range(NUM_SEGMENTS)]

    seg_docs = []
    for i, seg in enumerate(segments):
        print(f"[Server] Processing segment {i + 1}/{NUM_SEGMENTS}...", flush=True)
        seg_docs.append(nlp(seg))
    print("[Server] All segments processed.", flush=True)

    # Pass 3 — count each name per segment
    characters = []
    for name in names:
        counts = [
            sum(1 for ent in doc.ents
                if ent.label_ == "PERSON" and ent.text.strip() == name)
            for doc in seg_docs
        ]
        characters.append({"name": name, "counts": counts})

    print(f"[Server] Done. Returning {len(characters)} characters.", flush=True)
    return jsonify({"characters": characters, "segments": NUM_SEGMENTS})


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "ok"})


if __name__ == "__main__":
    app.run(host="localhost", port=5001, debug=False)