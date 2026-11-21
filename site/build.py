"""Build the Expeditions player site.

    python site/build.py            # writes site/_build
    python site/build.py --serve    # builds, then serves it on http://localhost:8000
    python site/build.py --out DIR  # writes somewhere else (CI uses this)

Pages are the Markdown files in site/content. They can call the helpers in
Helpers below, e.g. {{ sites("finds") }}, to pull tables generated from the
mod's defs into the text.
"""
from __future__ import annotations

import argparse
import copy
import functools
import http.server
import re
import shutil
import sys
from pathlib import Path

import markdown
from jinja2 import Environment, FileSystemLoader, StrictUndefined, select_autoescape
from lxml import etree
from markupsafe import Markup, escape

SITE = Path(__file__).resolve().parent
ROOT = SITE.parent
BUILD_MARKER = ".exp-site-build"  # written into the output so a rebuild only ever deletes its own folder

GITHUB = "https://github.com/bambaryla-mods/rimworldExpeditions"
SITE_URL = "https://bambaryla-mods.github.io/rimworldExpeditions/"  # for link previews
SCREENSHOT_EXTS = (".png", ".jpg", ".jpeg", ".webp")

SITE_TAG = "Expeditions.ExpeditionSiteDef"
CHALLENGE_TAG = "Expeditions.ExpeditionChallengeDef"
TUNING_TAG = "Expeditions.ExpeditionTuningDef"

# Top bar groups and the pages inside each, in reading order.
NAV = [
    ("Guide", [("guide", "How it works"), ("rides", "Rides and food"), ("prospecting", "Prospecting"),
               ("captives", "Captives")]),
    ("Sites", [("finds", "Places to search"), ("jobs", "Jobs for other factions")]),
    ("Challenges", [("challenges", "Challenges")]),
    ("About", [("settings", "Settings and mods")]),
]

