"""Private Discord control surface for the local development orchestrator."""
import asyncio
import json
import os
from pathlib import Path
import threading
import time

import discord
from discord import app_commands
from discord.ext import tasks

import orchestrator
import pipeline_store
import codex_planner


def acquire_instance_lock():
    path = orchestrator.LOCAL / "discord-control.lock"
    path.parent.mkdir(parents=True, exist_ok=True)
    handle = path.open("a+b")
    handle.seek(0)
    if path.stat().st_size == 0:
        handle.write(b"0")
        handle.flush()
        handle.seek(0)
    try:
        if os.name == "nt":
            import msvcrt
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError as exc:
        handle.close()
        raise RuntimeError("Discord 자동화 봇이 이미 실행 중입니다.") from exc
    return handle


def required_env(name):
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"{name} is not set.")
    return value


TOKEN = required_env("DISCORD_BOT_TOKEN")
GUILD_ID = int(required_env("DISCORD_GUILD_ID"))
OWNER_ID = int(required_env("DISCORD_OWNER_ID"))
GUILD = discord.Object(id=GUILD_ID)


async def allowed_button(interaction):
    allowed = interaction.guild_id == GUILD_ID and interaction.user.id == OWNER_ID
    if not allowed:
        await interaction.response.send_message("허용되지 않은 사용자입니다.", ephemeral=True)
    return allowed


def find_request(item_id, status=None):
    return next((item for item in pipeline_store.list_requests(status) if item["id"] == item_id), None)


def find_task(item_id, status=None):
    return next((item for item in orchestrator.queue_tasks(status) if item["id"] == item_id), None)


def latest_run(item_id):
    runs = []
    for path in (orchestrator.LOCAL / "runs").glob("*/task.json"):
        try:
            data = orchestrator.read_json(path)
        except (OSError, ValueError):
            continue
        if data.get("id") == item_id:
            runs.append(path.parent)
    return max(runs, key=lambda path: path.stat().st_mtime) if runs else None


def run_log_tail(run_path):
    if not run_path:
        return "로그 수집 중"
    chunks = []
    for name in ("stdout.json", "stderr.log"):
        path = run_path / name
        if path.exists():
            try:
                text = path.read_text(encoding="utf-8", errors="replace").strip()
            except OSError:
                text = ""
            if text:
                chunks.append(text[-1400:])
    if not chunks:
        return "에이전트 시작 중"
    text = "\n".join(chunks).replace("```", "''' ")
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    return discord.utils.escape_mentions(" ".join(lines[-4:])[-1400:])


class PlanDecisionView(discord.ui.View):
    def __init__(self, request_id):
        super().__init__(timeout=None)
        self.request_id = request_id
        approve = discord.ui.Button(label="계획 승인", style=discord.ButtonStyle.success,
                                    custom_id=f"automation:approve:{request_id}")
        reject = discord.ui.Button(label="작업 중지", style=discord.ButtonStyle.danger,
                                   custom_id=f"automation:reject:{request_id}")
        approve.callback = self.approve
        reject.callback = self.reject
        self.add_item(approve)
        self.add_item(reject)

    async def approve(self, interaction):
        if not await allowed_button(interaction):
            return
        request = find_request(self.request_id, "PlanReview")
        if request is None:
            await interaction.response.send_message("이미 처리되었거나 승인할 수 없는 계획입니다.", ephemeral=True)
            return
        plan = pipeline_store.read_json(request["plan_path"])
        orchestrator.materialize_plan(request, plan)
        pipeline_store.transition(request, "Ready")
        await interaction.response.send_message("계획을 승인했습니다. 자동 작업을 계속합니다.", ephemeral=True)

    async def reject(self, interaction):
        if not await allowed_button(interaction):
            return
        request = find_request(self.request_id)
        if request is None or request["status"] in ("Done", "Rejected"):
            await interaction.response.send_message("이미 종료된 요청입니다.", ephemeral=True)
            return
        pipeline_store.transition(request, "Rejected", rejection_reason="Discord 버튼으로 중지")
        await interaction.response.send_message("요청을 중지했습니다.", ephemeral=True)


