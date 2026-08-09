"""
Checkmate RPG — 졸업작품 PPT 생성 스크립트
python-pptx 기반, 40+ 슬라이드
"""

from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN
from pptx.util import Inches, Pt
import os
from pptx.enum.dml import MSO_THEME_COLOR

# ─── 색상 팔레트 ───
BG        = RGBColor(0x0A, 0x0C, 0x14)   # 배경 (짙은 네이비)
BG2       = RGBColor(0x11, 0x14, 0x22)   # 서브 배경
CARD      = RGBColor(0x16, 0x19, 0x28)   # 카드 배경
GOLD      = RGBColor(0xF5, 0xC8, 0x42)   # 강조 골드
GOLD2     = RGBColor(0xE8, 0xA8, 0x20)   # 서브 골드
CYAN      = RGBColor(0x00, 0xE5, 0xFF)   # 사이언
VIOLET    = RGBColor(0x9B, 0x59, 0xFF)   # 보라
RED       = RGBColor(0xFF, 0x47, 0x57)   # 레드
GREEN     = RGBColor(0x2E, 0xCC, 0x71)   # 그린
WHITE     = RGBColor(0xFF, 0xFF, 0xFF)
MUTED     = RGBColor(0x7A, 0x7F, 0x9A)
ORANGE    = RGBColor(0xFF, 0x70, 0x43)
VALHALLA  = RGBColor(0x7E, 0xB8, 0x5A)
AETHER    = RGBColor(0x00, 0xE5, 0xFF)
NEON      = RGBColor(0x9B, 0x59, 0xFF)

IMG_DIR = os.path.join(os.path.dirname(__file__), "Images")

# ─── PPT 초기화 (16:9 와이드) ───
prs = Presentation()
prs.slide_width  = Inches(13.33)
prs.slide_height = Inches(7.5)

W = prs.slide_width
H = prs.slide_height

def blank_slide(prs):
    layout = prs.slide_layouts[6]   # 완전 빈 슬라이드
    return prs.slides.add_slide(layout)

# ─── 배경 채우기 ───
def set_bg(slide, color=BG):
    from pptx.oxml.ns import qn
    from lxml import etree
    bg = slide.background
    fill = bg.fill
    fill.solid()
    fill.fore_color.rgb = color

# ─── 사각형 박스 추가 ───
def add_rect(slide, l, t, w, h, fill=None, line_color=None, line_width=Pt(1)):
    from pptx.util import Pt
    shape = slide.shapes.add_shape(1, l, t, w, h)
    shape.line.width = line_width
    if fill:
        shape.fill.solid()
        shape.fill.fore_color.rgb = fill
    else:
        shape.fill.background()
    if line_color:
        shape.line.color.rgb = line_color
    else:
        shape.line.fill.background()
    return shape

# ─── 텍스트박스 추가 ───
def add_text(slide, text, l, t, w, h,
             font_size=Pt(14), bold=False, color=WHITE,
             align=PP_ALIGN.LEFT, italic=False, font_name="Malgun Gothic"):
    txBox = slide.shapes.add_textbox(l, t, w, h)
    tf = txBox.text_frame
    tf.word_wrap = True
    p = tf.paragraphs[0]
    p.alignment = align
    run = p.add_run()
    run.text = text
    run.font.size = font_size
    run.font.bold = bold
    run.font.color.rgb = color
    run.font.italic = italic
    run.font.name = font_name
    return txBox

# ─── 멀티라인 텍스트박스 ───
def add_multiline(slide, lines, l, t, w, h,
                  font_size=Pt(13), color=MUTED, font_name="Malgun Gothic",
                  line_spacing=None):
    txBox = slide.shapes.add_textbox(l, t, w, h)
    tf = txBox.text_frame
    tf.word_wrap = True
    for i, (text, cfg) in enumerate(lines):
        if i == 0:
            p = tf.paragraphs[0]
        else:
            p = tf.add_paragraph()
        p.alignment = cfg.get("align", PP_ALIGN.LEFT)
        run = p.add_run()
        run.text = text
        run.font.size = cfg.get("size", font_size)
        run.font.bold = cfg.get("bold", False)
        run.font.color.rgb = cfg.get("color", color)
        run.font.name = font_name
    return txBox

# ─── 이미지 추가 (안전) ───
def add_image(slide, filename, l, t, w, h=None):
    path = os.path.join(IMG_DIR, filename)
    if not os.path.exists(path):
        return None
    if h:
        pic = slide.shapes.add_picture(path, l, t, w, h)
    else:
        pic = slide.shapes.add_picture(path, l, t, w)
    return pic

# ─── 섹션 태그 라벨 ───
def add_tag(slide, text, l, t, color=GOLD):
    tag = add_rect(slide, l, t, Inches(2.2), Inches(0.32),
                   fill=RGBColor(0x16, 0x14, 0x05) if color==GOLD else None,
                   line_color=color, line_width=Pt(1))
    add_text(slide, text, l + Inches(0.1), t + Inches(0.04),
             Inches(2.0), Inches(0.28),
             font_size=Pt(9), bold=True, color=color, align=PP_ALIGN.CENTER)

# ─── 슬라이드 번호 ───
slide_num = [0]
def add_slide_num(slide):
    slide_num[0] += 1
    add_text(slide, str(slide_num[0]), W - Inches(0.6), H - Inches(0.4),
             Inches(0.5), Inches(0.3),
             font_size=Pt(9), color=MUTED, align=PP_ALIGN.RIGHT)

# ─── 수평선 ───
def add_hline(slide, y, color=GOLD, alpha_approx=None):
    from pptx.util import Pt
    ln = slide.shapes.add_shape(1, Inches(0.4), y, W - Inches(0.8), Inches(0.01))
    ln.fill.solid()
    ln.fill.fore_color.rgb = color
    ln.line.fill.background()

# ══════════════════════════════════════════════════════
#  PART 1: 기획서 (전반부)
# ══════════════════════════════════════════════════════

# ─── 슬라이드 1: 표지 ───
def slide_cover():
    sl = blank_slide(prs)
    set_bg(sl)

    # 배경 체스판 패턴 암시 — 우측 장식용 박스들
    for row in range(4):
        for col in range(4):
            if (row + col) % 2 == 0:
                add_rect(sl,
                    W - Inches(3.5) + col * Inches(0.85),
                    Inches(0.5) + row * Inches(0.85),
                    Inches(0.82), Inches(0.82),
                    fill=RGBColor(0x14, 0x17, 0x28))

    # 좌측 골드 악센트 바
    add_rect(sl, Inches(0.4), Inches(1.5), Inches(0.08), Inches(4.2), fill=GOLD)

    # 서브 태그
    add_text(sl, "졸업작품  |  Unity 6000.3.10f1  |  2026",
             Inches(0.7), Inches(1.5), Inches(6), Inches(0.4),
             font_size=Pt(11), color=CYAN, bold=False)

    # 메인 타이틀
    add_text(sl, "Project:", Inches(0.7), Inches(2.1), Inches(10), Inches(0.7),
             font_size=Pt(28), bold=False, color=MUTED)
    add_text(sl, "Checkmate RPG",
             Inches(0.7), Inches(2.7), Inches(10), Inches(1.3),
             font_size=Pt(60), bold=True, color=WHITE)

    # 서브타이틀
    add_text(sl, "체스판 위의 실시간 전술전",
             Inches(0.7), Inches(4.0), Inches(9), Inches(0.7),
             font_size=Pt(24), bold=False, color=GOLD)

    # 키워드 태그들
    tags = ["AP 경제 시스템", "체스 이동 규칙", "넉백 / Splat", "원소 반응", "ActionBid AI"]
    for i, tag in enumerate(tags):
        add_rect(sl, Inches(0.7) + i * Inches(2.4), Inches(4.9),
                 Inches(2.2), Inches(0.38),
                 fill=RGBColor(0x16, 0x19, 0x28), line_color=GOLD, line_width=Pt(1))
        add_text(sl, tag, Inches(0.75) + i * Inches(2.4), Inches(4.94),
                 Inches(2.1), Inches(0.32),
                 font_size=Pt(10), color=GOLD, align=PP_ALIGN.CENTER)

    # 수치 정보
    stats = [("8×8", "전술 보드"), ("6", "체스 직군"), ("3", "진영"), ("50+", "세부 클래스"), ("12+", "원소 반응")]
    for i, (num, lbl) in enumerate(stats):
        x = Inches(0.7) + i * Inches(2.4)
        add_text(sl, num, x, Inches(5.7), Inches(2.2), Inches(0.55),
                 font_size=Pt(28), bold=True, color=GOLD, align=PP_ALIGN.CENTER)
        add_text(sl, lbl, x, Inches(6.2), Inches(2.2), Inches(0.35),
                 font_size=Pt(10), color=MUTED, align=PP_ALIGN.CENTER)

    add_slide_num(sl)

slide_cover()

# ─── 슬라이드 2: 목차 ───
def slide_toc():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.1), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_text(sl, "목  차", Inches(0.5), Inches(0.28), W - Inches(1), Inches(0.6),
             font_size=Pt(28), bold=True, color=WHITE)
    add_hline(sl, Inches(1.1), color=GOLD)

    sections = [
        ("PART 1 — 기획서 (Game Design Document)", GOLD, [
            "01. 프로젝트 개요 및 컨셉",
            "02. 세계관 — 프랙탈 존 & 싱크로나이저",
            "03. 핵심 플레이 3대 기둥",
            "04. AP 경제 시스템 설계",
            "05. 체스 이동 규칙 및 직군 명세",
            "06. 물리 엔진 2.0 — 넉백 & Splat",
            "07. 타일 시스템 & 지형 효과",
            "08. 원소 반응 콤보 시스템",
            "09. CC 매트릭스 시스템",
            "10. 3개 진영 설정",
            "11. 적 AI 알고리즘",
            "12. 성장 메타 시스템",
        ]),
        ("PART 2 — 개발 현황 (Development Status)", CYAN, [
            "13. 기술 스택 & 아키텍처",
            "14. 구현된 핵심 시스템 상세",
            "15. AP Manager / ActionScheduler / Interrupt System",
            "16. 체스 이동 패턴 구현",
            "17. 상태이상 & 원소 반응 구현",
            "18. AI 시스템 (ActionBid / UnitBrain)",
            "19. Replay & 결정론적 시뮬레이션",
            "20. 제작 에셋 현황",
            "21. 개발 마일스톤 & 향후 계획",
        ]),
    ]

    y = Inches(1.3)
    for sec_title, sec_color, items in sections:
        add_rect(sl, Inches(0.4), y, W - Inches(0.8), Inches(0.38),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=sec_color, line_width=Pt(1.5))
        add_text(sl, sec_title, Inches(0.55), y + Inches(0.06),
                 W - Inches(1.1), Inches(0.3),
                 font_size=Pt(12), bold=True, color=sec_color)
        y += Inches(0.42)

        cols = [items[:6], items[6:]]
        for col_idx, col_items in enumerate(cols):
            cx = Inches(0.7) + col_idx * Inches(6.2)
            for item in col_items:
                add_text(sl, "▸  " + item, cx, y, Inches(6.0), Inches(0.28),
                         font_size=Pt(10.5), color=MUTED)
                y += Inches(0.29)
        y += Inches(0.15)

    add_slide_num(sl)

slide_toc()