# Labels of the vanilla, DLC and mod defs the site defs refer to. The game's own files aren't in this repo.
THINGS = {
    "Steel": "steel", "Plasteel": "plasteel", "Uranium": "uranium", "Gold": "gold", "Silver": "silver",
    "ComponentIndustrial": "components", "ComponentSpacer": "advanced components",
    "MedicineIndustrial": "medicine", "Chemfuel": "chemfuel", "InsectJelly": "insect jelly",
    "Mechlink": "mechlink", "PsychicAmplifier": "psylink neuroformer", "ThrumboHorn": "thrumbo horn", "TransportPod": "transport pods", "AlphaThrumboHorn": "alpha thrumbo horn",
    "Leather_AlphaThrumbo": "thrumbomane", "Meat_AlphaThrumbo": "alpha thrumbo meat", "Gravcore": "gravcore",
    "GravlitePanel": "gravlite panels",
    "Leather_Thrumbo": "thrumbofur", "Meat_Thrumbo": "thrumbo meat",
}
CATEGORIES = {
    "WeaponsRanged": "ranged weapons", "WeaponsMelee": "melee weapons", "Weapons": "weapons",
    "Apparel": "apparel", "MeatRaw": "raw meat", "Leathers": "leathers", "Textiles": "textiles",
    "Foods": "food", "Drugs": "drugs", "Medicine": "medicine", "Manufactured": "manufactured goods",
    "Artifacts": "artifacts", "PlantFoodRaw": "raw plant food", "ResourcesRaw": "raw resources",
    "StoneChunks": "stone chunks",
    "Ammo": "ammunition", "AmmoRifles": "rifle ammo", "AmmoPistols": "pistol ammo",
    "AmmoShotguns": "shotgun shells", "AmmoHighCaliber": "high-caliber ammo",
    "AmmoShells": "artillery shells", "AmmoGrenades": "grenades",
}
BIOMES = {
    "TemperateForest": "temperate forest", "BorealForest": "boreal forest",
    "TropicalRainforest": "tropical rainforest", "AridShrubland": "arid shrubland", "Desert": "desert",
    "ExtremeDesert": "extreme desert", "Tundra": "tundra", "TemperateSwamp": "temperate swamp",
    "TropicalSwamp": "tropical swamp", "Grasslands": "grasslands",
}
HILLINESS = {"SmallHills": "small hills", "LargeHills": "large hills", "Mountainous": "mountains"}
PAWN_KINDS = {"Colonist": "colonist"}
# Stats a challenge reads: label, and how to show the value a decent pawn has.
STATS = {
    "ShootingAccuracyPawn": ("shooting accuracy", "pct"), "MeleeDPS": ("melee DPS", "num"),
    "MeleeHitChance": ("melee hit chance", "pct"),
    "ArmorRating_Sharp": ("sharp armor", "pct"), "ArmorRating_Blunt": ("blunt armor", "pct"),
    "HuntingStealth": ("hunting stealth", "pct"), "MoveSpeed": ("move speed", "c/s"),
    "MiningSpeed": ("mining speed", "pct"), "MiningYield": ("mining yield", "pct"),
    "ResearchSpeed": ("research speed", "pct"), "ConstructionSpeed": ("construction speed", "pct"),
    "ConstructSuccessChance": ("construct success chance", "pct"),
    "CarryingCapacity": ("carrying capacity", "kg"), "ImmunityGainSpeed": ("immunity gain speed", "pct"),
    "MedicalTendQuality": ("medical tend quality", "pct"), "ToxicResistance": ("toxic resistance", "pct"),
    "PlantWorkSpeed": ("plant work speed", "pct"), "PlantHarvestYield": ("plant harvest yield", "pct"),
    "HackingSpeed": ("hacking speed", "pct"), "SocialImpact": ("social impact", "pct"),
    "NegotiationAbility": ("negotiation ability", "pct"), "TameAnimalChance": ("tame animal chance", "pct"),
}
# packageIds in MayRequire, and the name players know them by.
PACKAGES = {
    "Ludeon.RimWorld.Royalty": "Royalty", "Ludeon.RimWorld.Ideology": "Ideology",
    "Ludeon.RimWorld.Biotech": "Biotech", "Ludeon.RimWorld.Anomaly": "Anomaly",
    "Ludeon.RimWorld.Odyssey": "Odyssey", "CETeam.CombatExtended": "Combat Extended",
}
COMBINE = {
    "Sum": ("adds up", "Every pawn's score is added: more pawns help."),
    "Min": ("weakest pawn", "The weakest pawn's score counts: one bad pawn drags the squad down."),
    "Max": ("best pawn", "The best pawn's score counts: one expert is enough."),
    "Average": ("average", "The squad's average counts: a poor pawn lowers it, a good one raises it."),
}
FACTION_NEED = {
    "Hostile": "a hostile faction's settlement nearby",
    "Ally": "an allied faction's settlement nearby",
    "Friendly": "a neutral or allied faction's settlement nearby",
}
# The C# defaults of ExpeditionTuningDef and ExpeditionSiteDef, for fields the XML leaves out.
TUNING_DEFAULTS = {
    "partialBand": 0.15, "offerIntervalTicks": "60000~180000", "offerLifetimeTicks": "180000~480000",
    "prospectLifetimeTicks": "300000~480000", "loudStealthCap": 30, "armedFightBonus": 60, "armedSpread": 0.5,
    "flightBaseDays": 0.25, "flightDaysPerTile": 0.01, "vehicleFootFactor": 0.5, "prospectBaseTicks": 15000,
    "xpPerChallengeWeight": 800, "worksiteShare": 0.6, "worksiteFightPerGuard": 90,
}
SITE_DEFAULTS = {"findChanceBase": 0.6, "daysOnSite": 0.5, "openness": 1, "squadSize": "1~6", "commonness": 1,
                 "xpFactor": 1}
