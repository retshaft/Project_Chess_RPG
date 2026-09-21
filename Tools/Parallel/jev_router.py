"""Small, typed Jev decisions for the development control plane."""
from dataclasses import dataclass
import json
import os


@dataclass(frozen=True)
class RouteDecision:
    task_id: str
    agent: str
    task_confidence: float
    agent_confidence: float
    raw: dict

    @property
    def confidence(self):
        return min(self.task_confidence, self.agent_confidence)


def _as_dict(value):
    if hasattr(value, "model_dump"):
        return value.model_dump(mode="json")
    if hasattr(value, "dict"):
        return value.dict()
    return value if isinstance(value, dict) else vars(value)


def _choice(response, name):
    answers = getattr(response, "answers", None)
    if answers is None and isinstance(response, dict):
        answers = response.get("answers", response.get("choices", {}))
    answer = answers[name]
    data = _as_dict(answer)
    return data["choice"], float(data.get("confidence", 0.0)), data


def choose_route(tasks, repository_state, model="jev-latest"):
    """Choose one ready task and one worker. No free-form output is accepted."""
    if not os.environ.get("TYPESAFE_API_KEY"):
        raise RuntimeError("TYPESAFE_API_KEY is not set.")
    if not tasks:
        return None

    from typesafe_sdk import Choice, TypeSafeClient

    task_options = {task["id"]: None for task in tasks}
    task_options["wait"] = None
    state = {
        "repository": repository_state,
        "ready_tasks": [
            {
                "id": task["id"],
                "goal": task["goal"],
                "priority": task.get("priority", 100),
                "attempts": task.get("attempts", 0),
                "allowed_files": task.get("allowed_files", []),
                "eligible_agents": task.get("eligible_agents", ["zcode", "antigravity"]),
                "last_failure": task.get("last_failure"),
            }
            for task in tasks
        ],
    }
    questions = {
        "task": Choice(
            instructions=(
                "Choose the safest useful task to run next. Prefer higher priority, clear scope, "
                "and low conflict risk. Choose wait when none should run unattended."
            ),
            criteria=task_options,
        ),
        "agent": Choice(
            instructions=(
                "Choose the best worker for the selected task. Use hold when the task needs a "
                "human decision or lead-developer design first."
            ),
            criteria={"zcode": None, "antigravity": None, "codex": None, "hold": None},
        ),
    }
    with TypeSafeClient() as client:
        try:
            response = client.system_one(state=state, questions=questions, model=model)
        except TypeError:
            response = client.system_one(state=state, questions=questions)
    task_id, task_confidence, task_raw = _choice(response, "task")
    agent, agent_confidence, agent_raw = _choice(response, "agent")
    return RouteDecision(
        task_id=task_id,
        agent=agent,
        task_confidence=task_confidence,
        agent_confidence=agent_confidence,
        raw={"task": task_raw, "agent": agent_raw, "state": state},
    )


def serialize_decision(decision):
    return json.loads(json.dumps(decision.__dict__, ensure_ascii=False, default=str))
