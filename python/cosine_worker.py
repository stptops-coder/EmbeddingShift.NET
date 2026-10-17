"""One JSON request per line: query + document vectors -> ordered cosine scores."""
import json
import sys

import numpy as np


def score(request):
    query = np.asarray(request["query"], dtype=np.float64)
    documents = np.asarray(request["documents"], dtype=np.float64)
    if query.ndim != 1 or query.size == 0:
        raise ValueError("query must be a non-empty vector")
    if documents.ndim != 2 or documents.shape[1] != query.size:
        raise ValueError("document vectors must have the query dimension")
    if not np.isfinite(query).all() or not np.isfinite(documents).all():
        raise ValueError("vectors must contain finite numbers")

    # Return one score per document in input order.
    query_norm = np.linalg.norm(query)
    doc_norms = np.linalg.norm(documents, axis=1)
    scores = np.zeros(len(documents))
    valid = (query_norm > 1e-12) & (doc_norms > 1e-12)
    scores[valid] = (documents[valid] @ query) / (doc_norms[valid] * query_norm)
    return np.clip(scores, -1.0, 1.0).tolist()


if __name__ == "__main__":
    try:
        for line in sys.stdin:
            print(json.dumps(score(json.loads(line)), allow_nan=False), flush=True)
    except (KeyError, TypeError, ValueError, OSError) as error:
        print(f"Python cosine: {error}", file=sys.stderr)
        sys.exit(1)