RISK_DEFAULTS = {  # RiskProfile in ExpeditionSiteDef.cs
    "success": {"unharmed": 0.8, "injured": 0.2, "killed": 0, "captured": 0},
    "partial": {"unharmed": 0.5, "injured": 0.4, "killed": 0.07, "captured": 0.03},
    "failure": {"unharmed": 0.1, "injured": 0.5, "killed": 0.2, "captured": 0.2},
}
TECH_LEVELS = ["Undefined", "Animal", "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech"]
TICKS_PER_DAY = 60000


def fmt_num(v, digits: int = 2) -> str:
    s = f"{v:,.{digits}f}"
    if "." in s:
        s = s.rstrip("0").rstrip(".")
    return s


def fmt_pct(v) -> str:
    return f"{fmt_num(v * 100, 1)}%"


def fmt_range(s: str, scale: float = 1, digits: int = 1) -> str:
    """ "1~5" -> "1–5", a single number stays one number."""
    lo, _, hi = str(s).partition("~")
    lo_v = float(lo) * scale
    hi_v = float(hi) * scale if hi else lo_v
    return fmt_num(lo_v, digits) if lo_v == hi_v else f"{fmt_num(lo_v, digits)}–{fmt_num(hi_v, digits)}"


def slugify(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")


def cap(s: str) -> str:
    """Capitalize the first letter only."""
    return s[:1].upper() + s[1:]


def split_front_matter(text: str) -> tuple[dict, str]:
    meta: dict[str, str] = {}
    if text.startswith("---\n"):
        head, _, body = text[4:].partition("\n---\n")
        for line in head.splitlines():
            key, sep, value = line.partition(":")
            if sep:
                meta[key.strip()] = value.strip()
        return meta, body
    return meta, text


def color_css(text: str | None) -> str:
    """ "(0.85, 0.7, 0.35)" -> "rgb(217 179 89)" """
    parts = [float(x) for x in (text or "(1,1,1)").strip("() ").split(",")[:3]]
    return "rgb({} {} {})".format(*(round(max(0.0, min(1.0, p)) * 255) for p in parts))


def curve_points(el) -> list[tuple[float, float]]:
    out = []
    for li in el.findall("points/li") if el is not None else []:
        x, y = (float(v) for v in li.text.strip("() ").split(","))
        out.append((x, y))
    return out


def curve_at(pts: list[tuple[float, float]], x: float) -> float:
    """SimpleCurve.Evaluate: straight lines between points, flat beyond the ends."""
    if not pts:
        return 0.0
    if x <= pts[0][0]:
        return pts[0][1]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        if x <= x1:
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0)
    return pts[-1][1]


# ---------- reading the mod ----------

def merge(base, child):
    """RimWorld's def inheritance: lists append, nodes with children merge, values replace."""
    for c in child:
        if not isinstance(c.tag, str):
            continue
        if c.tag == "li":
            base.append(copy.deepcopy(c))
            continue
        existing = base.find(c.tag)
        if existing is not None and len(c) and c.get("Inherit", "").lower() != "false":
            merge(existing, c)
            continue
        if existing is not None:
            base.remove(existing)
        base.append(copy.deepcopy(c))


def load_defs() -> list[tuple[Path, etree._Element]]:
    named, concrete = {}, []
    for f in sorted((ROOT / "Defs").rglob("*.xml")):
        for el in etree.parse(str(f)).getroot():
            if not isinstance(el.tag, str):
                continue
            if el.get("Name"):
                named[el.get("Name")] = el
            if el.get("Abstract", "").lower() != "true":
                concrete.append((f, el))

    def resolve(el):
        parent = named.get(el.get("ParentName", ""))
        if parent is None:
            return copy.deepcopy(el)
        out = resolve(parent)
        merge(out, el)
        for k, v in el.attrib.items():  # MayRequire and friends belong to the child
            out.set(k, v)
        return out

    return [(f, resolve(el)) for f, el in concrete]