class AttentionDecisionView(discord.ui.View):
    def __init__(self, item_id):
        super().__init__(timeout=None)
        self.item_id = item_id
        retry = discord.ui.Button(label="재시도", style=discord.ButtonStyle.primary,
                                  custom_id=f"automation:retry:{item_id}")
        stop = discord.ui.Button(label="중지", style=discord.ButtonStyle.danger,
                                 custom_id=f"automation:stop:{item_id}")
        retry.callback = self.retry
        stop.callback = self.stop
        self.add_item(retry)
        self.add_item(stop)

    async def retry(self, interaction):
        if not await allowed_button(interaction):
            return
        request = find_request(self.item_id, "Attention")
        if request:
            pipeline_store.transition(request, "Requested", error=None)
            await interaction.response.send_message("요청 계획을 다시 시도합니다.", ephemeral=True)
            return
        task = find_task(self.item_id, "Attention")
        if task:
            task["status"] = "Ready" if task.get("worktree") and task.get("task_file") else "Planned"
            task.pop("last_failure", None)
            orchestrator.write_json(task["_path"], {key: value for key, value in task.items() if key != "_path"})
            await interaction.response.send_message("작업을 다시 대기열에 넣었습니다.", ephemeral=True)
            return
        await interaction.response.send_message("이미 처리되었거나 재시도할 수 없습니다.", ephemeral=True)

    async def stop(self, interaction):
        if not await allowed_button(interaction):
            return
        request = find_request(self.item_id)
        if request and request["status"] not in ("Done", "Rejected"):
            pipeline_store.transition(request, "Rejected", rejection_reason="Discord 버튼으로 중지")
            await interaction.response.send_message("요청을 중지했습니다.", ephemeral=True)
            return
        task = find_task(self.item_id)
        if task and task["status"] != "Running":
            task["status"] = "Rejected"
            orchestrator.write_json(task["_path"], {key: value for key, value in task.items() if key != "_path"})
            await interaction.response.send_message("작업을 중지했습니다.", ephemeral=True)
            return
        await interaction.response.send_message("실행 중인 작업은 `/dev-pause`로 다음 배정부터 멈출 수 있습니다.", ephemeral=True)


def decision_view(item_id, status):
    if not item_id:
        return None
    if status == "PlanReview":
        return PlanDecisionView(item_id)
    if status == "Attention":
        return AttentionDecisionView(item_id)
    return None