# ─── 슬라이드 3: 프로젝트 개요 ───
def slide_overview():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  프로젝트 개요", Inches(0.5), Inches(0.12))
    add_text(sl, "프로젝트 개요 및 기획 방향", Inches(0.5), Inches(0.42),
             W - Inches(1), Inches(0.55),
             font_size=Pt(26), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    # 왼쪽: 기본 정보
    info_items = [
        ("프로젝트명", "Project: Checkmate RPG"),
        ("장  르", "실시간 전술 체스 RPG"),
        ("플랫폼", "PC (Windows)"),
        ("엔  진", "Unity 6000.3.10f1"),
        ("렌더링", "Universal Render Pipeline (URP)"),
        ("개발 기간", "2026년 졸업작품 프로젝트"),
    ]

    add_rect(sl, Inches(0.4), Inches(1.15), Inches(5.8), Inches(3.6),
             fill=CARD, line_color=RGBColor(0x25, 0x28, 0x3E), line_width=Pt(1))
    add_text(sl, "기본 정보", Inches(0.6), Inches(1.2), Inches(5.4), Inches(0.35),
             font_size=Pt(12), bold=True, color=GOLD)

    for i, (k, v) in enumerate(info_items):
        y = Inches(1.6) + i * Inches(0.48)
        add_rect(sl, Inches(0.55), y, Inches(1.5), Inches(0.36),
                 fill=RGBColor(0x1A, 0x1D, 0x32), line_color=None)
        add_text(sl, k, Inches(0.6), y + Inches(0.05), Inches(1.4), Inches(0.28),
                 font_size=Pt(9.5), bold=True, color=MUTED, align=PP_ALIGN.CENTER)
        add_text(sl, v, Inches(2.2), y + Inches(0.05), Inches(3.8), Inches(0.3),
                 font_size=Pt(10.5), bold=False, color=WHITE)

    # 오른쪽: 기획 핵심 방향
    add_rect(sl, Inches(6.5), Inches(1.15), Inches(6.4), Inches(3.6),
             fill=CARD, line_color=RGBColor(0x25, 0x28, 0x3E), line_width=Pt(1))
    add_text(sl, "핵심 기획 방향", Inches(6.7), Inches(1.2), Inches(6.0), Inches(0.35),
             font_size=Pt(12), bold=True, color=CYAN)

    direction_text = (
        "체스(Chess)를 명일방주(Arknights)식으로 해석한\n"
        "AP 기반 실시간 위치전 전략 RPG\n\n"
        "◆ 전통 체스의 이동 규칙 → 검증된 전술 깊이\n"
        "◆ 실시간 AP 경제 → 자원 관리 전략층 추가\n"
        "◆ 물리 기반 위치전 → 넉백·Splat 콤보 시스템\n"
        "◆ 원소 반응 레이어 → 전술 복합성 극대화\n"
        "◆ 전원 배치 시작 → 소환 대기 없이 즉각 지휘"
    )
    add_text(sl, direction_text, Inches(6.7), Inches(1.6), Inches(6.0), Inches(3.0),
             font_size=Pt(11), color=MUTED)

    # 하단: 차별화 포인트
    add_rect(sl, Inches(0.4), Inches(4.9), W - Inches(0.8), Inches(2.2),
             fill=RGBColor(0x0F, 0x12, 0x20), line_color=GOLD, line_width=Pt(1))
    add_text(sl, "기존 전략 게임과의 차별점",
             Inches(0.6), Inches(4.95), Inches(4), Inches(0.35),
             font_size=Pt(12), bold=True, color=GOLD)

    diffs = [
        ("❌ 기존 한계", "코스트 적립 대기 / 단순 유닛 강화 / 이동-공격 반복"),
        ("✅ 이 게임", "전원 배치 즉시 시작 / AP 자원 관리 / 체스 규칙 전술성"),
        ("✅ 이 게임", "무게·넉백 위치전 콤보 / 원소 반응 크로스오버 레이어"),
    ]
    for i, (label, desc) in enumerate(diffs):
        y = Inches(5.35) + i * Inches(0.38)
        c = RED if "❌" in label else GREEN
        add_text(sl, label, Inches(0.6), y, Inches(1.4), Inches(0.34),
                 font_size=Pt(9.5), bold=True, color=c)
        add_text(sl, desc, Inches(2.1), y, Inches(10.5), Inches(0.34),
                 font_size=Pt(10), color=MUTED)

    add_slide_num(sl)

slide_overview()

# ─── 슬라이드 4: 세계관 ───
def slide_worldbuilding():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  세계관 설정", Inches(0.5), Inches(0.12))
    add_text(sl, "세계관 — 프랙탈 존 & 싱크로나이저",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    blocks = [
        ("⚡ 프랙탈 존 (Fractal Zone)", GOLD,
         "물리·원소 법칙이 극대화되는 거대 에너지 돔 내부의 전장.\n"
         "탁 트인 평원·사막·수몰 유적이 주 무대. 시가지가 아닌 개활지이므로\n"
         "8×8 체스보드 전황을 와이드 뷰로 한눈에 파악 가능.\n"
         "기물 이동 시 바닥에 흙먼지·크레이터 생성 등 환경 상호작용 내포."),
        ("👑 킹 = 싱크로나이저 (Synchronizer)", CYAN,
         "플레이어는 후방 사령관이 아닌 전장에 배치되는 '킹(King)'.\n"
         "뇌파로 공간을 8×8 그리드로 안정화하며 전술 지휘 링크를 유지.\n"
         "마스터가 사망해 통제 영역이 붕괴되면 부대원 전원 강제 이탈 → 게임 오버.\n"
         "AP는 마스터의 '전술 연산력'. 위협 제거 시 AP 환불 구조로 내러티브와 연동."),
        ("🎭 진영 구조 (Faction Matrix)", VIOLET,
         "3개 진영이 프랙탈 존의 특수 자원을 차지하기 위해 기물을 파견.\n"
         "◆ 발할라 인더스트리 — 물리 중공업, 넉백/그랩 특화\n"
         "◆ 에테르 학원도시 — 원소 마법, 초전도·빙결 콤보 특화\n"
         "◆ 네온 신디케이트 — 기동·암살, 출혈·중독 특화"),
        ("🎨 비주얼 컨셉", GOLD2,
         "핵심 테마: '귀엽지만 묵직하고, 캐주얼하지만 치명적인 근미래 전술전'\n"
         "3~4등신 SD 비율 캐릭터 + URP 고해상도 렌더링\n"
         "Volumetric Lighting, 파티클 이펙트 활용한 원소 반응 시각화\n"
         "직군 실루엣 직관성: 캐릭터 본체보다 큰 무기·프랍으로 식별성 확보"),
    ]

    positions = [
        (Inches(0.4), Inches(1.15)),
        (Inches(6.65), Inches(1.15)),
        (Inches(0.4), Inches(4.0)),
        (Inches(6.65), Inches(4.0)),
    ]

    for (l, t), (title, color, body) in zip(positions, blocks):
        add_rect(sl, l, t, Inches(6.0), Inches(2.7),
                 fill=CARD, line_color=color, line_width=Pt(1.5))
        add_text(sl, title, l + Inches(0.2), t + Inches(0.12),
                 Inches(5.6), Inches(0.38),
                 font_size=Pt(12), bold=True, color=color)
        add_hline(sl, t + Inches(0.52), color=color)
        add_text(sl, body, l + Inches(0.2), t + Inches(0.58),
                 Inches(5.6), Inches(2.0),
                 font_size=Pt(9.5), color=MUTED)

    add_slide_num(sl)

slide_worldbuilding()

# ─── 슬라이드 5: 3대 기둥 ───
def slide_three_pillars():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  코어 컨셉", Inches(0.5), Inches(0.12))
    add_text(sl, "핵심 플레이 3대 기둥 (Three Core Pillars)",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    pillars = [
        ("01", "⚡ 전원 배치 시작", "All-Deployed Start", GOLD,
         "전투 시작 시 특수 스킬을 제외한 어떠한 소환 없이,\n"
         "아군(네임드 및 징집병)과 적군이 8×8 보드에\n"
         "전원 배치된 상태로 시작한다.\n\n"
         "코스트를 모으는 지루함 없이 64타일 전체의\n"
         "전황을 대화면으로 확인하며 즉각적인\n"
         "지휘 통제에 돌입한다.\n\n"
         "키보드 핫키와 마우스 정밀 타겟팅으로\n"
         "빠르고 쾌적한 지휘 경험 제공."),
        ("02", "🔋 행동력 경제", "AP Economy", CYAN,
         "전장의 모든 기물 행동에 AP가 소모된다.\n"
         "저비용 폰으로 자원을 벌고 고비용 퀸/룩을\n"
         "가동하는 유기적 자원 관리가 필수.\n\n"
         "◆ 기본 회복: +4 AP/sec 자연 회복\n"
         "◆ 처치 환불: 폰 처치 시 AP 환불\n"
         "◆ AP 부족 피드백: 중앙 게이지 붉게 점멸\n\n"
         "AP 수치는 마스터의 '전술 연산력'을 표현하는\n"
         "내러티브와 연동된 자원 시스템."),
        ("03", "♟ 기보와 공명", "Notation & Resonance", VIOLET,
         "체스 퍼즐을 풀어 스탯을 해방하는 '기보'\n"
         "시스템과 중복 캐릭터 획득 시 AP 소모를\n"
         "영구 감소시키는 '공명' 메타 성장 시스템.\n\n"
         "◆ 기보: TP를 소모해 체스판 노드를 열어\n"
         "   캐릭터 스탯 및 궁극 패시브 해금\n"
         "◆ 공명 3단계: 행동 소모 AP 영구 할인\n"
         "   (폰 -1, 나이트/비숍 -2, 룩 -5)\n\n"
         "졸업작품 범위에서는 기초 구조 구현 목표."),
    ]

    for i, (num, title, en, color, body) in enumerate(pillars):
        l = Inches(0.4) + i * Inches(4.27)
        add_rect(sl, l, Inches(1.15), Inches(4.1), Inches(6.1),
                 fill=CARD, line_color=color, line_width=Pt(1.5))

        # 번호 뱃지
        add_rect(sl, l + Inches(0.15), Inches(1.2), Inches(0.6), Inches(0.4),
                 fill=color, line_color=None)
        add_text(sl, num, l + Inches(0.15), Inches(1.22), Inches(0.6), Inches(0.36),
                 font_size=Pt(11), bold=True, color=BG, align=PP_ALIGN.CENTER)

        add_text(sl, title, l + Inches(0.85), Inches(1.22),
                 Inches(3.1), Inches(0.38),
                 font_size=Pt(14), bold=True, color=color)
        add_text(sl, en, l + Inches(0.2), Inches(1.65),
                 Inches(3.7), Inches(0.3),
                 font_size=Pt(9), color=RGBColor(0x50, 0x55, 0x70), italic=True)
        add_hline(sl, Inches(1.98), color=color)
        add_text(sl, body, l + Inches(0.2), Inches(2.05),
                 Inches(3.75), Inches(5.0),
                 font_size=Pt(10), color=MUTED)

    add_slide_num(sl)

slide_three_pillars()

# ─── 슬라이드 6: AP 경제 설계 ───
def slide_ap_design():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  AP 경제 시스템", Inches(0.5), Inches(0.12))
    add_text(sl, "AP 경제 시스템 — 자원 설계 명세",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=CYAN)

    # 수급 구조
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(6.0), Inches(2.5),
             fill=CARD, line_color=CYAN, line_width=Pt(1))
    add_text(sl, "AP 수급 구조", Inches(0.6), Inches(1.2), Inches(5.6), Inches(0.35),
             font_size=Pt(12), bold=True, color=CYAN)

    ap_sources = [
        ("자연 회복",       "+4 AP/sec  (전장 기본)",    GREEN),
        ("척후대(폰) 생존", "+0.8 AP/sec  (유닛당)",    GREEN),
        ("기수 폰 스킬",    "+65 AP / 18초  (폭발적)",  GOLD),
        ("적 처치 환불",    "+15 AP  (돌격대 처치 시)",  GOLD),
        ("책사 킹 클래스",  "스킬 사용 AP 비용 0",       VIOLET),
    ]
    for i, (src, val, color) in enumerate(ap_sources):
        y = Inches(1.6) + i * Inches(0.4)
        add_rect(sl, Inches(0.55), y, Inches(2.2), Inches(0.33),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=None)
        add_text(sl, src, Inches(0.6), y + Inches(0.04), Inches(2.1), Inches(0.26),
                 font_size=Pt(9.5), color=MUTED, align=PP_ALIGN.CENTER)
        add_text(sl, val, Inches(2.85), y + Inches(0.04), Inches(3.4), Inches(0.26),
                 font_size=Pt(10), color=color, bold=True)

    # 기물별 비용표
    add_rect(sl, Inches(6.6), Inches(1.15), Inches(6.3), Inches(3.2),
             fill=CARD, line_color=GOLD, line_width=Pt(1))
    add_text(sl, "기물별 이동 AP 비용", Inches(6.8), Inches(1.2), Inches(5.9), Inches(0.35),
             font_size=Pt(12), bold=True, color=GOLD)

    ap_costs = [
        ("♟ 폰 (Pawn)",   "8 AP",  "행동 속도: 보통~느림  |  쿨타임 4.0~5.0초"),
        ("♞ 나이트",       "24 AP", "행동 속도: 빠름~매우빠름  |  쿨타임 1.5~2.0초"),
        ("♝ 비숍",         "24 AP", "행동 속도: 보통  |  쿨타임 1.5~2.0초"),
        ("♜ 룩 (Rook)",   "36 AP", "행동 속도: 느림  |  쿨타임 6.0초"),
        ("♛ 퀸 (Queen)",  "48 AP", "행동 속도: 보통~느림"),
        ("♚ 킹 (King)",   "24 AP", "슈트 포지션에 따라 빠름~느림"),
    ]
    for i, (piece, cost, note) in enumerate(ap_costs):
        y = Inches(1.6) + i * Inches(0.44)
        add_text(sl, piece, Inches(6.8), y + Inches(0.04), Inches(1.8), Inches(0.33),
                 font_size=Pt(10.5), bold=True, color=WHITE)
        add_text(sl, cost, Inches(8.7), y + Inches(0.04), Inches(0.9), Inches(0.33),
                 font_size=Pt(11), bold=True, color=GOLD, align=PP_ALIGN.CENTER)
        add_text(sl, note, Inches(9.65), y + Inches(0.05), Inches(3.1), Inches(0.3),
                 font_size=Pt(8.5), color=MUTED)
        if i < len(ap_costs)-1:
            add_hline(sl, y + Inches(0.46), color=RGBColor(0x20, 0x24, 0x38))

    # 하단: AP 전략 구조
    add_rect(sl, Inches(0.4), Inches(3.8), W - Inches(0.8), Inches(1.5),
             fill=RGBColor(0x10, 0x13, 0x22), line_color=CYAN, line_width=Pt(1))
    add_text(sl, "AP 경제 전략 순환 구조", Inches(0.6), Inches(3.85),
             Inches(4), Inches(0.35), font_size=Pt(11), bold=True, color=CYAN)
    add_text(sl,
             "저비용 폰(8 AP) 대량 운용 → AP 수급 확보  →  고비용 퀸(48 AP) / 룩(36 AP) 가동  →  전선 돌파\n"
             "적 폰 처치(+15 AP 환불) → AP 풀 유지  →  AP 부족 시 행동 차단 + 붉은 점멸 UI 경고",
             Inches(0.6), Inches(4.2), W - Inches(1.2), Inches(1.0),
             font_size=Pt(11), color=MUTED)

    # 징집병 메커니즘
    add_rect(sl, Inches(0.4), Inches(5.45), W - Inches(0.8), Inches(1.8),
             fill=CARD, line_color=GOLD2, line_width=Pt(1))
    add_text(sl, "징집병 (Conscripts) 자동 생성 — '전원 배치 시작'의 핵심 메커니즘",
             Inches(0.6), Inches(5.5), W - Inches(1.2), Inches(0.35),
             font_size=Pt(11), bold=True, color=GOLD2)
    add_text(sl,
             "전투 시작 시, 플레이어 덱에 폰이 배치되지 않은 2열 빈칸에 '징집병'이 자동 생성.\n"
             "징집병 레벨 = 상위 4명 레벨 평균, 스탯은 척후대의 80% 수준. 스킬 없는 1차 고기방패 & AP 제물 역할.\n"
             "프로모션(8열 도달 시): 변신 + HP 비율 유지 + 디버프 해제 + AP 100% 회복 + 즉시 행동 가능.",
             Inches(0.6), Inches(5.88), W - Inches(1.2), Inches(1.3),
             font_size=Pt(10), color=MUTED)

    add_slide_num(sl)

slide_ap_design()

# ─── 슬라이드 7: 체스 직군 이동 규칙 ───
def slide_chess_movement():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  체스 이동 규칙", Inches(0.5), Inches(0.12))
    add_text(sl, "체스 이동 규칙 및 6대 직군 명세",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    pieces = [
        ("♟ 폰", "Pawn", "8 AP", GOLD,
         "• 전방 1칸 이동 (첫 이동 시 2칸)\n"
         "• 대각선 전방으로만 공격\n"
         "• 8열 도달 시 프로모션\n"
         "• 하위 직군: 척후대/돌격대/방어대/\n"
         "  기수/에이전트/공병 등 14종"),
        ("♞ 나이트", "Knight", "24 AP", CYAN,
         "• L자 점프 이동 (2+1칸)\n"
         "• 모든 기물을 뛰어넘어 이동\n"
         "• 하위 직군: 돌격기사/기동기사/\n"
         "  환영기사/마검사/전차 등 12종\n"
         "• 기동기사: 이동 AP 18로 할인"),
        ("♝ 비숍", "Bishop", "24 AP", VIOLET,
         "• 대각선 방향 슬라이딩\n"
         "• 동일 색 타일 이동 특성\n"
         "• 하위 직군: 대주교/사제/마도사/\n"
         "  저격수/성역술사 등 11종\n"
         "• 마도사: 체인 라이트닝 공격"),
        ("♜ 룩", "Rook", "36 AP", RED,
         "• 직선(가로·세로) 슬라이딩\n"
         "• 무게 Heavy, 쿨타임 6초\n"
         "• 하위 직군: 포트리스/집행관/\n"
         "  거신/주포/포탑 등 10종\n"
         "• 집행관: 그랩(당기기) CC 보유"),
        ("♛ 퀸", "Queen", "48 AP", ORANGE,
         "• 8방향 전방향 슬라이딩\n"
         "• 가장 높은 이동 비용\n"
         "• 하위 직군: 대왕/마녀/여제/\n"
         "  전쟁군주/기사왕 등 8종\n"
         "• 여제: 세계관 최강 단일 딜러"),
        ("♚ 킹", "King", "24 AP", GOLD2,
         "• 8방향 1칸 이동\n"
         "• 유일한 패배 조건\n"
         "• 슈트 교체로 클래스 변경:\n"
         "  군주/폭군/책사/성왕/\n"
         "  꼭두각시/거물/황제 7종"),
    ]

    cols = 3
    for i, (sym, en, cost, color, desc) in enumerate(pieces):
        row = i // cols
        col = i % cols
        l = Inches(0.4) + col * Inches(4.27)
        t = Inches(1.15) + row * Inches(3.1)

        add_rect(sl, l, t, Inches(4.1), Inches(3.0),
                 fill=CARD, line_color=color, line_width=Pt(1))

        # 헤더
        add_rect(sl, l, t, Inches(4.1), Inches(0.65),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=None)
        add_text(sl, sym, l + Inches(0.15), t + Inches(0.08),
                 Inches(1.1), Inches(0.52), font_size=Pt(20), bold=True, color=color)
        add_text(sl, en, l + Inches(1.1), t + Inches(0.08),
                 Inches(1.8), Inches(0.3), font_size=Pt(11), bold=True, color=WHITE)
        add_rect(sl, l + Inches(1.1), t + Inches(0.38), Inches(0.8), Inches(0.24),
                 fill=color, line_color=None)
        add_text(sl, cost, l + Inches(1.1), t + Inches(0.39), Inches(0.8), Inches(0.22),
                 font_size=Pt(8.5), bold=True, color=BG, align=PP_ALIGN.CENTER)

        add_text(sl, desc, l + Inches(0.15), t + Inches(0.72),
                 Inches(3.8), Inches(2.2), font_size=Pt(9.5), color=MUTED)

    add_slide_num(sl)

slide_chess_movement()

# ─── 슬라이드 8: 물리 엔진 ───
def slide_physics():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  물리 엔진", Inches(0.5), Inches(0.12))
    add_text(sl, "물리 엔진 2.0 — 무게, 넉백, 소프트 낙사(Splat)",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=RED)

    # 왼쪽
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(5.8), Inches(6.1),
             fill=CARD, line_color=RED, line_width=Pt(1))
    add_text(sl, "핵심 물리 규칙", Inches(0.6), Inches(1.2), Inches(5.4), Inches(0.35),
             font_size=Pt(12), bold=True, color=RED)

    physics_blocks = [
        ("무게 등급 (Weight Grade 0~4)", GOLD,
         "모든 기물은 0~4단계의 무게 등급 보유.\n"
         "넉백 거리 = Force - Weight (최소 1칸)\n"
         "비틀거림(Stagger) 상태: 임시 무게 1 감소 효과\n"
         "보스급 기물: 무게 감소 면역"),
        ("소프트 낙사 — 에너지 장벽", RED,
         "기존 '맵 밖 즉사(Ring-out)' 규칙 전면 폐기.\n"
         "맵 가장자리 = 타오르는 붉은색 '에너지 장벽(Red Zone)'.\n"
         "장벽 충돌 시: 즉사 대신 Splat 피해 적용."),
        ("충돌 피해 — Splat", RED,
         "넉백 궤적 상 기물·장애물·맵 가장자리 충돌 시:\n"
         "→ 충돌 직전 타일에서 강제 정지\n"
         "→ 밀린 대상 + 충돌 대상 양측 Max HP 10% 고정 피해\n"
         "→ 연쇄 충돌(당구장 효과)는 연산 과부하 방지상 불발"),
        ("당기기 — Grab", VIOLET,
         "집행관 등: 적을 자신의 전방으로 강제 인력.\n"
         "비틀거림(Stagger) 상태의 적에게 Grab 적중 시:\n"
         "→ 당긴 직후 첫 공격이 강제 치명타\n"
         "→ 40% 추가 피해 발생"),
    ]

    y = Inches(1.6)
    for title, color, body in physics_blocks:
        add_rect(sl, Inches(0.55), y, Inches(0.06), Inches(0.9),
                 fill=color, line_color=None)
        add_text(sl, title, Inches(0.75), y, Inches(5.2), Inches(0.3),
                 font_size=Pt(10.5), bold=True, color=color)
        add_text(sl, body, Inches(0.75), y + Inches(0.32), Inches(5.2), Inches(0.62),
                 font_size=Pt(9.5), color=MUTED)
        y += Inches(1.15)

    # 물리 상태이상
    add_rect(sl, Inches(0.55), y, Inches(5.5), Inches(0.55),
             fill=RGBColor(0x1A, 0x1D, 0x32), line_color=GOLD2, line_width=Pt(1))
    add_text(sl, "물리 상태이상 (Physical Status)",
             Inches(0.7), y + Inches(0.05), Inches(3), Inches(0.26),
             font_size=Pt(10), bold=True, color=GOLD2)
    add_text(sl, "비틀거림(Stagger) · 출혈(Bleed, 스택형) · 부상(Wound)",
             Inches(0.7), y + Inches(0.3), Inches(5.1), Inches(0.26),
             font_size=Pt(9.5), color=MUTED)

    # 오른쪽: 다이어그램 스타일
    add_rect(sl, Inches(6.5), Inches(1.15), Inches(6.4), Inches(6.1),
             fill=CARD, line_color=RED, line_width=Pt(1))
    add_text(sl, "넉백 판정 흐름도", Inches(6.7), Inches(1.2), Inches(6.0), Inches(0.35),
             font_size=Pt(12), bold=True, color=RED)

    # 플로우차트 시각화 (텍스트 다이어그램)
    flow_steps = [
        ("넉백 스킬 시전", GOLD, Inches(1.35)),
        ("대상 Weight 확인", MUTED, Inches(0.7)),
        ("넉백 거리 계산\nForce − Weight = 이동 거리", CYAN, Inches(0.9)),
        ("이동 경로 상 충돌 체크", MUTED, Inches(0.7)),
        ("장애물/기물 없음\n→ 목적지까지 이동", GREEN, Inches(0.8)),
        ("충돌 발생\n→ Splat: 양측 Max HP 10% 피해", RED, Inches(0.8)),
        ("맵 끝 장벽 충돌\n→ 에너지 장벽 대형 Splat", RED, Inches(0.8)),
    ]

    arrow_y = Inches(1.68)
    for step_text, color, box_h in flow_steps:
        add_rect(sl, Inches(7.2), arrow_y, Inches(5.3), box_h,
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=color, line_width=Pt(1))
        add_text(sl, step_text, Inches(7.35), arrow_y + Inches(0.08),
                 Inches(5.0), box_h - Inches(0.1),
                 font_size=Pt(10), color=color, bold=(color != MUTED))
        arrow_y += box_h + Inches(0.12)
        if arrow_y < Inches(6.8):
            add_text(sl, "▼", Inches(9.7), arrow_y - Inches(0.12),
                     Inches(0.4), Inches(0.2), font_size=Pt(9), color=MUTED, align=PP_ALIGN.CENTER)

    add_slide_num(sl)

slide_physics()

# ─── 슬라이드 9: 타일 시스템 ───
def slide_tiles():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  타일 시스템", Inches(0.5), Inches(0.12))
    add_text(sl, "타일 시스템 & 동적 지형 상호작용",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GREEN)

    tiles = [
        ("늪지대 (Swamp)", GREEN,
         "이동 AP 비용 × 2.0 배율 적용\n\n"
         "• 나이트의 '점프' 판정 스킬 사용 시에도\n"
         "  도착 지점이 늪지대면 페널티 강제 적용\n"
         "• 기동성 기물(나이트·정찰병)의 천적 타일\n"
         "• 지형술사(비숍 하위 직군)가 생성 가능",
         "Move Cost × 2"),
        ("가시밭 (Spikes)", RED,
         "매 1초마다 대상의 Max HP 3% 고정 피해\n\n"
         "• 방어력(Def) 완전 무시 True Damage\n"
         "• 아군·적군 구분 없이 위에 있으면 발동\n"
         "• 지형술사가 빈 타일에 생성 가능\n"
         "• 체력이 높은 탱커(룩)에게 특히 위협적",
         "Max HP 3% / sec (True DMG)"),
        ("성역 (Sanctuary)", CYAN,
         "진입한 '아군' 유닛에게만 적용되는 버프\n\n"
         "• 방어력 +15% 증가\n"
         "• 초당 HP 2% 지속 회복 (Regen)\n"
         "• 성역술사(비숍 하위 직군)가 생성\n"
         "• 적군에게는 효과 없음 (아군 전용)\n"
         "• 홀로그램 마법진 파티클 이펙트",
         "Def +15% / HP +2% Regen/sec"),
    ]

    for i, (name, color, desc, formula) in enumerate(tiles):
        l = Inches(0.4) + i * Inches(4.27)
        add_rect(sl, l, Inches(1.15), Inches(4.1), Inches(5.0),
                 fill=CARD, line_color=color, line_width=Pt(1.5))

        add_rect(sl, l, Inches(1.15), Inches(4.1), Inches(0.55),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=None)
        add_text(sl, name, l + Inches(0.2), Inches(1.2),
                 Inches(3.7), Inches(0.4), font_size=Pt(14), bold=True, color=color)

        add_rect(sl, l + Inches(0.2), Inches(1.75), Inches(3.7), Inches(0.35),
                 fill=color, line_color=None)
        add_text(sl, formula, l + Inches(0.25), Inches(1.78),
                 Inches(3.6), Inches(0.3),
                 font_size=Pt(10), bold=True, color=BG, align=PP_ALIGN.CENTER)

        add_text(sl, desc, l + Inches(0.2), Inches(2.18),
                 Inches(3.7), Inches(3.8), font_size=Pt(10), color=MUTED)

    # 하단: 고지대 폐기 결정
    add_rect(sl, Inches(0.4), Inches(6.3), W - Inches(0.8), Inches(0.95),
             fill=RGBColor(0x20, 0x10, 0x10), line_color=RED, line_width=Pt(1))
    add_text(sl, "⚠  설계 결정 사항",
             Inches(0.6), Inches(6.35), Inches(2.5), Inches(0.3),
             font_size=Pt(10), bold=True, color=RED)
    add_text(sl,
             "'고지대(High Ground) 사거리 증가' 효과는 기획에서 전면 삭제. "
             "체스 기물의 고정 이동 패턴(L자·대각선·직선)과 사거리 증가 개념이 시스템 충돌 우려.\n"
             "지형 고저차 개념은 TBD로 비워두고, 평면 8×8 보드의 타일 디버프/버프 구현에 집중.",
             Inches(0.6), Inches(6.68), W - Inches(1.2), Inches(0.5),
             font_size=Pt(9.5), color=MUTED)

    add_slide_num(sl)

slide_tiles()

# ─── 슬라이드 10: 원소 반응 ───
def slide_elements():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  원소 반응", Inches(0.5), Inches(0.12))
    add_text(sl, "원소 반응 콤보 다이어그램",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=VIOLET)

    reactions = [
        # (조합, 반응명, 색, 효과)
        ("🔥 불 + 불", "화상 (Burn)", ORANGE, "지속 마법 피해 + 마법 저항(Res) -20%"),
        ("🔥 불 + ❄ 냉기", "폭발 (Explosion)", RED, "십자 범위 즉시 마법 피해 + 경량 적 넉백"),
        ("🔥 불 + 🌿 자연", "발화 (Ignite)", ORANGE, "지속 피해 + 주변 타일에 화염 장판 생성"),
        ("🔥 불 + ⚡ 전기", "과부하 (Overload)", RED, "즉시 마법 피해 + 공격력(Atk) -25%"),
        ("⚡ 전기 + ⚡ 전기", "감전 (Shock)", GOLD, "연쇄 튕김 마법 피해 + 적 SP 게이지 소각"),
        ("⚡ 전기 + ❄ 냉기", "초전도 (Superconduct)", CYAN, "물리 방어(Def) -40% + 비틀거림 유발"),
        ("⚡ 전기 + 🌿 자연", "마비 (Paralysis)", GREEN, "기본 공격 완전 봉인 + 이동 절반 봉쇄"),
        ("❄ 냉기 + ❄ 냉기", "한기 (Chill)", CYAN, "모든 행동 AP 비용 +20% 증가"),
        ("❄ 한기 + ❄ 냉기", "빙결 (Freeze)", RGBColor(0xB0, 0xE0, 0xFF), "모든 행동 봉쇄. 보스는 Def/Res -25%로 변환"),
        ("❄ 빙결 + 물리", "쇄빙 (Shatter)", WHITE, "빙결 해제 + 막대한 추가 물리 피해"),
        ("❄ 냉기 + 🌿 자연", "괴사 (Necrosis)", GREEN, "HP 회복량 -60% 감소 (힐러 무력화)"),
        ("🌿 자연 + 🌿 자연", "중독 (Poison)", GREEN, "틱 마법 피해 지속"),
        ("☠ 중독 + 🌿 자연", "바이러스 (Virus)", GREEN, "중독 지속 최대화 + 주변 십자 범위 전염 (2차 재전염 Lock)"),
    ]

    # 4열 레이아웃
    cols = 4
    for i, (combo, name, color, effect) in enumerate(reactions):
        row = i // cols
        col = i % cols
        l = Inches(0.4) + col * Inches(3.2)
        t = Inches(1.15) + row * Inches(2.0)

        add_rect(sl, l, t, Inches(3.05), Inches(1.88),
                 fill=CARD, line_color=color, line_width=Pt(1))
        add_text(sl, combo, l + Inches(0.12), t + Inches(0.08),
                 Inches(2.8), Inches(0.3), font_size=Pt(8.5), color=MUTED, bold=False)
        add_text(sl, name, l + Inches(0.12), t + Inches(0.38),
                 Inches(2.8), Inches(0.35), font_size=Pt(11), bold=True, color=color)
        add_hline(sl, t + Inches(0.76), color=color)
        add_text(sl, effect, l + Inches(0.12), t + Inches(0.84),
                 Inches(2.8), Inches(0.95), font_size=Pt(9), color=MUTED)

    add_slide_num(sl)

slide_elements()

# ─── 슬라이드 11: CC 매트릭스 ───
def slide_cc():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  CC 매트릭스", Inches(0.5), Inches(0.12))
    add_text(sl, "군중 제어 (CC) 매트릭스 시스템",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=RED)

    cc_list = [
        ("⚡ 기절 (Stun)", RED,
         "이동·공격·스킬 사용 모두 불가\n"
         "지속시간 동안 자연 AP 회복 일시 중단\n"
         "집행관(룩)의 Grab 이후 연계 CC\n"
         "→ 가장 치명적인 Hard CC"),
        ("🔗 속박 (Root)", ORANGE,
         "이동 불가. 단 제자리에서\n"
         "기본 공격·스킬 사용은 가능\n"
         "매복자(폰)의 지뢰 밟을 때 발동\n"
         "→ 이동을 봉인, 행동은 허용"),
        ("🤐 침묵 (Silence)", VIOLET,
         "액티브 스킬 사용 봉인\n"
         "이동·기본 공격은 정상 가능\n"
         "마검사(나이트)의 SP 소각 공격\n"
         "→ 스킬 기반 기물 카운터"),
        ("🚫 무장해제 (Disarm)", CYAN,
         "기본 공격 불가\n"
         "이동·스킬 사용은 허용\n"
         "평타 기반 딜러 특화 카운터\n"
         "→ 스킬 기반 기물에게는 무해"),
        ("📢 도발 (Taunt)", GOLD,
         "적 AI 타겟팅 강제 오버라이드\n"
         "사거리 내 도발 시전자를 1순위 공격 강제\n"
         "점수 알고리즘을 무시하는 절대 Override\n"
         "→ 방어대·광대(폰) 보유 스킬"),
        ("👻 은신 (Stealth)", GREEN,
         "렌더링 반투명 처리\n"
         "적 타겟팅 대상 목록에서 강제 제외\n"
         "단, 광역기·지형 기믹 피해는 그대로 수령\n"
         "→ 선공 or 스킬 시전 즉시 해제"),
    ]

    for i, (name, color, desc) in enumerate(cc_list):
        row = i // 3
        col = i % 3
        l = Inches(0.4) + col * Inches(4.27)
        t = Inches(1.15) + row * Inches(3.0)

        add_rect(sl, l, t, Inches(4.1), Inches(2.85),
                 fill=CARD, line_color=color, line_width=Pt(1.5))
        add_rect(sl, l, t, Inches(4.1), Inches(0.5),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=None)
        add_text(sl, name, l + Inches(0.2), t + Inches(0.08),
                 Inches(3.7), Inches(0.38), font_size=Pt(14), bold=True, color=color)
        add_hline(sl, t + Inches(0.52), color=color)
        add_text(sl, desc, l + Inches(0.2), t + Inches(0.6),
                 Inches(3.7), Inches(2.1), font_size=Pt(10), color=MUTED)

    add_slide_num(sl)

slide_cc()

# ─── 슬라이드 12: 3개 진영 ───
def slide_factions():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  진영 설정", Inches(0.5), Inches(0.12))
    add_text(sl, "3개 진영 (Faction Matrix)",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    factions = [
        ("⚔  발할라 인더스트리", VALHALLA, "Faction A",
         "올리브 드랩 · 무광 강철 · 군사 중공업",
         "물리 / 넉백 특화",
         "우월한 체급(무게)과 물리 엔진(넉백/그랩) 특화 진영.\n"
         "압도적 화력과 물리력으로 전선을 밀어붙이고 낙사를 유도.\n\n"
         "◆ 폰: 방어대 (두꺼운 강화 외골격 탱커)\n"
         "◆ 룩: 집행관 (Grab CC) / 거신 (보스급 체급)\n"
         "◆ 주력 전술: 적을 맵 가장자리로 몰아 Splat 극대화"),
        ("🔮  에테르 학원도시", AETHER, "Faction B",
         "시안/화이트 · 테크웨어 · 홀로그램",
         "원소 / 상태이상 특화",
         "체급의 열세를 원소 반응과 상태이상 제어로 극복하는 진영.\n"
         "광역 마법과 입자 이펙트로 게임의 비주얼을 견인.\n\n"
         "◆ 비숍: 마도사 (체인 라이트닝) / 성역술사\n"
         "◆ 퀸: 여제 (세계관 최강 단일 딜러)\n"
         "◆ 주력 전술: 초전도(Def -40%) → 물리 덱 연계 크로스오버"),
        ("🗡  네온 신디케이트", NEON, "Faction C",
         "네온 그라피티 · 경량 기동 · 스트릿",
         "기동 / 암살 특화",
         "실행 속도 최고. 기동성으로 암살, 출혈, 중독으로 치고 빠지는 진영.\n"
         "변수 창출 및 템포 우위 전략.\n\n"
         "◆ 폰: 돌격대 (처치 시 AP +15 환불)\n"
         "◆ 나이트: 검객 (2연타) / 환영기사 (위치 교환)\n"
         "◆ 주력 전술: 은신 → 기습 → 치명타 + 출혈 지속 피해"),
    ]

    for i, (name, color, sub, theme, style, desc) in enumerate(factions):
        l = Inches(0.4) + i * Inches(4.27)
        add_rect(sl, l, Inches(1.15), Inches(4.1), Inches(6.1),
                 fill=CARD, line_color=color, line_width=Pt(1.5))

        add_rect(sl, l, Inches(1.15), Inches(4.1), Inches(0.65),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=None)
        add_text(sl, sub, l + Inches(0.2), Inches(1.18),
                 Inches(3.7), Inches(0.25), font_size=Pt(8.5), bold=True, color=color)
        add_text(sl, name, l + Inches(0.2), Inches(1.38),
                 Inches(3.7), Inches(0.38), font_size=Pt(13), bold=True, color=color)

        add_rect(sl, l + Inches(0.2), Inches(1.87), Inches(1.5), Inches(0.27),
                 fill=color, line_color=None)
        add_text(sl, style, l + Inches(0.2), Inches(1.89), Inches(1.5), Inches(0.24),
                 font_size=Pt(8), bold=True, color=BG, align=PP_ALIGN.CENTER)
        add_text(sl, theme, l + Inches(1.8), Inches(1.9),
                 Inches(2.1), Inches(0.25), font_size=Pt(8.5), color=MUTED)

        add_hline(sl, Inches(2.2), color=color)
        add_text(sl, desc, l + Inches(0.2), Inches(2.27),
                 Inches(3.7), Inches(4.8), font_size=Pt(9.5), color=MUTED)

    add_slide_num(sl)

slide_factions()

# ─── 슬라이드 13: 적 AI 알고리즘 ───
def slide_ai_design():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  AI 알고리즘", Inches(0.5), Inches(0.12))
    add_text(sl, "적 AI 의사결정 알고리즘",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=VIOLET)

    # 점수 기반 시스템
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(6.0), Inches(3.3),
             fill=CARD, line_color=VIOLET, line_width=Pt(1))
    add_text(sl, "틱 단위 점수 기반 연산 (Scoring System)",
             Inches(0.6), Inches(1.2), Inches(5.6), Inches(0.35),
             font_size=Pt(12), bold=True, color=VIOLET)

    scores = [
        ("King 처치",   "Kill Value +1000", RED,  "패배 조건 처치 = 최고 가치"),
        ("Queen 처치",  "Kill Value +50",   GOLD, ""),
        ("Pawn 처치",   "Kill Value +10",   MUTED,""),
        ("AP 가중치",   "Berserker/Tactician/Defensive 패턴", CYAN, "유닛 성향 ScriptableObject 기반"),
        ("위치 가중치", "킹 보호 거리, Setup Kill 각도",     GREEN, "공간 위협 연산"),
    ]
    for i, (k, v, c, note) in enumerate(scores):
        y = Inches(1.6) + i * Inches(0.53)
        add_rect(sl, Inches(0.55), y, Inches(2.0), Inches(0.38),
                 fill=RGBColor(0x16, 0x19, 0x2E), line_color=None)
        add_text(sl, k, Inches(0.6), y + Inches(0.06), Inches(1.9), Inches(0.28),
                 font_size=Pt(9.5), color=MUTED, align=PP_ALIGN.CENTER)
        add_text(sl, v, Inches(2.65), y + Inches(0.06), Inches(2.0), Inches(0.28),
                 font_size=Pt(10), bold=True, color=c)
        if note:
            add_text(sl, note, Inches(4.7), y + Inches(0.06), Inches(1.6), Inches(0.28),
                     font_size=Pt(8.5), color=MUTED)

    # Override 시스템
    add_rect(sl, Inches(0.4), Inches(4.55), Inches(6.0), Inches(2.7),
             fill=CARD, line_color=RED, line_width=Pt(1))
    add_text(sl, "절대 조건 트리거 (Override) — 점수 알고리즘을 무시하는 최우선 규칙",
             Inches(0.6), Inches(4.6), Inches(5.6), Inches(0.35),
             font_size=Pt(11), bold=True, color=RED)

    overrides = [
        ("Checkmate", "적 킹 처치 각이 나왔다면, 모든 손해를 감수하고 즉시 실행"),
        ("Danger",    "AI 킹이 공격 범위에 들어오면, 즉시 킹 대피 or 방어벽 구축"),
        ("Setup Kill","넉백으로 Red Zone / 벽 충돌 Splat 유도 각이 보이면 즉시 실행"),
        ("Taunted",   "도발 CC 시전자 존재 시, 점수 알고리즘 무시 → 강제 공격"),
    ]
    for i, (trigger, desc) in enumerate(overrides):
        y = Inches(5.02) + i * Inches(0.5)
        add_rect(sl, Inches(0.55), y, Inches(1.3), Inches(0.35),
                 fill=RED, line_color=None)
        add_text(sl, trigger, Inches(0.58), y + Inches(0.05), Inches(1.25), Inches(0.27),
                 font_size=Pt(9), bold=True, color=WHITE, align=PP_ALIGN.CENTER)
        add_text(sl, desc, Inches(1.95), y + Inches(0.05), Inches(4.3), Inches(0.3),
                 font_size=Pt(9.5), color=MUTED)

    # 오른쪽: AI 성향 프로파일
    add_rect(sl, Inches(6.6), Inches(1.15), Inches(6.3), Inches(6.1),
             fill=CARD, line_color=VIOLET, line_width=Pt(1))
    add_text(sl, "적 AI 성향 프로파일 (Enemy AI Profile)",
             Inches(6.8), Inches(1.2), Inches(5.9), Inches(0.35),
             font_size=Pt(12), bold=True, color=VIOLET)

    profiles = [
        ("광전사 (Berserker)", ORANGE,
         "AP가 모이는 즉시 스킬 시전.\n"
         "유저 진형 압박 우선.\n"
         "생존보다 딜 출력 극대화."),
        ("전략가 (Tactician)", GOLD,
         "AP를 철저히 보존.\n"
         "퀸·주포 등 고비용 기물을\n"
         "한 번에 가동하는 운영."),
        ("수비형 (Defensive)", CYAN,
         "'니가와(대기)' 전술 구사.\n"
         "유저가 사거리 내 진입 시만\n"
         "AP 소모해 카운터 공격."),
    ]

    for i, (name, color, desc) in enumerate(profiles):
        t = Inches(1.65) + i * Inches(1.8)
        add_rect(sl, Inches(6.75), t, Inches(5.9), Inches(1.65),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=color, line_width=Pt(1))
        add_text(sl, name, Inches(6.95), t + Inches(0.1),
                 Inches(3), Inches(0.35), font_size=Pt(11), bold=True, color=color)
        add_text(sl, desc, Inches(6.95), t + Inches(0.48),
                 Inches(5.5), Inches(1.1), font_size=Pt(10), color=MUTED)

    add_text(sl, "* 성향 데이터는 EnemyAIProfile ScriptableObject로 관리",
             Inches(6.8), Inches(7.0), Inches(5.9), Inches(0.28),
             font_size=Pt(8.5), color=MUTED, italic=True)

    add_slide_num(sl)

slide_ai_design()

# ─── 슬라이드 14: 성장 메타 ───
def slide_meta():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 1  ·  성장 메타", Inches(0.5), Inches(0.12))
    add_text(sl, "메타 성장 시스템 — 기보 & 공명 & 칙령",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    # 기보 시스템
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(4.0), Inches(5.5),
             fill=CARD, line_color=GOLD, line_width=Pt(1))
    add_text(sl, "📖 기보 시스템 (Notation)", Inches(0.6), Inches(1.2),
             Inches(3.6), Inches(0.35), font_size=Pt(12), bold=True, color=GOLD)
    add_text(sl,
             "캐릭터의 체스 대국 퍼즐을 풀어\n"
             "능력치를 해방하는 스킬 트리.\n\n"
             "◆ TP (Tactical Point) 소모로\n"
             "   체스판 위의 노드를 해금\n"
             "◆ 기물 이동 타일의 스탯 획득\n"
             "◆ 적 기물(노드) 처치 시 영구 스탯\n"
             "◆ 체크메이트 = 궁극 패시브 해금\n\n"
             "BM 설계:\n"
             "인게임 재화만으로는 TP가\n"
             "3~4개 부족하도록 의도 설계\n"
             "→ '공명' 시스템으로 유도",
             Inches(0.6), Inches(1.6), Inches(3.6), Inches(4.9),
             font_size=Pt(10), color=MUTED)

    # 공명 시스템
    add_rect(sl, Inches(4.6), Inches(1.15), Inches(4.3), Inches(5.5),
             fill=CARD, line_color=VIOLET, line_width=Pt(1))
    add_text(sl, "🔗 공명 시스템 (Resonance)", Inches(4.8), Inches(1.2),
             Inches(3.9), Inches(0.35), font_size=Pt(12), bold=True, color=VIOLET)

    resonance = [
        ("1단계", "전사 시 부활 대기 -10%"),
        ("2단계", "전투 시작 SP +10"),
        ("3단계 ★", "행동 소모 AP 영구 할인\n폰 -1 / 나이트·비숍 -2 / 룩 -5"),
        ("4단계", "상태이상 저항 +10%"),
        ("5단계", "제2재능(패시브) 최종 강화"),
    ]
    for i, (stage, desc) in enumerate(resonance):
        y = Inches(1.62) + i * Inches(0.92)
        color = RED if "★" in stage else MUTED
        add_rect(sl, Inches(4.75), y, Inches(1.1), Inches(0.35),
                 fill=RGBColor(0x16, 0x0E, 0x28) if "★" in stage else RGBColor(0x14, 0x17, 0x28),
                 line_color=VIOLET, line_width=Pt(1))
        add_text(sl, stage, Inches(4.77), y + Inches(0.05), Inches(1.06), Inches(0.27),
                 font_size=Pt(9), bold=True, color=VIOLET, align=PP_ALIGN.CENTER)
        add_text(sl, desc, Inches(5.95), y + Inches(0.04), Inches(2.8), Inches(0.5),
                 font_size=Pt(9.5), color=color)

    # 칙령 시스템
    add_rect(sl, Inches(9.1), Inches(1.15), Inches(3.8), Inches(5.5),
             fill=CARD, line_color=GOLD2, line_width=Pt(1))
    add_text(sl, "👑 칙령 시스템 (Edict)", Inches(9.3), Inches(1.2),
             Inches(3.4), Inches(0.35), font_size=Pt(12), bold=True, color=GOLD2)
    add_text(sl,
             "킹(마스터)만의 전용 모딩 시스템.\n"
             "한정된 싱크로 수용량(Capacity)에\n"
             "칙령 카드를 장착해 덱 방향성 확립.\n\n"
             "◆ 절대 칙령 (1개):\n"
             "   수용량을 오히려 늘리는\n"
             "   최상위 코어 모드\n"
             "   (워프레임의 Aura 모드)\n\n"
             "◆ 일반 칙령:\n"
             "   글로벌 / 클래스별 / 슈트 전용\n"
             "   타겟팅으로 덱 시너지 집중\n\n"
             "◆ 극성 배열:\n"
             "   슬롯-카드 극성 일치 시\n"
             "   비용 50% 절감 보너스",
             Inches(9.3), Inches(1.62), Inches(3.4), Inches(4.8),
             font_size=Pt(9.5), color=MUTED)

    # 하단 노트
    add_rect(sl, Inches(0.4), Inches(6.8), W - Inches(0.8), Inches(0.45),
             fill=RGBColor(0x10, 0x13, 0x22), line_color=MUTED, line_width=Pt(0.5))
    add_text(sl,
             "※ 졸업작품 범위: 기보·공명·칙령 시스템은 설계 구조(Calculator/Models/Runtime 클래스)까지 구현. 인게임 UI 연동은 추후 작업 예정.",
             Inches(0.6), Inches(6.87), W - Inches(1.2), Inches(0.35),
             font_size=Pt(9), color=MUTED, italic=True)

    add_slide_num(sl)

slide_meta()


# ══════════════════════════════════════════════════════
#  PART 2: 개발 현황 (후반부)
# ══════════════════════════════════════════════════════

def part2_divider():
    sl = blank_slide(prs)
    set_bg(sl, RGBColor(0x07, 0x09, 0x12))

    # 장식 선
    add_rect(sl, Inches(0.4), Inches(3.55), W - Inches(0.8), Inches(0.03), fill=CYAN)

    add_text(sl, "PART  2", Inches(0.5), Inches(1.8), W - Inches(1), Inches(1.0),
             font_size=Pt(72), bold=True, color=RGBColor(0x16, 0x1A, 0x2E),
             align=PP_ALIGN.CENTER)
    add_text(sl, "개발 현황",
             Inches(0.5), Inches(2.8), W - Inches(1), Inches(1.0),
             font_size=Pt(52), bold=True, color=WHITE, align=PP_ALIGN.CENTER)
    add_text(sl, "Development Status  ·  System Implementation",
             Inches(0.5), Inches(3.7), W - Inches(1), Inches(0.5),
             font_size=Pt(16), color=CYAN, align=PP_ALIGN.CENTER)
    add_text(sl, "프랍 수량보다 시스템 백엔드 구현에 집중한 졸업작품",
             Inches(0.5), Inches(4.3), W - Inches(1), Inches(0.5),
             font_size=Pt(14), color=MUTED, align=PP_ALIGN.CENTER, italic=True)

    add_slide_num(sl)

part2_divider()

# ─── 슬라이드 16: 기술 스택 & 아키텍처 ───
def slide_tech_stack():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  기술 스택", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "기술 스택 & 프로젝트 아키텍처",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=CYAN)

    # 좌: 기술 스택
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(6.0), Inches(5.1),
             fill=CARD, line_color=CYAN, line_width=Pt(1))
    add_text(sl, "기술 스택", Inches(0.6), Inches(1.2), Inches(5.6), Inches(0.35),
             font_size=Pt(12), bold=True, color=CYAN)

    stack = [
        ("게임 엔진", "Unity 6000.3.10f1 (최신 LTS)", GOLD),
        ("렌더링", "Universal Render Pipeline (URP)", CYAN),
        ("셰이더", "Shader Graph (URP) — 수면 셰이더 제작", CYAN),
        ("UI 텍스트", "TextMeshPro", MUTED),
        ("입력 시스템", "Unity New Input System", MUTED),
        ("데이터 구조", "ScriptableObject (UnitData / EnemyAIProfile / PlayerDeckData)", GREEN),
        ("패턴", "컴포넌트 기반 + 이벤트 버스 (EventBus<T>)", GREEN),
        ("AI 구조", "ActionBid 경쟁 입찰 시스템", VIOLET),
        ("결정론", "SeededRandomProvider + Snapshot/Replay", GOLD),
        ("버전 관리", "Git (retshaft/Project_Chess_RPG)", MUTED),
    ]

    for i, (cat, val, color) in enumerate(stack):
        y = Inches(1.6) + i * Inches(0.44)
        add_rect(sl, Inches(0.55), y, Inches(1.5), Inches(0.36),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=None)
        add_text(sl, cat, Inches(0.58), y + Inches(0.06), Inches(1.45), Inches(0.26),
                 font_size=Pt(8.5), color=MUTED, align=PP_ALIGN.CENTER)
        add_text(sl, val, Inches(2.15), y + Inches(0.06), Inches(4.1), Inches(0.3),
                 font_size=Pt(9.5), color=color)

    # 우: 폴더 구조 요약
    add_rect(sl, Inches(6.6), Inches(1.15), Inches(6.3), Inches(5.1),
             fill=CARD, line_color=GREEN, line_width=Pt(1))
    add_text(sl, "주요 스크립트 폴더 구조 (Assets/Scripts/)",
             Inches(6.8), Inches(1.2), Inches(5.9), Inches(0.35),
             font_size=Pt(12), bold=True, color=GREEN)

    folder_text = (
        "Core/\n"
        "  ├─ APManager.cs          ← 글로벌 AP 풀\n"
        "  ├─ Actions/\n"
        "  │    ├─ ActionScheduler.cs    ← 행동 큐\n"
        "  │    ├─ InterruptArbitration  ← 인터럽트\n"
        "  │    └─ ActionCostReservation ← AP 예약\n"
        "  ├─ Effects/EffectSystem.cs   ← 상태이상\n"
        "  ├─ Simulation/              ← 결정론\n"
        "  └─ Prediction/              ← AI 예측\n"
        "\n"
        "Grid/\n"
        "  └─ GridSystem.cs   ← 8×8 보드 Singleton\n"
        "\n"
        "Units/\n"
        "  ├─ UnitBrain.cs    ← AI 의사결정 (37KB)\n"
        "  └─ AITeamCommander.cs\n"
        "\n"
        "MovementPatterns/\n"
        "  └─ (6종 이동패턴 구현)\n"
        "\n"
        "Components/\n"
        "  ├─ HealthComponent\n"
        "  ├─ MovementComponent\n"
        "  ├─ CombatComponent\n"
        "  └─ StatusEffectComponent (671줄)"
    )
    add_text(sl, folder_text, Inches(6.8), Inches(1.62),
             Inches(5.9), Inches(4.4),
             font_size=Pt(8.5), color=MUTED, font_name="Courier New")

    # 하단 통계
    add_rect(sl, Inches(0.4), Inches(6.35), W - Inches(0.8), Inches(0.9),
             fill=RGBColor(0x10, 0x13, 0x22), line_color=CYAN, line_width=Pt(1))
    stats = [
        ("~79KB", "ActionRuntimeController"),
        ("~37KB", "UnitBrain (AI)"),
        ("~25KB", "SimulationRuntime"),
        ("~22KB", "EffectSystem"),
        ("~14KB", "PredictionPipeline"),
        ("671줄", "StatusEffectComponent"),
    ]
    for i, (size, name) in enumerate(stats):
        x = Inches(0.6) + i * Inches(2.1)
        add_text(sl, size, x, Inches(6.42), Inches(2.0), Inches(0.3),
                 font_size=Pt(14), bold=True, color=CYAN, align=PP_ALIGN.LEFT)
        add_text(sl, name, x, Inches(6.73), Inches(2.0), Inches(0.28),
                 font_size=Pt(8), color=MUTED)

    add_slide_num(sl)

slide_tech_stack()

# ─── 슬라이드 17~22: 시스템별 상세 ───

def slide_system_apmanager():
    """AP Manager / ActionScheduler / Interrupt 상세"""
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  AP & Action 시스템", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "AP Manager · ActionScheduler · Interrupt System",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(22), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=CYAN)

    # AP Manager
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(4.1), Inches(2.8),
             fill=CARD, line_color=GOLD, line_width=Pt(1))
    add_text(sl, "APManager.cs", Inches(0.6), Inches(1.2),
             Inches(3.7), Inches(0.3), font_size=Pt(11), bold=True, color=GOLD)

    ap_code = (
        "// 글로벌 AP 풀 — Singleton 패턴\n"
        "public class APManager : MonoBehaviour {\n"
        "  float _current, _max = 100f;\n"
        "  const float REGEN_RATE = 4f; // /sec\n"
        "\n"
        "  void Update() {\n"
        "    _current = Mathf.Min(\n"
        "      _current + REGEN_RATE * Time.deltaTime,\n"
        "      _max);\n"
        "    EventBus.Publish(new APChangedEvent(...));\n"
        "  }\n"
        "\n"
        "  public bool TrySpend(float cost, ...) {\n"
        "    if (_current < cost) {\n"
        "      EventBus.Publish(new InsufficientAPEvent);\n"
        "      return false;\n"
        "    }\n"
        "    _current -= cost;\n"
        "    return true;\n"
        "  }\n"
        "}"
    )
    add_text(sl, ap_code, Inches(0.55), Inches(1.55), Inches(3.8), Inches(2.3),
             font_size=Pt(7.5), color=GREEN, font_name="Courier New")

    # ActionScheduler
    add_rect(sl, Inches(4.65), Inches(1.15), Inches(4.2), Inches(2.8),
             fill=CARD, line_color=CYAN, line_width=Pt(1))
    add_text(sl, "ActionScheduler.cs  (~27KB)", Inches(4.85), Inches(1.2),
             Inches(3.8), Inches(0.3), font_size=Pt(11), bold=True, color=CYAN)
    add_text(sl,
             "행동 큐 관리 + AP 비용 예약 시스템\n\n"
             "흐름:\n"
             "TryEnqueueMove / TryEnqueueAttack\n"
             "  → ActionCostReservation (AP 검증)\n"
             "  → ActionQueue에 삽입\n"
             "  → Executing 상태로 전환\n"
             "  → Resolve → Recovery → Complete\n\n"
             "SP (Skill Points):\n"
             "AP와 독립적으로 자동 회복.\n"
             "스킬 시전 시 SP 소모.",
             Inches(4.85), Inches(1.55), Inches(3.8), Inches(2.35),
             font_size=Pt(9.5), color=MUTED)

    # Interrupt System
    add_rect(sl, Inches(9.05), Inches(1.15), Inches(3.85), Inches(2.8),
             fill=CARD, line_color=RED, line_width=Pt(1))
    add_text(sl, "Interrupt 시스템", Inches(9.25), Inches(1.2),
             Inches(3.45), Inches(0.3), font_size=Pt(11), bold=True, color=RED)
    add_text(sl,
             "Hard Interrupt:\n"
             "  Death / Stun / Freeze\n"
             "  → 현재 행동 즉시 Cancel\n\n"
             "Soft Interrupt:\n"
             "  Knockback / Slow\n"
             "  → 행동 지연 or 일부 변경\n\n"
             "동일 Tick 충돌 우선순위:\n"
             "  Critical > Major > Minor\n"
             "  → Action Speed → Queue Order\n\n"
             "Scheduler만 상태 변경 가능.\n"
             "직접 Cancel() 호출 금지.",
             Inches(9.25), Inches(1.55), Inches(3.45), Inches(2.35),
             font_size=Pt(9.5), color=MUTED)

    # 하단: Action State Flow
    add_rect(sl, Inches(0.4), Inches(4.05), W - Inches(0.8), Inches(1.45),
             fill=RGBColor(0x10, 0x13, 0x22), line_color=CYAN, line_width=Pt(1))
    add_text(sl, "Action 상태 흐름도 (Action State Flow)",
             Inches(0.6), Inches(4.1), Inches(4), Inches(0.3),
             font_size=Pt(11), bold=True, color=CYAN)

    states = ["Queued", "Executing", "Resolving", "Recovery", "Completed"]
    for i, state in enumerate(states):
        x = Inches(0.6) + i * Inches(2.5)
        color_map = {
            "Queued": MUTED, "Executing": CYAN, "Resolving": GREEN,
            "Recovery": GOLD, "Completed": GREEN
        }
        c = color_map[state]
        add_rect(sl, x, Inches(4.5), Inches(2.1), Inches(0.55),
                 fill=RGBColor(0x14, 0x18, 0x28), line_color=c, line_width=Pt(1))
        add_text(sl, state, x + Inches(0.05), Inches(4.56), Inches(2.0), Inches(0.42),
                 font_size=Pt(12), bold=True, color=c, align=PP_ALIGN.CENTER)
        if i < len(states)-1:
            add_text(sl, "→", x + Inches(2.15), Inches(4.58), Inches(0.3), Inches(0.38),
                     font_size=Pt(14), color=MUTED, align=PP_ALIGN.CENTER)

    add_text(sl,
             "Interrupt 발생 시:  Executing → Interrupted → Cancelled\n"
             "Resolve 단계 진입 후: 취소 불가 (데미지 판정 이후 결과 보장)\n"
             "Tick 처리 순서: ① Tick Advance  ② Action Update  ③ Interrupt Check  ④ Resolve Queue  ⑤ Effect Tick  ⑥ Death Check  ⑦ Event Dispatch",
             Inches(0.6), Inches(5.12), W - Inches(1.2), Inches(0.65),
             font_size=Pt(9.5), color=MUTED)

    # 인게임 스크린샷: AP 게이지 + 배치화면
    add_rect(sl, Inches(0.4), Inches(5.88), W - Inches(0.8), Inches(1.35),
             fill=CARD, line_color=GOLD, line_width=Pt(1))
    add_text(sl, "▼ 실제 구현 화면", Inches(0.6), Inches(5.92), Inches(3), Inches(0.28),
             font_size=Pt(9), bold=True, color=GOLD)
    try:
        add_image(sl, "Initialize_Phase_Place_Units.png",
                  Inches(0.5), Inches(6.22), Inches(5.5), Inches(0.95))
    except: pass
    add_text(sl,
             "좌: 배틀 초기화 단계 — 덱(Queen/Rook/Bishop/Pawn/King) 배치 및 START BATTLE 버튼\n"
             "AP 게이지(우측 하단 시안색 바)와 Move / Attack / Skill 버튼 UI 확인 가능",
             Inches(6.1), Inches(5.92), Inches(6.8), Inches(1.25),
             font_size=Pt(9.5), color=MUTED)

    add_slide_num(sl)