class Mod:
    """The mod's sites, challenges and tuning, ready for the templates."""

    def __init__(self, problems: list[str]):
        self.problems = problems
        defs = load_defs()
        tuning = [d for _, d in defs if d.tag == TUNING_TAG]
        self.tuning_el = tuning[0] if tuning else etree.Element(TUNING_TAG)
        if not tuning:
            problems.append("no ExpeditionTuningDef found in Defs")
        self.challenges = {d.findtext("defName"): self._challenge(d) for _, d in defs if d.tag == CHALLENGE_TAG}
        self.sites = [self._site(d) for _, d in defs if d.tag == SITE_TAG]
        for s in self.sites:
            for c in s["challenges"]:
                ch = self.challenges.get(c["def"])
                if ch is not None and s["label"] not in ch["used_by"]:
                    ch["used_by"].append(s["label"])

    # ----- labels -----

    def lookup(self, table: dict, name: str, what: str) -> str:
        if name in table:
            return table[name]
        self.problems.append(f"no label for {what} {name}; add it to the table at the top of build.py")
        return name

    def requires(self, el) -> list[str]:
        out = []
        for pid in (el.get("MayRequire") or "").split(","):
            pid = pid.strip()
            if pid:
                out.append(self.lookup(PACKAGES, pid, "package"))
        return out

    # ----- challenges -----

    def _challenge(self, d) -> dict:
        name = d.findtext("defName")
        combine = d.findtext("combine") or "Sum"
        stats = []
        for li in d.findall("stats/li"):
            ref = float(li.findtext("reference") or 1)
            if li.findtext("skill"):
                label, shown = f"{li.findtext('skill').lower()} skill", f"level {fmt_num(ref)}"
            else:
                stat = li.findtext("stat") or ""
                label, kind = STATS.get(stat) or (self.lookup(STATS, stat, "stat"), "num")
                shown = {"pct": fmt_pct(ref), "num": fmt_num(ref), "kg": f"{fmt_num(ref)} kg",
                         "c/s": f"{fmt_num(ref)} c/s"}[kind]
            stats.append({"label": label, "reference": shown, "weight": float(li.findtext("weight") or 1)})
        flags = [f for f, tag in (("stealth", "isStealth"), ("haul", "isHaul"), ("search", "isSearch"))
                 if (d.findtext(tag) or "").lower() == "true"]
        if combine not in COMBINE:
            self.problems.append(f"challenge {name}: unknown combine rule {combine}")
        return {
            "def": name, "label": d.findtext("label") or name, "description": d.findtext("description") or "",
            "combine": combine, "combine_short": COMBINE.get(combine, (combine, ""))[0],
            "combine_long": COMBINE.get(combine, ("", ""))[1],
            "stats": stats, "skills": [s.text.lower() for s in d.findall("skills/li")],
            "flags": flags, "used_by": [],
        }

    # ----- sites -----

    def _site(self, d) -> dict:
        name = d.findtext("defName")

        def val(tag):
            return d.findtext(tag) if d.find(tag) is not None else SITE_DEFAULTS.get(tag)

        def flag(tag):
            return (d.findtext(tag) or "").lower() == "true"

        if not d.findtext("label"):
            self.problems.append(f"site {name} has no label")
        if not d.findtext("description"):
            self.problems.append(f"site {name} has no description")

        where = [self.lookup(BIOMES, b.text, "biome") for b in d.findall("biomes/li")]
        hills = [self.lookup(HILLINESS, h.text, "hilliness") for h in d.findall("hilliness/li")]
        conditions = []
        if where:
            conditions.append(", ".join(where))
        if hills:
            conditions.append(" or ".join(hills))
        if flag("needsRiver"):
            conditions.append("by a river")
        if flag("needsRoad"):
            conditions.append("on a road")
        if flag("needsCoast"):
            conditions.append("on the coast")
        need = d.findtext("needsFaction")
        if need and need != "None":
            conditions.append(FACTION_NEED.get(need, need))
        if flag("needsEnemy"):
            conditions.append("and a hostile settlement near the site")
        if flag("growingSeason"):
            conditions.append("in the growing season")

        challenges = []
        for li in d.findall("challenges/li"):
            cdef = li.findtext("challenge")
            ch = self.challenges.get(cdef)
            if ch is None:
                self.problems.append(f"site {name} uses challenge {cdef}, which isn't in Defs")
            challenges.append({"def": cdef, "label": ch["label"] if ch else cdef,
                               "weight": float(li.findtext("weight") or 1),
                               "requirement": float(li.findtext("requirement") or 100)})
        total_w = sum(c["weight"] for c in challenges) or 1
        for c in challenges:
            c["share"] = c["weight"] / total_w
        challenges.sort(key=lambda c: -c["weight"])

        loot = []
        for li in d.findall("loot/li"):
            if li.findtext("thing"):
                label = self.lookup(THINGS, li.findtext("thing"), "item")
            else:
                label = self.lookup(CATEGORIES, li.findtext("category") or "", "category")
            loot.append({"label": label, "count": fmt_range(li.findtext("count") or "1", digits=0),
                         "chance": float(li.findtext("chance") or 1),
                         "success_only": (li.findtext("successOnly") or "").lower() == "true",
                         "requires": self.requires(li)})
        joiners = d.find("joiners")
        if joiners is not None:
            kind = self.lookup(PAWN_KINDS, joiners.findtext("kind") or "", "pawn kind")
            loot.append({"label": f"{kind}s who join you", "count": fmt_range(joiners.findtext("count") or "1", digits=0),
                         "chance": float(joiners.findtext("chance") or 1), "success_only": False, "requires": []})
        tamed = d.find("tamedAnimals")
        if tamed is not None:
            loot.append({"label": "tamed wild animals of the area",
                         "count": fmt_range(tamed.findtext("count") or "1", digits=0),
                         "chance": float(tamed.findtext("chance") or 1), "success_only": False, "requires": []})

        rewards = []
        if d.findtext("silverReward"):
            rewards.append(f"{d.findtext('silverReward')} silver")
        if d.findtext("goodwillReward"):
            rewards.append(f"+{d.findtext('goodwillReward')} goodwill with them")
        if d.findtext("goodwillAllFriendly"):
            rewards.append(f"+{d.findtext('goodwillAllFriendly')} with every friendly faction")
        xp = float(val("xpFactor"))
        if xp != 1:
            rewards.append(f"×{fmt_num(xp)} skill experience")
        if d.findtext("successText"):
            rewards.append(d.findtext("successText"))

        risk = {}
        for outcome in ("success", "partial", "failure"):
            node = d.find(f"risk/{outcome}")
            w = dict(RISK_DEFAULTS[outcome]) if node is None else \
                {k: float(node.findtext(k)) if node.findtext(k) else (1.0 if k == "unharmed" else 0.0)
                 for k in ("unharmed", "injured", "killed", "captured")}
            total = sum(w.values()) or 1
            risk[outcome] = {k: v / total for k, v in w.items()}

        need = d.findtext("needsFaction") or "None"
        if flag("rescueOnly"):
            page = "rescue"
        elif d.find("link") is not None:
            worker = d.findtext("link/workerClass") or ""
            page = "worksites" if worker.endswith("_Worksite") else "worldsites"
        elif need in ("Ally", "Friendly"):
            page = "jobs"
        else:
            page = "finds"
        has_search = any("search" in self.challenges.get(c["def"], {}).get("flags", []) for c in challenges)
        commonness = float(val("commonness"))
        return {
            "def": name, "page": page, "label": d.findtext("label") or name,
            "description": d.findtext("description") or "", "color": color_css(d.findtext("color")),
            "requires": self.requires(d), "conditions": conditions, "challenges": challenges, "loot": loot,
            "has_finds": bool(loot), "rewards": rewards, "risk": risk,
            "find": float(val("findChanceBase")), "has_search": has_search,
            "days": float(val("daysOnSite")), "squad": fmt_range(val("squadSize"), digits=0),
            "openness": float(val("openness")),
            "rare": commonness < 1, "commonness": commonness, "fallback": flag("fallback"),
        }

    # ----- tuning -----

    def tuning(self, field: str):
        el = self.tuning_el.find(field)
        if el is not None and len(el):
            return curve_points(el)
        if el is not None:
            return el.text.strip()
        if field in TUNING_DEFAULTS:
            return TUNING_DEFAULTS[field]
        self.problems.append(f"tuning field {field} not found")
        return None