class ControlClient(discord.Client):
    def __init__(self):
        intents = discord.Intents.none()
        intents.guilds = True
        super().__init__(intents=intents)
        self.tree = app_commands.CommandTree(self)
        self.stop_event = threading.Event()
        self.worker = None
        self.sent_events = set()
        self.last_progress_task = None
        self.last_progress_sent = 0.0
        self.last_progress_signature = None

    async def setup_hook(self):
        try:
            await self.tree.sync(guild=GUILD)
        except discord.Forbidden as exc:
            raise RuntimeError(
                "Discord 봇이 DISCORD_GUILD_ID 서버에 설치되어 있지 않거나 접근 권한이 없습니다. "
                "Developer Portal의 Installation에서 Guild Install 링크로 봇을 먼저 설치하세요."
            ) from exc
        self.worker = threading.Thread(target=orchestrator.serve, args=(self.stop_event,), daemon=True)
        self.worker.start()
        self.event_pump.start()
        self.progress_pump.start()
        for request in pipeline_store.list_requests():
            view = decision_view(request["id"], request["status"])
            if view:
                self.add_view(view)
        for task in orchestrator.queue_tasks("Attention"):
            self.add_view(AttentionDecisionView(task["id"]))

    async def close(self):
        self.stop_event.set()
        self.event_pump.cancel()
        self.progress_pump.cancel()
        await super().close()

    async def interaction_check(self, interaction):
        allowed = interaction.guild_id == GUILD_ID and interaction.user.id == OWNER_ID
        if not allowed:
            await interaction.response.send_message("허용되지 않은 사용자입니다.", ephemeral=True)
        return allowed

    @tasks.loop(seconds=5)
    async def event_pump(self):
        settings = orchestrator.config()["discord"]
        guild = self.get_guild(GUILD_ID)
        if guild is None:
            return
        channels = {channel.name: channel for channel in guild.text_channels}
        for path in sorted(orchestrator.EVENTS.glob("*.json")):
            if path.name in self.sent_events:
                continue
            event = orchestrator.read_json(path)
            if event.get("sent_utc"):
                self.sent_events.add(path.name)
                continue
            channel_name = settings.get(event["kind"] + "_channel", settings["attention_channel"])
            channel = channels.get(channel_name)
            if channel:
                data = event.get("data", {})
                view = decision_view(data.get("item_id"), data.get("status"))
                await channel.send(f"**{event['title']}**\n{event['body']}", view=view)
                self.sent_events.add(path.name)
                event["sent_utc"] = orchestrator.utc_now()
                orchestrator.write_json(path, event)

    @event_pump.before_loop
    async def before_event_pump(self):
        await self.wait_until_ready()

    @tasks.loop(seconds=30)
    async def progress_pump(self):
        current = orchestrator.state()
        task_id = current.get("running_task")
        if not task_id:
            self.last_progress_task = None
            return
        settings = orchestrator.config()
        now = time.monotonic()
        interval = float(settings.get("progress_update_seconds", 180))
        task = find_task(task_id)
        if task is None:
            return
        guild = self.get_guild(GUILD_ID)
        if guild is None:
            return
        channel = discord.utils.get(guild.text_channels, name=settings["discord"]["status_channel"])
        if channel is None:
            return
        run = latest_run(task_id)
        log_tail = run_log_tail(run) if run else "로그 수집 중"
        signature = f"{task_id}|{log_tail}"
        if (self.last_progress_task == task_id and signature == self.last_progress_signature
                and now - self.last_progress_sent < interval):
            return
        started = task.get("started_utc", "")
        elapsed = "계산 중"
        if started:
            try:
                start_time = discord.utils.parse_time(started)
                elapsed = f"{max(0, int((discord.utils.utcnow() - start_time).total_seconds() // 60))}분"
            except (TypeError, ValueError):
                pass
        queued = orchestrator.queue_tasks()
        waiting = sum(item["status"] in ("Planned", "Ready") for item in queued)
        model = task.get("model") or "Jev가 선택 중"
        files = ", ".join(task.get("allowed_files", [])[:3]) or "범위 확인 중"
        await channel.send(
            f"**작업 진행 중**\n`{task_id}` · 담당 `{task.get('agent', '선정 중')}` · 모델 `{model}` · 경과 {elapsed}\n"
            f"목표: {task.get('goal', '')}\n대상: `{files}`\n"
            f"최근 로그: {log_tail}\n다음: 작업 종료 후 변경 범위와 검증 결과를 확인합니다.\n"
            f"남은 대기 작업: {waiting}개"
        )
        self.last_progress_task = task_id
        self.last_progress_sent = now
        self.last_progress_signature = signature

    @progress_pump.before_loop
    async def before_progress_pump(self):
        await self.wait_until_ready()


INSTANCE_LOCK = acquire_instance_lock()
client = ControlClient()


@client.tree.command(name="dev-status", description="개발 자동화 상태를 확인합니다.", guild=GUILD)
async def dev_status(interaction: discord.Interaction):
    if not await client.interaction_check(interaction):
        return
    requests = pipeline_store.list_requests()
    queued = orchestrator.queue_tasks()
    current = orchestrator.state()
    latest_requests = [
        {"id": item["id"], "status": item["status"], "updated_utc": item.get("updated_utc")}
        for item in sorted(requests, key=lambda item: item.get("updated_utc", ""), reverse=True)[:5]
    ]
    latest_tasks = [
        {"id": item["id"], "status": item["status"], "agent": item.get("agent")}
        for item in sorted(queued, key=lambda item: item.get("started_utc", item.get("created_utc", "")), reverse=True)[:5]
    ]
    data = {
        "automation": "paused" if current.get("paused", True) else "running",
        "autonomy_mode": orchestrator.config().get("autonomy_mode", "guarded"),
        "current_task": current.get("running_task") or "none",
        "last_cycle_utc": current.get("last_cycle_utc", "not recorded yet"),
        "last_cycle_result": current.get("last_cycle_result", "not recorded yet"),
        "requests": {status: sum(1 for item in requests if item["status"] == status)
                     for status in sorted({item["status"] for item in requests})},
        "tasks": {status: sum(1 for item in queued if item["status"] == status)
                  for status in sorted({item["status"] for item in queued})},
        "latest_requests": latest_requests,
        "latest_tasks": latest_tasks,
    }
    await interaction.response.send_message(f"```json\n{json.dumps(data, ensure_ascii=False, indent=2)}\n```", ephemeral=True)