slide_system_apmanager()

def slide_system_chess():
    """체스 이동 패턴 구현"""
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  체스 이동 구현", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "체스 이동 패턴 구현 — IMovePattern 인터페이스",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(22), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    # 인터페이스 코드
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(5.0), Inches(2.3),
             fill=CARD, line_color=GOLD, line_width=Pt(1))
    add_text(sl, "IMovePattern.cs — 이동 패턴 인터페이스",
             Inches(0.6), Inches(1.2), Inches(4.6), Inches(0.3),
             font_size=Pt(10), bold=True, color=GOLD)
    code1 = (
        "public interface IMovePattern {\n"
        "  // 이동 가능한 셀 목록 반환\n"
        "  List<Vector2Int> GetMovableCells(\n"
        "    Vector2Int pos,\n"
        "    IReadOnlyGridSystem grid,\n"
        "    TeamComponent team);\n"
        "\n"
        "  // 공격 가능한 셀 목록 반환  \n"
        "  List<Vector2Int> GetAttackCells(\n"
        "    Vector2Int pos,\n"
        "    IReadOnlyGridSystem grid,\n"
        "    TeamComponent team);\n"
        "}"
    )
    add_text(sl, code1, Inches(0.55), Inches(1.52), Inches(4.7), Inches(1.9),
             font_size=Pt(8), color=GREEN, font_name="Courier New")

    # 팩토리 패턴
    add_rect(sl, Inches(5.6), Inches(1.15), Inches(4.0), Inches(2.3),
             fill=CARD, line_color=CYAN, line_width=Pt(1))
    add_text(sl, "MovePatternFactory.cs",
             Inches(5.8), Inches(1.2), Inches(3.6), Inches(0.3),
             font_size=Pt(10), bold=True, color=CYAN)
    code2 = (
        "public static IMovePattern Create(\n"
        "    PieceClass pieceClass) {\n"
        "  return pieceClass switch {\n"
        "    PieceClass.Pawn   =>\n"
        "        new PawnMovePattern(),\n"
        "    PieceClass.Knight =>\n"
        "        new KnightMovePattern(),\n"
        "    PieceClass.Bishop =>\n"
        "        new SlidingMovePattern(\n"
        "            SlidingMode.Diagonal),\n"
        "    PieceClass.Rook =>\n"
        "        new SlidingMovePattern(\n"
        "            SlidingMode.Orthogonal),\n"
        "    PieceClass.Queen =>\n"
        "        new SlidingMovePattern(\n"
        "            SlidingMode.All),\n"
        "    PieceClass.King =>\n"
        "        new KingMovePattern(),\n"
        "    _ => throw new ...\n"
        "  };\n"
        "}"
    )
    add_text(sl, code2, Inches(5.75), Inches(1.52), Inches(3.7), Inches(1.9),
             font_size=Pt(7.5), color=GREEN, font_name="Courier New")

    # 구현 상세
    add_rect(sl, Inches(9.8), Inches(1.15), Inches(3.1), Inches(2.3),
             fill=CARD, line_color=VIOLET, line_width=Pt(1))
    add_text(sl, "구현 포인트", Inches(10.0), Inches(1.2), Inches(2.7), Inches(0.3),
             font_size=Pt(10), bold=True, color=VIOLET)
    add_text(sl,
             "PawnMovePattern:\n"
             "• 전진(직선) / 공격(대각선) 분리\n"
             "• 첫 이동 2칸 특례 처리\n\n"
             "SlidingMovePattern:\n"
             "• Bishop/Rook/Queen 공통 사용\n"
             "• 방향별 루프 + 기물 충돌 차단\n"
             "• IReadOnlyGridSystem으로 의존성 최소화\n\n"
             "KnightMovePattern:\n"
             "• L자 8방향 하드코딩\n"
             "• 점프 특성상 충돌 무시",
             Inches(10.0), Inches(1.52), Inches(2.75), Inches(1.9),
             font_size=Pt(8.5), color=MUTED)

    # 스크린샷 섹션
    add_rect(sl, Inches(0.4), Inches(3.55), W - Inches(0.8), Inches(3.7),
             fill=CARD, line_color=GOLD, line_width=Pt(1))
    add_text(sl, "▼ 이동 타일 마킹 구현 스크린샷", Inches(0.6), Inches(3.6),
             Inches(5), Inches(0.3), font_size=Pt(10), bold=True, color=GOLD)

    try:
        add_image(sl, "Marking_Movable_Tiles_When_Click.png",
                  Inches(0.5), Inches(3.95), Inches(4.0), Inches(3.0))
    except: pass
    try:
        add_image(sl, "Marking_Movable_Tiles_When_Click02_Bishop.png",
                  Inches(4.65), Inches(3.95), Inches(4.0), Inches(3.0))
    except: pass
    try:
        add_image(sl, "Marking_Attackable_Tiles_Bishop.png",
                  Inches(8.85), Inches(3.95), Inches(4.0), Inches(3.0))
    except: pass

    captions = [
        (Inches(0.5), "폰 이동 — Pawn 클릭 시 이동 가능 타일\n시안색 윤곽선으로 하이라이팅"),
        (Inches(4.65), "비숍 이동 — 대각선 슬라이딩 패턴\nAP 87/100 소모 확인 가능"),
        (Inches(8.85), "비숍 공격 — 공격 가능 타일\n붉은색 윤곽선으로 구분 표시"),
    ]
    for cx, cap in captions:
        add_text(sl, cap, cx, Inches(6.98), Inches(4.0), Inches(0.55),
                 font_size=Pt(8.5), color=MUTED)

    add_slide_num(sl)