class Helpers:
    """Functions the Markdown pages can call. Each returns a placeholder that
    is swapped for real HTML after Markdown runs, so Markdown never mangles it."""

    def __init__(self, builder: "Builder"):
        self.b = builder

    def _macro(self, name: str, **kw) -> Markup:
        key = f"EXPBLOCK{len(self.b.blocks)}X"
        self.b.blocks[key] = str(getattr(self.b.macros, name)(**kw))
        return Markup(f"\n\n{key}\n\n")

    def _site(self, def_name: str):
        s = next((x for x in self.b.mod.sites if x["def"] == def_name), None)
        if s is None:
            self.b.problems.append(f"{self.b.current_page}: no site {def_name}")
        return s

    def sites(self, page: str):
        rows = [s for s in self.b.mod.sites if s["page"] == page]
        if not rows:
            self.b.problems.append(f"{self.b.current_page}: no sites for page {page!r}")
        return self._macro("sites", rows=rows)

    def site_count(self, page: str) -> Markup:
        return Markup(str(sum(1 for s in self.b.mod.sites if s["page"] == page)))

    def challenges_table(self):
        return self._macro("challenges", rows=list(self.b.mod.challenges.values()))

    def ratio_curve(self):
        pts = self.b.mod.tuning("challengeRatioCurve") or []
        return self._macro("curve", rows=[(f"{fmt_num(x * 100, 0)}% of the requirement", fmt_pct(y)) for x, y in pts],
                           head=("Squad score", "Chance for that challenge"))

    def tech_curve(self):
        """Every tech level an item can have, read off the curve as the game does."""
        pts = self.b.mod.tuning("techLevelCurve") or []
        rows = [(level, f"×{fmt_num(curve_at(pts, i))}") for i, level in enumerate(TECH_LEVELS) if i >= 2]
        return self._macro("curve", rows=rows, head=("Tech level", "Factor"))

    def value_curve(self):
        pts = self.b.mod.tuning("valueCurve") or []
        return self._macro("curve", rows=[(f"{fmt_num(x, 0)} silver", f"×{fmt_num(y)}") for x, y in pts],
                           head=("Market value", "Factor"))

    def tuning(self, field: str, fmt: str = "num") -> Markup:
        """A tuning number inside a sentence. fmt: num, pct, days (a tick count or range), tiles."""
        v = self.b.mod.tuning(field)
        if v is None or isinstance(v, list):
            self.b.problems.append(f"{self.b.current_page}: tuning({field}) is not a number")
            return Markup("?")
        out = {"num": lambda x: fmt_num(float(x)), "pct": lambda x: fmt_pct(float(x)),
               "days": lambda x: fmt_range(x, 1 / TICKS_PER_DAY),
               "hours": lambda x: fmt_range(x, 24 / TICKS_PER_DAY)}[fmt](v)
        return Markup(escape(out))

    def site_value(self, def_name: str, field: str, fmt: str = "num") -> Markup:
        s = self._site(def_name)
        v = s.get(field) if s else None
        if v is None:
            self.b.problems.append(f"{self.b.current_page}: site_value({def_name}, {field}) not found")
            return Markup("?")
        out = {"num": lambda x: fmt_num(x) if isinstance(x, (int, float)) else str(x), "pct": fmt_pct}[fmt](v)
        return Markup(escape(out))

    def site_link(self, def_name: str) -> Markup:
        s = self._site(def_name)
        if s is None:
            return Markup("?")
        page = {"finds": "finds", "worksites": "finds", "worldsites": "finds", "jobs": "jobs"}.get(s["page"], "captives")
        return Markup(f'<a href="{page}.html#{slugify(s["label"])}">{escape(s["label"])}</a>')

    def requirements(self):
        return self._macro("requirements", deps=self.b.dependencies())

    def screenshot(self, name: str, caption: str = ""):
        for ext in SCREENSHOT_EXTS:
            src = SITE / "screenshots" / f"{name}{ext}"
            if src.is_file():
                url = f"assets/screenshots/{name}{ext}"
                self.b.copy_asset(src, url)
                return self._macro("screenshot", src=url, caption=caption)
        self.b.missing_shots.append((self.b.current_page, name, caption))
        return Markup("")