@client.tree.command(name="dev-start", description="새 개발 작업의 자동 배정을 시작합니다.", guild=GUILD)
async def dev_start(interaction: discord.Interaction):
    if not await client.interaction_check(interaction):
        return
    orchestrator.update_state(paused=False)
    await interaction.response.send_message("자동 개발을 시작했습니다.", ephemeral=True)


@client.tree.command(name="dev-pause", description="새 작업의 자동 배정을 일시정지합니다.", guild=GUILD)
async def dev_pause(interaction: discord.Interaction):
    if not await client.interaction_check(interaction):
        return
    orchestrator.update_state(paused=True)
    await interaction.response.send_message("새 작업 배정을 일시정지했습니다.", ephemeral=True)


@client.tree.command(name="dev-queue", description="대기 중인 개발 작업을 확인합니다.", guild=GUILD)
async def dev_queue(interaction: discord.Interaction):
    if not await client.interaction_check(interaction):
        return
    tasks = [{"id": task["id"], "status": task["status"], "goal": task["goal"],
              "priority": task.get("priority", 100)} for task in orchestrator.queue_tasks()]
    body = json.dumps(tasks, ensure_ascii=False, indent=2)
    await interaction.response.send_message(f"```json\n{body[:1800]}\n```", ephemeral=True)


@client.tree.command(name="dev-request", description="새 개발 요청을 등록합니다.", guild=GUILD)
@app_commands.describe(description="원하는 동작, 현재 문제, 기대 결과를 적어주세요.")
async def dev_request(interaction: discord.Interaction, description: str):
    if not await client.interaction_check(interaction):
        return
    try:
        request = pipeline_store.add_request(description)
        current = orchestrator.state()
        state_note = "계획부터 검증까지 자동으로 진행합니다." if not current.get("paused", True) else "현재 일시정지 상태입니다. `/dev-start`를 실행하세요."
        await interaction.response.send_message(
            f"요청을 등록했습니다: `{request['id']}`\n{state_note}\n진행 확인: `/dev-status` 또는 `/dev-detail item_id:{request['id']}`",
            ephemeral=True,
        )
    except ValueError as exc:
        await interaction.response.send_message(str(exc), ephemeral=True)


@client.tree.command(name="dev-detail", description="요청 또는 작업의 상세 상태를 확인합니다.", guild=GUILD)
@app_commands.describe(item_id="확인할 요청 또는 작업 ID")
async def dev_detail(interaction: discord.Interaction, item_id: str):
    if not await client.interaction_check(interaction):
        return
    candidates = pipeline_store.list_requests() + orchestrator.queue_tasks()
    item = next((entry for entry in candidates if entry.get("id") == item_id), None)
    if item is None:
        await interaction.response.send_message("해당 ID를 찾지 못했습니다.", ephemeral=True)
        return
    visible = {key: value for key, value in item.items()
               if key not in ("_path", "last_output", "last_failure")}
    body = json.dumps(visible, ensure_ascii=False, indent=2, default=str)
    await interaction.response.send_message(f"```json\n{body[:1800]}\n```", ephemeral=True)


@client.tree.command(name="dev-trace", description="실행 중인 작업의 최근 로그와 현재 단계를 확인합니다.", guild=GUILD)
@app_commands.describe(item_id="확인할 요청 또는 작업 ID")
async def dev_trace(interaction: discord.Interaction, item_id: str):
    if not await client.interaction_check(interaction):
        return
    task = find_task(item_id)
    if task is None:
        await interaction.response.send_message("해당 작업 ID를 찾지 못했습니다.", ephemeral=True)
        return
    run = latest_run(item_id)
    log_tail = run_log_tail(run) if run else "아직 외부 에이전트 실행 로그가 없습니다."
    files = ", ".join(task.get("allowed_files", [])) or "확정 전"
    body = (
        f"작업: {item_id}\n상태: {task.get('status')}\n담당: {task.get('agent', '미정')}\n"
        f"모델: {task.get('model', 'Jev 선택 전')}\n대상 파일: {files}\n\n최근 로그:\n{log_tail}"
    )
    await interaction.response.send_message(body[:1900], ephemeral=True)


