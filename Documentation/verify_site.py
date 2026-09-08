#!/usr/bin/env python3
"""Check a DocFX build's local links, anchors, and optional pre-redesign content."""

import argparse
from collections import defaultdict
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit


class Page(HTMLParser):
    def __init__(self, path):
        super().__init__(convert_charrefs=True)
        self.anchors = defaultdict(list)
        self.article_anchors = set()
        self.links = []
        self.uids = set()
        self.code = []
        self.in_article = False
        self.in_code = False
        self.feed(path.read_text(encoding="utf-8"))

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "article":
            self.in_article = True
        for anchor in {attrs.get("id"), attrs.get("name") if tag == "a" else None} - {None, ""}:
            self.anchors[anchor].append((tag, attrs.get("data-uid", "")))
            if self.in_article:
                self.article_anchors.add(anchor)
        if attrs.get("data-uid"):
            self.uids.add(attrs["data-uid"])
        for key in ("href", "src"):
            if attrs.get(key):
                self.links.append(attrs[key])
        if tag == "meta" and attrs.get("name") in ("docfx:tocrel", "docfx:navrel") and attrs.get("content"):
            self.links.append(attrs["content"])
        if tag == "code" and any(name.startswith("lang-") for name in attrs.get("class", "").split()):
            self.in_code = True
            self.code.append("")

    def handle_endtag(self, tag):
        if tag == "article":
            self.in_article = False
        if tag == "code":
            self.in_code = False

    def handle_data(self, text):
        if self.in_code:
            self.code[-1] += text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("site", type=Path, help="Built site directory, normally docs")
    parser.add_argument("--before", type=Path, help="Earlier build to check for lost anchors, API UIDs, and code examples")
    args = parser.parse_args()
    root = args.site.resolve()
    pages = {path: Page(path) for path in root.rglob("*.html")}
    errors = []
    checked = 0
    if root / "index.html" not in pages:
        errors.append("Missing index.html; build the documentation first")
    for path, page in pages.items():
        relative = path.relative_to(root)
        for anchor, occurrences in page.anchors.items():
            # DocFX emits the same empty overload-group anchor before each overload.
            repeated_overload = len(set(occurrences)) == 1 and occurrences[0][0] == "a" and occurrences[0][1].endswith("*")
            if len(occurrences) > 1 and not repeated_overload:
                errors.append(f"{relative}: duplicate #{anchor}")
        for link in page.links:
            url = urlsplit(link)
            if url.scheme or url.netloc:
                continue
            checked += 1
            target = path
            if url.path:
                decoded = unquote(url.path)
                target = (root / decoded.lstrip("/") if decoded.startswith("/") else path.parent / decoded).resolve()
            if target.is_dir():
                target /= "index.html"
            if not target.is_relative_to(root) or not target.exists():
                errors.append(f"{relative}: missing local target {link}")
            elif url.fragment and target in pages and unquote(url.fragment) not in pages[target].anchors:
                errors.append(f"{relative}: missing anchor {link}")
        if args.before:
            previous_path = args.before / relative
            if previous_path.exists():
                previous = Page(previous_path)
                for anchor in sorted(previous.article_anchors - page.article_anchors):
                    errors.append(f"{relative}: lost section #{anchor}")
                for uid in sorted(previous.uids - page.uids):
                    errors.append(f"{relative}: lost API member {uid}")
                if relative.parts[0] in ("articles", "api") and previous.code != page.code:
                    errors.append(f"{relative}: changed code examples or signatures")
    if args.before:
        for previous_path in args.before.rglob("*.html"):
            if root / previous_path.relative_to(args.before) not in pages:
                errors.append(f"Lost page {previous_path.relative_to(args.before)}")
    if errors:
        raise SystemExit("\n".join(errors))
    print(f"Verified {len(pages)} pages and {checked} local links and assets.")
    if args.before:
        print("Existing article anchors, API members, code examples, and signatures are preserved.")


if __name__ == "__main__":
    main()