class Builder:
    def __init__(self, out: Path):
        self.out = out
        self.problems: list[str] = []
        self.missing_shots: list[tuple[str, str, str]] = []
        self.blocks: dict[str, str] = {}
        self.current_page = ""
        self.prepare_out()
        self.mod = Mod(self.problems)
        self.env = Environment(
            loader=FileSystemLoader(str(SITE / "templates")),
            autoescape=select_autoescape(["html"]),
            undefined=StrictUndefined,
            trim_blocks=True,
            lstrip_blocks=True,
        )
        self.env.filters.update(num=fmt_num, pct=fmt_pct, pct0=lambda v: f"{fmt_num(v * 100, 0)}%", slug=slugify,
                                cap=cap)
        self.macros = self.env.get_template("macros.html").module
        self.md_env = Environment(autoescape=False, undefined=StrictUndefined)

    def about(self):
        return etree.parse(str(ROOT / "About" / "About.xml")).getroot()

    def dependencies(self) -> list[dict]:
        versions = [li.text.strip() for li in self.about().findall("supportedVersions/li")]
        out = [{"name": "RimWorld " + " or ".join(versions), "url": ""}]
        for li in self.about().findall("modDependencies/li"):
            url = (li.findtext("steamWorkshopUrl") or li.findtext("downloadUrl") or "").strip()
            out.append({"name": (li.findtext("displayName") or "").strip(), "url": url})
        return out

    def workshop_url(self) -> str:
        """The Workshop page, once the mod is uploaded and About/PublishedFileId.txt exists."""
        f = ROOT / "About" / "PublishedFileId.txt"
        if not f.is_file():
            return ""
        return f"https://steamcommunity.com/sharedfiles/filedetails/?id={f.read_text(encoding='utf-8').strip()}"

    def copy_asset(self, src: Path, rel: str):
        dst = self.out / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)

    def prepare_out(self):
        """Empty the output folder, but only if this builder made it."""
        out = self.out
        if out in (ROOT, SITE) or out in ROOT.parents:
            raise SystemExit(f"Refusing to build into {out}: pick an empty or new folder.")
        if out.exists():
            if not (out / BUILD_MARKER).is_file() and any(out.iterdir()):
                raise SystemExit(f"Refusing to delete {out}: it isn't empty and wasn't made by this builder.")
            # Empty it rather than delete it: on Windows an editor or file watcher often holds the folder open.
            for child in out.iterdir():
                shutil.rmtree(child) if child.is_dir() else child.unlink()
        (out / "assets").mkdir(parents=True)
        (out / BUILD_MARKER).write_text("Output of site/build.py. Safe to delete.\n", encoding="utf-8")

    def render_markdown(self, slug: str) -> tuple[dict, str, list]:
        self.current_page = slug
        meta, body = split_front_matter((SITE / "content" / f"{slug}.md").read_text(encoding="utf-8"))
        helpers = Helpers(self)
        g = {name: getattr(helpers, name) for name in dir(helpers)
             if not name.startswith("_") and callable(getattr(helpers, name))}
        text = self.md_env.from_string(body).render(**g)
        md = markdown.Markdown(extensions=["toc", "tables", "attr_list", "md_in_html"],
                               extension_configs={"toc": {"toc_depth": "2-3"}})
        html = md.convert(text)
        for key, block in self.blocks.items():
            html = html.replace(f"<p>{key}</p>", block).replace(key, block)
        toc = [{"id": t["id"], "name": t["name"]} for t in md.toc_tokens if t["level"] == 2]
        return meta, html, toc

    def build(self):
        shutil.copytree(SITE / "static", self.out / "assets", dirs_exist_ok=True)
        preview = ROOT / "About" / "Preview.png"
        has_preview = preview.is_file()
        if has_preview:
            self.copy_asset(preview, "assets/preview.png")

        common = {"nav": NAV, "workshop": self.workshop_url(), "github": GITHUB, "site_url": SITE_URL,
                  "has_preview": has_preview}
        flat = [{"slug": s, "title": t, "group": g} for g, entries in NAV for s, t in entries]
        cards = []
        for i, page in enumerate(flat):
            meta, html, toc = self.render_markdown(page["slug"])
            group_pages = next(entries for g, entries in NAV if g == page["group"])
            out_html = self.env.get_template("page.html").render(
                **common, page=page, meta=meta, content=Markup(html), toc=toc, active_group=page["group"],
                group_pages=group_pages,
                prev=flat[i - 1] if i > 0 else None,
                next=flat[i + 1] if i + 1 < len(flat) else None,
            )
            (self.out / f"{page['slug']}.html").write_text(out_html, encoding="utf-8")
            cards.append({**page, "summary": meta.get("summary", ""), "swatches": self.swatches(meta.get("card", ""))})

        meta, html, _ = self.render_markdown("index")
        index = self.env.get_template("home.html").render(**common, meta=meta, content=Markup(html), cards=cards,
                                                          hero=self.swatches(meta.get("swatches", "")),
                                                          active_group="")
        (self.out / "index.html").write_text(index, encoding="utf-8")
        (self.out / ".nojekyll").write_text("", encoding="utf-8")

    def swatches(self, spec: str) -> list[dict]:
        """Front matter lists site defNames; each shows as a dot in that site's map color."""
        by_def = {s["def"]: s for s in self.mod.sites}
        out = []
        for name in filter(None, (s.strip() for s in spec.split(","))):
            if name in by_def:
                out.append({"color": by_def[name]["color"], "label": by_def[name]["label"]})
            else:
                self.problems.append(f"swatch {name} is not a site def")
        return out

    def report(self) -> int:
        if self.problems:
            print(f"{len(self.problems)} problem(s):")
            for n in self.problems:
                print("  -", n)
        if self.missing_shots:
            print(f"{len(self.missing_shots)} screenshot(s) not added yet (put them in site/screenshots/):")
            for page, name, caption in self.missing_shots:
                print(f"  - {name}.png  ({page}: {caption})")
        print(f"Built {self.out}")
        return len(self.problems)


def serve(out: Path, port: int):
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(out))
    with http.server.ThreadingHTTPServer(("127.0.0.1", port), handler) as httpd:
        print(f"Serving on http://localhost:{port}/ (Ctrl+C to stop)")
        httpd.serve_forever()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, default=SITE / "_build")
    ap.add_argument("--serve", nargs="?", const=8000, type=int, metavar="PORT")
    ap.add_argument("--strict", action="store_true", help="exit with an error if the build reports problems (CI)")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    b = Builder(args.out.resolve())
    b.build()
    problems = b.report()
    if args.strict and problems:
        raise SystemExit(f"--strict: {problems} problem(s)")
    if args.serve:
        serve(args.out.resolve(), args.serve)


if __name__ == "__main__":
    main()