slide_system_chess()

def slide_system_status():
    """상태이상 & 원소 반응 구현"""
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  상태이상 & 원소 반응", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "StatusEffectComponent & ReactionSystem 구현",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(22), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=VIOLET)

    # 상태이상 목록
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(4.5), Inches(5.5),
             fill=CARD, line_color=VIOLET, line_width=Pt(1))
    add_text(sl, "StatusEffectComponent.cs  (671줄)",
             Inches(0.6), Inches(1.2), Inches(4.1), Inches(0.3),
             font_size=Pt(11), bold=True, color=VIOLET)

    status_groups = [
        ("물리 상태이상", ORANGE, [
            "Stagger (비틀거림) — 넉백 거리 +1칸",
            "Bleed (출혈) — n중첩, 행동마다 고정 피해",
            "Wound (부상) — 이동 AP +20%, 행동 속도 저하",
            "GrabVulnerability — Grab 적중 시 치명타 확정",
        ]),
        ("원소 DoT", CYAN, [
            "Burn (화상) — 지속 마법 피해 + Res -20%",
            "Ignite (발화) — DoT + 주변 화염 장판",
            "Chill (한기) — 전 행동 AP +20%",
            "Freeze (빙결) — 전 행동 봉쇄",
            "Poison (중독) — 틱 마법 피해",
            "Virus (바이러스) — 전염 중독",
        ]),
        ("반응 결과", GREEN, [
            "Superconduct — Def -40%",
            "Overload — Atk -25%",
            "Paralysis — 공격/이동 봉인",
            "FrozenBossDebuff — Def/Res -25%",
        ]),
    ]

    y = Inches(1.6)
    for group_name, color, items in status_groups:
        add_text(sl, f"[ {group_name} ]", Inches(0.55), y, Inches(4.1), Inches(0.28),
                 font_size=Pt(9), bold=True, color=color)
        y += Inches(0.3)
        for item in items:
            add_text(sl, "  • " + item, Inches(0.55), y, Inches(4.1), Inches(0.28),
                     font_size=Pt(8.5), color=MUTED)
            y += Inches(0.28)
        y += Inches(0.1)

    # ReactionSystem
    add_rect(sl, Inches(5.1), Inches(1.15), Inches(7.8), Inches(5.5),
             fill=CARD, line_color=CYAN, line_width=Pt(1))
    add_text(sl, "ReactionSystem.cs — 원소 반응 처리",
             Inches(5.3), Inches(1.2), Inches(7.4), Inches(0.3),
             font_size=Pt(11), bold=True, color=CYAN)

    code3 = (
        "public class ReactionSystem {\n"
        "\n"
        "  // 원소 태그 적용 시 반응 체크\n"
        "  public void ApplyElement(\n"
        "      UnitRuntimeState target,\n"
        "      ElementType incoming) {\n"
        "\n"
        "    var existing = target.CurrentElement;\n"
        "\n"
        "    var reaction = ReactionTable\n"
        "        .Lookup(existing, incoming);\n"
        "\n"
        "    if (reaction == null) {\n"
        "      target.CurrentElement = incoming;\n"
        "      return;\n"
        "    }\n"
        "\n"
        "    // 반응 실행\n"
        "    ExecuteReaction(target, reaction);\n"
        "    target.CurrentElement = ElementType.None;\n"
        "  }\n"
        "\n"
        "  private void ExecuteReaction(\n"
        "      UnitRuntimeState t,\n"
        "      ReactionData r) {\n"
        "\n"
        "    // 즉시 피해\n"
        "    if (r.ImmediateDamage > 0)\n"
        "      DealMagicDamage(t, r.ImmediateDamage);\n"
        "\n"
        "    // 상태이상 부여\n"
        "    if (r.StatusEffect != null)\n"
        "      StatusEffectSystem.Apply(\n"
        "          t, r.StatusEffect);\n"
        "\n"
        "    // 스탯 변경\n"
        "    if (r.DefModifier != 0)\n"
        "      t.ApplyStatModifier(\n"
        "          StatType.Defense, r.DefModifier);\n"
        "  }\n"
        "}"
    )
    add_text(sl, code3, Inches(5.25), Inches(1.55), Inches(7.5), Inches(5.0),
             font_size=Pt(8), color=GREEN, font_name="Courier New")

    # 하단: 구현 통계
    add_rect(sl, Inches(0.4), Inches(6.75), W - Inches(0.8), Inches(0.55),
             fill=RGBColor(0x10, 0x13, 0x22), line_color=VIOLET, line_width=Pt(1))
    add_text(sl,
             "구현 현황:  물리 상태이상 4종  ·  원소 DoT 6종  ·  원소 반응 결과 4종  ·  "
             "총 14개 상태이상 클래스  |  반응 테이블 13개 조합 정의",
             Inches(0.6), Inches(6.82), W - Inches(1.2), Inches(0.35),
             font_size=Pt(9.5), color=MUTED)

    add_slide_num(sl)

