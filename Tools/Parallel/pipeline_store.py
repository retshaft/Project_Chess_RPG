"""Durable request storage for the autonomous development pipeline."""
import datetime as dt
import json
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parents[2]
LOCAL = ROOT / ".parallel"
REQUESTS = LOCAL / "requests"
PLANS = LOCAL / "plans"


def utc_now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def slug(text):
    words = re.findall(r"[a-z0-9]+", text.lower())[:5]
    base = "-".join(words) or "request"
    return (base[:39].rstrip("-") + "-" + uuid.uuid4().hex[:8])[:48]


def add_request(description, source="discord"):
    description = " ".join(str(description).split())
    if not 10 <= len(description) <= 1500:
        raise ValueError("요청은 10~1500자로 작성하세요.")
    request_id = slug(description)
    record = {
        "id": request_id,
        "description": description,
        "source": source,
        "status": "Requested",
        "created_utc": utc_now(),
        "updated_utc": utc_now(),
        "history": [{"status": "Requested", "at": utc_now()}],
    }
    write_json(REQUESTS / f"{request_id}.json", record)
    return record


def list_requests(status=None):
    REQUESTS.mkdir(parents=True, exist_ok=True)
    result = []
    for path in REQUESTS.glob("*.json"):
        item = read_json(path)
        item["_path"] = str(path)
        if status is None or item.get("status") == status:
            result.append(item)
    return sorted(result, key=lambda item: item["created_utc"])


def transition(record, new_status, **fields):
    current = record.get("status")
    allowed = {
        "Requested": {"Planning", "Rejected"},
        "Planning": {"Ready", "PlanReview", "Attention"},
        "PlanReview": {"Ready", "Rejected"},
        "Ready": {"Running", "Rejected"},
        "Running": {"Review", "Attention"},
        "Review": {"IntegrationReady", "Attention", "Rejected"},
        "IntegrationReady": {"AwaitingApproval", "Done", "Rejected"},
        "AwaitingApproval": {"Done", "Rejected"},
        "Attention": {"Requested", "Ready", "Rejected"},
        "Rejected": set(),
        "Done": set(),
    }
    if new_status not in allowed.get(current, set()):
        raise ValueError(f"Invalid pipeline transition: {current} -> {new_status}")
    record.update(fields)
    record["status"] = new_status
    record["updated_utc"] = utc_now()
    record.setdefault("history", []).append({"status": new_status, "at": record["updated_utc"]})
    path = Path(record.get("_path", REQUESTS / f"{record['id']}.json"))
    write_json(path, {key: value for key, value in record.items() if key != "_path"})
    record["_path"] = str(path)
    return record
