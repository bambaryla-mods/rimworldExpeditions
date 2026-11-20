# Player site

The site at https://databobek.github.io/rimworldExpeditions/ shows players what Expeditions does and what every site and challenge asks of a squad. It is not part of the mod: `_PublisherPlus.xml` keeps this folder out of the Workshop upload.

## How it works

`build.py` reads the mod's own files:

- the site, challenge and tuning defs in `Defs/`, with inheritance resolved (so a site that inherits a risk family shows its odds),
- `About/About.xml` for the supported versions and dependencies,
- `About/Preview.png` for link previews and `About/PublishedFileId.txt` for the Workshop button, once they exist.

Every table and number on the site comes from those files, so the pages stay correct when you retune. The written text lives in `content/` and has to be updated by hand when a mechanic changes.

The game's own files aren't in the repo, so the labels of the vanilla, DLC and mod defs the site defs refer to (items, categories, biomes, stats, DLC package ids) are typed into the tables at the top of `build.py`. If a def names something that isn't there, the build says so. Fields a def leaves out fall back to the C# defaults copied into `SITE_DEFAULTS`, `RISK_DEFAULTS` and `TUNING_DEFAULTS`; keep those in step with `Source/Defs`.

## Building and previewing

```
pip install -r site/requirements.txt
python site/build.py --serve
```

Then open http://localhost:8000. The build also lists problems it found, like a site without a description or a loot item without a label. It only ever empties an output folder it created itself.

Pushing to `main` rebuilds and publishes the site through `.github/workflows/pages.yml`. One-time setup on GitHub: **Settings > Pages > Source: GitHub Actions**. Pages only works on a public repository with a free account.

The workflow builds with `--strict`, so a problem fails the run and the live site stays as it was. GitHub emails you when a run fails.

## Where things live

| Path | What |
|---|---|
| `content/*.md` | One Markdown file per page. Front matter sets the home page card (`summary`, and `card` with the site defNames whose colors it shows). |
| `build.py` | The top bar groups and page order (`NAV`), labels, defaults, and the helpers pages can call. |
| `templates/` | Page layout (`base.html`, `page.html`, `home.html`) and the tables (`macros.html`). |
| `static/` | CSS, the menu script, the favicon and the Audiowide font (OFL). The look matches the Beyond Our Reach and Even More Linkables sites. |
| `screenshots/` | In-game screenshots, if you add any. |

Sites go on a page by what they are: `rescueOnly` sites on Captives, sites for an allied or friendly faction on Jobs, everything else on Places to search.

## Helpers a page can call

```
{{ sites("finds") }}           the sites table for a page: finds, jobs or rescue
{{ site_count("jobs") }}       how many sites go on that page
{{ site_link("Exp_Rumor") }}   a site's label, linked to its row
{{ site_value("Exp_Dig", "days") }}
                               a number from one site. Fields: days, find, openness, commonness, squad.
                               fmt: num, pct
{{ challenges_table() }}       every challenge, what it reads and which sites use it
{{ ratio_curve() }}            squad score against requirement, and the chance it gives
{{ tech_curve() }} {{ value_curve() }}
                               the prospecting factors
{{ tuning("partialBand", "pct") }}
                               a number from Expeditions_Tuning (or its C# default).
                               fmt: num, pct, days (from ticks; ranges too), hours
{{ requirements() }}           RimWorld versions and dependencies, from About.xml
{{ screenshot("name", "caption") }}   site/screenshots/name.png, once you add it
```

Prefer a helper over typing a number into the text: when the def changes, the page follows.