slide_system_status()

def slide_system_ai():
    """AI 시스템 — ActionBid / UnitBrain"""
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  AI 시스템", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "AI 시스템 — ActionBid 경쟁 입찰 & UnitBrain",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(22), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=VIOLET)

    # UnitBrain 구조
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(5.5), Inches(2.8),
             fill=CARD, line_color=VIOLET, line_width=Pt(1))
    add_text(sl, "UnitBrain.cs (~37KB, ~962줄) — 유닛 AI 의사결정",
             Inches(0.6), Inches(1.2), Inches(5.1), Inches(0.3),
             font_size=Pt(10.5), bold=True, color=VIOLET)

    flow = (
        "DecideBestAction() 호출\n"
        "  ①  Checkmate Override 체크\n"
        "       └─ 적 킹 처치 가능? → 즉시 실행 (Score +1000)\n"
        "  ②  Danger Override 체크\n"
        "       └─ 아군 킹 위협받음? → 킹 대피 or 방어 폰 배치\n"
        "  ③  모든 공격 대상 점수 계산\n"
        "       └─ KillValue(처치 시 점수) + AP 가중치 + 킹 거리\n"
        "  ④  모든 이동 목적지 점수 계산\n"
        "       └─ 공격 사거리 확보 + 킹 안전거리 유지\n"
        "  ⑤  최고 점수 ActionBid 제출 → AITeamCommander 수집\n"
        "  ⑥  AP 예산 내 최우선 Bid 실행"
    )
    add_text(sl, flow, Inches(0.6), Inches(1.55), Inches(5.1), Inches(2.35),
             font_size=Pt(9), color=MUTED, font_name="Courier New")

    # ActionBid 코드
    add_rect(sl, Inches(6.1), Inches(1.15), Inches(6.8), Inches(2.8),
             fill=CARD, line_color=CYAN, line_width=Pt(1))
    add_text(sl, "ActionBid.cs — 점수 입찰 구조체",
             Inches(6.3), Inches(1.2), Inches(6.4), Inches(0.3),
             font_size=Pt(10.5), bold=True, color=CYAN)

    bid_code = (
        "public struct ActionBid {\n"
        "  public UnitBrain Bidder;\n"
        "  public ActionType ActionType;\n"
        "  // MOVE / ATTACK / SKILL\n"
        "\n"
        "  public float Score;    // 높을수록 우선\n"
        "  public float APCost;   // 소모 AP\n"
        "\n"
        "  public Vector2Int TargetCell;\n"
        "  public UnitBrain TargetUnit;\n"
        "\n"
        "  // 정렬: Score 내림차순\n"
        "  public int CompareTo(ActionBid other)\n"
        "    => other.Score.CompareTo(Score);\n"
        "}\n"
        "\n"
        "// AITeamCommander에서 수집 후 실행\n"
        "var bids = units.Select(u => u.GetBestBid())\n"
        "               .OrderByDescending(b => b.Score);\n"
        "foreach (var bid in bids) {\n"
        "  if (availableAP >= bid.APCost)\n"
        "    ExecuteBid(bid);\n"
        "}"
    )
    add_text(sl, bid_code, Inches(6.25), Inches(1.52), Inches(6.5), Inches(2.35),
             font_size=Pt(8), color=GREEN, font_name="Courier New")

    # 점수 가중치 상세
    add_rect(sl, Inches(0.4), Inches(4.05), Inches(5.5), Inches(3.2),
             fill=CARD, line_color=GOLD, line_width=Pt(1))
    add_text(sl, "점수 가중치 상세 (Score Weights)",
             Inches(0.6), Inches(4.1), Inches(5.1), Inches(0.3),
             font_size=Pt(11), bold=True, color=GOLD)

    weights = [
        ("King 처치",      "+1000 + LethalBonus + KingTargetBonus", RED),
        ("Queen 처치",     "+50",  GOLD),
        ("부상 대상 처치", "+부상도 비례 추가 보너스",  ORANGE),
        ("치명타 가능",    "+LethabilityBonus",  ORANGE),
        ("AP 가중치",      "유닛 성향(Berserker/Tactician/Defensive)",  CYAN),
        ("킹 안전 거리",   "킹으로부터 멀수록 Danger 패널티",  MUTED),
    ]
    for i, (k, v, c) in enumerate(weights):
        y = Inches(4.5) + i * Inches(0.45)
        add_text(sl, k, Inches(0.6), y, Inches(2.2), Inches(0.35),
                 font_size=Pt(9), color=MUTED)
        add_text(sl, v, Inches(2.9), y, Inches(2.8), Inches(0.35),
                 font_size=Pt(9.5), bold=True, color=c)

    # Prediction 연동
    add_rect(sl, Inches(6.1), Inches(4.05), Inches(6.8), Inches(3.2),
             fill=CARD, line_color=GREEN, line_width=Pt(1))
    add_text(sl, "PredictionPipeline — AI 시뮬레이션 선예측",
             Inches(6.3), Inches(4.1), Inches(6.4), Inches(0.3),
             font_size=Pt(11), bold=True, color=GREEN)
    add_text(sl,
             "AI가 행동 실행 전 결과를 미리 시뮬레이션.\n\n"
             "PredictionQueryService:\n"
             "• 예측 피해량 (Predicted Damage)\n"
             "• 예측 사망 여부 (WillDie)\n"
             "• 예측 넉백 목적지 (Knockback Target Cell)\n\n"
             "AIPredictionAdapter:\n"
             "AI가 PredictionPipeline 결과를\n"
             "점수 계산에 반영.\n\n"
             "GridPathfinder:\n"
             "• A* / BFS로 이동 경로 탐색\n"
             "• 점유 상태 반영한 최단 경로 계산\n"
             "• AP 예산 내 도달 가능 거리 필터링",
             Inches(6.3), Inches(4.48), Inches(6.4), Inches(2.65),
             font_size=Pt(9.5), color=MUTED)

    add_slide_num(sl)

