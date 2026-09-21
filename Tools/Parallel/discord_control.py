"""Private Discord control surface for the local development orchestrator."""
import asyncio
import json
import os
from pathlib import Path
import threading

import discord
from discord import app_commands
from discord.ext import tasks

import orchestrator
import pipeline_store


def required_env(name):
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"{name} is not set.")
    return value


TOKEN = required_env("DISCORD_BOT_TOKEN")
GUILD_ID = int(required_env("DISCORD_GUILD_ID"))
OWNER_ID = int(required_env("DISCORD_OWNER_ID"))
GUILD = discord.Object(id=GUILD_ID)


class ControlClient(discord.Client):
    def __init__(self):
        intents = discord.Intents.none()
        intents.guilds = True
        super().__init__(intents=intents)
        self.tree = app_commands.CommandTree(self)
        self.stop_event = threading.Event()
        self.worker = None
        self.sent_events = set()

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

    async def close(self):
        self.stop_event.set()
        self.event_pump.cancel()
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
                await channel.send(f"**{event['title']}**\n{event['body']}")
                self.sent_events.add(path.name)
                event["sent_utc"] = orchestrator.utc_now()
                orchestrator.write_json(path, event)

    @event_pump.before_loop
    async def before_event_pump(self):
        await self.wait_until_ready()


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
        state_note = "작업 계획을 곧 시작합니다." if not current.get("paused", True) else "현재 일시정지 상태입니다. `/dev-start`를 실행하세요."
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


@client.tree.command(name="dev-approve", description="검토 중인 작업 계획을 승인합니다.", guild=GUILD)
@app_commands.describe(request_id="승인할 요청 ID")
async def dev_approve(interaction: discord.Interaction, request_id: str):
    if not await client.interaction_check(interaction):
        return
    request = next((item for item in pipeline_store.list_requests("PlanReview")
                    if item["id"] == request_id), None)
    if request is None:
        await interaction.response.send_message("승인 가능한 계획을 찾지 못했습니다.", ephemeral=True)
        return
    plan = pipeline_store.read_json(request["plan_path"])
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