@client.tree.command(name="dev-approve", description="검토 중인 작업 계획을 승인합니다.", guild=GUILD)
@app_commands.describe(request_id="승인할 요청 ID", decision="계획 질문에 대한 답변(필요한 경우)")
async def dev_approve(interaction: discord.Interaction, request_id: str, decision: str | None = None):
    if not await client.interaction_check(interaction):
        return
    request = next((item for item in pipeline_store.list_requests("PlanReview")
                    if item["id"] == request_id), None)
    if request is None:
        await interaction.response.send_message("승인 가능한 계획을 찾지 못했습니다.", ephemeral=True)
        return
    plan = pipeline_store.read_json(request["plan_path"])
    questions = codex_planner.user_questions(plan)
    if questions and not decision:
        body = "\n".join(f"{index + 1}. {question}" for index, question in enumerate(questions))
        await interaction.response.send_message(
            "결정이 필요한 항목이 있습니다.\n" + body[:1400]
            + f"\n답변: `/dev-approve request_id:{request_id} decision:<결정 내용>`",
            ephemeral=True,
        )
        return
    if questions:
        decision = " ".join(decision.split())[:1000]
        description = (request["description"] + "\n\n사용자 결정: " + decision)[:1500]
        pipeline_store.transition(
            request, "Requested", description=description, user_decision=decision,
            plan_path=None, plan_summary=None,
        )
        await interaction.response.send_message("결정 내용을 반영해 작업 계획을 다시 만듭니다.", ephemeral=True)
        return
    orchestrator.materialize_plan(request, plan)
    pipeline_store.transition(request, "Ready")
    await interaction.response.send_message("계획을 승인하고 작업 큐에 등록했습니다.", ephemeral=True)


@client.tree.command(name="dev-reject", description="요청을 거부하고 종료합니다.", guild=GUILD)
@app_commands.describe(request_id="거부할 요청 ID", reason="거부 이유")
async def dev_reject(interaction: discord.Interaction, request_id: str, reason: str):
    if not await client.interaction_check(interaction):
        return
    request = next((item for item in pipeline_store.list_requests()
                    if item["id"] == request_id and item["status"] not in ("Done", "Rejected")), None)
    if request is None:
        await interaction.response.send_message("거부할 요청을 찾지 못했습니다.", ephemeral=True)
        return
    try:
        pipeline_store.transition(request, "Rejected", rejection_reason=reason[:500])
    except ValueError as exc:
        await interaction.response.send_message(str(exc), ephemeral=True)
        return
    await interaction.response.send_message("요청을 종료했습니다.", ephemeral=True)


@client.tree.command(name="dev-retry", description="확인이 필요한 요청 또는 작업을 다시 시도합니다.", guild=GUILD)
@app_commands.describe(item_id="다시 시도할 요청 또는 작업 ID")
async def dev_retry(interaction: discord.Interaction, item_id: str):
    if not await client.interaction_check(interaction):
        return
    request = next((item for item in pipeline_store.list_requests("Attention")
                    if item["id"] == item_id), None)
    if request:
        pipeline_store.transition(request, "Requested", error=None)
        await interaction.response.send_message("요청 계획을 다시 시도합니다.", ephemeral=True)
        return
    task = next((item for item in orchestrator.queue_tasks("Attention") if item["id"] == item_id), None)
    if task:
        task["status"] = "Ready" if task.get("worktree") and task.get("task_file") else "Planned"
        task.pop("last_failure", None)
        orchestrator.write_json(task["_path"], {key: value for key, value in task.items() if key != "_path"})
        await interaction.response.send_message("작업을 다시 대기열에 넣었습니다.", ephemeral=True)
        return
    await interaction.response.send_message("재시도 가능한 항목을 찾지 못했습니다.", ephemeral=True)


client.run(TOKEN, log_handler=None)