slide_system_ai()

def slide_system_replay():
    """Replay & 결정론적 시뮬레이션"""
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  Replay & 결정론", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "결정론적 시뮬레이션 & Replay 시스템",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(22), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GREEN)

    blocks = [
        ("SeededRandomProvider", GOLD,
         "모든 RNG를 고정 시드(Seed)로 초기화.\n"
         "동일 시드 → 동일 결과 보장.\n"
         "멀티플레이 동기화 및 리플레이 재현의 기반.",
         Inches(0.4), Inches(1.15), Inches(4.1), Inches(2.5)),
        ("SimulationSnapshot.cs (~21KB)", GREEN,
         "전투 상태 전체를 직렬화.\n"
         "매 Tick마다 상태 스냅샷 저장.\n"
         "포함 내용:\n"
         "• 모든 유닛 위치·HP·AP·상태이상\n"
         "• 타일 상태\n"
         "• 글로벌 AP 값\n"
         "• 행동 큐 상태",
         Inches(4.65), Inches(1.15), Inches(4.1), Inches(2.5)),
        ("SnapshotDiffUtility", CYAN,
         "두 스냅샷 간 차이 비교.\n"
         "동기화 발산(Divergence) 자동 감지.\n"
         "발산 시 경고 로그 + 진단 출력.\n"
         "SimulationDivergenceTests로\n"
         "자동화 테스트 커버리지 포함.",
         Inches(9.1), Inches(1.15), Inches(3.8), Inches(2.5)),
        ("ReplayRecorder", VIOLET,
         "모든 행동 이벤트를 타임라인에 기록.\n"
         "SimulationTimelineRecorder:\n"
         "전체 전투 이벤트 연대기 저장.",
         Inches(0.4), Inches(3.75), Inches(4.1), Inches(2.0)),
        ("ReplayValidationService", RED,
         "리플레이 재생 시 결과가 동일한지\n"
         "원본과 검증.\n"
         "디버깅·QA 용도로 활용.",
         Inches(4.65), Inches(3.75), Inches(4.1), Inches(2.0)),
        ("APDebugUI.cs", GOLD2,
         "런타임 AP 상태를\n"
         "실시간 오버레이로 표시.\n"
         "개발 중 AP 흐름 시각화 도구.",
         Inches(9.1), Inches(3.75), Inches(3.8), Inches(2.0)),
    ]

    for (title, color, body, l, t, w, h) in [
        (b[0], b[1], b[2], b[3], b[4], b[5], b[6]) for b in blocks
    ]:
        add_rect(sl, l, t, w, h, fill=CARD, line_color=color, line_width=Pt(1))
        add_text(sl, title, l + Inches(0.15), t + Inches(0.1), w - Inches(0.3), Inches(0.3),
                 font_size=Pt(10.5), bold=True, color=color)
        add_hline(sl, t + Inches(0.45), color=color)
        add_text(sl, body, l + Inches(0.15), t + Inches(0.52), w - Inches(0.3), h - Inches(0.6),
                 font_size=Pt(9.5), color=MUTED)

    # 의의 섹션
    add_rect(sl, Inches(0.4), Inches(5.85), W - Inches(0.8), Inches(1.4),
             fill=RGBColor(0x0F, 0x14, 0x20), line_color=GREEN, line_width=Pt(1))
    add_text(sl, "💡 이 시스템이 중요한 이유", Inches(0.6), Inches(5.9),
             Inches(4), Inches(0.3), font_size=Pt(11), bold=True, color=GREEN)
    add_text(sl,
             "실시간 전술 게임에서 결정론적 시뮬레이션 구현은 매우 높은 기술 난이도를 요구합니다.\n"
             "SeededRNG + Snapshot 기반의 리플레이 검증 시스템은 졸업작품 수준을 넘어서는 '네트워크 동기화 준비 완료 아키텍처'입니다.\n"
             "향후 멀티플레이 확장 시, 이 시스템이 락스텝(Lock-step) 동기화의 기반이 됩니다.",
             Inches(0.6), Inches(6.25), W - Inches(1.2), Inches(0.9),
             font_size=Pt(10), color=MUTED)

    add_slide_num(sl)

slide_system_replay()

# ─── 슬라이드 23: 씬 구성 스크린샷 ───
def slide_scene_screenshots():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  씬 구성", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "씬 구성 & 인게임 화면",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=CYAN)

    # BattleTest 씬 설명
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(5.5), Inches(0.9),
             fill=CARD, line_color=GREEN, line_width=Pt(1))
    add_text(sl, "BattleTest Scene — 현재 테스트 씬 구조",
             Inches(0.6), Inches(1.22), Inches(5.1), Inches(0.28),
             font_size=Pt(11), bold=True, color=GREEN)
    add_text(sl,
             "Hierarchy: Main Camera / Directional Light / TestSceneBattleManager / Plane / WaterPlane / GridSystem / GlobalAPManager",
             Inches(0.6), Inches(1.52), Inches(5.1), Inches(0.48),
             font_size=Pt(8.5), color=MUTED, font_name="Courier New")

    # 보드 + 수면 스크린샷
    try:
        add_image(sl, "Chessboard_Tiles_Ruins_Water.png",
                  Inches(0.4), Inches(2.15), Inches(5.5), Inches(3.5))
    except: pass
    add_text(sl, "◀  폐허 느낌의 타일 텍스처 + URP 수면 셰이더 적용 체스보드\n(Scene View 상단 / Game View 하단 동시 표시)",
             Inches(0.4), Inches(5.72), Inches(5.5), Inches(0.55),
             font_size=Pt(8.5), color=MUTED)

    # 전투 시작 배치
    try:
        add_image(sl, "Initialize_Phase_Place_Units.png",
                  Inches(6.2), Inches(1.15), Inches(6.7), Inches(3.6))
    except: pass
    add_text(sl, "▲  초기화 단계 — 배치 UI\n"
             "하단 바: Queen(5)/Rook(4)/Bishop(3)/Rook(3)/Pawn×3/King(3) 배치 슬롯\n"
             "녹색 격자: 아군 배치 가능 영역  ·  핑크 점: 커서 위치\n"
             "AP 게이지(시안색 바) + START BATTLE 버튼",
             Inches(6.2), Inches(4.82), Inches(6.7), Inches(0.7),
             font_size=Pt(8.5), color=MUTED)

    # 워터 셰이더 그래프
    try:
        add_image(sl, "Water_Shader_Graph.png",
                  Inches(6.2), Inches(5.6), Inches(3.8), Inches(1.65))
    except: pass
    add_text(sl, "▲  Water Shader Graph\n"
             "노드: WaterNormal / Speed_A,B / Tiling / Smoothness / Shallow/Deep Color / FoamTex",
             Inches(10.1), Inches(5.6), Inches(2.8), Inches(1.65),
             font_size=Pt(8.5), color=MUTED)

    add_slide_num(sl)

slide_scene_screenshots()

# ─── 슬라이드 24: 제작 에셋 현황 ───
def slide_assets():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  에셋 현황", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "제작 에셋 현황",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=GOLD)

    # 캐릭터 베이스
    add_rect(sl, Inches(0.4), Inches(1.15), Inches(3.0), Inches(5.7),
             fill=CARD, line_color=VIOLET, line_width=Pt(1))
    add_text(sl, "캐릭터 베이스 (제작 중)",
             Inches(0.55), Inches(1.2), Inches(2.7), Inches(0.3),
             font_size=Pt(10), bold=True, color=VIOLET)
    try:
        add_image(sl, "CharacterBase_LowPoly_unfinished.png",
                  Inches(0.5), Inches(1.55), Inches(2.8), Inches(3.2))
    except: pass
    add_text(sl,
             "3~4등신 SD 비율 캐릭터 베이스\n"
             "Blender 로우폴리 모델링 진행 중\n"
             "현재 상태: 와이어프레임 단계\n"
             "• 머리카락 메시 구조 작업 중\n"
             "• 손(총기 파지) 부위 UV 정리 중\n"
             "• 텍스처 미적용 상태\n"
             "GDD 기준 각 직군별 특징 무기·\n"
             "프랍을 추가로 작업 예정",
             Inches(0.55), Inches(4.82), Inches(2.7), Inches(1.95),
             font_size=Pt(8.5), color=MUTED)

    # 프랍들
    prop_data = [
        ("Prop01 — 깃발 (Flag)", Inches(3.55), "Prop01_Flag.png",
         "군사 깃발 프랍\n"
         "진지/점령 지점 표시용\n"
         "포탑 포함 일체형 구조\n"
         "Blender 완성 상태"),
        ("Prop02 — 포탑 (Turret)", Inches(6.7), "Prop02_Turret.png",
         "방어형 자동 포탑 프랍\n"
         "로봇 카메라 형태 디자인\n"
         "포탑 기물 시각화 목적\n"
         "Blender 완성 상태"),
        ("Prop03A — 바리케이드", Inches(9.85), "Prop03_Barricade.png",
         "이동 제한 장벽 프랍\n"
         "콘크리트 블록 형태\n"
         "Blender 완성 상태"),
    ]

    for title, l, filename, desc in prop_data:
        add_rect(sl, l, Inches(1.15), Inches(2.95), Inches(5.7),
                 fill=CARD, line_color=GOLD, line_width=Pt(1))
        add_text(sl, title, l + Inches(0.15), Inches(1.2), Inches(2.65), Inches(0.3),
                 font_size=Pt(10), bold=True, color=GOLD)
        try:
            add_image(sl, filename, l + Inches(0.1), Inches(1.55), Inches(2.75), Inches(3.2))
        except: pass
        add_text(sl, desc, l + Inches(0.15), Inches(4.82), Inches(2.65), Inches(1.95),
                 font_size=Pt(8.5), color=MUTED)

    # 와이어 장애물 (넓게)
    add_rect(sl, Inches(0.4), Inches(6.95), W - Inches(0.8), Inches(0.3),
             fill=RGBColor(0x10, 0x13, 0x22), line_color=MUTED, line_width=Pt(0.5))
    add_text(sl,
             "Prop03B — 철조망(Wire Entanglement)도 제작 완료. 전체 프랍 수량: 캐릭터 베이스 1종(진행 중) + 완성 프랍 4종 (Flag / Turret / Barricade / Wire)",
             Inches(0.6), Inches(6.99), W - Inches(1.2), Inches(0.26),
             font_size=Pt(9), color=MUTED)

    add_slide_num(sl)

slide_assets()

# ─── 슬라이드 25: 구현 진행도 ───
def slide_progress():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  진행도", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "구현 진행도 현황",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=CYAN)

    systems = [
        ("AP 경제 시스템",        95, "완료",     GREEN, "글로벌 AP풀·자연회복·소모·부족 피드백·Debug UI 완전 구현"),
        ("체스 이동 패턴",        90, "완료",     GREEN, "6직군 Pawn/Knight/Bishop/Rook/Queen/King 완전 구현·점유 연동"),
        ("Replay / Debug 시스템", 85, "완료",     GREEN, "Snapshot·Replay 검증·발산 감지·Diagnostics 오버레이"),
        ("Action Runtime",         70, "진행 중",  GOLD,  "ActionScheduler·비용 예약·인터럽트·SP 자동 회복 구조 구현"),
        ("전투 루프",              65, "진행 중",  GOLD,  "BattleTest 씬·부트스트랩 구조·승패 조건 검증 중"),
        ("넉백 / Splat",           60, "진행 중",  GOLD,  "Knockback·Grab·Splat·충돌 처리 구조 구현·시각 예측 보강 필요"),
        ("원소/상태이상",          55, "진행 중",  GOLD,  "Burn·Chill·Freeze·Superconduct·Poison·Virus·Stagger 구조 구현"),
        ("AI 시스템",              50, "진행 중",  GOLD,  "ActionBid·AP 고려·킹 가치·점수화 흐름·시연 품질 조정 중"),
        ("예측 / UI",              45, "진행 중",  GOLD,  "Prediction 런타임·Battle selection overlay·Ghosting UI 보강 필요"),
        ("최종 아트 / UI",          5,  "미시작",   RED,   "캐릭터 3D 모델·텍스처·애니메이션·최종 전투 UI 미작업"),
        ("캐릭터 모델링",          20, "진행 중",  GOLD,  "SD 베이스 바디 로우폴리 작업 중 (Blender)"),
        ("프랍 에셋",              50, "진행 중",  GOLD,  "Flag / Turret / Barricade / Wire 4종 완성, 추가 프랍 예정"),
    ]

    bar_total_w = W - Inches(7.2)

    for i, (name, pct, status, color, detail) in enumerate(systems):
        y = Inches(1.15) + i * Inches(0.52)

        # 상태 뱃지
        status_color = GREEN if status == "완료" else (RED if status == "미시작" else GOLD)
        add_rect(sl, Inches(0.4), y + Inches(0.05), Inches(1.0), Inches(0.3),
                 fill=RGBColor(0x14, 0x18, 0x28), line_color=status_color, line_width=Pt(0.8))
        add_text(sl, status, Inches(0.42), y + Inches(0.07), Inches(0.96), Inches(0.24),
                 font_size=Pt(8), bold=True, color=status_color, align=PP_ALIGN.CENTER)

        # 이름
        add_text(sl, name, Inches(1.5), y + Inches(0.08), Inches(2.9), Inches(0.3),
                 font_size=Pt(10), color=WHITE)

        # 프로그레스 바 트랙
        add_rect(sl, Inches(4.55), y + Inches(0.1), bar_total_w, Inches(0.25),
                 fill=RGBColor(0x18, 0x1C, 0x30), line_color=None)
        # 프로그레스 바 채움
        fill_w = bar_total_w * pct / 100
        if fill_w > 0:
            add_rect(sl, Inches(4.55), y + Inches(0.1), fill_w, Inches(0.25),
                     fill=color, line_color=None)

        # 퍼센트
        add_text(sl, f"{pct}%", Inches(4.55) + bar_total_w + Inches(0.1), y + Inches(0.07),
                 Inches(0.65), Inches(0.3),
                 font_size=Pt(9.5), bold=True, color=color)

        # 상세
        add_text(sl, detail, Inches(9.55), y + Inches(0.07),
                 Inches(3.55), Inches(0.3),
                 font_size=Pt(8), color=MUTED)

    add_slide_num(sl)

slide_progress()

# ─── 슬라이드 26: 마일스톤 로드맵 ───
def slide_roadmap():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  마일스톤", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "개발 마일스톤 & 향후 계획",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=CYAN)

    milestones = [
        ("M0", "환경 구축", GREEN, "완료",
         "• 8×8 GridSystem Singleton\n"
         "• BattleTest 씬 구성\n"
         "• 유닛 점유 추적 시스템\n"
         "• ScriptableObject 데이터 구조"),
        ("M1", "AP 경제 구현", GREEN, "완료",
         "• APManager 글로벌 풀\n"
         "• +4 AP/sec 자연 회복\n"
         "• 행동 비용 소모·차단\n"
         "• AP 부족 피드백 UI"),
        ("M2", "체스 이동 규칙", GREEN, "완료",
         "• IMovePattern 인터페이스\n"
         "• 6직군 이동 패턴 구현\n"
         "• 전원 배치 시작 시스템\n"
         "• 이동 타일 마킹 오버레이"),
        ("M3", "타일 & 상태이상", GOLD, "진행 중",
         "• Swamp/Spikes/Sanctuary\n"
         "• 상태이상 프레임워크\n"
         "• 원소 반응 2~3개 완성\n"
         "• 타일 시각 이펙트"),
        ("M4", "물리 엔진", GOLD, "진행 중",
         "• 넉백 Weight 계산\n"
         "• Splat 고정 피해\n"
         "• 소프트 낙사 완성\n"
         "• 예측 Ghost UI"),
        ("M5", "AI 고도화", MUTED, "예정",
         "• 점수화 AI 시연 품질\n"
         "• Override 트리거 안정화\n"
         "• AP 인식 전술 판단\n"
         "• Encounter 설계"),
        ("M6", "졸업작품 완성", MUTED, "예정",
         "• 최종 전투 루프 잠금\n"
         "• 아트 최소 완성도 확보\n"
         "• Debug 토글 정리\n"
         "• 시연 시나리오 완성"),
    ]

    for i, (code, name, color, status, items) in enumerate(milestones):
        l = Inches(0.4) + i * Inches(1.83)
        t = Inches(1.15)

        # 헤더
        add_rect(sl, l, t, Inches(1.75), Inches(0.6),
                 fill=color if status == "완료" else RGBColor(0x14, 0x18, 0x28),
                 line_color=color, line_width=Pt(1.5))
        add_text(sl, code, l + Inches(0.05), t + Inches(0.02),
                 Inches(1.65), Inches(0.28),
                 font_size=Pt(14), bold=True,
                 color=BG if status == "완료" else color,
                 align=PP_ALIGN.CENTER)
        add_text(sl, status, l + Inches(0.05), t + Inches(0.3),
                 Inches(1.65), Inches(0.25),
                 font_size=Pt(9),
                 color=BG if status == "완료" else MUTED,
                 align=PP_ALIGN.CENTER)

        # 이름
        add_text(sl, name, l, t + Inches(0.65), Inches(1.75), Inches(0.35),
                 font_size=Pt(10), bold=True, color=color, align=PP_ALIGN.CENTER)

        # 내용
        add_rect(sl, l, t + Inches(1.05), Inches(1.75), Inches(5.15),
                 fill=CARD, line_color=color, line_width=Pt(1))
        add_text(sl, items, l + Inches(0.1), t + Inches(1.12),
                 Inches(1.6), Inches(5.0), font_size=Pt(8.5), color=MUTED)

    # 범례
    add_rect(sl, Inches(0.4), Inches(6.8), W - Inches(0.8), Inches(0.45),
             fill=RGBColor(0x10, 0x13, 0x22), line_color=None)
    legend_items = [("■ 완료", GREEN), ("■ 진행 중", GOLD), ("■ 예정", MUTED)]
    for i, (text, color) in enumerate(legend_items):
        add_text(sl, text, Inches(0.6) + i * Inches(1.5), Inches(6.87),
                 Inches(1.4), Inches(0.3),
                 font_size=Pt(10), bold=True, color=color)

    add_slide_num(sl)

slide_roadmap()

# ─── 슬라이드 27: 시스템 아키텍처 요약 ───
def slide_architecture_summary():
    sl = blank_slide(prs)
    set_bg(sl)

    add_rect(sl, 0, 0, W, Inches(1.0), fill=RGBColor(0x0D, 0x10, 0x1E))
    add_tag(sl, "PART 2  ·  아키텍처 요약", Inches(0.5), Inches(0.12), color=CYAN)
    add_text(sl, "전체 시스템 아키텍처 다이어그램",
             Inches(0.5), Inches(0.42), W - Inches(1), Inches(0.55),
             font_size=Pt(24), bold=True, color=WHITE)
    add_hline(sl, Inches(1.02), color=CYAN)

    # 레이어 다이어그램
    layers = [
        ("🎮  플레이어 입력 / AI 의사결정 레이어",
         "PlayerInputHandler  ·  UnitBrain (ActionBid)  ·  AITeamCommander",
         GOLD, RGBColor(0x18, 0x16, 0x08)),
        ("⚙  행동 실행 레이어 (Action Runtime)",
         "ActionScheduler  ·  ActionCostReservation  ·  InterruptArbitrationService  ·  ActionStateMachine",
         CYAN, RGBColor(0x08, 0x16, 0x20)),
        ("🔋  자원 & 효과 레이어",
         "APManager  ·  StatusEffectComponent  ·  ReactionSystem  ·  EffectSystem  ·  TickScheduler",
         VIOLET, RGBColor(0x12, 0x0C, 0x20)),
        ("🗺  보드 상태 레이어",
         "GridSystem (8×8 Singleton)  ·  UnitRuntimeState  ·  MovementComponent  ·  HealthComponent",
         GREEN, RGBColor(0x0A, 0x18, 0x10)),
        ("📡  데이터 & 결정론 레이어",
         "SimulationRuntime  ·  SimulationSnapshot  ·  SeededRandomProvider  ·  ReplayRecorder  ·  EventBus<T>",
         MUTED, RGBColor(0x14, 0x15, 0x20)),
        ("📦  데이터 에셋 레이어",
         "UnitData (SO)  ·  EnemyAIProfile (SO)  ·  PlayerDeckData (SO)  ·  AbilityDataSO  ·  EffectDataSO",
         GOLD2, RGBColor(0x18, 0x14, 0x06)),
    ]

    for i, (title, content, color, bg_color) in enumerate(layers):
        y = Inches(1.15) + i * Inches(0.98)
        add_rect(sl, Inches(0.4), y, W - Inches(0.8), Inches(0.9),
                 fill=bg_color, line_color=color, line_width=Pt(1.5))
        add_text(sl, title, Inches(0.6), y + Inches(0.06),
                 Inches(4.5), Inches(0.3), font_size=Pt(10.5), bold=True, color=color)
        add_text(sl, content, Inches(0.6), y + Inches(0.42),
                 W - Inches(1.2), Inches(0.4),
                 font_size=Pt(9.5), color=MUTED, font_name="Courier New")

        if i < len(layers) - 1:
            add_text(sl, "▼", W/2 - Inches(0.15), y + Inches(0.92),
                     Inches(0.3), Inches(0.2),
                     font_size=Pt(10), color=MUTED, align=PP_ALIGN.CENTER)

    add_slide_num(sl)

slide_architecture_summary()

# ─── 슬라이드 28: 마무리 ───
def slide_closing():
    sl = blank_slide(prs)
    set_bg(sl, RGBColor(0x07, 0x09, 0x12))

    add_rect(sl, Inches(0.4), Inches(2.8), W - Inches(0.8), Inches(0.03), fill=GOLD)

    add_text(sl, "♚", Inches(0.5), Inches(0.8), W - Inches(1), Inches(1.5),
             font_size=Pt(60), bold=True, color=GOLD, align=PP_ALIGN.CENTER)

    add_text(sl, "감사합니다", Inches(0.5), Inches(2.2), W - Inches(1), Inches(0.8),
             font_size=Pt(44), bold=True, color=WHITE, align=PP_ALIGN.CENTER)

    add_text(sl, "Project: Checkmate RPG",
             Inches(0.5), Inches(2.9), W - Inches(1), Inches(0.5),
             font_size=Pt(18), color=GOLD, align=PP_ALIGN.CENTER)

    summary_text = (
        "체스 이동 규칙  ×  실시간 AP 경제  ×  물리 기반 위치전  ×  원소 반응 콤보\n"
        "ActionBid AI  ×  결정론적 Replay 시스템  ×  ScriptableObject 데이터 아키텍처"
    )
    add_text(sl, summary_text, Inches(0.5), Inches(3.55), W - Inches(1), Inches(0.8),
             font_size=Pt(12), color=MUTED, align=PP_ALIGN.CENTER)

    tags = ["Unity 6000.3", "URP", "8×8 체스 보드", "AP Economy",
            "ActionBid AI", "Replay System", "ScriptableObject"]
    for i, tag in enumerate(tags):
        x = Inches(0.6) + (i % 4) * Inches(3.0)
        y = Inches(4.5) + (i // 4) * Inches(0.5)
        add_rect(sl, x, y, Inches(2.8), Inches(0.38),
                 fill=RGBColor(0x12, 0x16, 0x28), line_color=GOLD, line_width=Pt(0.8))
        add_text(sl, tag, x + Inches(0.05), y + Inches(0.06),
                 Inches(2.7), Inches(0.28),
                 font_size=Pt(10), color=GOLD, align=PP_ALIGN.CENTER)

    add_text(sl, "졸업작품  |  2026  |  Unity 6000.3.10f1  |  retshaft/Project_Chess_RPG",
             Inches(0.5), Inches(6.9), W - Inches(1), Inches(0.4),
             font_size=Pt(9), color=MUTED, align=PP_ALIGN.CENTER)

    add_slide_num(sl)

slide_closing()

# ─── 저장 ───
output_path = os.path.join(os.path.dirname(__file__), "Checkmate_RPG_Graduation_PPT.pptx")
prs.save(output_path)
print(f"[DONE] Saved: {output_path}")
print(f"Total slides: {len(prs.slides)}")
